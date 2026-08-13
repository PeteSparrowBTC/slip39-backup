#!/usr/bin/env bash
# Mechanical half of the repository review. Reports, never edits.
#
# These are the checks a reader or an heir would eventually hit and that a human
# reviewer reliably misses: a shipped recovery document naming a file the tool no
# longer produces, a document whose status line still says "not yet implemented"
# for something that shipped weeks ago, a committed third-party bundle nothing
# rebuilds, and work sitting on a branch main will never take.
#
# The judgement half of the review is in SKILL.md and is not automatable.
#
# Adapted from bitcoin-backup-framework/.claude/skills/framework-review/checks.sh.
# Checks 1 to 5 are specific to this repository; 6 to 8 are general and were kept
# close to the original so a fix to either can be carried across.
#
# Exit code is the number of findings, so CI could gate on it later. Today it is
# run by hand at the start of a review.

set -u

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
cd "$ROOT"

# Prose a user or an heir actually reads. Deliberately NOT docs/specs or
# docs/plans: those are dated records of what was decided, and correcting them
# means adding a status line that points forward, never editing the body.
DOCS="README.md TAILS_INSTRUCTIONS.md CLAUDE.md docs/online-detection.md docs/decisions/*.md"

# The subset that gives current instruction. docs/decisions is excluded because
# recording a reversal means quoting the arrangement that was reversed: decision 2
# has to be able to say what it used to say, or the record is worthless.
INSTRUCTIONS="README.md TAILS_INSTRUCTIONS.md CLAUDE.md docs/online-detection.md"

# Text that is written INTO a backup and travels with it. Nobody can correct
# these after a bundle is generated, so they are checked as artifacts rather than
# as documentation.
SHIPPED=$(ls Slip39Demo.Core/Bundle/*.cs 2>/dev/null)

FINDINGS=0

say() { printf '\n== %s\n' "$1"; }
hit() { printf '  %s\n' "$1"; FINDINGS=$((FINDINGS + 1)); }

# --------------------------------------------------------------------------
# 1. The payload filename, against the constant that decides it.
# --------------------------------------------------------------------------
# The payload file has been renamed twice. Each rename left the old spelling in
# prose that reads as current instruction, including inside the documents that
# ship with the backup.
#
# The match is anchored so the retired name is not found as a prefix of the
# current one: grepping "payload.age" matches "payload.age.gpg.asc" and reports a
# completed fix as unshipped while hiding a genuinely stale line.
#
# A line that names a retired form to help somebody holding an old backup is
# allowed, but it has to say so ON THE SAME LINE. That qualifier is the only
# thing separating a compatibility note from a stale instruction, for a reader as
# well as for this check.
#
# payload.age also legitimately appears as the INTERMEDIATE file: recovery is
# `gpg -d payload.age.gpg.asc > payload.age` and then `age -d payload.age`, so the
# recovery guide has to name it. Those lines are recognised by the command they
# carry rather than by a qualifier.
say "payload filename, against OutputBundleBuilder"
ACTUAL=$(grep -oE 'PayloadFileName = "[^"]+"' Slip39Demo.Core/Bundle/OutputBundleBuilder.cs 2>/dev/null \
         | grep -oE '"[^"]+"' | tr -d '"')
if [ -z "$ACTUAL" ]; then
    hit "could not read PayloadFileName from Slip39Demo.Core/Bundle/OutputBundleBuilder.cs"
else
    printf '  tool emits: %s\n' "$ACTUAL"
    # The qualifier is accepted on the line before as well as on the line itself,
    # because this text is hard-wrapped: "Older backups may hold payload.age or"
    # puts the qualifier and the name on different lines, and a same-line-only test
    # reports every wrapped compatibility note in the repository.
    # Word-guarded, and not optionally: an unguarded "[Oo]lder" matches the "older"
    # inside "folder", which silenced a genuinely stale line in PayloadReadme.cs the
    # first time this ran. Substring matching is the failure mode this whole check
    # exists to catch, so it should not be the failure mode of the check itself.
    QUALIFIED='(^|[^A-Za-z])([Oo]lder|[Ee]arlier|[Rr]etired|[Pp]revious|[Ll]egacy)|used to|no longer|inner file'
    INTERMEDIATE='age -d|rage -d|gpg -d|> payload\.age'
    STALE=$(awk -v actual="$ACTUAL" -v ok="$QUALIFIED|$INTERMEDIATE" '
        FNR == 1 { prev = "" }
        {
            line = $0
            if (line ~ /payload\.age[A-Za-z.]*/ && index(line, actual) == 0 &&
                line !~ ok && prev !~ ok)
                printf "%s:%d:%s\n", FILENAME, FNR, line
            prev = line
        }' $INSTRUCTIONS $SHIPPED 2>/dev/null | head -12)
    if [ -n "$STALE" ]; then
        printf '%s\n' "$STALE" | sed 's/^/    /'
        hit "prose names a payload file the tool does not emit, with no compatibility qualifier"
    fi
