# Envelope, entropy and implementation choices

Status: settled, 2026-08-09.

These questions were argued out at length and reached conclusions. This record
exists so they are not reopened from scratch. Each entry states what was decided,
why, what was rejected, and, where it matters, **what the decision does not
buy**. The limits are the part most worth reading: several of these choices are
correct only within a boundary, and a future change that ignores the boundary
will look like an improvement while removing the protection.

Authored by Pete Sparrow (human) and Claude (AI, Anthropic).

---

## 1. The payload is wrapped twice: age inside OpenPGP

`payload.age.gpg` is the age file inside an OpenPGP symmetric envelope
(AES-256).

**Why.** Nesting composes in a way that parallel copies do not. With
`gpg(age(payload))` an attacker must defeat both formats, so the wallet survives
a break in either. Keeping a second copy in another format does not do this:
breaking age simply exposes the age copy.

Concretely it lifts the post-quantum margin from 2^64 to 2^128. age's file key
is 128 bits, which Grover halves to 64; AES-256 restores the headroom. That
128-bit file key is the one structural weakness identified in age v1, and it is
a property of the format, not of any implementation.

**The precedent, and its limit.** Bitcoin's `HASH160` composes two different
primitives for the same hedging reason. Two differences matter. Hash composition
is bounded by the shorter output (HASH160 is capped at 160 bits, and Taproot
later dropped RIPEMD-160 because it had become the binding constraint), whereas a
cipher cascade takes its work factor from the stronger layer. And Bitcoin's
double SHA-256 is *not* an instance of this reasoning at all: it is the same
function twice, defeating length extension, and it would not survive a break in
SHA-256.

The Taproot lesson is the one to keep: **an added layer can become the new
ceiling.** AES-256 sits above age's 128-bit file key, so it raises the floor.
A weaker outer layer would have lowered the roof.

## 2. One payload artifact: REVERSED, 2026-08-11

**This section said the opposite, and it was wrong.** It specified that
`payload.age`, `payload.age.txt` and `payload.age.gpg` all ship, that any one
recovers the wallet, and it instructed against "simplifying" to the wrapped form
alone. The bundle now contains exactly one ciphertext, **`payload.age.gpg.asc`**.

**Why the original was incoherent.** Decision 1 justifies the wrapper on
confidentiality: an attacker must break both formats. Shipping the unwrapped age
file beside it means an attacker who obtains the folder takes the weaker file and
breaks one format. The storage guidance made that certain rather than merely
possible, because it told the owner to put `payload.age` in the password manager, on
the USB and in the safe. The availability argument was real, and it was paid for
with the entire confidentiality argument of the decision directly above it, which
went unnoticed because the two were written at different times.

The lesson generalises past this instance: **a defence-in-depth layer is worth
nothing while the thing it defends is also shipped undefended.**

**Why armored rather than binary.** Armor is a lossless re-encoding, demonstrable in
one command (`gpg --enarmor` then `gpg --dearmor` returns identical bytes), so the
binary form offers no capability the text form lacks. Text survives every channel
this artifact must pass: a password-manager note, an email body, a printed page, a
retyping by hand. OpenPGP armor also carries a CRC24, so a mangled paste is
detected, and age's armor has no checksum at all, which is the second reason the
shipped text is PGP armor rather than age armor.

**The availability cost, now paid rather than dodged.** Recovery needs both locks
off, in order. Mitigated rather than ignored: GnuPG ships with Tails while age does
not, this tool unwraps OpenPGP in-process so recovery never requires GnuPG
installed, and both `MANUAL-RECOVERY.txt` and `VERIFY-THIS-BACKUP.txt` give the two
commands with the same key.

**The armor is self-describing.** A `Comment:` header names what the file is and how
to recover it, so somebody who finds this text alone in a password manager, with no
bundle around it, is not holding an anonymous base64 block. `Version:` is
suppressed, since fingerprinting the producing library benefits no reader.

