namespace Slip39Demo.Core.Bundle;

// The tool-independent recovery manual. Travels inside EVERY share zip (and the
// bundle root): at recovery time the executor typically holds gathered share
// zips + payload.age.gpg.asc, not our app, so this document lets them rebuild
// the wallet using only widely-available standard tools (any SLIP-39
// implementation, GnuPG, and any age implementation), per the design's core
// durability pillar.
//
// Every tool recommended here has been executed against a real backup produced
// by this tool (2026-07-19): python-shamir-mnemonic and the revised
// 3rditeration web page both recovered K byte-identically; typage decrypted
// the payload. The original iancoleman page REJECTS our extendable shares, so
// the guide warns about it and good shares are not misdiagnosed as corrupt.
//
// Contains NO secret data. Plain text, printable, written for someone with
// basic technical skills following steps under stress.
public static class ManualRecoveryGuide
{
    public const string FileName = "MANUAL-RECOVERY.txt";

    public const string Text =
"""
RECOVERING THIS WALLET WITHOUT THE ORIGINAL TOOL
================================================================
This backup uses three open standards: SLIP-39 (Shamir share
mnemonics), OpenPGP (the outer lock) and age v1 (the inner one).
Nothing about it needs the tool that created it. Any SLIP-39
implementation, GnuPG, and any age implementation can rebuild the
wallet. This document shows how, including how to install the
tools.

WHAT YOU NEED
----------------------------------------------------------------
 1. Enough share mnemonics to meet the backup's threshold. Each
    share is one line of words in a file named share.slip39.
    (A SLIP-39 tool will tell you how many you need: it is
    encoded in the shares themselves.)
 2. The encrypted payload file, called payload.age.gpg.asc. It
    was kept by the owner: password manager, safe, or with their
    executor. Older backups may hold payload.age or
    payload.age.txt instead, which is the same secret with one
    lock rather than two; Step 2 covers both.
 3. An OFFLINE computer running Tails (tails.net) from a USB
    stick. Boot it and do NOT connect to the internet. This is
    mandatory, not a preference: recovery puts your seed words
    on the screen, and a network connection at that moment is
    the one mistake you cannot undo afterwards.
 4. A second, ordinary USB stick carrying the tools, prepared
    below BEFORE going offline.

PART A: DOWNLOAD THE TOOLS (online machine, no secrets here)
----------------------------------------------------------------
Do this on any normal computer. Nothing secret is involved yet.

A1. SLIP-39 tool, command-line (the reference implementation):
    Open a terminal and run:
        pip download shamir-mnemonic -d shamir-pkgs
    This creates a folder shamir-pkgs with everything needed for
    an offline install. Copy the whole folder to the USB stick.

A2. SLIP-39 tool, browser (no command line needed):
    Visit:
        https://3rditeration.github.io/slip39/src/
    In the browser: Ctrl+S (Save Page As...) and choose
    "Webpage, Complete". Copy the saved .html file AND the folder
    saved next to it onto the USB stick.
    (Do NOT use the older page at iancoleman.io/slip39: see the
    warning at the end of Step 1.)

A3. age, for decrypting the payload:
    NOTE: age is NOT preinstalled on Tails (Tails ships GnuPG,
    not age), and installing software on Tails needs internet,
    so bring the program on the USB stick instead.
    Visit the releases page:
        https://github.com/FiloSottile/age/releases
    Download the Linux build, a file named like:
        age-v1.2.x-linux-amd64.tar.gz
    (For a Windows or macOS recovery machine, take that OS's file
    instead.) Copy it to the USB stick.

Now move to the offline machine with that USB stick. Never type
seed material on a computer that is, or will be, connected to
the internet.

PART B: INSTALL ON THE OFFLINE MACHINE
----------------------------------------------------------------
On Tails: open Applications > Utilities > Terminal. Plug in the
USB stick and change into it (it mounts under /media/amnesia/).

B1. Install the SLIP-39 command-line tool (from A1):
        python3 -m pip install --no-index --find-links shamir-pkgs shamir-mnemonic
    This works with no internet. The command installed is called
    `shamir`. If typing `shamir` says "command not found", call it
    as:
        ~/.local/bin/shamir
    or:
        python3 -m shamir_mnemonic.cli

B2. Unpack age (from A3):
        tar -xzf age-v*-linux-amd64.tar.gz
    This creates a folder `age` containing the program, also
    called `age`. Make sure it is runnable:
        chmod +x age/age

STEP 1: REBUILD THE MASTER KEY FROM THE SHARES
----------------------------------------------------------------
Command-line path (tool from B1):
 1. Run:
        shamir recover
 2. It prompts for a share mnemonic. Type ONE share's words (all
    on one line, single spaces) and press Enter.
 3. It shows how many more shares are needed; repeat for each
    share until it says the secret was recovered. Use EXACTLY
    threshold-many shares, and do not enter extras (if you hold 5
    shares of a 3-of-5 backup, enter any 3 and stop).
 4. It prints the recovered master secret: a 64-character
    hexadecimal string (digits 0-9 and letters a-f). This is the
    KEY, not yet the wallet. Copy it exactly, all lowercase.

Browser path (page from A2, works fully offline):
 1. Open the saved .html file in the browser (double-click it).
 2. Scroll to the recovery section with the box labelled
    "existing shares".
 3. Paste EXACTLY threshold-many share mnemonics into that box,
    ONE SHARE PER LINE.
 4. Read the 64-character result from the "reconstructed hex"
    field below. Copy it exactly, all lowercase.

WARNING about the well-known iancoleman.io/slip39 web page: as of
2026 it does NOT understand modern "extendable" SLIP-39 shares
(which this backup uses) and reports "Invalid mnemonic checksum".
Do not conclude your shares are bad because that page rejects
them: both tools above are verified to work with these shares.

STEP 2: DECRYPT THE PAYLOAD WITH THAT KEY
----------------------------------------------------------------
The file has TWO locks, one inside the other, and the same key
opens both. So this is two commands, not one.

FIRST LOCK, with GnuPG. It is already installed on Tails, so
there is nothing to download. In the terminal, in the folder
holding your payload file:

    gpg -d payload.age.gpg.asc > payload.age

It will ask for a passphrase. Type (or paste) the 64-character
hex string from Step 1, lowercase, no spaces. Nothing appears
while you type a passphrase; that is normal. Press Enter.

You now have a file called payload.age. That is the inner file,
still locked.

SECOND LOCK, with age, the program unpacked in Part B:

    ./age/age -d payload.age > wallet.txt

Same passphrase, the same 64-character hex string. That hex
string IS the passphrase for both locks.

Why two locks. A weakness found one day in one of the two
encryption formats would still leave the other one standing.
Older backups made by this tool held the inner file on its own,
called payload.age or payload.age.txt; if that is what you have,
skip the GnuPG command and start at the age command.

Any other age implementation also works the same way, e.g. rage
(https://github.com/str4d/rage: download rage-...-linux.tar.gz
from its releases page, unpack, then `./rage -d payload.age`).

STEP 3: READ THE WALLET
----------------------------------------------------------------
Open wallet.txt (or run: cat wallet.txt). It is plain text:
    seed_words:       the BIP-39 seed phrase of the wallet
    passphrase:       (if present) the BIP-39 passphrase, needed
                      together with the seed words
    derivation_path:  where the accounts live (e.g. m/84'/0'/0')
    xpub_fingerprint: 8 hex characters identifying the wallet.
                      After restoring, your wallet software should
                      show this same fingerprint. If it does not,
                      something is wrong: stop and re-check.
Enter the seed words (and passphrase, if any) into a hardware
wallet or wallet software of your choice to restore the funds.

SPACES IN THE PASSPHRASE LINE. Every line reads "key: value"
with exactly ONE space after the colon. That space is the
separator; everything after it belongs to the value. So a line
reading
    passphrase:  hunter2
carries the passphrase " hunter2", with a leading space, and a
line whose last character is a space carries a passphrase that
ends in one. This matters only for the passphrase, and only
rarely, but a space you cannot see is the difference between the
right wallet and an empty one. If the wallet you restore shows
an xpub_fingerprint different from the one in this file, then
count the spaces on that line before looking anywhere else.

IF SOMETHING FAILS
----------------------------------------------------------------
 - "Invalid mnemonic checksum"  -> a word is wrong in one share.
   Re-read it carefully from the printed/typed copy. (Or you are
   on the old iancoleman page: see the warning above.)
 - SLIP-39 tool wants more shares -> you are below the threshold;
   gather more.
 - age says the passphrase is wrong -> re-copy the hex from
   Step 1 (all 64 characters, lowercase, no spaces/line breaks).
 - "command not found" -> use the full path (~/.local/bin/shamir
   or ./age/age) or re-check Part B.
 - "Permission denied" running age -> chmod +x age/age
 - Shares from different backups do not mix. All shares must come
   from the same generation (their first words look similar).

SPECIFICATIONS (for reimplementation from scratch)
----------------------------------------------------------------
 SLIP-39:  https://github.com/satoshilabs/slips/blob/master/slip-0039.md
 OpenPGP:  https://www.rfc-editor.org/rfc/rfc9580.html
 age v1:   https://age-encryption.org/v1
================================================================
""";
}
