//! Runs the official age program bundled beside this executable, rather than a
//! library compiled into the application.
//!
//! WHY, carried over from Slip39Demo.Desktop/Services/NativeAgeEncryptor.cs: a bug in
//! an encryptor is invisible. A file written with a reused nonce or a weak key
//! decrypts perfectly and stays weak forever, so no amount of round-trip testing
//! finds it. A bug in a decryptor is loud. This artifact is the one people run
//! against real seed phrases, so the side where mistakes cannot be seen gets the
//! reference implementation.
//!
//! HOW THE KEY IS PASSED: in AGE_PASSPHRASE, which age-plugin-batchpass reads, and
//! never on the command line, where every other process could read it from the
//! process list. PATH is pinned to the bundled directory so age cannot pick up some
//! other age-plugin-batchpass that happens to be on the machine.
//!
//! This module applies NO policy. It does not decide whether an exit code is
//! acceptable, does not build the transcript, and does not fall back to anything.
//! Those judgements live in Slip39Demo.Tauri/Services/TauriAgeEncryptor.cs, where the
//! test suite is.

use base64::{engine::general_purpose::STANDARD, Engine};
use serde::Serialize;
use sha2::{Digest, Sha256};
use std::io::Write;
use std::path::{Path, PathBuf};
use std::process::{Command, Stdio};

/// Where build-appimage.sh puts the official binaries, relative to the executable.
const AGE_SUBDIR: &str = "age";

#[derive(Serialize)]
#[serde(rename_all = "camelCase")]
pub struct AgeRun {
    pub exit_code: i32,
    pub stdout_b64: String,
    pub stdout_text: String,
    pub stderr_text: String,
    pub age_path: String,
    pub age_sha256: String,
    pub plugin_path: String,
    pub age_missing: bool,
    pub plugin_missing: bool,
}

/// Resolved from the running executable, which inside a mounted AppImage is
/// /tmp/.mount_xxxx/usr/bin/slip39-backup, so the sibling age directory resolves
/// correctly without reading APPDIR.
pub fn age_dir_for(exe: &Path) -> PathBuf {
    exe.parent().unwrap_or(Path::new(".")).join(AGE_SUBDIR)
}

/// Reported when the bundle is not there. Built here rather than inline so the
/// missing-binary shape is defined in exactly one place and a test can assert it.
fn bundle_missing(age: &Path, plugin: &Path) -> AgeRun {
    AgeRun {
        exit_code: -1,
        stdout_b64: String::new(),
        stdout_text: String::new(),
        stderr_text: String::new(),
        age_path: age.display().to_string(),
        age_sha256: String::new(),
        plugin_path: plugin.display().to_string(),
        age_missing: !age.exists(),
        plugin_missing: !plugin.exists(),
    }
}

fn run(exe: &Path, args: &[&str], dir: &Path, stdin: Option<&[u8]>, passphrase: Option<&str>)
    -> std::io::Result<(i32, Vec<u8>, String)>
{
    let mut command = Command::new(exe);
    command
        .args(args)
        .current_dir(dir)
        .env("PATH", dir)
        .stdin(Stdio::piped())
        .stdout(Stdio::piped())
        .stderr(Stdio::piped());

    if let Some(value) = passphrase {
        command.env("AGE_PASSPHRASE", value);
    }

    let mut child = command.spawn()?;
    if let Some(bytes) = stdin {
        child.stdin.as_mut().expect("stdin was piped").write_all(bytes)?;
    }
    drop(child.stdin.take());

    let output = child.wait_with_output()?;
    Ok((
        output.status.code().unwrap_or(-1),
        output.stdout,
        String::from_utf8_lossy(&output.stderr).into_owned(),
    ))
}

/// The whole of the work, with the directory passed in rather than discovered.
///
/// Split from the command for one reason: a test can point it at a directory it
/// controls. The version that read `current_exe()` internally could only be tested
/// against wherever the test harness happened to live, which made the missing-bundle
/// test assert something that was true either way.
pub fn encrypt_with(dir: &Path, plaintext: &[u8], passphrase_hex: &str) -> Result<AgeRun, String> {
    let age = dir.join("age");
    let plugin = dir.join("age-plugin-batchpass");

    // Reported rather than decided. C# owns the message a user sees, and the reason
    // there is no fallback.
    if !age.exists() || !plugin.exists() {
        return Ok(bundle_missing(&age, &plugin));
    }

    // Identify the exact binary being trusted. C# used to compute this itself, and
    // WebAssembly cannot read a file, so it moves here.
    let bytes = std::fs::read(&age).map_err(|e| format!("cannot read {}: {e}", age.display()))?;
    let age_sha256 = format!("{:x}", Sha256::digest(&bytes));

    let (version_code, version_out, version_err) = run(&age, &["--version"], dir, None, None)
        .map_err(|e| format!("the bundled age program would not run: {e}"))?;
    if version_code != 0 {
        return Err(format!("age --version exited with {version_code}: {version_err}"));
    }

    let (code, stdout, stderr) = run(
        &age,
        &["--encrypt", "-j", "batchpass"],
        dir,
        Some(plaintext),
        Some(passphrase_hex),
    )
    .map_err(|e| format!("age failed to run: {e}"))?;

    Ok(AgeRun {
        exit_code: code,
        stdout_b64: STANDARD.encode(&stdout),
        // Carries the `age --version` output, because that is what the transcript
        // prints. The ciphertext travels in stdout_b64, never as text.
        stdout_text: String::from_utf8_lossy(&version_out).trim().to_string(),
        stderr_text: stderr,
        age_path: age.display().to_string(),
        age_sha256,
        plugin_path: plugin.display().to_string(),
        age_missing: false,
        plugin_missing: false,
    })
}

