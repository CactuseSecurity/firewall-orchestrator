const kDeviceSpecificHighlightClass = "device-specific-field-highlight";
const kDeviceSpecificHighlightDurationMs = 1600;

function scrollToElementAndHighlight(htmlObjId) {
    let obj = document.getElementById(htmlObjId);
    if (!obj) {
        return false;
    }
    obj.scrollIntoView({ behavior: "smooth", block: "center" });
    obj.classList.add(kDeviceSpecificHighlightClass);
    setTimeout(() => {
        obj.classList.remove(kDeviceSpecificHighlightClass);
    }, kDeviceSpecificHighlightDurationMs);
    return true;
}
