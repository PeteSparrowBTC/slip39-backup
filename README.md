# SLIP-39 Wallet Backup Tool

A secure, offline-capable web application for backing up BIP-39 cryptocurrency wallet seed phrases using SLIP-39 multi-group secret sharing.

## What is This?

This tool allows you to split your BIP-39 wallet seed phrase (and optional passphrase) into multiple **shares** organized into **groups**, using the [SLIP-39](https://github.com/satoshilabs/slips/blob/master/slip-0039.md) standard. You can then distribute these shares to different people or locations, and recover your wallet by collecting a threshold number of groups.

**Built with:** [Xecrets.Slip39](https://github.com/xecrets/xecrets-slip39) NuGet package - a C# implementation of SLIP-39

## Why Use SLIP-39?

Traditional BIP-39 seed phrases have a problem: if someone finds your 12/24 words, they have complete access to your wallet. SLIP-39 solves this with **threshold secret sharing**:

- Split your seed into **multiple shares**
- Organize shares into **groups** (e.g., personal backups, friends, family)
- Define **thresholds**: how many shares/groups needed to recover
- Even if some shares are lost or stolen, your wallet remains secure

## How SLIP-39 Works

### Shares and Groups

**Shares**: Individual pieces of your secret
- Each share is a list of 20-33 mnemonic words
- A single share reveals nothing about your secret

**Groups**: Collections of shares with different purposes
- Example groups: "Personal Backup", "Friends", "Family"
- Each group has its own threshold (e.g., "3 of 5 friends' shares needed")

**Group Threshold**: How many groups required to recover
- Example: "Any 2 of 4 groups can reconstruct the wallet"

### Example Configuration

**Default setup in this app:**
- **4 groups**, need **2 groups** to recover
  - Group 0 (Personal Backup #1): 1-of-1 (you keep this)
  - Group 1 (Personal Backup #2): 1-of-1 (you keep this)
  - Group 2 (Friends): 3-of-5 (distribute to 5 friends, need 3)
  - Group 3 (Family): 2-of-6 (distribute to 6 family members, need 2)

**Recovery scenarios:**
- ✅ You + Personal Backup #2 = Access
- ✅ 3 Friends + 2 Family = Access
- ❌ Just 3 Friends = No access (need 2 groups)
- ❌ Personal Backup #1 alone = No access (need 2 groups)

### Passphrase Encryption

**Two types of passphrases:**

1. **BIP-39 Passphrase** (the "25th word")
   - Part of your wallet (used during key derivation)
   - ✅ **Backed up in the SLIP-39 shares**
   - Combined with seed: `[16 bytes seed][passphrase bytes]`

2. **SLIP-39 Passphrase** (encrypts the shares)
   - Encrypts the SLIP-39 shares themselves
   - ❌ **NOT backed up** - you must remember this!
   - Provides extra security layer - stolen shares are useless without it

## Features

### Web Application

- **Two-column layout**: Configuration on left, results on right
- **Configurable groups**: Add, remove, customize thresholds and counts
- **QR codes**: Scannable QR codes (250px) for each share
- **Dark theme**: Easy on the eyes for extended use
- **On-demand hex/bytes**: Technical details only when requested
- **Flexible recovery**: Paste multiple shares at once, auto-detects groups
- **100% client-side**: All crypto runs in browser (WebAssembly)

### Security

✅ **No server processing**: Everything runs in your browser (Blazor WASM)
✅ **No data transmission**: Verifiable in browser DevTools (Network tab)
✅ **Offline capable**: Works completely offline on Tails Linux
✅ **Efficient binary storage**: BIP-39 seed stored as 16 bytes (not 80+ char text)
✅ **Tails-optimized**: RAM-only operation, everything wiped on shutdown

## Usage

### Option 1: Online (GitHub Pages)

**Live app:** https://bitcoin-self-custody.github.io/Seed-Phrase-Storage-SLIP39

Access via any browser (Tor Browser recommended):
- Works on any device with a modern browser
- Perfect for Tails Linux with internet access

**Still secure:**
- All crypto runs in browser (WebAssembly)
- No data sent to server - verify in DevTools Network tab
- Can disconnect network after page loads
- Source code visible for verification

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

# 3. Configure Tor Browser
#    Type in address bar: about:preferences
#    Network Settings → Settings
#    Under "No Proxy for", add: 127.0.0.1, localhost

# 4. Open app
#    Navigate to: http://127.0.0.1:9876
```

See [TAILS_INSTRUCTIONS.md](TAILS_INSTRUCTIONS.md) for complete guide.

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

### Backing Up Your Wallet

1. **Enter credentials:**
   - BIP-39 seed phrase (12 words)
   - BIP-39 passphrase (optional "25th word")
   - SLIP-39 passphrase (to encrypt shares)

2. **Configure groups:**
   - Set group threshold (how many groups to recover)
   - Customize each group (name, threshold, share count)

3. **Generate shares:**
   - Click "🔐 Generate Shares"
   - Get shares for all groups with QR codes
   - Write shares on paper/metal (NOT digital!)

4. **Distribute shares:**
   - Give shares to designated people/locations
   - Never store all shares together!

### Recovering Your Wallet

1. **Collect shares:**
   - Gather shares from required number of groups
   - Paste all shares in recovery textarea (one per line)

2. **Enter SLIP-39 passphrase:**
   - The passphrase you used when generating shares

3. **Recover:**
   - Click "🔓 Recover Wallet"
   - Get back your BIP-39 seed and passphrase
   - Import into wallet application

## Technical Details

### Storage Format

**What gets backed up in shares:**
```
[16 bytes: BIP-39 seed][N bytes: BIP-39 passphrase][padding if needed]
```

**Efficiency:**
- BIP-39 seed: 12 words → 16 bytes (not 80+ chars as text!)
- BIP-39 passphrase: UTF-8 encoded
- Total: ~30-35 bytes typical (vs 100+ if stored as text)

### Dependencies

- [Xecrets.Slip39](https://www.nuget.org/packages/Xecrets.Slip39/) - SLIP-39 implementation
- [QRCoder](https://www.nuget.org/packages/QRCoder/) - QR code generation
- Blazor WebAssembly (.NET)

### SLIP-39 Implementation

Uses the Xecrets.Slip39 library which implements:
- SLIP-0039 standard for Shamir's Secret Sharing
- BIP-39 interoperability (convert between formats)
- PBKDF2 encryption with configurable iteration count
- Checksum validation
- URL-safe base64 encoding option

## Development

### Project Structure

```
Seed-Phrase-Storage-SLIP39/
├── ReadmeExample.linq          # LINQPad demo/exploration script
├── Slip39Demo.Web/             # Blazor WASM application
│   ├── Pages/
│   │   └── Home.razor          # Main UI
│   ├── wwwroot/
│   │   ├── start-server.sh     # Tails server startup script
│   │   └── README_OFFLINE.md   # Offline usage guide
│   └── Slip39Demo.Web.csproj
├── .github/workflows/
│   └── build-and-release.yml   # CI/CD pipeline
├── TAILS_INSTRUCTIONS.md       # Complete Tails guide
└── README.md                   # This file
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
git tag v1.0.0
git push origin v1.0.0
```

This triggers:
- Build and publish
- Create GitHub Release
- Attach `slip39-backup-v1.0.0.zip` for download

## Security Considerations

### What This Tool Does

✅ **Client-side only**: All operations in browser (WebAssembly)
✅ **No external requests**: Zero network traffic (verify in DevTools)
✅ **No persistence**: Doesn't save anything to disk automatically
✅ **Tails compatible**: Works on RAM-only OS with offline mode

### What You Must Do

⚠️ **Write shares on paper/metal** - Never digital storage!
⚠️ **Remember SLIP-39 passphrase** - Not backed up for security!
⚠️ **Distribute shares securely** - Never all in one place!
⚠️ **Test recovery** - Always test before distributing!
⚠️ **No screenshots** - Don't screenshot shares!

### Privacy on Tails

When using on Tails Linux:
- Everything runs in RAM
- Browser doesn't save history/cache
- Full system wipe on shutdown
- Tor Browser provides additional isolation
- Local Python server (127.0.0.1) ensures no network exposure

## FAQ

**Q: Is my seed phrase transmitted to a server?**
A: No. All cryptography runs in your browser via WebAssembly. Check browser DevTools Network tab to verify.

**Q: Can I use this without internet?**
A: Yes! Run the Python server locally on Tails or any OS. No internet required.

**Q: What if I forget the SLIP-39 passphrase?**
A: Your shares become useless. The SLIP-39 passphrase is critical - it's not backed up by design.

**Q: What if I lose some shares?**
A: As long as you have the required threshold of groups, you can recover. Example: If configured as "2 of 4 groups", losing 2 entire groups is OK.

**Q: Is this better than just writing down my BIP-39 seed?**
A: Yes, because:
- Single point of failure eliminated
- Distributable to multiple locations/people
- Configurable redundancy (you choose thresholds)
- Passphrase encryption adds extra security layer

**Q: Can I recover my wallet with this tool?**
A: This tool generates SLIP-39 shares and can test recovery. To actually access your wallet, import the recovered BIP-39 seed into your wallet application.

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

## Disclaimer

This tool is provided as-is for educational and personal use. Always:
- Test thoroughly before trusting with real funds
- Understand the cryptography before using
- Keep backups of backups
- Verify the source code yourself

**Use at your own risk. This is experimental software for handling sensitive cryptographic material.**

---

*Collaboration by Claude*