fi

# --------------------------------------------------------------------------
# 2. The download filename, against the function that builds it.
# --------------------------------------------------------------------------
# The bundle was called output.zip for a long time and is now named after the
# wallet and the date. Owner.razor shows the real name on screen, so a document
# still saying output.zip contradicts the app in front of the reader.
say "download filename, against Owner.razor"
if grep -q 'slip39-wallet-backup' Slip39Demo.UI/Pages/Owner.razor 2>/dev/null; then
    printf '  app emits: slip39-wallet-backup-<label>-<date>.zip\n'
    OLDZIP=$(grep -nE 'output\.zip' $DOCS $SHIPPED 2>/dev/null | head -12)
    if [ -n "$OLDZIP" ]; then
        printf '%s\n' "$OLDZIP" | sed 's/^/    /'
        hit "prose calls the download output.zip; the app does not"
    fi
else
    hit "could not find the bundle filename builder in Slip39Demo.UI/Pages/Owner.razor"
fi

# --------------------------------------------------------------------------
# 3. Status lines that outlived what they describe.
# --------------------------------------------------------------------------
# A spec marked "approved, not yet implemented" for something that shipped, or
# "Draft for review" on a document whose guidance has since been reversed, sends
# a reader to the wrong authority. Reported for confirmation rather than judged:
# only a human knows whether the body is still current.
say "document status lines"
STATUS=$(grep -rniE '^\**(status|\*\*status\*\*)\**:?\**[[:space:]]*(draft|approved, not yet implemented|not yet implemented|proposed)' \
         docs/ 2>/dev/null | head -12)
if [ -n "$STATUS" ]; then
    printf '%s\n' "$STATUS" | sed 's/^/    /'
    hit "status lines to confirm against what actually shipped"
