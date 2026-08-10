//! Native save dialog. Replaces the browser blob download, which in a WebView writes
//! to a downloads directory the user did not choose.

use base64::{engine::general_purpose::STANDARD, Engine};
use std::io::Write;
use std::path::PathBuf;
use tauri::AppHandle;
use tauri_plugin_dialog::DialogExt;

/// Where the picker should open. The Photino shell this replaces pre-filled the whole
/// suggested path under the user's home directory, and dropping that would be a small
/// regression in the one scenario that matters: an AppImage launched from a USB stick
/// has that stick as its working directory, so a picker with no directory set can open
/// somewhere mounted read-only and make the user's first attempt to save fail.
///
/// Returns None when the value is absent or is not a directory, in which case the picker
/// keeps its own default rather than being pointed at somewhere that does not exist.
///
/// HOME is passed in rather than read here, and that is not a style preference. The version
/// that read the variable itself could only be tested by setting it, and setting a variable
/// mutates the process environment, which libc shares with every thread. cargo runs tests
/// on parallel threads and one of them spawns a real subprocess, so the test and that spawn
/// were a live data race on `environ`: undefined behaviour, in the suite that guards the
/// encrypt path. Taking the value as an argument removes the reason to mutate anything.
fn preferred_directory(home: Option<std::ffi::OsString>) -> Option<PathBuf> {
    let home = PathBuf::from(home?);
    home.is_dir().then_some(home)
}

/// Returns the path written, or None if the user cancelled. Cancelling is not an
/// error: the caller asked to save and the user said no.
#[tauri::command]
pub async fn save_file(
    app: AppHandle,
    suggested_name: String,
    bytes_b64: String,
) -> Result<Option<String>, String> {
    let bytes = STANDARD
        .decode(bytes_b64)
        .map_err(|e| format!("the frontend sent something that is not base64: {e}"))?;

    let mut dialog = app
        .dialog()
        .file()
        .set_file_name(&suggested_name)
        // The Photino shell titled this "Save file". Naming what is being saved is more
        // use than naming the verb, on a screen where the user is several steps into a
        // process they will perform once.
        .set_title("Save the backup bundle");

    if let Some(directory) = preferred_directory(std::env::var_os("HOME")) {
        dialog = dialog.set_directory(directory);
    }

    let chosen = dialog.blocking_save_file();

    let Some(path) = chosen else {
        return Ok(None);
    };

    let path = path
        .into_path()
        .map_err(|e| format!("the chosen location is not a path this program can write: {e}"))?;

    write_whole_file(&path, &bytes)?;

    Ok(Some(path.display().to_string()))
}

/// Writes the file so that the chosen name never holds a partial one.
///
/// std::fs::write would be one line, and on a full disk it leaves a truncated file sitting
/// at exactly the name the user chose. For a backup that is the worst available failure:
/// the file looks like the backup, is named like the backup, and does not recover. Nothing
/// about it says so until someone needs it, which may be years later.
///
/// So the bytes go to a sibling temporary file first and are renamed into place only once
/// they are all written and flushed to the device. Rename within a directory is atomic, so
/// the destination either does not exist or is the complete file. The temporary is removed
/// if anything fails, and it is a sibling rather than in the system temp directory because
/// rename cannot be atomic across filesystems, and the whole point of this dialog is
/// choosing somewhere else: a USB stick, usually.
///
/// sync_all before the rename is what makes this hold against the stick being pulled out
/// rather than only against a crash: without it the rename can reach the device before the
/// data does.
fn write_whole_file(path: &std::path::Path, bytes: &[u8]) -> Result<(), String> {
    let partial = partial_path_for(path);

    let write = || -> std::io::Result<()> {
        let mut file = std::fs::File::create(&partial)?;
        file.write_all(bytes)?;
        file.sync_all()
    };

    if let Err(e) = write() {
        let _ = std::fs::remove_file(&partial);
        return Err(format!("could not write {}: {e}", partial.display()));
    }

    std::fs::rename(&partial, path).map_err(|e| {
        let _ = std::fs::remove_file(&partial);
        format!(
            "wrote {} but could not move it into place as {}: {e}",
            partial.display(),
            path.display()
        )
    })
}

