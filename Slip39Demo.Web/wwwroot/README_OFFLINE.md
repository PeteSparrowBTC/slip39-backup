# SLIP-39 + age Wallet Backup - Offline Usage

This folder contains everything needed to run the wallet backup tool offline. The app has two pages:

- **`/owner`** — create a backup (downloads `output.zip`).
- **`/recoverer`** — recover a wallet from share zips + `payload.age`.

## Quick Start on Tails Linux

### 1. Copy to USB
Copy this entire folder to a USB drive.

### 2. On Tails Terminal
```bash
cd /media/amnesia/YOUR_USB_NAME/slip39-backup
./start-server.sh
```

### 3. Open the App in your browser

**LibreWolf (recommended):** just navigate to `http://127.0.0.1:9876`.

**Tor Browser:** localhost is blocked by the Tor proxy by default. Configure once:
1. Type in address bar: `about:preferences`
2. Search for: "Network Settings"
3. Click **Settings** button
4. Under **"No Proxy for"**, add: `127.0.0.1, localhost`
5. Click **OK**
6. Navigate to: `http://127.0.0.1:9876`

### 4. Create a backup

1. Click **Start backup** (or go to `/owner`).
2. Enter your BIP-39 seed words in **Top-level seed words**.
3. (Optional) Set a label, add a BIP-39 passphrase to a cosigner, or adjust the threshold (default 3-of-5).
4. Click **Generate**. The browser downloads `output.zip`.
5. Save `output.zip` to your USB. Inside it:
   - `shares/share-K-of-N.zip` — distribute to physical storage locations.
   - `payload/payload.age` + `payload/payload.age.txt` — store in a password-manager entry with Emergency Access for your executor.
   - `verification-record.txt` — keep alongside the payload for periodic dry-run checks.

### 5. (Recommended) Dry-run recover before trusting the backup

Still on the same offline session, click **Start recovery** (or go to `/recoverer`):
1. Drop threshold-many share `.zip` files into the mnemonics picker.
2. Drop `payload.age` into the ciphertext picker.
3. Click **Recover**. Confirm the seed words match what you entered.

### 6. When Done
- Press `Ctrl+C` in terminal to stop server
- Shutdown Tails (all RAM wiped — only files written to USB persist)
- On a separate trusted device, split the contents of `output.zip` to their long-term homes and delete the zip itself

## Security Notes

✅ **100% Client-Side**: All cryptography runs in your browser (WebAssembly)
✅ **Standard primitives**: SLIP-39 for the share split, age for payload encryption — no proprietary encoding
✅ **No SLIP-39 passphrase to remember**: security boundary is "threshold-many shares **and** `payload.age`"
✅ **No Network Access**: Server binds to 127.0.0.1 only (localhost)
✅ **Verifiable**: Check browser Network tab - zero outbound requests
✅ **Tails-Safe**: Everything wiped on shutdown

⚠️ **Important**: Store share zips offline (paper / metal / trusted holders), and put `payload.age` in a password-manager entry with Emergency Access. Never keep all of them in one place.

## What's Included

- `index.html` - Main application (landing page with Owner / Recoverer chooser)
- `_framework/` - WebAssembly runtime and compiled app
- `start-server.sh` - Easy server startup script
- `serve.py` - Python server (if you prefer manual control)
- All dependencies (Xecrets.Slip39, in-tree age implementation)

## File Size

Total: ~27 MB (mostly .NET WebAssembly runtime)

## Troubleshooting

**Script won't run?**
```bash
chmod +x start-server.sh
./start-server.sh
```

**Different port?**
```bash
python3 -m http.server 8080 --bind 127.0.0.1
```

**Tor Browser refuses connection?**
- Must configure "No Proxy for" as described above
- Or use LibreWolf (works with localhost out of the box)

See `TAILS_INSTRUCTIONS.md` in the parent repo for full details.
