const kProvisioningHighlightClass = "provisioning-field-highlight";
const kProvisioningHighlightDurationMs = 1600;

function scrollToElementAndHighlight(htmlObjId) {
    let obj = document.getElementById(htmlObjId);
    if (!obj) {
        return false;
    }
    obj.scrollIntoView({ behavior: "smooth", block: "center" });
    obj.classList.add(kProvisioningHighlightClass);
    setTimeout(() => {
        obj.classList.remove(kProvisioningHighlightClass);
    }, kProvisioningHighlightDurationMs);
    return true;
}
