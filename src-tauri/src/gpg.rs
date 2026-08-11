//! Runs the system GnuPG to unwrap the artifact this tool just produced, so the
//! outer OpenPGP lock is verified by software this project did not ship.
//!
//! WHY THE SYSTEM gpg AND NOT A BUNDLED LIBRARY
//! The release gate exists to have our output checked by an implementation we had no
//! hand in. A JavaScript OpenPGP library bundled into our own verify script would sit
//! uncomfortably close to the problem that got an earlier in-bundle browser checker
//! deleted: a checker we ship cannot independently vouch for its own producer.
//!
//! GnuPG is already on the target machine. Tails lists it in its own included-software
//! page, "GnuPG, the GNU implementation of OpenPGP for email and data encryption and
//! signing", and notably does not ship age. So the outer lock is checked by a program
//! Tails put there and the GnuPG project maintains, and nothing has to be bundled to
//! get it.
//!
//! HOW THE KEY IS PASSED
//! On stdin, read through --passphrase-fd 0, never on the command line where any other
//! process could read it from the process list. That differs from age.rs, which uses an
//! environment variable because age's batchpass plugin reads one; gpg takes a file
//! descriptor directly, so the tighter option is available here and is used.
//!
//! The ciphertext goes in as a temporary file rather than on stdin, because stdin is
//! carrying the passphrase. That file holds the already-encrypted armor, the same bytes
//! the bundle ships, so it discloses nothing that is not already destined for a password
//! manager. It is removed before returning, on every path.
//!
//! This module applies NO policy. It does not decide whether a missing gpg should stop
//! generation, nor whether the output matches what was expected. Those judgements live
//! in Slip39Demo.Tauri/Services/TauriPgpVerifier.cs, where the tests are.

use base64::{engine::general_purpose::STANDARD, Engine};
use serde::Serialize;
use std::io::Write;
use std::path::PathBuf;
use std::process::{Command, Stdio};

#[derive(Serialize)]
#[serde(rename_all = "camelCase")]
pub struct GpgRun {
    pub exit_code: i32,
    /// The unwrapped inner file, base64 so it survives the JSON hop intact.
    pub stdout_b64: String,
    pub stderr_text: String,
    /// What `gpg --version` reported, first line only, for the transcript.
    pub version: String,
    /// True when gpg could not be started at all. The distinction matters: a missing
    /// program is an environment problem to report as such, not a failed decryption.
    pub gpg_missing: bool,
}

fn missing(detail: String) -> GpgRun {
    GpgRun {
        exit_code: -1,
        stdout_b64: String::new(),
        stderr_text: detail,
        version: String::new(),
        gpg_missing: true,
    }
}

/// A temporary path for the ciphertext. Uses the process id and a counter rather than a
/// random name because collisions here are a nuisance rather than a security property:
/// the content is ciphertext, and the file is removed before this function returns.
fn temp_path(suffix: &str) -> PathBuf {
    std::env::temp_dir().join(format!("slip39-verify-{}-{}", std::process::id(), suffix))
}

fn gpg_version() -> Option<String> {
    let output = Command::new("gpg").arg("--version").output().ok()?;
    if !output.status.success() {
        return None;
    }
    let text = String::from_utf8_lossy(&output.stdout);
    Some(text.lines().next().unwrap_or("").trim().to_string())
}

/// Unwraps an OpenPGP message, binary or ASCII armored, with a passphrase.
///
/// Returns what gpg said rather than a verdict. An exit code other than zero, or output
/// that does not match what the caller expected, is for C# to judge.
#[tauri::command]
pub fn gpg_decrypt(armored: String, passphrase: String) -> GpgRun {
    let version = match gpg_version() {
        Some(v) => v,
        None => return missing(
            "gpg could not be run. It is expected to be present: Tails ships GnuPG.".to_string()),
    };

    let input = temp_path("in.asc");
    if let Err(e) = std::fs::write(&input, armored.as_bytes()) {
        return missing(format!("could not write the temporary ciphertext: {e}"));
    }

    let result = run_gpg(&input, &passphrase);
    // Removed on every path, including the error ones above this point's success.
    let _ = std::fs::remove_file(&input);

    match result {
        Ok((exit_code, stdout, stderr_text)) => GpgRun {
            exit_code,
            stdout_b64: STANDARD.encode(&stdout),
            stderr_text,
            version,
            gpg_missing: false,
        },
        Err(e) => missing(format!("gpg failed to start: {e}")),
    }
}

