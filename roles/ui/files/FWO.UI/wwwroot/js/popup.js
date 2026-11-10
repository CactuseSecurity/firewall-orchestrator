// Makes the dialogs of PopUp.razor movable (dragging the header) and resizable (CSS resize handle).
// PopUp.razor only sets the classes popup-movable / popup-resizable, a single delegated listener on
// the document handles all popups, so no handler has to be attached per popup.
//
// The dialog is centered with automatic margins (see PopUp.razor.css). Before the first move or
// resize it is "pinned": its current position is written as explicit top/left so that the
// resize handle and the drag follow the pointer instead of growing the dialog around its center.
// Never use a CSS transform for the offset, it would shift the fixed dropdown menus inside.

const kPopupMinVisiblePx = 48;
const kPopupDialogSelector = ".popup-dialog";
const kPopupMovableClass = "popup-movable";
const kPopupResizableClass = "popup-resizable";
const kPopupDraggingClass = "popup-dragging";
const kPopupHeaderSelector = ".modal-header";
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

function startPopupDrag(dialog, header, downEvent) {
    pinPopupDialog(dialog);
    const offsetX = downEvent.clientX - dialog.offsetLeft;
    const offsetY = downEvent.clientY - dialog.offsetTop;
    header.setPointerCapture(downEvent.pointerId);
    dialog.classList.add(kPopupDraggingClass);

    const onMove = (moveEvent) => {
        const position = clampPopupPosition(moveEvent.clientX - offsetX, moveEvent.clientY - offsetY, dialog);
        dialog.style.left = `${position.left}px`;
        dialog.style.top = `${position.top}px`;
    };
    const onUp = () => {
        header.removeEventListener("pointermove", onMove);
        header.removeEventListener("pointerup", onUp);
        header.removeEventListener("pointercancel", onUp);
        dialog.classList.remove(kPopupDraggingClass);
    };
    header.addEventListener("pointermove", onMove);
    header.addEventListener("pointerup", onUp);
    header.addEventListener("pointercancel", onUp);
    downEvent.preventDefault();
}

function onPopupPointerDown(event) {
    if (event.button !== 0 || !(event.target instanceof Element)) {
        return;
    }

    // A pointer down on the resize handle targets the dialog itself (its children cover the rest),
    // so pin the dialog before the browser starts resizing.
    if (event.target.classList.contains(kPopupResizableClass)) {
        pinPopupDialog(event.target);
        return;
    }

    const header = event.target.closest(kPopupHeaderSelector);
    const dialog = header?.closest(kPopupDialogSelector);
    if (!dialog || !dialog.classList.contains(kPopupMovableClass) || event.target.closest(kPopupInteractiveSelector)) {
        return;
    }
    startPopupDrag(dialog, header, event);
}

document.addEventListener("pointerdown", onPopupPointerDown);
