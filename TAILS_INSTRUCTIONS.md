# Running SLIP-39 + age Wallet Backup on Tails Linux

This guide shows you how to run the SLIP-39 + age seed phrase backup tool completely offline on Tails Linux.

The app has two pages you'll use:
- **`/owner`** — create a backup. Outputs a single `output.zip` (share zips + `payload.age` + verification record). Save it to your USB.
- **`/recoverer`** — recover a wallet. Drop threshold-many share zips and `payload.age` in, click Recover.

## Prerequisites

- USB drive (for transferring files)
- Tails Linux booted on your computer
- Published app files (in `Slip39Demo.Web/publish/wwwroot/`)
- **LibreWolf browser** (recommended): Download [LibreWolf.x86_64.AppImage](https://gitlab.com/librewolf-community/browser/appimage/-/releases) or from [librewolf.net](https://librewolf.net/installation/linux/)

## Publishing the App (Do this on Windows/Mac/Linux with .NET)

```bash
cd Slip39Demo.Web
dotnet publish -c Release -o publish
```

Static files will be in: `publish/wwwroot/` (~27 MB)

## Option 1: Run Local Web Server on Tails (Recommended for Offline Use)

### Step 1: Copy Files to USB

1. Copy the `publish/wwwroot` folder to your USB drive
2. Rename it to `slip39-backup`

### Step 2: Boot Tails and Mount USB

1. Boot into Tails Linux
2. Insert USB drive (should auto-mount)
3. Note the path (usually `/media/amnesia/USB_NAME/`)

### Step 3: Start Local Web Server

Open **Terminal** in Tails and run:

```bash
# Navigate to the app folder
cd /media/amnesia/YOUR_USB_NAME/slip39-backup

# Option A: Use included script (easiest)
./start-server.sh

# Option B: Manual Python command
python3 -m http.server 9876 --bind 127.0.0.1
```

You'll see: `Serving HTTP on 127.0.0.1 port 9876 ...`

### Step 4: Open in Browser

**Option A: LibreWolf (Recommended - Works Out of the Box)**

LibreWolf is a privacy-focused Firefox fork that works perfectly with localhost without any configuration:

1. **Download LibreWolf AppImage** (do this before booting Tails):
   - From [GitLab Releases](https://gitlab.com/librewolf-community/browser/appimage/-/releases)
   - Or from [librewolf.net](https://librewolf.net/installation/linux/)
   - Save `LibreWolf.x86_64.AppImage` to your USB drive

2. **On Tails**, copy AppImage to your USB (if not already there):
   ```bash
   # Copy to USB if downloaded elsewhere
   cp ~/Downloads/LibreWolf.x86_64.AppImage /media/amnesia/YOUR_USB/
   ```

3. **Make it executable and run**:
   ```bash
   cd /media/amnesia/YOUR_USB
   chmod +x LibreWolf.x86_64.AppImage
   ./LibreWolf.x86_64.AppImage
   ```

4. **Navigate to**: `http://127.0.0.1:9876`
5. The app loads and runs **100% locally**

**Why LibreWolf?**
- ✅ No proxy configuration needed
- ✅ Works with localhost immediately
- ✅ Privacy-focused (like Tor Browser)
- ✅ Portable AppImage (keep on USB)
- ✅ No installation required

**Option B: Tor Browser (Requires Proxy Configuration)**

⚠️ **Note:** Tor Browser on Tails routes localhost through Tor proxy by default, which blocks local connections.

If you must use Tor Browser:

1. Open **Tor Browser**
2. Type in address bar: `about:preferences`
3. Scroll to **Network Settings** → Click **Settings**
4. Under **"No Proxy for"**, add: `127.0.0.1, localhost`
5. Click **OK**
6. Navigate to: `http://127.0.0.1:9876`

**Tor Browser issues:**
- Proxy configuration can be reset
- More complex setup
- LibreWolf is simpler for this use case

### Step 5: Use Offline

✅ **This is completely offline:**
- Server runs locally on your Tails machine
- No network traffic (check with `127.0.0.1` URL)
- All crypto operations in browser
- Nothing sent to internet

**To verify offline mode:**
```bash
# In another terminal, check if any network connections
sudo netstat -tunlp | grep 9876
# Should show only 127.0.0.1:9876 (local only)
```

### Step 6: Create or recover a backup

**Creating a backup (Owner mode):**
1. From the landing page, click **Start backup** (or go to `/owner`).
2. Enter your BIP-39 seed words in the **Top-level seed words** field. Optionally add a label, a BIP-39 passphrase, or adjust the threshold/group shape (default 3-of-5 single group).
3. Click **Generate**. The browser downloads `output.zip`.
4. Save `output.zip` to your USB (e.g. `/media/amnesia/YOUR_USB/`). Inside it you'll find:
   - `shares/share-1-of-5.zip` … `share-5-of-5.zip` — distribute to your storage locations.
   - `payload/payload.age` and `payload/payload.age.txt` — store in a password-manager entry with Emergency Access for your executor.
   - `verification-record.txt` — keep alongside the payload for periodic dry-run recovery checks.
5. Split the contents of `output.zip` to their respective homes immediately, then delete the zip itself.

**Recovering a wallet (Recoverer mode):**
1. From the landing page, click **Start recovery** (or go to `/recoverer`).
2. Drop threshold-many share `.zip` files into the mnemonics file picker (or paste mnemonics one per line).
3. Drop `payload.age` (binary) or `payload.age.txt` (ASCII armor) into the ciphertext picker.
4. Click **Recover**. The recovered seed words appear with a reveal-on-click.

### Step 7: When Done

- Press `Ctrl+C` in terminal to stop web server
- Close the browser
- Shutdown Tails (all data cleared from RAM — only the files you wrote to USB persist)

## Option 2: GitHub Pages (Online Access via Tor)

If you want to access it anytime without USB:

### Setup (One Time):

```bash
# In the Seed-Phrase-Storage-SLIP39 repo
cd Slip39Demo.Web
dotnet publish -c Release

# Copy wwwroot contents to docs/ or gh-pages branch
cp -r publish/wwwroot/* ../docs/

# Commit and push
git add .
git commit -m "Publish SLIP-39 app to GitHub Pages"
git push
```

Enable GitHub Pages in repo settings → Pages → Source: `/docs`

### Access on Tails:

1. Boot Tails
2. Open Tor Browser
3. Navigate to: `https://yourusername.github.io/Seed-Phrase-Storage-SLIP39`
4. **Still 100% client-side** - no data sent to server
5. Can disconnect network after page loads if desired

## Option 3: Host on IPFS (Decentralized)

For maximum censorship resistance:

1. Publish app to IPFS
2. Access via IPFS gateway in Tor Browser
3. Pin to ensure availability

## Security Notes

### Why This is Safe:

✅ **100% Client-Side Processing**
- All WebAssembly runs in your browser
- No server-side code
- Xecrets.Slip39 library compiled to WASM

✅ **No Data Transmission**
- Nothing sent to any server
- All crypto happens locally
- Can verify with browser dev tools (Network tab)

✅ **Tails Security**
- RAM-only OS (no persistence)
- Tor Browser clears on close
- Full system wipe on shutdown

### Best Practices:

1. **Verify the Code**: Review source before use
2. **Offline Mode**: Use Python server method (127.0.0.1) for air-gapped
3. **No Screenshots**: Don't screenshot shares or the recovered seed
4. **Split `output.zip` immediately**: Move share zips to long-term homes and `payload.age` to your password manager, then delete `output.zip`
5. **Test Recovery**: Always do a dry-run recovery (Recoverer page) with the freshly produced shares before relying on the backup

## File Structure for USB

```
USB Drive
└── slip39-backup/
    ├── index.html
    ├── scroll.js
    ├── css/
    ├── lib/
    ├── _framework/
    │   ├── blazor.webassembly.js
    │   ├── dotnet.*.wasm
    │   ├── Slip39Demo.Web.*.dll.wasm
    │   └── ... (other WASM files)
    └── ... (other assets)
```

## Troubleshooting

**Python server not working?**
- Try port 8080: `python3 -m http.server 8080`
- Access: `http://127.0.0.1:8080`

**App not loading?**
- Check browser console (F12) for errors
- Verify all files copied correctly
- Try with network connected first, then disconnect

**WASM not loading?**
- This is normal with `file://` - you MUST use a web server
- Python server solves this

## Why Not Just file:// ?

Blazor WASM requires:
- Proper HTTP headers for WASM MIME types
- Module loading from same origin
- WebAssembly streaming compilation

The `file://` protocol doesn't support these, so a local web server is required.

## Recommended Workflow

**For Maximum Security (Air-Gapped backup creation):**

1. Boot Tails WITHOUT network
2. Insert USB with app
3. Run: `./start-server.sh` (or `python3 -m http.server 9876 --bind 127.0.0.1`) in app folder
4. Open LibreWolf (or configured Tor Browser) → `http://127.0.0.1:9876`
5. Click **Start backup**, fill the form, click **Generate**, save `output.zip` to USB
6. Immediately do a dry-run recovery on the same offline session (Recoverer page) using the freshly produced shares + `payload.age` to confirm everything reconstructs
7. Shutdown Tails (RAM wiped — only the files written to USB persist)

After shutdown, on a separate trusted device, split the contents of `output.zip` into their long-term homes (share zips to physical storage, `payload.age` to password-manager with Emergency Access) and delete the zip.

**For Convenience (demo / learning only):**

Use GitHub Pages and access via Tor Browser anytime — but **never** with a real seed phrase.

---

*Collaboration by Claude*
