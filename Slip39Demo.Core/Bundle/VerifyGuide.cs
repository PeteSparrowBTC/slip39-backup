namespace Slip39Demo.Core.Bundle;

// Owner-facing verification procedure, shipped as payload/VERIFY-THIS-BACKUP.txt.
//
// The app already refuses to release a bundle unless independent implementations
// round-trip it in both directions. That check is worth having, but it runs
// inside this program, using libraries this program ships. It cannot rule out
// the case where the whole binary is wrong, or substituted. The only check that
// can is one the user runs with software that has no relationship to this tool.
//
// Distinct from MANUAL-RECOVERY.txt, which is written for the heir at recovery
// time, years from now, under stress. This one is for the OWNER, right now,
// while the airgapped machine is still booted and nothing has been distributed:
// the last moment at which discovering a problem is cheap.
//
// Contains NO secret data. Plain text, printable, wrapped for paper.
public static class VerifyGuide
{
    public const string FileName = "VERIFY-THIS-BACKUP.txt";

    public const string Text =
"""
VERIFY THIS BACKUP YOURSELF
================================================================
The tool told you this backup was verified. It checked itself,
with software it shipped. That catches a lot, but it cannot
catch a tool that is wrong (or replaced) in the same way twice.

This page shows how to prove your payload.age decrypts, using
programs written by other people that know nothing about this
tool. It takes about twenty minutes and you only do it once.

DO IT NOW, BEFORE you hand out any share and before you shut
down. Right now the shares are all in front of you and a problem
is a minor annoyance. After distribution it is an expedition.

  WARNING: this procedure decrypts your wallet to a plain file.
  Do it ONLY on the offline machine (Tails), and shut that
  machine down when finished. Tails keeps nothing after
  shutdown, which is exactly why it is the right place.

WHAT YOU NEED
----------------------------------------------------------------
 1. Threshold-many share mnemonics from this backup (if it is a
    3-of-5, any 3 of the share zips).
 2. payload.age and payload.age.txt from the payload/ folder.
 3. verification-record.txt from the bundle root.
 4. A USB stick with the tools, prepared in PART A below.

PART A - COLLECT THE TOOLS (on your ONLINE machine)
----------------------------------------------------------------
Do this BEFORE booting the offline machine. Nothing secret is
involved in this part, so an ordinary computer is fine.

Every program below was written by someone else. That is the
whole point of this exercise: they have no reason to agree with
the tool that made your backup unless the backup is genuinely
correct. Download them from their own project pages, listed
here, and not from anywhere this tool sent you.

Tails does not come with any of them. Tails ships GnuPG, and
installing software on Tails needs internet access, which
defeats the point of an airgapped session. So carry the programs
in on a USB stick instead.

A1. age, by Filippo Valsorda, who designed the age format. This
    is the reference implementation, written in Go.
        Downloads: github.com/FiloSottile/age/releases
        Source:    github.com/FiloSottile/age
    Take the newest version, and take the LINUX file, because
    you are going to run it on Tails:
        age-v1.3.1-linux-amd64.tar.gz
    (version numbers will differ; "amd64" is right for any
    ordinary PC or Intel Mac. There are Windows and macOS builds
    on that page. Ignore them. You do not want to be typing your
    seed words into your everyday computer.)

    There is no AppImage and none is needed. Inside the archive
    is a single self-contained program, about 6 MB, that needs
    nothing installed: unpack it and run it. Checked against
    v1.3.1: it is statically linked and depends on no system
    libraries at all, so it runs on Tails as it stands.

    Note the fingerprint of what you downloaded:
        sha256sum age-v1.3.1-linux-amd64.tar.gz
    Write the result down. After copying the file to the USB
    stick, run the same command on the offline machine and check
    you get the same answer. That proves the copy arrived intact.

    age does not publish a plain list of checksums to compare
    against, so this checks the copy, not the origin. Your
    protection there is that you downloaded it over a secure
    connection from the project's own release page. If you want
    more, every download has a matching .proof file and the age
    project documents how to verify it with sigsum.

    Copy the .tar.gz (or .zip) to the USB stick.

A2. A SLIP-39 tool, to turn your shares back into the key that
    age needs as its passphrase. This one is by SatoshiLabs, who
    wrote the SLIP-39 specification:
        Source:    github.com/trezor/python-shamir-mnemonic
    Install it for offline use with:
        pip download shamir-mnemonic -d shamir-pkgs
    That folder installs with no internet later. Copy the whole
    shamir-pkgs folder to the USB stick.

    Prefer a browser? There is a third-party page that does the
    same job, and it works offline once saved:
        Page:      3rditeration.github.io/slip39/src/
        Source:    github.com/3rdIteration/slip39
    Open it, press Ctrl+S, choose "Webpage, Complete", and copy
    the saved .html file AND the folder saved beside it onto the
    USB stick.

    Do NOT use the older page at iancoleman.io/slip39. It
    predates the SLIP-39 extendable-backup flag and rejects
    valid shares from this tool as if they were corrupt.

A3. Optional: rage, a separate age implementation written in
    Rust by str4d. Unrelated to age's author and to this tool,
    so if it agrees as well, three independent groups of people
    agree about your backup.
        Downloads: github.com/str4d/rage/releases
        Source:    github.com/str4d/rage
    Again the Linux file (version numbers will differ):
        rage-v0.12.1-x86_64-linux.tar.gz
    Same idea: note its fingerprint, copy it to the USB stick.

    If Tails refuses to run it, complaining about a missing or
    too-old library, take the musl build from the same page
    instead, which carries everything it needs inside it:
        rage-musl_0.12.1-1_amd64.deb
    You do not install it. Unpack it in place with:
        dpkg-deb -x rage-musl_0.12.1-1_amd64.deb rage-musl
    and the program is then at rage-musl/usr/bin/rage.

PART B - CHECK IT (on the OFFLINE machine)
----------------------------------------------------------------
Boot Tails, stay offline, plug in both USB sticks. Open
Applications > Utilities > Terminal, and change into the folder
holding payload.age.

B1. Unpack the tools:
        tar -xzf age-v*-linux-amd64.tar.gz
        chmod +x age/age
        python3 -m pip install --no-index -f shamir-pkgs shamir-mnemonic
    (-f is the short form of --find-links: install from that
    folder instead of the internet.)

B2. Rebuild the key from your shares:
        shamir recover
    Type ONE share's words per prompt, all on one line. Enter
    EXACTLY threshold-many shares and no more. It prints a
    64-character hexadecimal string. That string is the key.
    (If `shamir` is not found, try ~/.local/bin/shamir or
    python3 -m shamir_mnemonic.cli)

B3. Decrypt the binary blob with it:
        ./age/age -d payload.age > check.txt
    At "Enter passphrase:" type the 64 hex characters from B2,
    lowercase, no spaces. Nothing appears as you type. Enter.

        cat check.txt

    You should see your seed words, your passphrase if you set
    one, your derivation path and label. Read them. Compare them
    against what you typed into the tool, character for
    character, especially the passphrase: a leading space, a
    stray character, anything that does not match means this
    backup restores a DIFFERENT wallet than you think.

B4. Decrypt the armored copy too, and confirm it is identical:
        ./age/age -d payload.age.txt > check2.txt
        diff check.txt check2.txt && echo "ARMOR OK"
    Silence from diff plus "ARMOR OK" means the text form you
    paste into a password manager carries the same secret as the
    binary. If they differ, do not distribute anything.

B5. Confirm the file has not been altered since it was made:
        sha256sum payload.age
    Compare with the "Payload integrity" line in
    verification-record.txt. Same value means the blob on your
    USB is the blob the tool produced.

B6. (Optional, from A3) Have a second, unrelated implementation
    read the same file. Unpack it first:
        tar -xzf rage-*-linux.tar.gz
        chmod +x rage/rage
    then:
        ./rage/rage -d payload.age > check3.txt
        diff check.txt check3.txt && echo "SECOND TOOL OK"

WHAT THE RESULT MEANS
----------------------------------------------------------------
Everything above passes:
    Your payload.age is a valid age file, the standard tools can
    open it, your shares really do produce its key, and the
    contents are what you intended. This backup does not depend
    on the tool that made it. That is the whole point.

age says "incorrect passphrase" or similar:
    The key from B2 is not the key the file was encrypted with.
    Either you entered the wrong shares (shares from a DIFFERENT
    backup look equally valid), or the shares and the blob are
    from different runs. Check you used shares from this bundle,
    then regenerate the backup from scratch and start over.

The decrypted text is wrong or garbled:
    Do not distribute. Regenerate, and this time re-check the
    passphrase field for leading or trailing spaces before you
    generate.

sha256sum does not match verification-record.txt:
    The copy you tested is not the copy the tool made. Suspect
    the transfer, the USB stick, or the storage it came from.
    Use a fresh copy.

KEEPING A SECOND COPY THAT THIS TOOL DID NOT WRITE
----------------------------------------------------------------
Optional, and useful if you want protection against a defect in
one implementation rather than in one file. With check.txt still
present from B3:

    ./age/age -p -o payload-by-age-cli.age check.txt

Type the SAME 64 hex characters when it asks. You now
hold two blobs, written by two unrelated programs, either of
which alone recovers the wallet with the same shares. Store it
alongside the original.

Expect a DIFFERENT sha256 for this file, and do not treat that
as a fault. age encrypts with fresh randomness every time (a new
salt, file key and nonce), so the same secret encrypted twice
never produces the same bytes. Identical output would be the
bug. Record its checksum separately if you want to track it.

BEFORE YOU WALK AWAY
----------------------------------------------------------------
    rm check.txt check2.txt check3.txt

Then shut the machine down. On Tails that wipes memory and
leaves nothing behind. Anything you want to keep, including the
optional second blob, must be copied to your USB stick first.
""";
}
