function newWindow (link) {
    window.open(link, "name", "width=1300,height=800");
}

function globalScroll(dotNetHelper) {
    window.addEventListener('scroll', function (event) { dotNetHelper.invokeMethodAsync('InvokeOnGlobalScroll', event.target.id); }, true);
}

function globalResize(dotNetHelper) {
    window.addEventListener('resize', function (event) { dotNetHelper.invokeMethodAsync('InvokeOnGlobalResize'); }, true);
}

function globalClick(dotNetHelper) {
    window.addEventListener("click", function (event) { dotNetHelper.invokeMethodAsync('InvokeOnGlobalClick', event.target.id); }, true);
}

function isChild(childId, parentId) {
    const parent = document.getElementById(parentId);    
    const child = document.getElementById(childId);
    if (parent == null || child == null)
        return false;
    return parent.contains(child);
}

function setProperty (element, property, value) {
    element[property] = value;
}