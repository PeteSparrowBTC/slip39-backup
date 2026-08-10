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
// is_online needs no entry in src-tauri/capabilities/*.json. Plugin commands (the
// dialog and fs plugins Task 3 and Task 5 add) are matched by their "plugin:" prefix
// and always go through the ACL regardless of this, so they will need capability
// entries. If this crate ever gains its own permissions/*.toml for an app command,
// every app command, including is_online, starts needing one too.

#![cfg_attr(not(debug_assertions), windows_subsystem = "windows")]

mod net;

fn main() {
    tauri::Builder::default()
        .invoke_handler(tauri::generate_handler![net::is_online])
        .run(tauri::generate_context!())
        .expect("slip39-backup: failed to start the window");
}
