// Pulls the content from a .NET stream reference instead of receiving it as one argument.
// A byte[] argument is base64 encoded into the interop message, so a large report export would
// otherwise be inflated by a third on top of the copy that is already held on the server.
async function DownloadFileFromStream(filename, contentType, streamReference) {
    const arrayBuffer = await streamReference.arrayBuffer();
    const blob = new Blob([arrayBuffer], { type: contentType });
    const exportUrl = URL.createObjectURL(blob);

    const a = document.createElement("a");
    document.body.appendChild(a);
    a.href = exportUrl;
    a.download = filename;
    a.target = "_blank";
    a.click();

    URL.revokeObjectURL(exportUrl);
    a.remove();
}

function DownloadFile(filename, contentType, data) {
    // Create the URL
    const file = new File([data], filename, { type: contentType });
    const exportUrl = URL.createObjectURL(file);

    // Create the <a> element and click on it
    const a = document.createElement("a");
    document.body.appendChild(a);
    a.href = exportUrl;
    a.download = filename;
    a.target = "_blank";
    a.click();

    URL.revokeObjectURL(exportUrl);
    a.remove();
}