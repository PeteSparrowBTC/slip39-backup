---
name: repo-review
description: Use when reviewing this repository for defects, drift, or gaps; when README.md, TAILS_INSTRUCTIONS.md, the decisions log, or the documents shipped inside a backup bundle may disagree with the code that produces them; when checking whether a settled decision has been quietly reversed by a later change; when comparing this repository against its sibling dice-to-seed; or when asked what this tool should adopt from the other repositories in the organisation.
---

# Reviewing this repository

## The core principle

This repository is the tool. Almost every claim in its prose is a claim about
code sitting a few directories away: what the bundle contains, what the file is
called, what opens which lock, what the app refuses to do. The code is the
truth and the prose is a copy of it, so **a review that reads only the markdown
will find it beautifully self-consistent and miss that a constant changed six
weeks ago.**

Adapted from `bitcoin-backup-framework/.claude/skills/framework-review`, and the
core principle is inverted from it. That repository's claims are about *other*
repositories, so its review has to end outside itself. This repository's claims
are about *itself*, so its review has to end in the source: read
`OutputBundleBuilder.cs`, not the README's description of the bundle.

There is one exception, and it is the sharpest thing in here. Some documents are
**written into the backup and travel with it**: `MANUAL-RECOVERY.txt`,
`README.txt` in each share, `IMPORTANT-READ-FIRST.txt`, `VERIFY-THIS-BACKUP.txt`.
Those cannot be corrected after generation. A backup made today carries today's
text to an heir in 2040. Wrong prose in `Slip39Demo.Core/Bundle/*.cs` is not a
documentation defect; it is a defect in a shipped artifact, and it outranks
almost everything else in this file.

## The six passes

Run them in this order. Each one feeds the next.

### 1. The shipped documents, against the code that fills them

Start here, not with the README, because this is the text nobody can fix later.

| Claim | Where the truth is |
| --- | --- |
| what files the bundle contains and their names | `Slip39Demo.Core/Bundle/OutputBundleBuilder.cs`, `PayloadFileName` |
| what the download is called | `Slip39Demo.UI/Pages/Owner.razor`, `BuildBundleFileName` |
| what is inside a share zip | `Slip39Demo.Core/Bundle/ShareFolder.cs` |
| the commands an heir is told to run | `Slip39Demo.Core/Bundle/ManualRecoveryGuide.cs` against `PgpEnvelope` and the age layer |
| what the owner is told to verify, and how | `Slip39Demo.Core/Bundle/VerifyGuide.cs` |
| the default group shape and threshold | `Slip39Demo.UI/Pages/Owner.razor` |
| the payload fields an heir will read | `Slip39Demo.Core/Payload/PayloadEmitter.cs` |

Two failure shapes recur. A **count** that stopped being true: "three forms",
"any one of these", "both files". And a **name** that moved: the payload file has
been renamed twice, and each rename left the old spelling in prose that reads as
current instruction.

When a document deliberately mentions a retired name to help somebody holding an
old backup, it must say so on the same line ("older backups may hold ..."). That
is the difference between a helpful compatibility note and a stale instruction,
and it is the only thing distinguishing them to a reader or to `checks.sh`.

### 2. The decisions log, against the code

`docs/decisions/` records what was settled and, more importantly, the **limit** of
each decision. Decisions get reversed by accident: someone adds a file, or a
fallback, or a second copy, and the reasoning three sections up stops holding.
Decision 2 was reversed exactly this way and went unnoticed because the two
halves were written months apart.

For each decision, ask the question the other way round: not "is this still what
we do" but "does anything we now do contradict the limit stated here". The
limits, not the conclusions, are where the review earns its keep.

Then check the risk ordering at the end of that document. An item marked unfixed
that has since been fixed makes the whole list untrustworthy, and an item marked
fixed that regressed is worse.

### 3. Third-party artifacts, and which standard each one is held to

This repository holds third-party code to two different standards, and the
inconsistency is easy to reintroduce:

- `age` and `appimagetool` are fetched at build time, pinned by version **and**
  SHA-256, and compared explicitly. See `packaging/appimage/build-appimage.sh`.
- `Slip39Demo.UI/wwwroot/js/independent-verify.min.js` is committed, minified,
  built from `tools/independent-verify`, and excluded from the external-origin
  scan in both workflows.

For every committed artifact nobody reads in a diff, ask what proves it came from
the inputs it claims. `CLAUDE.md` states the rule as "third-party binaries are
never committed"; a committed bundle is the same class of thing whatever its
extension. `checks.sh` reports whether CI rebuilds and compares it.