/// The sibling name the bytes are written under before being renamed into place. Leading
/// dot and explicit suffix so that if it is ever left behind by a kill signal, it is
/// obvious what it is and obvious that it is not the backup.
fn partial_path_for(path: &std::path::Path) -> PathBuf {
    let name = path
        .file_name()
        .map(|n| n.to_string_lossy().into_owned())
        .unwrap_or_else(|| "backup".to_string());

    path.with_file_name(format!(".{name}.partial"))
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::path::Path;

    // Three cases rather than one, and no environment mutation at all. The version this
    // replaces set HOME and restored it afterwards, which was a data race against the test
    // that spawns a real subprocess on another thread: libc's environ is shared by every
    // thread, and cargo runs tests in parallel. Passing the value in removed the reason to
    // touch the environment, and with the race gone there is no reason to crowd the cases
    // into one test either.
    #[test]
    fn the_picker_opens_at_home_when_home_is_a_real_directory() {
        let real = std::env::temp_dir();

        assert_eq!(preferred_directory(Some(real.clone().into_os_string())), Some(real));
    }

    // A HOME that points at nothing must not be handed to the picker: better its own
    // default than a directory that does not exist.
    #[test]
    fn a_home_that_is_not_a_directory_is_ignored() {
        let missing = std::ffi::OsString::from("/definitely/not/a/directory/here");

        assert_eq!(preferred_directory(Some(missing)), None);
    }

    #[test]
    fn an_absent_home_is_ignored() {
        assert_eq!(preferred_directory(None), None);
    }

    // The other half of a contract that spans two languages. C# encodes the bundle with
    // Convert.ToBase64String and this engine decodes it, and until now that agreement was
    // established only by reading the base64 crate's source. The literal below was produced
    // by Convert.ToBase64String over exactly these bytes, so a change of alphabet or padding
    // on either side fails a test rather than corrupting a backup.
    //
    // The bytes are deliberately not text: 0x00, 0xFF and 0x80 are the ones a stray UTF-8
    // conversion anywhere on the path would mangle, and the real payload is a zip, which is
    // full of them. The first four are a zip's own magic number.
    #[test]
    fn decodes_exactly_what_csharp_encodes() {
        let bytes: &[u8] = &[0x50, 0x4B, 0x03, 0x04, 0x00, 0xFF, 0x80, 0x7F, 0x0A, 0x0D];
        let from_csharp = "UEsDBAD/gH8KDQ==";

        assert_eq!(STANDARD.decode(from_csharp).unwrap(), bytes);
    }

    // A partial write must never occupy the name the user chose, so it is written beside it
    // under a name that says what it is.
    #[test]
    fn the_partial_file_is_a_hidden_sibling_of_the_chosen_name() {
        let partial = partial_path_for(Path::new("/media/usb/backup-2026.zip"));

        assert_eq!(partial, PathBuf::from("/media/usb/.backup-2026.zip.partial"));
        assert_eq!(partial.parent(), Path::new("/media/usb/backup-2026.zip").parent());
    }

    // The whole point of the sibling temporary: after a successful write the destination
    // holds the complete file and no partial is left behind.
    #[test]
    fn writing_leaves_the_complete_file_and_no_partial() {
        let dir = std::env::temp_dir().join("slip39-save-whole");
        let _ = std::fs::remove_dir_all(&dir);
        std::fs::create_dir_all(&dir).unwrap();
        let target = dir.join("bundle.zip");
        let bytes: &[u8] = &[0x50, 0x4B, 0x03, 0x04, 0x00, 0xFF];

        write_whole_file(&target, bytes).unwrap();

        assert_eq!(std::fs::read(&target).unwrap(), bytes);
        assert!(!partial_path_for(&target).exists(), "the partial should be gone");
    }

    // Failure must not leave anything at the chosen name at all, which is the difference
    // between "no backup" and "a backup that does not open".
    #[test]
    fn a_write_that_cannot_start_leaves_the_destination_untouched() {
        let target = Path::new("/definitely/not/a/directory/here/bundle.zip");

        let result = write_whole_file(target, b"payload");

        assert!(result.is_err());
        assert!(!target.exists());
    }
}
