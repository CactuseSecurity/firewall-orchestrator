function newWindow (link) {
    window.open(link, "name", "width=1300,height=800");
}

let onScroll = null;
let onResize = null;
let onClick = null;
let onFocusIn = null;
let navbarObserver = null;

const useCapture = true;

function initializeEventHandlers(dotNetHelper) {
    // Dispose old event handlers, if existent
    disposeEventHandlers();

    onScroll = (event) => {
        dotNetHelper.invokeMethodAsync("InvokeOnGlobalScroll", resolveTargetId(event.target));
    };

    onResize = (_event) => {
        dotNetHelper.invokeMethodAsync("InvokeOnGlobalResize", resolveTargetId(event.target));
    };

    onClick = (event) => {
        dotNetHelper.invokeMethodAsync("InvokeOnGlobalClick", resolveTargetId(event.target));
    };

    onFocusIn = (event) => {
        dotNetHelper.invokeMethodAsync("InvokeOnGlobalFocus", resolveTargetId(event.target));
    };

    window.addEventListener("scroll", onScroll, useCapture);
    window.addEventListener("resize", onResize, useCapture);
    window.addEventListener("click", onClick, useCapture);
    window.addEventListener("focusin", onFocusIn, useCapture);

    observeNavbarHeight(dotNetHelper);
}

function observeNavbarHeight(dotNetHelper) {
    const navbar = document.getElementById("navbar");

    if (!navbar) {
        console.warn("Navbar height observation: Navbar not found");
        return;
    }

    const send = () => dotNetHelper.invokeMethodAsync(
        "InvokeNavbarHeightChanged",
        Math.round(navbar.getBoundingClientRect().height)
    );

    // Initial send of the navbar height
    send();

    // Observe changes to the navbar height and send the update
    navbarObserver = new ResizeObserver(send);
    navbarObserver.observe(navbar);
}

function disposeEventHandlers() {
    if (onScroll) window.removeEventListener("scroll", onScroll, useCapture);
    if (onResize) window.removeEventListener("resize", onResize, useCapture);
    if (onClick) window.removeEventListener("click", onClick, useCapture);
    if (onFocusIn) window.removeEventListener("focusin", onFocusIn, useCapture);

    navbarObserver?.disconnect();
    onScroll = onResize = onClick = onFocusIn = navbarObserver = null;
}

function resolveTargetId(target) {
    if (!(target instanceof Element)) {
        return "";
    }
    const firstElementWithId = target.closest("[id]");
    return firstElementWithId?.id ?? "";
}

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

function getElementPosition(elementId) {
    const element = document.getElementById(elementId);
    if (element == null) {
        return [0, 0];
    }

    const rect = element.getBoundingClientRect();
    return [Math.round(rect.left), Math.round(rect.bottom)];
}
