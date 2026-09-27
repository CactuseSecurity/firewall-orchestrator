function scrollToElementAndHighlight(htmlObjId) {
    let obj = document.getElementById(htmlObjId);
    if (!obj) {
        return false;
    }
    obj.scrollIntoView({ behavior: "smooth", block: "center" });
    obj.style.transition = "background-color 500ms linear, box-shadow 500ms linear";
    obj.style.backgroundColor = "#fff3cd";
    obj.style.boxShadow = "0 0 0 2px #ffc107";
    setTimeout(() => {
        obj.style.backgroundColor = "";
        obj.style.boxShadow = "";
    }, 1600);
    return true;
}