/// The command. Finds the bundle, decodes what the frontend sent, and delegates. No
/// judgement here either: see the module comment.
#[tauri::command]
pub fn age_encrypt(plaintext_b64: String, passphrase_hex: String) -> Result<AgeRun, String> {
    let exe = std::env::current_exe().map_err(|e| format!("cannot locate this program: {e}"))?;
    let plaintext = STANDARD
        .decode(plaintext_b64)
        .map_err(|e| format!("the frontend sent something that is not base64: {e}"))?;
    encrypt_with(&age_dir_for(&exe), &plaintext, &passphrase_hex)
}

#[cfg(test)]
mod tests {
    use super::*;

    fn temp(name: &str) -> PathBuf {
        let dir = std::env::temp_dir().join(format!("slip39-age-{name}"));
        let _ = std::fs::remove_dir_all(&dir);
        std::fs::create_dir_all(&dir).unwrap();
        dir
    }

    #[test]
    fn age_dir_sits_beside_the_executable() {
        let dir = age_dir_for(Path::new("/tmp/.mount_abc/usr/bin/slip39-backup"));
        assert_eq!(dir, PathBuf::from("/tmp/.mount_abc/usr/bin/age"));
    }

    // The other half of the contract with C#. Slip39Demo.Tests/Tauri/AgeRunDtoTests.cs
    // pins these names from the C# side; this pins them here, so a rename fails a test
    // instead of arriving in the frontend as a default-valued report that reads like a
    // successful run producing nothing.
    #[test]
    fn the_struct_serializes_to_the_names_csharp_expects() {
        let json = serde_json::to_string(&bundle_missing(
            Path::new("/x/age"),
            Path::new("/x/age-plugin-batchpass"),
        ))
        .unwrap();

        for name in [
            "exitCode",
            "stdoutB64",
            "stdoutText",
            "stderrText",
            "agePath",
            "ageSha256",
            "pluginPath",
            "ageMissing",
            "pluginMissing",
        ] {
            assert!(json.contains(&format!("\"{name}\"")), "{name} missing from {json}");
        }
        // No snake_case leaked through, which is the shape the rename is guarding against.
        assert!(!json.contains("age_missing"), "serde rename did not apply: {json}");
    }

    // A missing bundle is a structured answer, not an Err. C# turns it into the
    // message about refusing to fall back, and it can only do that if it is told
    // which of the two binaries is absent.
    #[test]
    fn a_missing_bundle_is_reported_not_thrown() {
        let run = encrypt_with(&temp("empty"), b"hi", &"00".repeat(32)).unwrap();

        assert!(run.age_missing, "age_missing");
        assert!(run.plugin_missing, "plugin_missing");
        assert_eq!(run.exit_code, -1);
        assert!(run.stdout_b64.is_empty());
        assert!(run.age_path.ends_with("age"), "age_path was {}", run.age_path);
    }

    // The plugin is the half people forget, so it is reported separately. age itself
    // is present here, and nothing is run: the bundle is incomplete, so there is
    // nothing to decide.
    #[test]
    fn a_missing_plugin_is_reported_on_its_own() {
        let dir = temp("no-plugin");
        std::fs::write(dir.join("age"), b"not really age").unwrap();

        let run = encrypt_with(&dir, b"hi", &"00".repeat(32)).unwrap();

        assert!(!run.age_missing, "age was present");
        assert!(run.plugin_missing, "plugin_missing");
        assert!(run.age_sha256.is_empty(), "nothing was hashed or run");
    }

    // The one test that runs the real program, and the reason the plan's admission
    // about losing subprocess coverage no longer holds. Set SLIP39_AGE_DIR to an
    // unpacked age release to enable it; CI does, after verifying the pinned
    // checksum. Absent, it reports why it did nothing rather than failing, so a
    // contributor without the binaries sees a partial run instead of a red suite.
    // This mirrors the convention CLAUDE.md documents for the C# suite.
    #[test]
    fn the_real_age_program_produces_an_age_file() {
        let Ok(dir) = std::env::var("SLIP39_AGE_DIR") else {
            eprintln!("skipped: SLIP39_AGE_DIR is not set, so no age release is available");
            return;
        };

        let run = encrypt_with(Path::new(&dir), b"a wallet payload", &"11".repeat(32))
            .expect("the bundled age program should have run");

        assert_eq!(run.exit_code, 0, "stderr was: {}", run.stderr_text);
        assert_eq!(run.age_sha256.len(), 64, "sha256 should be 64 hex characters");
        assert!(
            run.stdout_text.contains("1.3.1"),
            "age --version said {}",
            run.stdout_text
        );

        let ciphertext = STANDARD.decode(&run.stdout_b64).unwrap();
        let header = String::from_utf8_lossy(&ciphertext[..21]);
        assert_eq!(header, "age-encryption.org/v1");
    }
}
