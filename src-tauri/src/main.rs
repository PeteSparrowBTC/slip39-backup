// The desktop shell.
//
// Tauri serves the frontend through its own protocol, in process, so nothing binds a
// port and nothing listens on any interface. It uses webkit2gtk-4.1, which Tails
// ships, so this AppImage bundles no browser engine.
//
// Three commands are exposed and no more. Each is a capability WebAssembly does not
// have, and none of them decides anything: the policy lives in C#, where the tests
// are.
//
// Capability question, settled by reading tauri (2.11.5) and tauri-utils (2.9.3) in
// this machine's cargo registry rather than from a running window (see
// tauri-2.11.5/src/webview/mod.rs, around handle_ipc_message): the ACL check on an
// invoke is skipped for local-origin requests to a command with no "plugin:" prefix,
// unless the app itself has defined an ACL manifest, meaning at least one file under
// src-tauri/permissions/*.toml that names an app command. This crate defines none, so
// is_online needs no entry in src-tauri/capabilities/*.json. Plugin commands are matched
// by their "plugin:" prefix and always go through the ACL regardless of this, so they do
// need capability entries. If this crate ever gains its own permissions/*.toml for an app
// command, every app command, including is_online, starts needing one too.
//
// All three commands bear this out: is_online, save_file and age_encrypt are app commands,
// registered through generate_handler! and given no entry, and need none. The dialog
// plugin's own save command, which save_file calls into via DialogExt, carries the
// "plugin:dialog" prefix and is why src-tauri/capabilities/default.json exists at all,
// granting "dialog:allow-save" and nothing else.
//
// ONE PLUGIN, NOT TWO. tauri-plugin-fs was registered here and was never used: every file
// this shell touches goes through std::fs, and no fs: permission is granted anywhere. A
// review caught it. It is gone, because an unused plugin in an artifact people run against
// real seed phrases is supply chain surface bought for nothing, and this project's own
// decision record ranks supply chain above implementation bugs as a risk.

#![cfg_attr(not(debug_assertions), windows_subsystem = "windows")]

mod age;
mod gpg;
mod net;
mod save;

fn main() {
    tauri::Builder::default()
        .plugin(tauri_plugin_dialog::init())
        .invoke_handler(tauri::generate_handler![
            net::is_online,
            save::save_file,
            age::age_encrypt,
            // Verifies the outer OpenPGP lock with the system gpg, so the artifact that
            // ships is checked by a program this project did not write. See gpg.rs.
            gpg::gpg_decrypt
        ])
        .run(tauri::generate_context!())
        .expect("slip39-backup: failed to start the window");
}
