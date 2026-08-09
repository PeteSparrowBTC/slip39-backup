using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using Age;
using Age.Format;
using Age.Recipients;
using Xunit;

namespace Slip39Demo.Tests.Age.Cctv;

// Wire-format conformance gate for the age implementation we actually ship.
//
// WHY THIS EXISTS HERE
// AgeSharp used to be consumed as a local fork (third_party/AgeSharp) whose own
// test project ran the full 143-vector CCTV suite. The fork is gone: upstream
// fixed our WASM report (pscheid92/AgeSharp#2) and backported 39 correctness and
// security fixes (#91), so Slip39Demo.Core now references the released NuGet
// package. That removed the vendored suite along with the fork, and with it the
// only check that the bytes we hand a user are readable by anything but us.
//
// Upstream runs all 143 vectors in its own CI, but that proves things about
// upstream's build. This project verifies the exact binary this repository
// resolves and ships, which is the thing a wallet backup depends on.
//
// SCOPE: scrypt + armor only (58 of the 143 vectors). Those are the two code
// paths this tool exercises: AgePassphrase encrypts in scrypt mode with the
// hex-encoded SLIP-39 master secret as the passphrase, and AgeArmor emits the
// PEM-fenced text form. X25519, SSH, ML-KEM and plugin recipients are never
// constructed by this application, so their vectors are upstream's business.
//
// VECTOR SOURCE: C2SP/CCTV (https://github.com/C2SP/CCTV), the vectors the Go
// reference implementation is itself tested against, vendored under testdata/.
//
// LINE ENDINGS: testdata/.gitattributes marks the vectors `-text`. These files
// are byte-exact reference data; Git's core.autocrlf=true on Windows would
// rewrite the LF-only ones to CRLF and corrupt 61 of them. That is exactly the
// bug we reported upstream as pscheid92/AgeSharp#3, and the `.gitattributes`
// here is what keeps it from reappearing in this repository.
public class CctvScryptArmorVectorTests
{
    static readonly string TestDataDir = Path.Combine(AppContext.BaseDirectory, "Age", "Cctv", "testdata");

    // The vectors are extensionless, matching upstream C2SP/CCTV naming. Only
    // .gitattributes (which is what keeps them byte-exact, see below) is skipped.
    static IEnumerable<string> VectorFiles() =>
        Directory.Exists(TestDataDir)
            ? Directory.EnumerateFiles(TestDataDir).Where(f => Path.GetFileName(f) != ".gitattributes").OrderBy(f => f)
            : [];

    public static IEnumerable<object[]> Vectors() =>
        VectorFiles().Select(f => new object[] { Path.GetFileName(f), f });

    // Guards against the failure mode where the vectors stop being copied to the
    // output directory: xunit's [Theory] over an empty MemberData source reports
    // success, so a missing testdata folder would silently turn this whole gate
    // into a no-op that still shows green. Assert the expected count explicitly.
    [Fact]
    public void VectorCorpus_IsPresentAndComplete() =>
        Assert.Equal(58, VectorFiles().Count());

    [Theory]
    [MemberData(nameof(Vectors))]
    public void RunVector(string name, string path)
    {
        _ = name; // shown in the test-runner display; behaviour comes from the file
        var (metadata, identityStrings, ageFileBytes) = ParseTestFile(path);

        // Identities the vector says the decryptor should be handed. Only the
        // X25519 and ML-KEM forms appear in the scrypt/armor subset (a handful of
        // armor vectors are armored X25519 files); the passphrase, when present,
        // becomes a ScryptRecipient, which implements IIdentity as well.
        var identities = identityStrings
            .Select(ParseIdentity)
            .Where(i => i is not null)
            .Select(i => i!)
            .ToList();

        if (metadata.GetValueOrDefault("passphrase") is { } passphrase)
            identities.Add(new ScryptRecipient(passphrase));

        // A few negative vectors (armor_empty) name neither an identity nor a
        // passphrase: the file is malformed before any key material is reached,
        // so the vector is indifferent to which identity is supplied. AgeSharp
        // requires at least one (it throws ArgumentException for an empty set,
        // upstream PR #82), so supply a placeholder to keep the header-parsing
        // path the thing under test rather than the argument check.
        if (identities.Count == 0)
            identities.Add(new ScryptRecipient("placeholder, never matched"));

        var file = ageFileBytes;
        var ids = identities.ToArray();

        // `expect` is the vector's verdict. Note that most of these are NEGATIVE
        // cases: the vector is a deliberately malformed file and conformance means
        // REJECTING it with the right class of error. An implementation that
        // accepts a truncated payload or a non-canonical salt is the dangerous
        // kind of wrong, so the negative cases matter as much as the successes.
        switch (metadata["expect"])
        {
            case "success":
                AssertDecryptsTo(file, ids, metadata["payload"]);
                break;
            case "no match":
                Assert.Throws<NoIdentityMatchException>(() => Decrypt(file, ids));
                break;
            case "HMAC failure":
                Assert.Throws<AgeHmacException>(() => Decrypt(file, ids));
                break;
            case "header failure":
                AssertThrowsAnyOf<AgeHeaderException, AgeHmacException>(file, ids);
                break;
            case "payload failure":
                Assert.Throws<AgePayloadException>(() => Decrypt(file, ids));
                break;
            case "armor failure":
                AssertThrowsAnyOf<AgeArmorException, AgeHeaderException>(file, ids);
                break;
            default:
                Assert.Fail($"unknown expect value: {metadata["expect"]}");
                break;
        }
    }

