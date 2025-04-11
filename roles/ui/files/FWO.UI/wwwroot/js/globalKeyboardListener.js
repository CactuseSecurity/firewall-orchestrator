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

    onBlur: function(){
        if (window.globalKeyboardListener.dotNetRef) {
            window.globalKeyboardListener.dotNetRef.invokeMethodAsync('OnBlur') 
        }
    },

    init: function (dotNetHelper) {
        this.dotNetRef = dotNetHelper;
        document.addEventListener('keydown', this.onKeyDown);
        document.addEventListener('keyup', this.onKeyUp);
        window.addEventListener("blur", this.onBlur);

    },

    dispose: function () {
        document.removeEventListener("keydown", this.onKeyDown);
        document.removeEventListener("keyup", this.onKeyUp);
        window.removeEventListener("blur", this.onBlur);

        this.dotNetRef = null; // Clear reference to prevent memory leaks
    }
};
