# Running slip39-backup on Tails

The tool ships as a single AppImage that opens a **native window** — no
browser, no local server, no Tor configuration. All cryptography runs locally;
the window is rendered by WebKitGTK, which Tails ships out of the box.

The app has two pages you'll use:
- **`/owner`** — create a backup. Outputs a single `output.zip` (share zips + `payload.age` + verification record).
- **`/recoverer`** — recover a wallet. Drop threshold-many share zips and `payload.age` in, click Recover.

## Requirements

- **Tails 7.0 or later** (Debian 13 base). Tails 6 and older are end-of-life
  and unsupported — on them the app fails to start with a `GLIBC_2.38 not
  found` error. Check your version: Applications → Tails → About Tails.
- A USB drive with the AppImage (and its `.sha256`, if you downloaded it from GitHub
  Releases). The filename carries the version, for example
  `slip39-backup-2.0.0-x86_64.AppImage`. Substitute the version you downloaded in the
  commands below; the app shows the same number in its footer, so you can check the
  file you ran is the file you meant to run.

## Steps

1. **Boot Tails.** For real backups, do **not** connect to a network — the
   whole point is generating secrets on an airgapped machine. The tool detects
   connectivity and watermarks anything generated while online as
   INSECURE-TEST.

2. **Copy the AppImage** from the USB into the home folder (`/home/amnesia`).
   Running from the home folder avoids filesystem quirks of FAT-formatted
   sticks (where the executable bit can't be set).

3. **Verify the download** (only needed if the file came from GitHub rather
   than your own build):
   ```bash
   sha256sum -c slip39-backup-2.0.0-x86_64.AppImage.sha256
   ```

4. **Run it** (Files → right-click the folder → Open Terminal Here):
   ```bash
   chmod +x slip39-backup-2.0.0-x86_64.AppImage
   ./slip39-backup-2.0.0-x86_64.AppImage
   ```
   The app window opens directly. (Double-clicking in the Files app does
   nothing — GNOME deliberately refuses to launch raw executables; the
   terminal is the supported path.)

5. **Create your backup** in the Owner page. Save the output where you choose
   via the native save dialog; print the recovery kit via the print dialog
   (print-to-PDF works out of the box).

6. **Shut down Tails.** Everything outside your explicit saves is wiped — the
   app itself writes nothing.

## Troubleshooting

- **`GLIBC_2.38 not found`** — your Tails is version 6 or older. Upgrade the
  stick to Tails 7+ (a 6→7 jump needs a fresh install / Tails Cloner, not the
  automatic upgrader).
- **`Permission denied` when running** — the file is on a FAT-formatted stick
  where `chmod +x` silently does nothing. Copy it to the home folder first.
- **Window doesn't open, webkit errors in terminal** — you're not on Tails
  (the AppImage deliberately relies on Tails's system WebKitGTK; other distros
  need `libwebkit2gtk-4.1` installed).
