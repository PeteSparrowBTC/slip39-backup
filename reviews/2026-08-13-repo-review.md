# Repository review, 2026-08-13

Reviewed at `5cced21` (origin/main), following
[.claude/skills/repo-review](../.claude/skills/repo-review/SKILL.md), which was adapted
from `bitcoin-backup-framework`'s `framework-review` skill in the same pass.

Authored by Pete Sparrow (human) and Claude (AI, Anthropic).

Ranked by what a reader or an heir loses by acting on the repository as it stands.
Each finding says whether it was **verified** (a file read, a command run) or
**inferred**.

---

## 1. Shipped documents naming things the tool does not produce

These travel inside the backup and cannot be corrected after generation, which is why
they lead.

| where | what it said | verified by |
| --- | --- | --- |
| `TAILS_INSTRUCTIONS.md:8-9` | the tool outputs "`payload.age` in three forms" and recovery takes "any one of the three" | reading `OutputBundleBuilder.PayloadFileName`, which is one file |
| `VerifyGuide.cs:29,142,200` | prove "your payload.age" decrypts; work in the folder holding `payload.age` | same constant |
| `VerifyGuide.cs:226` | "With check.txt still present from B3" | B3 produces `payload.age`; `check.txt` comes from B4 |
| `VerifyGuide.cs:243` | `rm check.txt check2.txt check3.txt` | no step anywhere creates `check2.txt`, and `payload.age`, which B3 creates and the guide promises to delete, is absent from the line |
| `ManualRecoveryGuide.cs:25-29` | "two open standards", "Any SLIP-39 implementation plus any age implementation can rebuild the wallet" | the OpenPGP lock is mandatory; without GnuPG the heir cannot start |
| `ManualRecoveryGuide.cs:215` | specifications list SLIP-39 and age | OpenPGP missing from a list whose purpose is reimplementation from scratch |
| `ReadmeTemplate.cs:98` | recovery "using only standard SLIP-39 and age software" | same omission, in the document inside every share zip |
| `ShareFolder.cs:13`, `PayloadReadme.cs:4` | comments describing the artifact as `payload.age` | same constant |

The first four were found by `checks.sh`. The three GnuPG omissions were found by reading
`ManualRecoveryGuide.cs` against `PgpEnvelope`, which is pass 1 of the skill and the
reason it says to read the code rather than the prose.

**Why they survived:** every test in `Slip39Demo.Tests/Bundle` asserted facts about one
document at a time. Nothing compared a document against the builder, or against another
document, so a rename updated the code and the assertions and left the prose.

## 2. `independent-verify.min.js` is committed and nothing verifies it

**Verified:** 190 KB of minified slip39-js + typage + noble-hashes at
`Slip39Demo.UI/wwwroot/js/independent-verify.min.js`, last rebuilt in August, excluded
from the external-origin scan in both workflows (`--exclude=independent-verify.min.js`),
and no workflow runs `npm ci` or compares the output against `tools/independent-verify`.

The same repository fetches `age` and `appimagetool` at build time, pinned by version and
SHA-256, and compares them explicitly (`packaging/appimage/build-appimage.sh:81-97`).
`CLAUDE.md` states the rule as "third-party binaries are never committed". Two standards,
and the weaker one is on the artifact whose entire job is to be independent of us, and
which gates every backup the tool releases.

## 3. The hosted demo can show a green "offline" tick

**Verified:** `git grep ServingOrigin` returns nothing, so the recommendation recorded in
`docs/online-detection.md` is still unimplemented. On the Pages demo a visitor can pull
the network, watch `ConnectivityBanner` turn green, and conclude it is safe to type a real
seed while running WebAssembly a server sent them, unverifiable against this repository,
on their everyday computer. The banner is right about connectivity and wrong about safety,
and a green tick beats a paragraph of warning.

Not fixed in this pass. It changes a safety-relevant UI state and belongs in its own pull
request, as that document already says.

## 4. Documents that read as current and are not

- **Verified:** `docs/specs/2026-05-21-slip39-age-redesign-design.md` was marked "Draft
  for review" and its §5.3 and §5.7 tell the owner to put `payload.age` in a password
  manager, on a USB and in a safe. That is the arrangement decision 2 reversed as
  incoherent. Nothing in the document pointed forward.
- **Verified:** `docs/specs/2026-08-09-tauri-shell-and-styling-design.md` was marked
  "approved, not yet implemented". `Slip39Demo.Desktop` is deleted, `src-tauri` exists,
  `app.css` exists, and no Bootstrap is tracked.
