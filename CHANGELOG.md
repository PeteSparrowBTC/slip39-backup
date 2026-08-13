# Changelog

[Semantic versioning](https://semver.org), with one addition that matters more than the
rest.

## What the public interface is here

For most software the interface is an API. For this tool it is **a backup somebody already
made**, sitting on a USB stick and in a password manager, which has to open years from now
by the procedure printed inside it.

> **MAJOR is reserved for a change that stops an existing backup opening by its own
> documented procedure.**

Nothing else this tool can do is as bad, and there is no known reason to do it. If the day
comes, it needs a new name in the release title, an explanation at the top of the release
notes, and instructions for holders of the old format.

|  | means | examples |
| --- | --- | --- |
| **MAJOR** | old backups stop working, or their instructions stop being true | the payload format changes without a compatible reader, the artifact set changes in a way `MANUAL-RECOVERY.txt` cannot cover |
| **MINOR** | new capability, existing backups unaffected | a new verification gate, a packaging target, a new cross-check |
| **PATCH** | fixes and text | a UI defect, a wrong instruction, dependency bumps, documentation |

Reading that table: **almost everything is MINOR or PATCH.** A change that makes you reach
for MAJOR is worth stopping over, because it means somebody's existing backup is affected.

## Unreleased

### Two silent-wrongness fixes

- **A passphrase with leading whitespace no longer recovers a different wallet.**
  `PayloadParser` trimmed leading whitespace from every value, so a BIP-39 passphrase of
  `" hunter2"` was written correctly and read back as `"hunter2"`: valid, derivable, and
  empty. Nothing could see it, because the independent verifier compared the ciphertext
  against the same text the emitter had produced and the master fingerprint in the
  verification record came from the form rather than the payload. The parser now drops
  exactly the one separator space it writes, which also **repairs backups already made**:
  those files carried the intended value all along.
- **`PayloadRoundTrip.EmitChecked` is now the only emit path.** It reparses and compares
  field by field before anything is encrypted and refuses by name if a value does not
  survive, so the residual case (a value containing a line break) is a refusal rather than
  a substitution. Refusals never echo the value, because the values are seed words and
  passphrases and the message lands in an on-screen banner.
- **The outer OpenPGP lock is opened by the system GnuPG before a backup is released**, and
  must return the age file byte for byte. That layer was the only part of the artifact
  written by code from this repository and never opened by anything else. A real backup is
  refused if the check cannot run, GnuPG missing included; an INSECURE-TEST backup
  continues and says in its transcript that nothing independent opened it.

### Documents that had stopped matching the tool

Found by a repository review, recorded in
[reviews/2026-08-13-repo-review.md](reviews/2026-08-13-repo-review.md), and worth listing
individually because each one shipped inside a backup where nobody can correct it:

- `MANUAL-RECOVERY.txt` opened by telling the heir that "any SLIP-39 implementation plus
  any age implementation can rebuild the wallet". Since the OpenPGP lock became mandatory
  that was wrong, and an heir following it would stall on the first command. It now names
  three standards, lists GnuPG among the tools, and cites RFC 9580.
- `VERIFY-THIS-BACKUP.txt` told the owner to delete `check2.txt`, which no step creates,
  and did not delete `payload.age`, which one does. It also referenced the wrong step for
  `check.txt` and still described the file to verify as `payload.age`.
- The share `README.txt` promised recovery "using only standard SLIP-39 and age software".
- `TAILS_INSTRUCTIONS.md` still described three payload forms and an `output.zip`.
- The README called the download `output.zip` in nine places, and its "Two-layer
  encryption" diagram omitted the OpenPGP layer the section is named after.
- `ShippedDocumentConsistencyTests` now holds these as invariants across documents rather
  than as facts about one of them, which is why they all drifted at once.

### Reproducibility

- **The share `README.txt` is now identical whoever builds it.** It was assembled with
  `StringBuilder.AppendLine`, so it shipped with CRLF from a Windows build and LF from the
  Linux CI that publishes the AppImage. `ShareZipWriter` fixes timestamps specifically to
  make share zips reproducible, and this defeated that for anyone comparing two builds.

### Repository

- `CLAUDE.md` documented `gh api .../branches/main/protection` for checking that `main` is
  protected. Protection here is a **ruleset**, and that legacy endpoint returns 404 "Branch
  not protected" on a fully protected repository. A review ran it and nearly reported the
  repository as wide open.
- The two historical design specs now say what is still true in them and what has been
  superseded, rather than reading as current instruction.
- A `repo-review` skill, adapted from `bitcoin-backup-framework`'s `framework-review`, with
  the mechanical half in `checks.sh`.
- Removed an empty second solution file, four tracked JetBrains files, and three
  unreferenced root scratch files.

## 2.0.0, 2026-08-11

Replaced the v1 single-page tool. A random 32-byte key is split with SLIP-39 and encrypts
the wallet payload with age, so threshold-many shares and the encrypted payload are both
required and neither alone reveals anything.

- Native window for Tails: no browser, no local server, no port bound
- Encryption runs the official `age` binary, bundled and pinned by checksum, with a
  transcript showing the command, the binary hash and the exit code
- A second OpenPGP AES-256 envelope around the age file
- Every generated backup is round-tripped by independent implementations (slip39-js,
  typage) in both directions before the download is released
- An owner-facing verification procedure routing only to upstream tools

Two things stated in that release changed afterwards and are described under Unreleased
above: the unwrapped payload forms no longer ship (one artifact, `payload.age.gpg.asc`),
and the leading-whitespace trap listed there as a known gap is fixed.

## 1.0.0

The original single-page tool. Superseded by 2.0.0, which is not backward compatible with
it: a v1 backup is recovered with v1 instructions.