**If this is revisited**, the question is not "would a second copy help an heir", it
is "does a second copy hand an attacker the weaker file". Anything shipped beside
the wrapped form must be at least as strong as it, or the wrapper stops meaning
anything.

## 3. Both layers use the same K, and this bounds what the cascade hedges

Both take the same 64-character lowercase hex encoding of the 32-byte SLIP-39
master secret.

**What it protects against.** A break in either cipher or format. Recovering
age's internals does not yield K, because K enters only through scrypt, which is
one-way, so an attacker who breaks age still faces AES-256.

**What it does NOT protect against.** A break that recovers the *passphrase
itself*, for instance scrypt or the OpenPGP S2K being inverted so that a derived
key yields its input. Because both layers take the same K, recovering it from
either opens the other, and the cascade collapses.

**Why accepted.** Independent keys via HKDF would close that case, and would
also mean the heir cannot recover with ordinary tools: `gpg -d` then `age -d`
becomes a scripted derivation nobody can perform by hand. KDF inversion is a far
stranger event than a cipher break. The trade is deliberate. If it is ever
revisited, the cost to weigh is recovery simplicity, not implementation effort.

## 4. Encryption uses the bundled official age binary; decryption uses AgeSharp

The AppImage bundles the official `age` release and encrypts by running it.
AgeSharp decrypts.

**Why.** Encryption failures are silent and decryption failures are loud. A file
written with a reused nonce or a weak key decrypts perfectly and stays weak
forever, so no round-trip test finds it. A bad decrypt announces itself
immediately. The side where mistakes are invisible gets the most-scrutinised
implementation available.

**Fail closed.** A missing binary fails generation. There is deliberately no
fallback to in-process encryption: falling back to the implementation this
exists to avoid would make it decorative. Do not add a fallback.

**The browser build is exempt** because it cannot start a subprocess, and that is
acceptable only because it is marked DEMONSTRATION AND TESTING ONLY and
watermarks its output INSECURE-TEST.

## 5. The OpenPGP layer is implemented in-process, on BouncyCastle

Not by shelling out to GnuPG.

**Why.** The heir's machine may not have GnuPG and must still be able to unwrap;
the WASM build cannot start a subprocess; and a second implementation gives
something to test the first against. BouncyCastle was already a dependency, so
this adds no supply chain.

**GnuPG compresses before encrypting by default.** A real `payload.age.gpg` will
usually carry a compressed packet that this tool never produces itself. The
decrypt path must keep handling all of gpg's compression algorithms, or recovery
fails on precisely the files most heirs are handed while every test using our own
envelopes passes. This is covered by tests against a real GnuPG.

**In-process for recovery does not mean in-process for verification.** See section
10: because this layer is written by code from this repository, the system's GnuPG
opens it before a real backup is released.

## 6. Rejected: a browser-based verifier shipped in our own bundle

A single-file HTML checker was built and then removed.

**Why rejected.** A checker produced by this project and distributed inside its
own output cannot independently verify that output, whatever third-party
libraries sit inside it. It was convenient and it was not independence.

There is no third-party browser tool to point at instead for the age layer:
agewasm.marin-basic.com, nkcmr/age-online and webencrypt.org were all checked and
are key-based, while this tool's payload is passphrase mode. The SLIP-39 layer
does have one, 3rdIteration/slip39, and the guide points there.

If this is revisited, the only version worth having is published under a
different identity and account, so it is not ours to vouch for itself.

## 7. Rejected: zip with password protection as the outer layer

**Why rejected.** The zip encryption available everywhere is the broken one.
Info-ZIP's `zip -e` produces ZipCrypto, which falls to a known-plaintext attack,
and our inner file begins with the 21 published bytes `age-encryption.org/v1`,
which is an ideal known-plaintext position. AES-256 zip is sound but needs a tool
that is not on Tails, so it trades one download for another and gains nothing.

