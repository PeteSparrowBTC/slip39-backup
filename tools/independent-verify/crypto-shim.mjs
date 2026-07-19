// Browser stand-in for the THREE node:crypto functions slip39-js actually calls
// (randomBytes / pbkdf2Sync / createHmac-sha256), backed by @noble/hashes —
// pure-JS, audited, no node built-ins. Aliased in via esbuild --alias:crypto=…
// instead of crypto-browserify, which drags the whole node stream stack along.
import { pbkdf2 } from '@noble/hashes/pbkdf2.js';
import { sha256 } from '@noble/hashes/sha2.js';
import { hmac } from '@noble/hashes/hmac.js';

export function randomBytes(length = 32) {
  const b = new Uint8Array(length);
  globalThis.crypto.getRandomValues(b);
  return b; // slip39-js only Array.prototype.slice.call()s the result
}

export function pbkdf2Sync(password, salt, iterations, keylen, digest) {
  if (digest !== 'sha256') throw new Error(`crypto-shim: unsupported digest ${digest}`);
  return pbkdf2(sha256, Uint8Array.from(password), Uint8Array.from(salt), { c: iterations, dkLen: keylen });
}

export function createHmac(alg, key) {
  if (alg !== 'sha256') throw new Error(`crypto-shim: unsupported algorithm ${alg}`);
  const chunks = [];
  return {
    update(data) { chunks.push(Uint8Array.from(data)); return this; },
    digest() {
      const total = chunks.reduce((n, c) => n + c.length, 0);
      const all = new Uint8Array(total);
      let offset = 0;
      for (const c of chunks) { all.set(c, offset); offset += c.length; }
      return hmac(sha256, Uint8Array.from(key), all);
    },
  };
}

export default { randomBytes, pbkdf2Sync, createHmac };
