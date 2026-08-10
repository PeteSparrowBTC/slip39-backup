//! Native save dialog. Replaces the browser blob download, which in a WebView writes
//! to a downloads directory the user did not choose.

use base64::{engine::general_purpose::STANDARD, Engine};
use tauri::AppHandle;
use tauri_plugin_dialog::DialogExt;

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

    let chosen = app
        .dialog()
        .file()
        .set_file_name(&suggested_name)
        .blocking_save_file();

    let Some(path) = chosen else {
        return Ok(None);
    };

    let path = path
        .into_path()
        .map_err(|e| format!("the chosen location is not a path this program can write: {e}"))?;

    std::fs::write(&path, &bytes).map_err(|e| format!("could not write {}: {e}", path.display()))?;

    Ok(Some(path.display().to_string()))
}
