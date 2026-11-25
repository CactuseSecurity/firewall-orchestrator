
// longest smooth scroll we wait for before expanding the row details (no scrollend if nothing had to be scrolled)
const kRsbScrollEndFallbackMs = 1000;
// only the latest scroll may expand its row: an older expansion would render during the latest smooth scroll and cancel it
let rsbScrollGeneration = 0;

// smooth: only when no further render follows (object already loaded) - a render during a smooth scroll cancels it,
// so the scrolls retried while and after lazily fetching the sidebar objects are instant
function scrollIntoRSBView(htmlObjId, smooth) {
    let obj =  document.getElementById(htmlObjId)?.parentElement?.parentElement; // gets the tr element containing the obj
    if (!obj)
    {
        return false;
    }
    let generation = ++rsbScrollGeneration;
    if (smooth)
    {
        obj.scrollIntoView({ behavior: "smooth", block: "center" });
        // Expand row only after scrolling: rendering the row details during a smooth scroll cancels it
        runAfterScrollEnd(() => { if (generation === rsbScrollGeneration) expandRsbRow(obj); });
    }
    else
    {
        obj.scrollIntoView({ behavior: "instant", block: "center" });
        expandRsbRow(obj);
    }
    // Highlight the row
    obj.style.transition = "background-color 500ms linear";
    obj.style.backgroundColor = "#a4d7f5";
    // Remove highlight after 800ms
    setTimeout(() => obj.style.backgroundColor = "", 800);
    return obj.offsetParent !== null; // element visible?
}

function expandRsbRow(obj) {
    let toggleLink = obj.previousElementSibling?.querySelector("a");
    if (toggleLink?.getAttribute("aria-expanded") == 'false')
    {
        toggleLink.click();
    }
}

function runAfterScrollEnd(action) {
    let done = false;
    let runOnce = () => {
        if (!done)
        {
            done = true;
            document.removeEventListener("scrollend", runOnce, true);
            action();
        }
    };
    // scrollend does not bubble, a capturing listener also gets it from the scrolled sidebar container
    document.addEventListener("scrollend", runOnce, true);
    setTimeout(runOnce, kRsbScrollEndFallbackMs);
}

function getCurrentUrl() {
  return window.location.href;
}

function removeUrlFragment() {
  history.replaceState(null, document.title, window.location.pathname + window.location.search);
}