fi
SUPERSEDED=$(grep -rlniE 'supersed' docs/specs/*.md 2>/dev/null | wc -l | tr -d ' ')
printf '  %s spec(s) declare what they supersede\n' "$SUPERSEDED"

# --------------------------------------------------------------------------
# 4. Committed third-party artifacts nothing rebuilds.
# --------------------------------------------------------------------------
# age and appimagetool are fetched, pinned by version AND checksum, and compared
# explicitly. independent-verify.min.js is committed, minified, excluded from the
# external-origin scan, and gates every backup. Two standards, and the weaker one
# is on the artifact whose whole job is to be independent.
say "third-party bundle provenance"
BUNDLE="Slip39Demo.UI/wwwroot/js/independent-verify.min.js"
if [ -f "$BUNDLE" ]; then
    if grep -rq 'npm ci\|npm install' .github/workflows/ 2>/dev/null; then
        printf '  a workflow installs the bundle inputs; check that it also compares the output\n'
    else
        hit "$BUNDLE is committed and no workflow rebuilds it from tools/independent-verify"
    fi
    if [ -f tools/independent-verify/package-lock.json ]; then
        printf '  inputs are locked (tools/independent-verify/package-lock.json)\n'
    else
        hit "tools/independent-verify has no package-lock.json, so the inputs are not pinned"
    fi
fi
if grep -q 'AGE_SHA256=' packaging/appimage/build-appimage.sh 2>/dev/null; then
    printf '  age tarball is pinned by checksum\n'
else
    hit "packaging/appimage/build-appimage.sh no longer pins the age tarball by checksum"
fi

# --------------------------------------------------------------------------
# 5. Style constructions this repository has ruled out, in prose people read.
# --------------------------------------------------------------------------
# Scoped to $DOCS and $SHIPPED for the reason given at the top: the historical
# specs carry hundreds of these and rewriting them would destroy the record they
# exist to be.
say "banned constructions in user-facing prose"
for pattern in '—' '–' '“' '”' '’'; do
    found=$(grep -n "$pattern" $DOCS $SHIPPED 2>/dev/null | head -4)
    if [ -n "$found" ]; then
        printf '  pattern %s\n' "$pattern"
        printf '%s\n' "$found" | sed 's/^/    /'
        hit "banned construction: $pattern"
    fi
done

# --------------------------------------------------------------------------
# 6. Files tracked that should not be.
# --------------------------------------------------------------------------
say "tracked files that look accidental"
STRAY=$(git ls-files | grep -E '(^|/)\.idea/|\.linq$|\.csx$|\.user$|\.DotSettings$' | head -10)
if [ -n "$STRAY" ]; then
    printf '%s\n' "$STRAY" | sed 's/^/    /'
    hit "IDE or scratch files are tracked"
fi
for sln in $(git ls-files '*.slnx' '*.sln' 2>/dev/null); do
    if ! grep -q '<Project' "$sln" 2>/dev/null; then
        hit "$sln lists no projects; opening it gives an empty solution"
    fi
done

# --------------------------------------------------------------------------
# 7. Commits that exist only on a branch main will never take.
# --------------------------------------------------------------------------
# Kept from the original, which explains it best: the sharpest case is the one
# that looks finished. A pull request merges, a further commit lands on the same
# branch minutes later, and nothing ever takes it. "Ahead of main" cannot detect
# that, because a squash merge leaves every merged branch permanently ahead. What
# separates the two is the commit GitHub actually took.
say "stranded commits"
git fetch origin --quiet 2>/dev/null
if ! command -v gh >/dev/null 2>&1; then
    printf '  skipped: gh not on PATH\n'
else
for ref in $(git for-each-ref --format='%(refname:short)' refs/remotes/origin | grep -v 'origin/main\|origin/HEAD'); do
    ahead=$(git rev-list --count "origin/main..$ref" 2>/dev/null || echo 0)
    [ "$ahead" = "0" ] && continue
    head_name="${ref#origin/}"
    tip=$(git rev-parse "$ref")
    pr=$(gh pr list --state all --head "$head_name" --limit 1 \
         --json number,state,headRefOid --jq '.[] | "\(.number) \(.state) \(.headRefOid)"' 2>/dev/null)
    if [ -z "$pr" ]; then
        hit "$head_name: $ahead commits ahead of main and never had a pull request"
        continue
    fi
    set -- $pr
    num=$1; state=$2; merged_oid=$3
    case "$state" in
        OPEN)   printf '  %s: open in #%s\n' "$head_name" "$num" ;;
        MERGED)
            if [ "$tip" != "$merged_oid" ]; then
                extra=$(git rev-list --count "$merged_oid..$ref")
                hit "$head_name: #$num merged at ${merged_oid:0:8}, branch has $extra commit(s) after it"
                git --no-pager log --oneline "$merged_oid..$ref" | sed 's/^/      /'
            else
                printf '  %s: merged in #%s (squash artifact)\n' "$head_name" "$num"
            fi ;;
        *)      hit "$head_name: #$num is $state and the branch is $ahead ahead" ;;
    esac
done
fi

# --------------------------------------------------------------------------
# 8. The sibling repository, and the deployed demo.
# --------------------------------------------------------------------------
# Reviewing a sibling on a feature branch is how a corrected tool gets reported
# as broken, and a broken one as fine.
say "sibling checkouts"
for sib in ../dice-to-seed ../bitcoin-backup-framework ../tails-appimage; do
    [ -d "$sib/.git" ] || continue
    branch=$(git -C "$sib" rev-parse --abbrev-ref HEAD 2>/dev/null)
    behind=$(git -C "$sib" rev-list --count "HEAD..origin/main" 2>/dev/null || echo "?")
    if [ "$branch" != "main" ] || [ "$behind" != "0" ]; then
        hit "$(basename "$sib"): on '$branch', $behind commits behind origin/main"
    else
        printf '  %s: main, current\n' "$(basename "$sib")"
    fi
done

# The demo is the last deployed commit, not the working tree.
say "published demo"
if command -v gh >/dev/null 2>&1; then
    gh run list --workflow=pages.yml --limit 1 \
        --json headSha,conclusion,createdAt \
        --jq '.[] | "  last deploy \(.headSha[0:8]) \(.conclusion) \(.createdAt)"' 2>/dev/null
    printf '  origin/main is %s\n' "$(git rev-parse --short origin/main)"
fi

# Protection here is a ruleset. The legacy endpoint returns 404 on a protected
# repository, which has already produced one false finding.
say "main protection (ruleset, not legacy branch protection)"
if command -v gh >/dev/null 2>&1; then
    gh api repos/PeteSparrowBTC/slip39-backup/rulesets \
        --jq '.[] | "  ruleset \(.name): \(.enforcement)"' 2>/dev/null \
        || printf '  could not read rulesets\n'
fi

printf '\n%d finding(s). The judgement half of the review is in SKILL.md.\n' "$FINDINGS"
exit "$FINDINGS"
