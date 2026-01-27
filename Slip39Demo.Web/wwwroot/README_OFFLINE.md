# SLIP-39 Wallet Backup - Offline Usage

This folder contains everything needed to run the SLIP-39 wallet backup tool offline.

## Quick Start on Tails Linux

### 1. Copy to USB
Copy this entire folder to a USB drive.

### 2. On Tails Terminal
```bash
cd /media/amnesia/YOUR_USB_NAME/slip39-backup
./start-server.sh
```

### 3. Configure Tor Browser
1. Type in address bar: `about:preferences`
2. Search for: "Network Settings"
3. Click **Settings** button
4. Under **"No Proxy for"**, add: `127.0.0.1, localhost`
5. Click **OK**

### 4. Open the App
Navigate to: `http://127.0.0.1:9876`

### 5. When Done
- Press `Ctrl+C` in terminal to stop server
- Shutdown Tails (all RAM wiped)

## Security Notes

✅ **100% Client-Side**: All cryptography runs in your browser (WebAssembly)
✅ **No Network Access**: Server binds to 127.0.0.1 only (localhost)
✅ **Verifiable**: Check browser Network tab - zero outbound requests
✅ **Tails-Safe**: Everything wiped on shutdown

⚠️ **Important**: Write shares on paper/metal, NOT digital storage!

## What's Included

- `index.html` - Main application
- `_framework/` - WebAssembly runtime and compiled app
- `start-server.sh` - Easy server startup script
- `serve.py` - Python server (if you prefer manual control)
- All dependencies (Xecrets.Slip39, QRCoder)

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
- Must configure "No Proxy for" as described in step 3
- Or use GitHub Pages version (still 100% client-side)

See `TAILS_INSTRUCTIONS.md` in parent repo for full details.
