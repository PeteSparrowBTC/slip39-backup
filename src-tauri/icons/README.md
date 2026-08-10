# Icons

`icon.png` is the application icon, copied from `Slip39Demo.UI/wwwroot/favicon.png`.
It is 240x240 RGBA. Tauri rejects a non-RGBA icon outright, so if it is ever replaced,
check the colour type before assuming it works.

`icon.ico` exists for one reason and it is not a Windows build target. `tauri-build`
runs a Windows resource step whenever the host triple contains `windows`, and that step
requires an `.ico` and fails the build without one, before `rustc` reaches `src/main.rs`.
The shipped artifact is a Linux AppImage for Tails, so nothing in a release uses this
file. Without it, a contributor on Windows cannot compile the shell at all, and cannot
run `cargo test` either, because `cargo test` runs `build.rs` too. That is how the Rust
side of this shell came to be written with nothing having compiled it: worth one 20 KB
file to avoid.

Regenerate it from the PNG:

```bash
python -c "from PIL import Image; Image.open('icon.png').convert('RGBA').save('icon.ico', sizes=[(16,16),(32,32),(48,48),(64,64),(128,128),(256,256)])"
```
