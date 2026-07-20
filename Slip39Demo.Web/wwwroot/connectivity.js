// Connectivity probe for the airgap warning. Reports whether the internet is
// REACHABLE right now:
//   - navigator.onLine === false  -> no network interface at all: offline.
//   - otherwise actively probe well-known endpoints with short timeouts; ANY
//     response (even opaque no-cors) means the internet is reachable.
// A failed probe is EVIDENCE of airgap, not proof (captive portals, blocked
// egress) — the UI pairs this with an explicit user attestation. The fail-safe
// direction lives on the C# side: if this can't run at all, the app assumes
// ONLINE and watermarks the backup.
window.SPSConn = {
  async isOnline() {
    if (!navigator.onLine) return false;
    const targets = [
      'https://www.gstatic.com/generate_204',
      'https://cloudflare-dns.com/dns-query?name=example.com',
      'https://www.google.com/generate_204',
    ];
    const probe = (url) => {
      const ctl = new AbortController();
      const t = setTimeout(() => ctl.abort(), 3000);
      return fetch(url, { mode: 'no-cors', cache: 'no-store', signal: ctl.signal })
        .then(() => true)
        .catch(() => false)
        .finally(() => clearTimeout(t));
    };
    const results = await Promise.all(targets.map(probe));
    return results.some(Boolean);
  },
};
