window.globalKeyboardListener = {
    dotNetRef: null,
    
    onKeyDown: function (e) {
        if (window.globalKeyboardListener.dotNetRef) {
            window.globalKeyboardListener.dotNetRef.invokeMethodAsync('OnKeyDown', {
                Key: e.key,
                Code: e.code,
                AltKey: e.altKey,
                CtrlKey: e.ctrlKey,
                ShiftKey: e.shiftKey,
                MetaKey: e.metaKey,
                Location: e.location,
                Repeat: e.repeat
            });
        }
    },

    onKeyUp: function (e) {
        if (window.globalKeyboardListener.dotNetRef) {
            window.globalKeyboardListener.dotNetRef.invokeMethodAsync('OnKeyUp', {
                Key: e.key,
                Code: e.code,
                AltKey: e.altKey,
                CtrlKey: e.ctrlKey,
                ShiftKey: e.shiftKey,
                MetaKey: e.metaKey,
                Location: e.location,
                Repeat: e.repeat
            });
        }
    },

    init: function (dotNetHelper) {
        this.dotNetRef = dotNetHelper;
        document.addEventListener('keydown', this.onKeyDown);
        document.addEventListener('keyup', this.onKeyUp);
    },

    dispose: function () {
        document.removeEventListener("keydown", this.onKeyDown);
        document.removeEventListener("keyup", this.onKeyUp);
        this.dotNetRef = null; // Clear reference to prevent memory leaks
    }
};
