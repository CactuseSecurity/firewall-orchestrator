function newWindow (link) {
    window.open(link, "name", "width=1200,height=800");
}

function globalScroll(dotNetHelper) {
    window.addEventListener('scroll', function (event) { dotNetHelper.invokeMethodAsync('InvokeOnGlobalScroll', event.target.id); }, true)
}

function globalResize (dotNetHelper) {
    window.addEventListener('resize', function(event) { dotNetHelper.invokeMethodAsync('InvokeOnGlobalResize'); }, true)
}


function setProperty (element, property, value) {
    element[property] = value;
}