# SLIP-39 Seed Phrase Backup - Blazor WASM

A client-side web application for backing up BIP-39 wallet seed phrases using SLIP-39 multi-group secret sharing.

## Features

- **100% Client-Side**: Runs entirely in your browser - no server, no data sent anywhere
- **Tor Browser Compatible**: Safe to use on Tails Linux in Tor Browser
- **Efficient Storage**: BIP-39 seed stored as 16 bytes (not text) + passphrase
- **Multi-Group Sharing**: Default 4-group configuration (Alice, Friends, Family)
- **Complete Backup**: Backs up both BIP-39 seed AND BIP-39 passphrase together
- **Interactive Recovery**: Test recovery right in the browser

## What Gets Backed Up

✅ **In the SLIP-39 shares:**
- BIP-39 seed phrase (12 words → 16 bytes)
- BIP-39 passphrase (the "25th word" if you use one)

❌ **Not in shares (must remember):**
- SLIP-39 passphrase (encrypts the shares for security)

## Running Locally

```bash
cd Slip39Demo.Web
dotnet run
```

Then open browser to: `https://localhost:5001` (or HTTP port shown)

## Publishing for Static Hosting

```bash
dotnet publish -c Release
```

Output will be in `bin/Release/net10.0/publish/wwwroot/`

You can serve this folder with any static file server, or even open `index.html` directly in a browser.

## Using on Tails Linux

1. Copy the published `wwwroot` folder to a USB drive
2. Boot into Tails Linux
3. Mount the USB drive
4. Open Tor Browser
5. Navigate to `file:///path/to/wwwroot/index.html`
6. Use completely offline - no network required!

## Security Notes

- Uses Xecrets.Slip39 NuGet package for SLIP-39 implementation
- All cryptographic operations happen in your browser
- No analytics, no tracking, no external dependencies
- Perfect for air-gapped or Tor Browser usage

*Collaboration by Claude*
