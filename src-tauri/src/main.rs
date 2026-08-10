// The desktop shell.
//
// Tauri serves the frontend through its own protocol, in process, so nothing binds a
// port and nothing listens on any interface. It uses webkit2gtk-4.1, which Tails
// ships, so this AppImage bundles no browser engine.
//
// Three commands are exposed and no more. Each is a capability WebAssembly does not
// have, and none of them decides anything: the policy lives in C#, where the tests
// are.

#![cfg_attr(not(debug_assertions), windows_subsystem = "windows")]

fn main() {
    tauri::Builder::default()
        .run(tauri::generate_context!())
        .expect("slip39-backup: failed to start the window");
}
