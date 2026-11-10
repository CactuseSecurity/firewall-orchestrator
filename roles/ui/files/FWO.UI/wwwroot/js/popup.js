// Makes the dialogs of PopUp.razor movable (dragging the header) and resizable (dragging the handle
// in the lower right corner). PopUp.razor only renders the classes popup-movable / popup-resizable and
// the handle, a single delegated listener on the document handles all popups, so no handler has to be
// attached per popup. Both gestures use pointer capture, which behaves the same in all browsers.
//
// The dialog is centered with automatic margins (see PopUp.razor.css). Before the first move or
// resize it is "pinned": its current position is written as explicit top/left so that the dialog
// follows the pointer instead of moving or growing around its center.
// Never use a CSS transform for the offset, it would shift the fixed dropdown menus inside.

const kPopupMinVisiblePx = 48;
const kPopupDialogSelector = ".popup-dialog";
const kPopupMovableClass = "popup-movable";
const kPopupDraggingClass = "popup-dragging";
const kPopupHeaderSelector = ".modal-header";
const kPopupResizeHandleSelector = ".popup-resize-handle";
const kPopupInteractiveSelector = "button, a, input, textarea, select, [role='button']";

function pinPopupDialog(dialog) {
    if (dialog.dataset.fwoPinned === "true") {
        return;
    }
    const rect = dialog.getBoundingClientRect();
    dialog.style.margin = "0";
    dialog.style.right = "auto";
    dialog.style.bottom = "auto";
    dialog.style.left = `${rect.left}px`;
    dialog.style.top = `${rect.top}px`;
    dialog.style.width = `${rect.width}px`;
    dialog.dataset.fwoPinned = "true";
}

function clampPopupPosition(left, top, dialog) {
    const maxLeft = window.innerWidth - kPopupMinVisiblePx;
    const minLeft = kPopupMinVisiblePx - dialog.offsetWidth;
    const maxTop = window.innerHeight - kPopupMinVisiblePx;
    return {
        left: Math.min(Math.max(left, minLeft), maxLeft),
        top: Math.min(Math.max(top, 0), maxTop)
    };
}

// Follows the pointer from downEvent until it is released and reports every move to onMove
// with the distance from the start point.
function trackPopupPointer(dialog, captureElement, downEvent, onMove) {
    const startX = downEvent.clientX;
    const startY = downEvent.clientY;
    captureElement.setPointerCapture(downEvent.pointerId);
    dialog.classList.add(kPopupDraggingClass);

    const onPointerMove = (moveEvent) => onMove(moveEvent.clientX - startX, moveEvent.clientY - startY);
    const onPointerUp = () => {
        captureElement.removeEventListener("pointermove", onPointerMove);
        captureElement.removeEventListener("pointerup", onPointerUp);
        captureElement.removeEventListener("pointercancel", onPointerUp);
        dialog.classList.remove(kPopupDraggingClass);
    };
    captureElement.addEventListener("pointermove", onPointerMove);
    captureElement.addEventListener("pointerup", onPointerUp);
    captureElement.addEventListener("pointercancel", onPointerUp);
    downEvent.preventDefault();
}

function startPopupMove(dialog, header, downEvent) {
    pinPopupDialog(dialog);
    const startLeft = dialog.offsetLeft;
    const startTop = dialog.offsetTop;
    trackPopupPointer(dialog, header, downEvent, (deltaX, deltaY) => {
        const position = clampPopupPosition(startLeft + deltaX, startTop + deltaY, dialog);
        dialog.style.left = `${position.left}px`;
        dialog.style.top = `${position.top}px`;
    });
}

// The minimum size is enforced by the min-width / min-height of .popup-resizable, the maximum keeps
// the lower right corner inside the viewport.
function startPopupResize(dialog, handle, downEvent) {
    pinPopupDialog(dialog);
    const startWidth = dialog.offsetWidth;
    const startHeight = dialog.offsetHeight;
    trackPopupPointer(dialog, handle, downEvent, (deltaX, deltaY) => {
        const maxWidth = window.innerWidth - dialog.offsetLeft;
        const maxHeight = window.innerHeight - dialog.offsetTop;
        dialog.style.width = `${Math.min(startWidth + deltaX, maxWidth)}px`;
        dialog.style.height = `${Math.min(startHeight + deltaY, maxHeight)}px`;
    });
}

function onPopupPointerDown(event) {
    if (event.button !== 0 || !(event.target instanceof Element)) {
        return;
    }

    const handle = event.target.closest(kPopupResizeHandleSelector);
    const resizedDialog = handle?.closest(kPopupDialogSelector);
    if (resizedDialog) {
        startPopupResize(resizedDialog, handle, event);
        return;
    }

    const header = event.target.closest(kPopupHeaderSelector);
    const movedDialog = header?.closest(kPopupDialogSelector);
    if (!movedDialog || !movedDialog.classList.contains(kPopupMovableClass) || event.target.closest(kPopupInteractiveSelector)) {
        return;
    }
    startPopupMove(movedDialog, header, event);
}

document.addEventListener("pointerdown", onPopupPointerDown);
