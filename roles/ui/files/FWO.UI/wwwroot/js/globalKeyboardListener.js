window.globalKeyboardListener = {
    init: function(dotnetHelper) {
        document.addEventListener('keydown', function(e) {
            dotnetHelper.invokeMethodAsync('OnKeyDown', {
                Key: e.key,
                Code: e.code,
                AltKey: e.altKey,
                CtrlKey: e.ctrlKey,
                ShiftKey: e.shiftKey,
                MetaKey: e.metaKey,
                Location: e.location,
                Repeat: e.repeat
            });
        });
        document.addEventListener('keyup', function(e) {
            dotnetHelper.invokeMethodAsync('OnKeyUp', {
                Key: e.key,
                Code: e.code,
                AltKey: e.altKey,
                CtrlKey: e.ctrlKey,
                ShiftKey: e.shiftKey,
                MetaKey: e.metaKey,
                Location: e.location,
                Repeat: e.repeat
            });
        });
    }
};
