# Working in this repository

## Settled design decisions: read before proposing changes to the crypto

The choices below were argued out in full and reached conclusions. The reasoning,
the rejected alternatives, and above all the **limits** of each are recorded in
[docs/decisions/2026-08-09-envelope-entropy-and-implementations.md](docs/decisions/2026-08-09-envelope-entropy-and-implementations.md).
Read that before reopening any of them. Several are correct only within a
boundary, and a change that ignores the boundary will look like an improvement
while removing the protection.

The short form:

- **The payload is wrapped twice**, age inside OpenPGP AES-256. Nesting composes:
  an attacker must break both. It lifts the post-quantum margin from 2^64 to
  2^128, because age's file key is 128 bits.
- **All three payload forms ship** (`payload.age`, `payload.age.txt`,
  `payload.age.gpg`) and any ONE recovers the wallet. The wrapper is protection,
  not a gate. Do not ship only the wrapped form.
- **Both layers take the same K.** This hedges a cipher or format break. It does
  NOT hedge a break that recovers the passphrase itself. Accepted deliberately:
  independent keys would make recovery a scripted derivation no heir can perform
  by hand.
- **Encryption runs the bundled official age binary; AgeSharp decrypts.**
  Encryption failures are silent, decryption failures are loud, so the invisible
  side gets the most-scrutinised implementation. **Fails closed** if the binary is
  missing. Do not add a fallback.
- **The OpenPGP layer is in-process on BouncyCastle**, so recovery never needs
  GnuPG installed. Keep handling gpg's compression: GnuPG compresses by default,
  so real files carry packets we never produce ourselves.
- **Dice entropy for K is 50 rolls, not 99**, XORed with the system RNG, never
  replacing it. Never reuse the seed's rolls.
- **Rejected:** a browser verifier shipped in our own bundle (it cannot verify
  its own producer), and zip password protection (the ubiquitous variant is
  ZipCrypto, which is broken, and our plaintext prefix is published).

**Where the real risk sits**, in order: operator error; the `PayloadParser`
leading-whitespace trap; key generation; supply chain; implementation bugs;
cryptanalysis last. The first two are unfixed. Do not propose work on the last
while they remain open.

## CRITICAL: main moves only through a pull request

- **Never push to main.** Not `git push origin main`, not a bare `git push` while
  main is checked out, not `git push origin HEAD:main`, and no `--force` variant.
  Open a pull request instead.
- **Never merge a pull request.** Not `gh pr merge`, not the REST API, not the web
  UI. Opening the PR is the agent's job; merging is the human's.
- **A version tag is a release.** `.github/workflows/appimage.yml` builds the
  AppImage, checksums it and publishes a GitHub Release on `v*` tag push, and that
  artifact is what people run against real seed phrases. Push a tag only when
  explicitly asked, and only once the release commit is on main through a merged
  pull request.
- Pushing feature branches (`git push -u origin <branch>`) is safe and expected.

## Testing conventions

- `SLIP39_AGE_DIR` points at an unpacked age release and enables the
  native-encryptor tests. Without it they skip rather than fail, so a contributor
  without the binaries sees a partial run instead of a red suite. CI sets it after
  fetching the pinned version.
- The gpg interop tests skip the same way when GnuPG is absent.
- Third-party binaries are never committed. They are fetched, pinned by version
  AND checksum, and verified at build time.

## Writing style

No em dashes. Use a colon, semicolon, comma, parentheses, or a sentence break
instead. This applies to prose, comments, and commit messages.