- **Verified:** `README.md` called the download `output.zip` nine times while
  `Owner.BuildBundleFileName` produces `slip39-wallet-backup-<label>-<date>.zip` and shows
  that name on screen. One passage was also left half-edited by the artifact retirement,
  ending mid-sentence and still explaining "why all three ship".
- **Verified:** the README's "Two-layer encryption" diagram showed age and SLIP-39 and
  omitted the OpenPGP layer the section is named after.

## 5. `CLAUDE.md`'s branch-protection commands report the wrong answer

**Verified by running them.** `main` is protected by ruleset 12211639 (active; rules
`deletion`, `non_fast_forward`, `pull_request`; empty bypass list). `CLAUDE.md` documented
`gh api repos/.../branches/main/protection`, which is the legacy API and returns 404
"Branch not protected" here. This review ran that command and was one step from reporting
the repository as unprotected. The documented setup command would have added legacy
protection alongside the ruleset rather than confirming it.

`dice-to-seed` has both mechanisms, which is why the same text appears to work there.

## 6. Reproducibility: the share README depends on the build host

**Verified:** `ReadmeTemplate.Build` uses `StringBuilder.AppendLine`, so the shipped
`README.txt` carries CRLF from a Windows build and LF from the Linux CI that publishes the
AppImage. `ShareZipWriter` fixes timestamps specifically so share zips are reproducible;
this quietly defeated that for anyone comparing two builds.

## 7. Hygiene

**Verified unreferenced by `git grep`:** an empty `Seed-Phrase-Storage-SLIP39.slnx` with
no `<Project>` entries beside the real `Slip39Demo.slnx`, four tracked
`Slip39Demo.Web/.idea/` files with no `.idea` entry in `.gitignore`, and three root
leftovers (`ReadmeExample.linq`, `repro-xecrets-bug.csx`, `image.png`).

**Verified stale:** `AppImageEncryptorReachabilityTests.cs:20-22` described
`TauriAgeEncryptor` as not existing "until a later task" and a `NotWiredYet` placeholder
that is gone; `StylesheetContractTests.cs:28` cited `task-1-brief.md`, which is not in the
repository.

**Not acted on:** test method naming splits 140 `lower_snake_case` to 108
`Method_Condition_Result`, roughly per file. Renaming 248 tests to settle it is churn with
no reader benefit, and this review added to both camps.

**Verified, needs a decision that is not mine:** `origin/feat/one-armored-artifact` carries
two commits after the commit #20 merged. Both are on main under different hashes
(`26e5167`, `e6cfe16`), so the work is not lost and the branch is simply stale. Deleting a
remote branch is outward-facing, so it is left alone:
`git push origin --delete feat/one-armored-artifact` when convenient.

## 8. Against the sibling repositories

`dice-to-seed` is the only true peer (same stack, same Tails target, same Tauri shell).

| | slip39-backup | dice-to-seed |
| --- | --- | --- |
| version source | `Directory.Build.props` + `Cargo.toml`, with `VersionConsistencyTests` pinning agreement and asserting `tauri.conf.json` has no version of its own | a `VERSION` file plus `VERSIONING.md`, same test discipline |
| changelog | none | `CHANGELOG.md`, per release, in prose |
| decisions log | `docs/decisions/` | none |
| workflows | 2 (`appimage.yml` is CI and release) | 3 (`ci.yml`, `release.yml`, `pages.yml`) |
| `pre-push` release-tag warning | yes | **no**, though `release.yml` publishes on `v*` |
| `main` protection | ruleset only | ruleset **and** legacy protection |

Two of these are findings against the *sibling*, and they are the sharper ones: pushing a
`v*` tag in `dice-to-seed` publishes an AppImage people run against real seeds, its hook
does not warn, and its `CLAUDE.md` has no "a version tag is a release" rule. This
repository fixed both. A fix that stays in one of two repositories with the same hazard is
half a fix.

Coming the other way, this repository has no `CHANGELOG.md`, which is the same hole as the
release-notes gap: `v2.0.0` published the workflow's boilerplate instead of the annotated
tag message written for it.

`tails-appimage`'s field notes hold: appimagetool is pinned by version and checksum, and
no WebKitGTK is bundled.

---

## What was fixed in response

Everything in 1, 4, 5, 6 and 7, plus a `CHANGELOG.md`, in one pull request per theme.
Cross-document tests now hold the invariants that were missing (`ShippedDocumentConsistencyTests`),
including that a cleanup line may only name files the procedure creates, which is what
`check2.txt` violated.

Left open, each for a stated reason: 2 and 3 (their own pull requests, one CI and one
safety-relevant behaviour change), the test-naming split, the stale remote branch, and the
release-notes body.
