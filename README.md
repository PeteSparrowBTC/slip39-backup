# SLIP-39 + age Wallet Backup Tool

## 🔴 LIVE DEMO - DO NOT USE FOR REAL WALLETS 🔴

**Demo URL:** https://bitcoin-self-custody.github.io/Seed-Phrase-Storage-SLIP39

### ⚠️ CRITICAL: THIS ONLINE VERSION IS FOR DEMONSTRATION ONLY ⚠️

**DO NOT ATTEMPT ACTUAL WALLET BACKUPS FROM THIS GITHUB PAGES VERSION!**

**Why?**
- You should never enter real seed phrases in any online service
- Even though all crypto runs client-side, use offline for real wallets
- This demo is for learning and testing the flow only

**For actual wallet backup, you MUST:**
1. **Download** the latest release: [Releases](https://github.com/Bitcoin-Self-Custody/Seed-Phrase-Storage-SLIP39/releases)
2. **Extract** to USB drive
3. **Run offline** on Tails Linux with the included local server (see [TAILS_INSTRUCTIONS.md](TAILS_INSTRUCTIONS.md))
4. **No internet connection** - completely air-gapped

---

## About This Tool

A secure, offline-capable web application for backing up BIP-39 cryptocurrency wallet seed phrases. Phase 2 of this project replaces the legacy proprietary "seed + passphrase" encoding with a clean two-layer design:

1. **SLIP-39 splits a random 32-byte key `k`** into threshold shares.
2. **The wallet payload (seed words, optional BIP-39 passphrase, cosigner data, descriptor, notes) is encrypted with [age](https://age-encryption.org/) using `k`** as the recipient passphrase.

The output is a single `output.zip` that you save to a USB drive. Recovery only requires threshold-many share zips plus the encrypted `payload.age` blob.

**Built with:** [Xecrets.Slip39](https://github.com/xecrets/xecrets-slip39) (SLIP-39 secret-sharing) and an in-tree age-passphrase implementation.

## What is This?

This tool implements two roles that map directly to two routes in the app:

- **Owner mode** (`/owner`) — you have the wallet seed and want to create a distributable backup.
- **Recoverer mode** (`/recoverer`) — you (or your executor) have the shares and the encrypted payload and want to recover the wallet.

Landing page (`/`) lets you pick between the two.

## Why Use SLIP-39 + age?

Traditional BIP-39 seed phrases have a problem: if someone finds your 12/24 words, they have complete access to your wallet. The SLIP-39 + age design here gives you:

- **Threshold secret sharing** — split your wallet into multiple shares; only threshold-many can recover it.
- **Standard cryptography end-to-end** — SLIP-39 for the share split, age for the payload encryption. No proprietary "concatenate seed and passphrase with padding" encoding.
- **One file to distribute, one file to keep online** — the `output.zip` is laid out so share zips go to physical/offline storage and `payload.age` goes to a password-manager entry with Emergency Access for your executor.
- **Verifiable on recovery without exposing the seed** — a `verification-record.txt` lets you do periodic dry-run recovery checks against a stored fingerprint.

## How the New Design Works

### Two-layer encryption

```
   Wallet payload (JSON: seed words, passphrase, cosigners, descriptor, notes)
                              │
                              │  encrypted with age (passphrase = k)
                              ▼
                          payload.age
                              ▲
                              │  k is the only thing SLIP-39 protects
                              │
   Random 32-byte key k ──► SLIP-39 split ──► share-1.zip … share-N.zip
```

No part of the wallet seed leaves the age-encrypted payload. SLIP-39 only protects `k`. There is **no** SLIP-39 passphrase to remember — the security boundary is the threshold of share zips plus possession of the `payload.age` file.

### Groups and thresholds

**Shares**: Individual SLIP-39 mnemonics (20-33 words).
**Groups**: Optional grouping of shares with their own thresholds (e.g. "Personal", "Family", "Friends").
**Group threshold**: How many groups are required to recover.

Default setup: **single group, 3-of-5**. You can change the threshold/count or add additional groups in the Owner UI.

## Features

### Web Application

- **Two routes**: `/owner` for creating a backup, `/recoverer` for recovering one. `/` is the chooser.
- **Single download**: Owner mode emits one `output.zip` (share zips + `payload.age` + verification record + read-me).
- **Drop-zone recovery**: Recoverer mode accepts share `.zip` files dropped directly, or pasted mnemonics one-per-line.
- **Dark theme**: Easy on the eyes for extended use.
- **100% client-side**: All crypto runs in browser (WebAssembly). No network calls.

### Security

✅ **No server processing**: Everything runs in your browser (Blazor WASM)
✅ **No data transmission**: Verifiable in browser DevTools (Network tab)
✅ **Offline capable**: Works completely offline on Tails Linux
✅ **Standard primitives**: SLIP-39 for sharing, age for encryption — no custom encoding
✅ **Tails-optimized**: RAM-only operation, everything wiped on shutdown

## Usage

### ⚠️ IMPORTANT SECURITY WARNING ⚠️

**DO NOT ENTER YOUR REAL SEED PHRASE ON THE ONLINE DEMO!**

The GitHub Pages version is for **DEMONSTRATION AND TESTING ONLY**.

**For real wallet backups:**
- Download the release zip (Option 2 below)
- Run on Tails Linux offline with the included server
- Or build from source and run locally

### Option 1: Online Demo (GitHub Pages) - FOR TESTING ONLY

**Demo app:** https://bitcoin-self-custody.github.io/Seed-Phrase-Storage-SLIP39

**⚠️ WARNING: DO NOT USE WITH REAL SEED PHRASES!**

This is a demonstration version. While all crypto runs client-side:
- ❌ **DO NOT** enter your actual wallet seed phrase
- ❌ **DO NOT** use this for real wallet backups
- ✅ **DO** use it to understand how the flow works
- ✅ **DO** test with dummy/test seed phrases only

**For real use, download and run offline on Tails (Option 2).**

### Option 2: Offline on Tails Linux (Recommended)

**Download from GitHub Releases:**
1. Go to [Releases](https://github.com/Bitcoin-Self-Custody/Seed-Phrase-Storage-SLIP39/releases)
2. Download `slip39-backup-vX.X.X.zip`
3. Extract to USB drive

**On Tails:**
```bash
# 1. Insert USB and navigate to app folder
cd /media/amnesia/YOUR_USB/slip39-backup

# 2. Start local web server (localhost only)
./start-server.sh

# 3. Open in LibreWolf (recommended) or Tor Browser
#    Navigate to: http://127.0.0.1:9876
```

See [TAILS_INSTRUCTIONS.md](TAILS_INSTRUCTIONS.md) for the complete guide, including LibreWolf setup and Tor Browser proxy configuration.

### Option 3: Build from Source

**Requirements:**
- .NET 8 SDK or later
- Git

**Build:**
```bash
git clone https://github.com/Bitcoin-Self-Custody/Seed-Phrase-Storage-SLIP39.git
cd Seed-Phrase-Storage-SLIP39/Slip39Demo.Web
dotnet publish -c Release -o publish
```

**Run locally:**
```bash
cd publish/wwwroot
python3 -m http.server 9876 --bind 127.0.0.1
```

Open browser to: `http://127.0.0.1:9876`

## How It Works

### Creating a backup (Owner mode)

1. Open `/` in the browser. Click **Start backup** (or go straight to `/owner`).
2. In the Owner page:
   - Enter your BIP-39 seed words in the **Top-level seed words** field (single-sig / shared-seed case). For multisig with distinct per-cosigner seeds, leave this empty and fill the per-cosigner seed fields instead.
   - (Optional) Set a label for the wallet.
   - (Optional) Add a BIP-39 passphrase to a cosigner.
   - (Optional) Adjust derivation path, descriptor, group threshold, or group shape. Default is 3-of-5 single-group.
3. Click **Generate**.
4. The browser downloads a single `output.zip` containing:
   - `shares/share-1-of-5.zip` … `share-5-of-5.zip` — distribute to your storage locations (paper / metal / trusted holders).
   - `payload/payload.age` (binary) and `payload/payload.age.txt` (ASCII armor) — upload to your dedicated password-manager entry with Emergency Access for your executor.
   - `verification-record.txt` — keep alongside the payload for periodic dry-run verification.
   - `payload/IMPORTANT-READ-FIRST.txt` — owner-only note about how to use what's in the zip.

The `output.zip` itself is **not** something you store long-term — it's a one-shot distribution package. Split its contents to their respective homes immediately, then delete the zip.

### Recovering a wallet (Recoverer mode)

1. Open `/` → click **Start recovery** (or go straight to `/recoverer`).
2. In the Recoverer page:
   - Drop threshold-many share `.zip` files into the mnemonics file picker (or paste the mnemonics one per line).
   - Drop `payload.age` (binary) or `payload.age.txt` (ASCII armor) into the ciphertext picker (or paste the armor text).
3. Click **Recover**. The recovered wallet payload appears with a reveal-on-click for the seed words.

The recoverer never needs a SLIP-39 passphrase or any out-of-band secret — possession of threshold-many shares plus the `payload.age` is sufficient.

## Technical Details

### File layout in `output.zip`

```
output.zip
├── shares/
│   ├── share-1-of-5.zip   (or {group}-share-1-of-N.zip when multi-group)
│   ├── share-2-of-5.zip
│   ├── …
│   └── share-5-of-5.zip
├── payload/
│   ├── payload.age              (binary age ciphertext)
│   ├── payload.age.txt          (ASCII armor of the same)
│   └── IMPORTANT-READ-FIRST.txt
└── verification-record.txt
```

Each individual `share-K-of-N.zip` contains the SLIP-39 mnemonic for that share plus a per-share README explaining what it is and how it's used.

### Dependencies

- [Xecrets.Slip39](https://www.nuget.org/packages/Xecrets.Slip39/) - SLIP-39 implementation
- In-tree age-passphrase encryption (scrypt + ChaCha20-Poly1305) — see `Slip39Demo.Core/Age/`
- Blazor WebAssembly (.NET 8)

## Development

### Project Structure

```
Seed-Phrase-Storage-SLIP39/
├── Slip39Demo.Core/             # Pure C# core: SLIP-39, age, payload, bundle
├── Slip39Demo.Core.Tests/       # xUnit tests (Phase 1 + Phase 2)
├── Slip39Demo.Web/              # Blazor WASM application
│   ├── Pages/
│   │   ├── Index.razor          # Owner / Recoverer chooser
│   │   ├── Owner.razor          # /owner — backup creation
│   │   └── Recoverer.razor      # /recoverer — wallet recovery
│   ├── wwwroot/
│   │   ├── start-server.sh      # Tails server startup script
│   │   └── README_OFFLINE.md    # Offline usage guide
│   └── Slip39Demo.Web.csproj
├── .github/workflows/
│   └── build-and-release.yml    # CI/CD pipeline
├── TAILS_INSTRUCTIONS.md        # Complete Tails guide
└── README.md                    # This file
```

### Running Locally

```bash
cd Slip39Demo.Web
dotnet run
```

Open: `http://localhost:5259`

## Releases

GitHub Actions automatically creates releases when version tags are pushed:

```bash
git tag v2.0.0
git push origin v2.0.0
```

This triggers:
- Build and publish
- Create GitHub Release
- Attach `slip39-backup-v2.0.0.zip` for download

## Security Considerations

### What This Tool Does

✅ **Client-side only**: All operations in browser (WebAssembly)
✅ **No external requests**: Zero network traffic (verify in DevTools)
✅ **No persistence**: Doesn't save anything to disk except the `output.zip` you explicitly download
✅ **Tails compatible**: Works on RAM-only OS with offline mode

### What You Must Do

⚠️ **Split `output.zip` immediately** - Move share zips and `payload.age` to their long-term homes, then delete `output.zip`.
⚠️ **Store shares offline** - Paper, metal, or trusted holders. Never digital storage on internet-connected devices.
⚠️ **Distribute shares securely** - Never all in one place!
⚠️ **Test recovery** - Always do a dry-run recovery before relying on the backup.
⚠️ **No screenshots** - Don't screenshot shares or the recovered seed.

### Privacy on Tails

When using on Tails Linux:
- Everything runs in RAM
- Browser doesn't save history/cache
- Full system wipe on shutdown
- LibreWolf or Tor Browser provides additional isolation
- Local Python server (127.0.0.1) ensures no network exposure

## FAQ

**Q: Is my seed phrase transmitted to a server?**
A: No. All cryptography runs in your browser via WebAssembly. Check browser DevTools Network tab to verify.

**Q: Can I use this without internet?**
A: Yes. Run the included local server on Tails (or any OS). No internet required after the release zip is on the USB.

**Q: Do I need to remember a SLIP-39 passphrase?**
A: No. The new design has no SLIP-39 passphrase. The security boundary is "threshold-many shares **and** the `payload.age` file". Anything inside `payload.age` (including any BIP-39 passphrase you set on a cosigner) is recovered automatically once you have both.

**Q: What if I lose some shares?**
A: As long as you have threshold-many shares and the `payload.age`, you can recover. With the default 3-of-5 single-group setup, losing up to 2 shares is fine.

**Q: What if I lose `payload.age`?**
A: The shares alone are not enough — they only reveal the random key `k`, not the wallet payload. That's why `payload.age` should be stored with redundancy (e.g. password-manager Emergency Access plus an offline copy).

**Q: Is this better than just writing down my BIP-39 seed?**
A: Yes, because:
- Single point of failure eliminated
- Distributable to multiple locations/people
- Configurable redundancy (you choose thresholds)
- Cleanly separates "what shares protect" (`k`) from "what's in the wallet payload" (seed + passphrase + descriptor + notes)

**Q: Can I recover my wallet with this tool?**
A: Yes. The Recoverer page reconstructs the payload and shows the seed words (revealed on click). To actually access funds, import the recovered BIP-39 seed into your wallet application.

## License

See [LICENSE](LICENSE) file.

## Contributing

This is a security-focused tool. All contributions should prioritize:
- Client-side processing (no server dependencies)
- Offline capability
- Tails Linux compatibility
- Clear security documentation

## Acknowledgments

- [Xecrets.Slip39](https://github.com/xecrets/xecrets-slip39) by Svante Seleborg - SLIP-39 C# implementation
- [SLIP-0039 Specification](https://github.com/satoshilabs/slips/blob/master/slip-0039.md) by SatoshiLabs
- [BIP-39 Specification](https://github.com/bitcoin/bips/blob/master/bip-0039.mediawiki)
- [age encryption](https://age-encryption.org/) by Filippo Valsorda and Ben Cartwright-Cox

## Disclaimer

This tool is provided as-is for educational and personal use. Always:
- Test thoroughly before trusting with real funds
- Understand the cryptography before using
- Keep backups of backups
- Verify the source code yourself

**Use at your own risk. This is experimental software for handling sensitive cryptographic material.**

> Screenshots will be updated post-Phase-2 release.

---

*Collaboration by Claude*
