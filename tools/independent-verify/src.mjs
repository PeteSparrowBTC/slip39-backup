// Independent in-browser verification of a freshly generated backup.
//
// Uses THIRD-PARTY implementations only — deliberately NOT the libraries the
// generator used (AgeSharp / Xecrets.Slip39) — so a bug in the generation stack
// cannot vouch for itself:
//   - slip39 (ilap/slip39-js)   reconstructs the 32-byte K from mnemonic subsets
//   - age-encryption (typage)   decrypts payload.age with hex(K) as passphrase
//
// The Blazor app calls window.SPSVerify.verifyBackup after generating and
// REFUSES to hand out the bundle unless this independent round-trip succeeds.
//
// Contract:
//   verifyBackup({
//     subsets:      string[][]  // groups of mnemonics; EVERY share must appear in
//                               // at least one subset, each subset must satisfy the
//                               // threshold on its own (built by the C# side, which
//                               // knows the group structure)
//     payloadAgeB64: string     // the binary payload.age, base64-encoded
//     expectedPayloadText: string // what the payload must decrypt to
//   }) → Promise<{ ok: boolean, kHex: string|null, error: string|null }>
//
// slip39-js requires EXACTLY threshold-many mnemonics per recovery (it rejects
// extras) — which is why the caller passes explicit subsets rather than the
// whole pile.
import slip39pkg from 'slip39';
import * as age from 'age-encryption';

const Slip39 = slip39pkg.default ?? slip39pkg;

function toHex(bytes) {
  return Array.from(bytes, b => b.toString(16).padStart(2, '0')).join('');
}

function fromBase64(b64) {
  const bin = atob(b64);
  const bytes = new Uint8Array(bin.length);
  for (let i = 0; i < bin.length; i++) bytes[i] = bin.charCodeAt(i);
  return bytes;
}

async function verifyBackup({ subsets, payloadAgeB64, expectedPayloadText }) {
  try {
    if (!subsets?.length) return { ok: false, kHex: null, error: 'no share subsets provided' };

    // 1. Every subset must independently reconstruct the SAME K.
    let kHex = null;
    for (let i = 0; i < subsets.length; i++) {
      const kBytes = Slip39.recoverSecret(subsets[i]); // throws on any failure
      const h = toHex(kBytes);
      if (kHex === null) kHex = h;
      else if (h !== kHex)
        return { ok: false, kHex: null, error: `subset ${i} recovered a DIFFERENT key than subset 0` };
    }

    // 2. K must decrypt payload.age (typage) to exactly the emitted payload text.
    const d = new age.Decrypter();
    d.addPassphrase(kHex.toLowerCase());
    const plain = await d.decrypt(fromBase64(payloadAgeB64), 'text');
    if (plain !== expectedPayloadText)
      return { ok: false, kHex, error: 'payload decrypted but does not match the generated payload text' };

    return { ok: true, kHex, error: null };
  } catch (e) {
    return { ok: false, kHex: null, error: `${e?.name ?? 'Error'}: ${e?.message ?? e}` };
  }
}

// Produces a complete backup using ONLY the third-party implementations, so the
// C# stack can be made to read something it did not write.
//
// verifyBackup above covers one direction: C# writes, JS reads. That is the
// direction Owner mode needs. Recoverer mode does the opposite, and the inputs
// it gets are not necessarily ours: a payload.age re-encrypted with the Go age
// CLI, shares typed off paper and re-emitted by another SLIP-39 tool. A parsing
// divergence there shows up at recovery, on an airgapped machine, with nobody
// around to debug it.
//
// The caller (JsIndependentVerifier) recovers this with Xecrets + AgeSharp and
// checks the plaintext. The key is deliberately NOT returned: the C# side has to
// reconstruct it from the mnemonics, otherwise the SLIP-39 half proves nothing.
//
// Work factor: this probe protects nothing (the key is thrown away when the
// function returns), so it uses the cheapest scrypt cost the age spec allows
// rather than the ~1 second default. The gate runs on the airgapped machine at
// generation time and should not cost the user a visible pause. The CCTV
// reference vectors themselves use a work factor of 10.
async function produceForeignProbe() {
  try {
    const key = new Uint8Array(32);
    crypto.getRandomValues(key);
    const keyHex = toHex(key);

    // A 2-of-3 split is the smallest shape that exercises a real threshold
    // (recovery from fewer shares than were issued) rather than a trivial 1-of-1.
    const split = Slip39.fromArray([...key], {
      passphrase: '',
      threshold: 1,
      groups: [[2, 3, 'probe']],
    });
    const mnemonics = split.fromPath('r/0').mnemonics;

    const payloadText = 'SPS foreign-implementation probe\nnot a wallet payload\n';
    const encrypter = new age.Encrypter();
    encrypter.setPassphrase(keyHex);
    encrypter.setScryptWorkFactor(10);
    const ciphertext = await encrypter.encrypt(payloadText);

    return {
      ok: true,
      mnemonics,
      payloadAgeB64: toBase64(ciphertext),
      expectedPayloadText: payloadText,
      error: null,
    };
  } catch (e) {
    return { ok: false, mnemonics: null, payloadAgeB64: null, expectedPayloadText: null,
             error: `${e?.name ?? 'Error'}: ${e?.message ?? e}` };
  }
}

function toBase64(bytes) {
  let bin = '';
  for (let i = 0; i < bytes.length; i++) bin += String.fromCharCode(bytes[i]);
  return btoa(bin);
}

// Expose for Blazor JS interop.
window.SPSVerify = { verifyBackup, produceForeignProbe };