### 4. The workflows, and the repository settings they assume

Both workflows publish something a user runs, so read them as code:

- `appimage.yml` gates the released AppImage on the test suite and must allow
  **no** external origin at all. `pages.yml` allows exactly the three
  connectivity-probe URLs. That difference is deliberate; a change that unifies
  them by loosening the AppImage side is a defect.
- The external-origin allowlist exists in both files and in
  `Slip39Demo.Web/wwwroot/connectivity.js`. Nothing enforces agreement.
- `pull_request` on `appimage.yml` carries no paths filter, deliberately: a
  filtered-out pull request reports no check rather than a passing one.

Then check the live settings rather than the documentation of them. `main` is
protected by a **ruleset**, not by legacy branch protection, so
`gh api repos/.../branches/main/protection` returns 404 "Branch not protected" on
a repository that is fully protected. Use `gh api repos/.../rulesets`. This has
already produced one false finding in a review; do not let it produce a second.

### 5. Safety-relevant states in the interface

The app has states that tell a user it is safe to proceed. Enumerate them and ask
what each one is actually evidence of.

The known one, documented in `docs/online-detection.md`: on the hosted demo a
visitor can pull the network and be shown a green "no internet reachable" tick,
which is true about connectivity and wrong about safety, because the code they
are running arrived from a server they cannot audit. A green tick beats a
paragraph of warning every time.

Apply the same test to any check that can pass vacuously. A verification that
cannot fail reads as evidence and proves nothing, which is worse than no
verification at all: it is why the in-bundle browser verifier was deleted, why
the browser build reports its outer-lock check as unavailable rather than
verified, and the first thing to look for in any new gate.

### 6. The sibling repository

`dice-to-seed` is the only real peer: same stack, same Tails target, same Tauri
shell, same author. `tails-appimage` is the field-notes source for packaging;
`bitcoin-backup-framework` is downstream of this tool and quotes its output.

Compare mechanisms rather than features, and be willing to find that this
repository is the one behind. Check both directions: what does the sibling do
that is better, and what has this repository fixed that the sibling still has
wrong. A fix that stays in one of two repositories with the same hazard is half
a fix, and the sibling's `pre-push` hook and `CLAUDE.md` are worth reading every
time for exactly that reason.

**Check out the right branch first.** `git log origin/main` in the sibling. A
sibling left on a feature branch shows you the tool as it was, and you will
confirm a claim its main has already contradicted.

## Producing the review

Write to `reviews/YYYY-MM-DD-repo-review.md`, and open no pull request until the
review exists. Each finding carries a file and line reference, what is wrong, and
what makes it wrong: a source file, a command and its output, or a computation.

Rank by what a reader or an heir loses by acting on the repository as it stands.
A shipped recovery document that names a file the tool no longer produces beats
any number of stale comments, because somebody follows it under stress with money
at stake and nobody can correct it by then.

State plainly which findings you verified and which you inferred. "I read the
constant in `OutputBundleBuilder.cs` on origin/main" and "the README implies" are
different strengths of claim, and the review is worth less if they read alike.

## Common mistakes

| Mistake | What it costs |
| --- | --- |
| Reviewing the markdown and calling the repository consistent | You miss every drift between prose and code, which is the only way this repository goes wrong |
| Grepping for a name that is a prefix of the corrected name | `payload.age` matches `payload.age.gpg.asc`, so a completed fix looks unshipped and a stale mention hides behind a correct one |
| Reading `branches/main/protection` and reporting main unprotected | A false finding. Protection here is a ruleset |
| Treating the bundle documents as documentation | They are shipped artifacts. Nobody can correct them after a backup is made |
| Fixing findings on a branch stacked on an unmerged branch | Work strands when the parent merges first. It has happened repeatedly in this organisation |
| Declaring the demo correct from the working tree | The demo is the last deployed commit. Check `gh run list --workflow=pages.yml` against `origin/main` |
| Rewriting the historical specs and plans | They are dated records of what was decided. Correct them with a status line pointing forward, never by editing the body |

## Scope

Review only. Do not fix while reviewing: mixing findings with fixes hides which
finding a change answered, and a fix written mid-review tends to answer the
symptom. Fixes go on a branch off `main`, one pull request per theme, after the
review is written.

Never push to `main` and never merge a pull request; see `CLAUDE.md`.
