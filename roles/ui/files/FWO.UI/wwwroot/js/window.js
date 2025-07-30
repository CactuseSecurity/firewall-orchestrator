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

function observeNavbarHeight(dotNetHelper) {
    const selector = ".navbar";
    const navbar = document.querySelector(selector);
    if (!navbar) {
        console.error("Navbar not found for selector:", selector);
        return;
    }

    const measure = () => Math.round(navbar.getBoundingClientRect().height);
    const send = () => dotNetHelper.invokeMethodAsync("InvokeNavbarHeightChanged", measure());

    // Send initial height
    send();

    // Observe changes to the navbar height and send the update
    const resize_observer = new ResizeObserver(send);
    resize_observer.observe(navbar);
};

function isChild(childId, parentId) {
    const parent = document.getElementById(parentId);    
    const child = document.getElementById(childId);
    if (parent == null || child == null)
        return false;
    return parent.contains(child);
}

function setProperty(element, property, value) {
    if (element != null) {
        element[property] = value;
    }
}
