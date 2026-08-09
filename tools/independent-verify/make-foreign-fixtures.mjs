// Produces the "foreign backup" fixtures consumed by
// Slip39Demo.Tests/Interop/ForeignBackupRoundTripTests.cs.
//
// WHY
// The runtime gate in Owner mode proves one direction: C# generates, and the
// third-party JS stack (slip39-js + typage) can read it. That leaves the other
// direction untested, and the other direction is what Recoverer mode does for
// real. An heir arrives with a payload.age and mnemonics that this tool did not
// necessarily produce: a copy re-encrypted with the Go age CLI, shares typed off
// paper and re-emitted by iancoleman/slip39, a blob from `rage`. If Xecrets or
// AgeSharp diverges from the specs when PARSING rather than when writing, that
// divergence surfaces at recovery, which is the worst possible moment.
//
// These fixtures are produced entirely by the JS implementations, with no C#
// involvement, and committed. The C# test then has to recover them cold.
//
// Deliberately NOT recorded in the fixture: the 32-byte key. The C# side must
// reconstruct it from the mnemonics, not be handed it.
//
// Usage:  node make-foreign-fixtures.mjs
// Rerun only when you want fresh fixtures; the committed ones are the test
// corpus and changing them is a deliberate act, not a build step.

import { writeFileSync, mkdirSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { webcrypto } from 'node:crypto';
import slip39pkg from 'slip39';
import * as age from 'age-encryption';

const Slip39 = slip39pkg.default ?? slip39pkg;
const here = dirname(fileURLToPath(import.meta.url));
const outFile = join(here, '..', '..', 'Slip39Demo.Tests', 'Interop', 'foreign-backups.json');

const toHex = bytes => Array.from(bytes, b => b.toString(16).padStart(2, '0')).join('');
const toB64 = bytes => Buffer.from(bytes).toString('base64');

// A payload shaped like the real thing (PayloadEmitter's output), so the test
// exercises a realistic plaintext size and character set rather than "hello".
const payloadText = (label, seedWords) =>
  `schema_version: 1.1\ncreated: 2026-01-01\nlabel: "${label}"\n\n` +
  `seed_words: ${seedWords}\n\ncosigners:\n  - id: main\n` +
  `    wallet_type: single_sig\n    derivation_path: m/84'/0'/0'\n\n` +
  `threshold: 3-of-5\nslip39_extendable: false\n`;

// groups follow slip39-js's [threshold, count, name] shape.
//
// extendable is the SLIP-39 extendable-backup flag, one bit in every share's
// header. It selects the PBKDF2 salt used to encrypt the master secret: with the
// flag clear the salt is "shamir" || identifier, so the 15-bit identifier is
// bound into the encryption; with it set the salt is empty and the identifier is
// not. This tool always generates extendable shares, but a recoverer does not
// control what arrives, so both values are covered here. The non-extendable case
// is the one that matters most: it exercises the Feistel path where Xecrets had
// its defect (xecrets/xecrets-slip39#28).
const CASES = [
  {
    name: '3-of-5 single group, extendable',
    extendable: 1,
    groupThreshold: 1,
    groups: [[3, 5, 'group']],
    payload: payloadText('Main wallet', 'abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about'),
  },
  {
    name: '2-of-3 single group, NON-extendable',
    extendable: 0,
    groupThreshold: 1,
    groups: [[2, 3, 'group']],
    payload: payloadText('Small wallet', 'legal winner thank year wave sausage worth useful legal winner thank yellow'),
  },
  {
    name: '3-of-5 single group, NON-extendable',
    extendable: 0,
    groupThreshold: 1,
    groups: [[3, 5, 'group']],
    payload: payloadText('Legacy-format wallet', 'zoo zoo zoo zoo zoo zoo zoo zoo zoo zoo zoo wrong'),
  },
  {
    name: '2 groups, both required (2-of-3 and 2-of-3), extendable',
    extendable: 1,
    groupThreshold: 2,
    groups: [[2, 3, 'alpha'], [2, 3, 'beta']],
    payload: payloadText('Two-path wallet', 'letter advice cage absurd amount doctor acoustic avoid letter advice cage above'),
  },
];

const fixtures = [];
for (const c of CASES) {
  const key = new Uint8Array(32);
  webcrypto.getRandomValues(key);
  const keyHex = toHex(key);

  // slip39-js splits the key, with the extendable flag this case asks for.
  const split = Slip39.fromArray([...key], {
    passphrase: '',
    threshold: c.groupThreshold,
    groups: c.groups,
    extendableBackupFlag: c.extendable,
  });

  // Every share of every group, flattened in group order, matching the order
  // Slip39Wrapping.SplitKey returns so the C# side can slice it the same way.
  const mnemonics = c.groups
    .flatMap((_, gi) => split.fromPath(`r/${gi}`).mnemonics);

  // Self-check: the JS stack must be able to read back what it just wrote,
  // otherwise the fixture is junk and the C# failure would be misattributed.
  const firstGroupThresholdShares = split.fromPath('r/0').mnemonics.slice(0, c.groups[0][0]);
  const selfCheckShares = c.groupThreshold === 1
    ? firstGroupThresholdShares
    : c.groups.flatMap((g, gi) => split.fromPath(`r/${gi}`).mnemonics.slice(0, g[0]));
  if (toHex(Slip39.recoverSecret(selfCheckShares, '')) !== keyHex)
    throw new Error(`fixture "${c.name}": slip39-js could not recover its own split`);

  // typage encrypts under the hex-encoded key, the same convention AgePassphrase
  // uses on the C# side (64 lowercase hex characters).
  const encrypter = new age.Encrypter();
  encrypter.setPassphrase(keyHex);
  const ciphertext = await encrypter.encrypt(c.payload);

  fixtures.push({
    name: c.name,
    extendable: c.extendable === 1,
    groupThreshold: c.groupThreshold,
    groups: c.groups.map(([threshold, count, name]) => ({ name, threshold, count })),
    mnemonics,
    payloadAgeB64: toB64(ciphertext),
    expectedPayloadText: c.payload,
  });
}

mkdirSync(dirname(outFile), { recursive: true });
writeFileSync(outFile, JSON.stringify({
  producedBy: 'slip39-js 0.1.9 + typage (age-encryption 0.3.0), no C# involvement',
  note: 'The 32-byte key is deliberately absent. The C# side must reconstruct it from the mnemonics.',
  fixtures,
}, null, 2) + '\n');

console.log(`wrote ${fixtures.length} fixtures to ${outFile}`);
