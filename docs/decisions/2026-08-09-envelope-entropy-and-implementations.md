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

## 2. All three payload forms ship; the wrapper is protection, not a gate

The bundle contains `payload.age`, `payload.age.txt` and `payload.age.gpg`, and
**any one of them recovers the wallet**.

**Why.** Double hashing costs nothing operationally. Double *encryption* costs a
recovery step that must still work decades later, performed by someone under
stress with tools that must still exist. Shipping only the wrapped form would
buy resistance to cryptanalysis, the least likely failure, by adding to recovery
complexity, the most likely one.

Do not "simplify" this by shipping only `payload.age.gpg`.

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
2. The `PayloadParser` leading-whitespace trap: a passphrase with a leading space silently recovers a different wallet and defeats every automated check
3. Key generation, unverifiable from inside, catastrophic if wrong
4. Supply chain, with no reproducible builds
5. An implementation bug in the encryption path
6. Cryptanalysis of the ciphers themselves

Items 1 and 2 are unfixed. Do not propose work on item 6 while they remain open.