fn run_gpg(input: &std::path::Path, passphrase: &str) -> std::io::Result<(i32, Vec<u8>, String)> {
    let mut child = Command::new("gpg")
        .args([
            "--batch",
            "--yes",
            "--decrypt",
            // Read the passphrase from stdin rather than from the command line or the
            // environment, both of which other processes can read.
            "--passphrase-fd",
            "0",
            // Without this, gpg tries to open a pinentry dialog and fails in a
            // headless or embedded context with a message about no tty.
            "--pinentry-mode",
            "loopback",
        ])
        .arg(input)
        .stdin(Stdio::piped())
        .stdout(Stdio::piped())
        .stderr(Stdio::piped())
        .spawn()?;

    {
        let mut stdin = child.stdin.take().expect("stdin was piped");
        // No trailing newline: gpg takes the whole line, and a newline would be part of
        // the passphrase, which would fail against a key that does not contain one.
        stdin.write_all(passphrase.as_bytes())?;
    }

    let output = child.wait_with_output()?;
    Ok((
        output.status.code().unwrap_or(-1),
        output.stdout,
        String::from_utf8_lossy(&output.stderr).to_string(),
    ))
}

#[cfg(test)]
mod tests {
    use super::*;

    fn gpg_present() -> bool {
        gpg_version().is_some()
    }

    /// A message this module produced cannot be tested without gpg, so these skip
    /// rather than fail where it is absent, the same convention the C# suite uses.
    #[test]
    fn a_gpg_message_round_trips() {
        if !gpg_present() {
            eprintln!("skipping: gpg is not on PATH");
            return;
        }

        let passphrase = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
        let plaintext = b"age-encryption.org/v1\npretend inner file";

        // Encrypt with gpg itself, so the fixture is not produced by the code under test.
        let input = temp_path("plain.bin");
        let armored = temp_path("out.asc");
        std::fs::write(&input, plaintext).unwrap();
        let status = Command::new("gpg")
            .args(["--batch", "--yes", "--armor", "--symmetric", "--cipher-algo", "AES256",
                   "--passphrase", passphrase, "--pinentry-mode", "loopback",
                   "--output", armored.to_str().unwrap()])
            .arg(&input)
            .status()
            .unwrap();
        assert!(status.success());

        let text = std::fs::read_to_string(&armored).unwrap();
        let run = gpg_decrypt(text, passphrase.to_string());

        assert!(!run.gpg_missing);
        assert_eq!(run.exit_code, 0, "stderr: {}", run.stderr_text);
        assert_eq!(STANDARD.decode(run.stdout_b64).unwrap(), plaintext);
        assert!(run.version.to_lowercase().contains("gpg"));

        let _ = std::fs::remove_file(&input);
        let _ = std::fs::remove_file(&armored);
    }

    /// The wrong passphrase must be a failed decryption, not a missing program: C#
    /// reports those differently, and conflating them would tell a user their machine
    /// lacks gpg when in fact their shares did not match.
    #[test]
    fn a_wrong_passphrase_is_a_failure_not_a_missing_program() {
        if !gpg_present() {
            eprintln!("skipping: gpg is not on PATH");
            return;
        }

        let input = temp_path("plain2.bin");
        let armored = temp_path("out2.asc");
        std::fs::write(&input, b"anything").unwrap();
        Command::new("gpg")
            .args(["--batch", "--yes", "--armor", "--symmetric", "--cipher-algo", "AES256",
                   "--passphrase", "the-right-one", "--pinentry-mode", "loopback",
                   "--output", armored.to_str().unwrap()])
            .arg(&input)
            .status()
            .unwrap();

        let text = std::fs::read_to_string(&armored).unwrap();
        let run = gpg_decrypt(text, "the-wrong-one".to_string());

        assert!(!run.gpg_missing, "gpg ran; it simply could not open the message");
        assert_ne!(run.exit_code, 0);

        let _ = std::fs::remove_file(&input);
        let _ = std::fs::remove_file(&armored);
    }

    /// Nothing may be left behind: the temporary file holds the ciphertext, and on an
    /// amnesic system litter is merely untidy, but on a persistent one it is a copy of
    /// the backup that nobody asked for.
    #[test]
    fn the_temporary_ciphertext_is_removed() {
        if !gpg_present() {
            eprintln!("skipping: gpg is not on PATH");
            return;
        }

        let _ = gpg_decrypt("-----BEGIN PGP MESSAGE-----\n\nnonsense\n-----END PGP MESSAGE-----\n".to_string(),
                            "irrelevant".to_string());

        assert!(!temp_path("in.asc").exists());
    }
}
