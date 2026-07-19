// esbuild --inject shim: gives slip39-js (written for node) a browser Buffer.
export { Buffer } from 'buffer';