    static IIdentity? ParseIdentity(string s) =>
        s.StartsWith("AGE-SECRET-KEY-PQ-", StringComparison.OrdinalIgnoreCase) ? MlKem768X25519Identity.Parse(s)
        : s.StartsWith("AGE-SECRET-KEY-1", StringComparison.OrdinalIgnoreCase) ? X25519Identity.Parse(s)
        : null;

    static byte[] Decrypt(byte[] ageFileBytes, IIdentity[] identities)
    {
        using var input = new MemoryStream(ageFileBytes);
        using var output = new MemoryStream();
        AgeEncrypt.Decrypt(input, output, identities);
        return output.ToArray();
    }

    // The vector states the expected plaintext as a SHA256 hex digest rather than
    // inline bytes, so compare digests.
    static void AssertDecryptsTo(byte[] ageFileBytes, IIdentity[] identities, string expectedPayloadSha256) =>
        Assert.Equal(expectedPayloadSha256, Convert.ToHexStringLower(SHA256.HashData(Decrypt(ageFileBytes, identities))));

    // Some verdicts admit two acceptable error classes (a malformed header may be
    // caught either while parsing or by the header HMAC, depending on where the
    // corruption lands). Accept either, reject anything else.
    static void AssertThrowsAnyOf<T1, T2>(byte[] ageFileBytes, IIdentity[] identities)
        where T1 : AgeException where T2 : AgeException
    {
        var ex = Assert.ThrowsAny<AgeException>(() => Decrypt(ageFileBytes, identities));
        Assert.True(ex is T1 or T2,
            $"expected {typeof(T1).Name} or {typeof(T2).Name}, got {ex.GetType().Name}: {ex.Message}");
    }

    // CCTV vector file layout:
    //   "key: value" header lines (expect, payload, passphrase, identity, ...)
    //   a blank line
    //   the raw age file bytes, optionally zlib-compressed (`compressed: zlib`)
    // Parsed over the raw bytes rather than as text: the body is binary and must
    // not pass through any encoding or newline translation.
    static (Dictionary<string, string> Metadata, List<string> Identities, byte[] Body) ParseTestFile(string path)
    {
        var allBytes = File.ReadAllBytes(path);
        var metadata = new Dictionary<string, string>();
        var identities = new List<string>();

        var pos = 0;
        while (pos < allBytes.Length)
        {
            var lineEnd = Array.IndexOf(allBytes, (byte)'\n', pos);
            if (lineEnd < 0)
                break;

            // Blank line terminates the header block; the body starts after it.
            if (lineEnd == pos)
            {
                pos = lineEnd + 1;
                break;
            }

            var line = Encoding.UTF8.GetString(allBytes, pos, lineEnd - pos);
            pos = lineEnd + 1;

            var colonIdx = line.IndexOf(": ", StringComparison.Ordinal);
            if (colonIdx < 0)
                continue;

            var key = line[..colonIdx];
            var value = line[(colonIdx + 2)..];
            // `identity` may repeat, so it is accumulated as well as recorded.
            if (key == "identity")
                identities.Add(value);
            metadata[key] = value;
        }

        var body = allBytes[pos..];
        return (metadata, identities,
            metadata.GetValueOrDefault("compressed") == "zlib" ? ZlibDecompress(body) : body);
    }

    static byte[] ZlibDecompress(byte[] data)
    {
        using var input = new MemoryStream(data);
        using var zlib = new ZLibStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        zlib.CopyTo(output);
        return output.ToArray();
    }
}
