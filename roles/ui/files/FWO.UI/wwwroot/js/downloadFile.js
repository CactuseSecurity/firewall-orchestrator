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