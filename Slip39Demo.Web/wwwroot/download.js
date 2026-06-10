// Triggers a browser file download from raw bytes provided by Blazor.
// filename: string the browser shows in the Save dialog
// base64:   base64-encoded file bytes (Blazor side encodes via Convert.ToBase64String)
// mime:     MIME type (e.g., "application/zip")
window.spsDownload = function (filename, base64, mime) {
    const bin = atob(base64);
    const bytes = new Uint8Array(bin.length);
    for (let i = 0; i < bin.length; i++) bytes[i] = bin.charCodeAt(i);

    const blob = new Blob([bytes], { type: mime });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = filename;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);

    // Defer revoke so the browser has actually started the download.
    setTimeout(() => URL.revokeObjectURL(url), 1000);
};