Zip also leaves filenames and sizes outside the encryption, and the WinZip AE
spec authenticates with HMAC-SHA1 truncated to 80 bits.

Do not password-protect the **output bundle** either: it holds share files meant
to be separated, and a memorable password reintroduces the human-chosen secret
this design eliminated.

## 8. Dice entropy for K: superseded, and the XOR rule is retired

This section originally specified a dice feature for this repository: 50 rolls,
and `K = SHA256(rolls) XOR RandomNumberGenerator.GetBytes(32)` under the heading
"combine, never replace". **Both of those numbers and that formula are withdrawn.**
The feature was built in a separate tool,
[dice-to-seed](https://github.com/PeteSparrowBTC/dice-to-seed), and building it
settled two questions differently.

**The XOR combine was considered there and dropped, on grounds.** Mixing a
generated value into K hedges against a biased or observed die, but it costs the
one property the design will not give up: that a value can be recomputed and
checked against an independent implementation, by its owner and by anyone else.
An XOR'd K is checkable by nobody. So K is the dice and nothing else:

```
k = SHA-256(the roll digits, joined by nothing)
```

which stays verifiable with one shell command, `printf '%s' "$ROLLS" | sha256sum`.
See `DiceToSeed.Core/BackupKey.cs`, which records the rejected alternative next to
the chosen one.

**Bias is handled by rolling past the minimum, not by mixing.** 50 rolls give 129
bits only on the ideal-die measure. dice-to-seed asks for 60, which clears 128
bits on the conservative measure instead, and that is the number to use.

**Two rules from the original section survive unchanged**, because dice-to-seed
honours both:

- **Never reuse the seed's rolls for K.** Shared entropy makes the dice record a
  single artifact that reconstructs both layers, which is exactly the single point
  of failure the threshold scheme removes. One sitting is fine if the halves are
  disjoint and the record is destroyed afterwards.
- **It must show its work.** A dice feature that cannot be audited produces
  confidence without substance, so the hash of the rolls and the resulting K are
  both displayed.

**This tool now consumes such a key.** The Owner page takes 64 hex characters and
the four-character check code that dice-to-seed prints, and uses that as K instead
of calling the generator. The check code is required whenever a key is entered: a
key with nothing catching a transcription slip is a key that fails at recovery,
years later, with nobody able to say why.

**And it discharges the obligation dice-to-seed delegates to it.** That comment
says the roll log must never be the one used for a seed, and that the consuming
tool enforces the rest by comparing K against the seed it was given. If one log
produced both, K *is* the wallet's BIP-39 entropy, so the key becomes derivable
from the wallet it protects and the threshold scheme stops protecting anything.
This tool recovers checksum-verified BIP-39 entropy from every non-blank seed in
the form, top-level and per-cosigner, and refuses to generate when K's leading
bytes match any of them. It also refuses when a seed cannot be read as BIP-39 at
all, because a comparison that did not happen must not be reported as one that
passed.

The reason section 8 is rewritten rather than deleted: an unqualified "combine,
never replace" is advice somebody would follow, and the argument against it is the
useful part.

## 9. Where the real risk sits

Ranked by likelihood of actually costing someone their coins. Effort belongs at
the top of this list, not the bottom.

1. Operator error, above all one location accumulating threshold-many shares plus the blob
2. Key generation, unverifiable from inside, catastrophic if wrong
3. Supply chain, with no reproducible builds
4. An implementation bug in the encryption path
5. Cryptanalysis of the ciphers themselves

Item 1 is unfixed. Do not propose work on item 5 while it remains open.

**Fixed, 2026-08-11: the `PayloadParser` leading-whitespace trap**, which used to
sit second on this list. `SplitKV` trimmed leading whitespace off every value
after `key: `, and one of those values is a BIP-39 passphrase, so a passphrase of
`" hunter2"` was written correctly and read back as `"hunter2"`: a different
wallet, valid and empty.

What made it worth ranking above key generation was not the trim, it was that
nothing could see it. The age file was well formed. The independent verifier
compared the ciphertext against the same text the emitter had produced, so it
agreed. The master fingerprint in the verification record was computed from the
form rather than from the payload, so it agreed too. Every check the tool
performs passed on a backup that would recover the wrong wallet, and it would
have surfaced years later as "the backup is broken" with nothing to diagnose.

The fix has two halves, and the second is the one that matters for anything added
to the payload later:

- The parser drops exactly the one separator space the emitter writes, instead of
  trimming. This also repairs backups already in the field: those files carry the
  intended value verbatim, only the read side discarded it.
- `PayloadRoundTrip.EmitChecked` is the only emit path generation uses. It emits,
  reparses, compares field by field, and refuses by name if anything differs. The
  residual hole (a value containing a line break, which a one-line-per-value
  format cannot carry) is now a refusal rather than a substitution. A test only
  covers values somebody thought of; this runs on what the owner typed.

Refusal messages never echo the value they are complaining about, and the
parser's "unknown key" error no longer quotes non-identifier text. Both land in
an on-screen banner, and the values are seed words and passphrases.

## 10. The outer lock is opened by the system GnuPG before a real backup is released

Added 2026-08-11. Generation runs `gpg --decrypt` on the armored envelope it has
just produced and requires the exact age file back, byte for byte, before the
backup reaches the user.

**Why.** Decision 4 put the official `age` binary on the encryption path because
encryption failures are silent. That argument applies to the second layer too, and
until now nothing implemented it: the OpenPGP envelope was the only part of the
shipped artifact written by code from this repository and never opened by anything
else. BouncyCastle asked to open its own envelope agrees with itself whatever it
wrote, which is not a check, and every existing test that used our own envelopes
would keep passing.

**Why the system GnuPG and not a bundled JavaScript OpenPGP library.** A checker
shipped inside our own bundle cannot vouch for its own producer, which is what got
the in-bundle browser verifier deleted (decision 6). GnuPG is already on the
target machine: Tails lists it in its own included-software page and does not ship
age. So the outer lock is checked by a program Tails put there and the GnuPG
project maintains, and nothing has to be bundled to get a genuinely foreign
opinion.

**The three outcomes, and why they are three.** `Verified`, `Unavailable` (gpg
could not be run at all) and `Failed` (gpg ran and disagreed). Collapsing the last
two into a success/failure pair would push the distinction into string matching on
an error message, and the distinction carries the policy:

- A REAL backup refuses on anything short of `Verified`, GnuPG missing included.
  "Could not check" and "checked and wrong" are different facts about the machine
  but the same fact about the backup. Tails ships GnuPG, so on the machine this
  tool is built for the `Unavailable` branch does not fire.
- A watermarked INSECURE-TEST backup continues on `Unavailable`, and the
  transcript says in a warning that nothing independent opened it. The hosted demo
  runs in a browser that cannot start a subprocess at all, and refusing there
  would leave nothing to demonstrate.
- `Failed` refuses even for a test backup. That is not a fact about the machine:
  it means this build produced an envelope GnuPG cannot open, which is exactly the
  signal the gate exists to raise.

**Do not make the browser build verify with BouncyCastle** to turn `Unavailable`
into `Verified`. BouncyCastle wrote the envelope, so opening it proves only
self-consistency, and a check that always passes is worse than a missing one
because it reads as evidence. `AppImageEncryptorReachabilityTests` enforces the
structural half: every `IOuterLockVerifier` reachable from the AppImage frontend
is declared in `Slip39Demo.Tauri`, so an in-process one cannot be wired in there.

**Where the judgement lives.** `src-tauri/src/gpg.rs` reports what gpg said and
decides nothing. `TauriPgpVerifier` turns that report into one of the three
outcomes. `Owner` decides what an outcome costs, because that depends on whether
the backup is real. Each of the three has its own tests, and the passphrase goes
to gpg on stdin rather than on the command line, where any other process could
read it.
