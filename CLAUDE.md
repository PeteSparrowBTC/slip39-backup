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
- **One payload artifact ships**: `payload.age.gpg.asc`, armored. This REVERSES an
  earlier decision that shipped the unwrapped forms alongside it, which handed
  anyone who obtained the folder the weaker file and so nullified the wrapper.
  Anything shipped beside it must be at least as strong as it.
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

- **Never push to main.** Not `git push origin main`, not a bare `git push` while main
  is checked out, not `git push origin HEAD:main`, and no `--force` variant. Open a pull
  request instead.
- **Never merge a pull request.** Not `gh pr merge`, not the REST API, not the web UI.
  Opening the PR is the agent's job; merging is the human's.
- **A version tag is a release.** `.github/workflows/appimage.yml` builds the AppImage,
  checksums it and publishes a GitHub Release on `v*` tag push, and that artifact is what
  people run against real seed phrases. Push a tag only when explicitly asked, and only
  once the release commit is on main through a merged PR.
- Pushing feature branches (`git push -u origin <branch>`) is safe and expected.

### The three mechanisms, and what each actually does

| mechanism | what it actually does |
| --- | --- |
| GitHub branch protection on `main` | **The real enforcement.** Server-side, survives a reclone, applies to every client and to the web UI. Requires setup once per repo (below). |
| `.githooks/pre-push` | **Blocks locally**, so a mistake fails before the network round-trip and prints the way out. Opt in per clone: `git config core.hooksPath .githooks`. Bypassable with `--no-verify`, by design. |
| `.claude/settings.json` deny rules | Stops an agent from issuing the common main-targeting push and merge commands, plus the `gh api` verbs that could remove the protection itself. Matching is prefix-based and cannot cover every spelling, so it is a backstop for judgement, not a replacement. |

Deliberately **not** used: an `on: push` workflow that "prevents" direct pushes. Such a
workflow runs after the server has already accepted the push, so it can only report, never
block. A sibling repository had one that failed 39 of 39 runs, including every legitimate
merge, because `github.event.pull_request` is always null on a push event. A permanently red
check is worse than no check. This repository is public, so real server-side enforcement is
available and is used instead.

### Setup: enable branch protection (once per repo)

Requires admin on the repo. Because this repository is public, branch protection is
available on the free plan (private repos would need GitHub Pro or Team).

```bash
gh api -X PUT repos/PeteSparrowBTC/slip39-backup/branches/main/protection --input - <<'JSON'
{
  "required_status_checks": null,
  "enforce_admins": true,
  "required_pull_request_reviews": {
    "required_approving_review_count": 0,
    "dismiss_stale_reviews": false,
    "require_code_owner_reviews": false
  },
  "restrictions": null,
  "allow_force_pushes": false,
  "allow_deletions": false
}
JSON
```

Why `required_approving_review_count` is 0: a pull request is still required, but a solo
maintainer can merge their own. GitHub does not allow self-approval, so any value above 0
would make every PR unmergeable until a second person exists. Raise it when there is one.

`enforce_admins: true` is what stops the repo owner from pushing to main directly. It does
not prevent merging a PR.

Verify:

```bash
gh api repos/PeteSparrowBTC/slip39-backup/branches/main/protection \
  --jq '{pr_required: (.required_pull_request_reviews != null), admins_included: .enforce_admins.enabled}'
```

Equivalent UI path: Settings, Branches, Add branch ruleset, target `main`, tick "Require a
pull request before merging" and "Do not allow bypassing the above settings".

### Enable the local hook after cloning

```bash
git config core.hooksPath .githooks
```

Tested behaviour: a push targeting `main` exits 1 with instructions, a feature branch exits
0 silently, a `v*` tag exits 0 with a release warning.

## Testing conventions

- `SLIP39_AGE_DIR` points at an unpacked age release and enables
  `the_real_age_program_produces_an_age_file` in `src-tauri/src/age.rs`, the one
  Rust test that runs the real `age` binary end to end. Locally, without the
  variable set, it prints a skip message and returns; Rust has no equivalent of
  `SkippableFact`, so a plain early return would look like a pass with nothing
  checked. To avoid that, the test also checks for a `CI` environment variable: if
  that is set and `SLIP39_AGE_DIR` is not, it fails outright rather than passing
  silently. CI sets `SLIP39_AGE_DIR` after fetching the pinned age release, so this
  path is not expected to trigger there.
- The gpg interop tests skip the same way when GnuPG is absent.
- Third-party binaries are never committed. They are fetched, pinned by version
  AND checksum, and verified at build time.

## Writing style

No em dashes. Use a colon, semicolon, comma, parentheses, or a sentence break instead.
This applies to prose, comments, and commit messages.
