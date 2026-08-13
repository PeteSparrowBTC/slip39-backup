using System.Net;

namespace Slip39Demo.Core;

// Whether this copy of the app is running locally or is being served to you by somebody
// else.
//
// WHY THIS DECIDES ANYTHING
// The connectivity probe answers "is there a network". That is the right question for the
// AppImage, where it gates the INSECURE-TEST watermark. It is the wrong question for a
// hosted copy, and the difference is not academic: on the demo at
// petesparrowbtc.github.io a visitor could open the page, pull the network cable, watch
// the banner turn green and say "This machine is offline", and reasonably conclude it was
// now safe to type a real seed. At that moment they are running WebAssembly a server sent
// them, which they cannot check against this repository, on their everyday computer. The
// banner was telling the truth about connectivity and the wrong thing about safety, and a
// green tick beats a paragraph of warning every time.
//
// So the two questions are asked separately. This one is about provenance: did these bytes
// cross a network to get here, and can the person running them tell what they are.
//
// Ported from dice-to-seed's DiceToSeed.Core/ServingOrigin.cs, which reached this
// conclusion first, with its case analysis intact. See docs/online-detection.md for the
// comparison of the two repositories that produced it.
//
// WHAT COUNTS AS LOCAL, and why each case is here rather than only the obvious two:
//
//   a non-web scheme          tauri:, file: and the like never crossed a network; they are
//                             an in-process handler reading bytes off the disk. The
//                             AppImage's WebView loads through one of these, so it must
//                             not be warned at
//   the exact host localhost  the ordinary case, matched case-insensitively because host
//                             names are
//   any loopback IP literal   127.0.0.1, the rest of 127.0.0.0/8, and ::1 bracketed or
//                             bare. Serving on 127.0.0.2 is no less local
//   anything under .localhost RFC 6761 reserves that name for loopback and browsers
//                             resolve it without asking DNS. Tauri serves from
//                             tauri.localhost on some platforms, which is local by the
//                             same rule
//
// WHAT MUST NOT COUNT, and this is why it is a function rather than a substring test:
// 127.0.0.1.example.com and localhost.evil.com are ordinary internet hosts that a naive
// "contains" or "starts with" check waves through, handing an attacker exactly the silence
// they want.
public static class ServingOrigin
{
    // True when the app is running locally, so the page may show the ordinary banners and
    // a real backup is possible. False means somebody else is serving it: the provenance
    // warning belongs on screen and nothing generated here may be unwatermarked.
    public static bool IsLocal(Uri uri)
    {
        // Not http or https: nothing crossed a network to get here. The AppImage lands in
        // this branch whichever scheme its WebView happens to use.
        if (!uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            && !uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            return true;

        // DnsSafeHost strips the brackets an IPv6 literal carries in Host, so ::1 parses
        // whichever form it arrives in.
        var host = uri.DnsSafeHost;

        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
            return true;

        // A whole-label suffix, never a substring: "evil.localhost" is loopback by RFC
        // 6761, "localhost.evil.com" is not, and only one of them ends with ".localhost".
        if (host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase))
            return true;

        return IPAddress.TryParse(host, out var address) && IPAddress.IsLoopback(address);
    }

    // Convenience for the callers that hold a string, which is what NavigationManager
    // gives them. Anything that is not an absolute URI with a scheme is treated as NOT
    // local: this is a safety gate, and the direction to fail in is the cautious one.
    public static bool IsLocal(string uri) =>
        HasScheme(uri) && Uri.TryCreate(uri, UriKind.Absolute, out var parsed) && IsLocal(parsed);

    // The scheme is required explicitly rather than left to Uri.TryCreate, because
    // TryCreate does not answer this the same way on every platform. On Unix a bare path
    // IS an absolute path, so "/slip39-backup/" parses as an absolute file: URI and would
    // reach the non-web-scheme branch above and read as LOCAL. On Windows the same string
    // fails to parse and reads as remote. CI caught that disagreement between the two.
    //
    // A safety gate whose answer depends on the operating system of the machine that built
    // it is not a gate, and the platform where it went permissive is the one the AppImage
    // ships on.
    static bool HasScheme(string uri)
    {
        var colon = uri.IndexOf(':');
        return colon > 0
            && char.IsAsciiLetter(uri[0])
            && uri.Take(colon).All(c => char.IsAsciiLetterOrDigit(c) || c is '+' or '-' or '.');
    }

    // What to put on screen: scheme and host together, with the port when there is one.
    //
    // The scheme is included deliberately. Without it a reader cannot tell an ordinary
    // local server from the AppImage's in-process handler.
    public static string Describe(Uri uri) =>
        uri.IsDefaultPort ? $"{uri.Scheme}://{uri.Host}" : $"{uri.Scheme}://{uri.Host}:{uri.Port}";

    public static string Describe(string uri) =>
        Uri.TryCreate(uri, UriKind.Absolute, out var parsed) ? Describe(parsed) : uri;
}
