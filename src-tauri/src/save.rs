//! Native save dialog. Replaces the browser blob download, which in a WebView writes
//! to a downloads directory the user did not choose.

use base64::{engine::general_purpose::STANDARD, Engine};
use std::path::PathBuf;
use tauri::AppHandle;
use tauri_plugin_dialog::DialogExt;

/// Where the picker should open. The Photino shell this replaces pre-filled the whole
/// suggested path under the user's home directory, and dropping that would be a small
/// regression in the one scenario that matters: an AppImage launched from a USB stick
/// has that stick as its working directory, so a picker with no directory set can open
/// somewhere mounted read-only and make the user's first attempt to save fail.
///
/// Returns None when HOME is unset or is not a directory, in which case the picker keeps
/// its own default rather than being pointed at somewhere that does not exist.
fn preferred_directory() -> Option<PathBuf> {
    let home = PathBuf::from(std::env::var_os("HOME")?);
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

    if let Some(directory) = preferred_directory() {
        dialog = dialog.set_directory(directory);
    }

    let chosen = dialog.blocking_save_file();

    let Some(path) = chosen else {
        return Ok(None);
    };

    let path = path
        .into_path()
        .map_err(|e| format!("the chosen location is not a path this program can write: {e}"))?;

    std::fs::write(&path, &bytes).map_err(|e| format!("could not write {}: {e}", path.display()))?;

    Ok(Some(path.display().to_string()))
}

#[cfg(test)]
mod tests {
    use super::*;

    // One test rather than three, and deliberately so: these mutate HOME, and cargo runs
    // tests on parallel threads, so separate cases would race each other over the same
    // process-wide variable and fail intermittently. Nothing else in this crate reads
    // HOME.
    #[test]
    fn the_picker_opens_at_home_only_when_home_is_a_real_directory() {
        let original = std::env::var_os("HOME");
        let real = std::env::temp_dir();

        std::env::set_var("HOME", &real);
        assert_eq!(preferred_directory(), Some(real));

        // A HOME that points at nothing must not be handed to the picker: better its own
        // default than a directory that does not exist.
        std::env::set_var("HOME", "/definitely/not/a/directory/here");
        assert_eq!(preferred_directory(), None);

        std::env::remove_var("HOME");
        assert_eq!(preferred_directory(), None);

        match original {
            Some(value) => std::env::set_var("HOME", value),
            None => std::env::remove_var("HOME"),
        }
    }
}
