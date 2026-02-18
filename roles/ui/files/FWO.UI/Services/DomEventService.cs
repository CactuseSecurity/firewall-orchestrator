using FWO.Logging;
using Microsoft.JSInterop;

namespace FWO.Ui.Services
{
    public class DomEventService : IAsyncDisposable
    {
        public delegate void OnDomEvent(string elementId);

        public event OnDomEvent? OnGlobalScroll;
        public event OnDomEvent? OnGlobalClick;
        public event OnDomEvent? OnGlobalFocus;
        public event OnDomEvent? OnGlobalResize;

        private Action<int>? _navbarHeightSubscribers;
        private int? _lastNavbarHeight;

        public event Action<int>? OnNavbarHeightChanged
        {
            add
            {
                _navbarHeightSubscribers += value;
                // Fire immediately (once) if we already have a cached value
                if (_lastNavbarHeight.HasValue)
                {
                    value?.Invoke(_lastNavbarHeight.Value);
                }
            }
            remove => _navbarHeightSubscribers -= value;
        }

        private DotNetObjectReference<DomEventService>? _dotNetRef;
        private IJSRuntime? _runtime;

        public bool Initialized { get; private set; }

        public async Task Initialize(IJSRuntime runtime)
        {
            if (!Initialized)
            {
                try
                {
                    _runtime = runtime;
                    _dotNetRef ??= DotNetObjectReference.Create(this);
                    await runtime.InvokeVoidAsync("initializeEventHandlers", _dotNetRef);
                    Initialized = true;
                }
                catch (Exception exception)
                {
                    Log.WriteError("DomEventService", $"Initialization failure", exception);
                }
            }
        }

        [JSInvokable]
        public void InvokeOnGlobalScroll(string elementId)
        {
            OnGlobalScroll?.Invoke(elementId);
        }

        [JSInvokable]
        public void InvokeOnGlobalResize(string elementId)
        {
            OnGlobalResize?.Invoke(elementId);
        }

        [JSInvokable]
        public void InvokeOnGlobalClick(string elementId)
        {
            OnGlobalClick?.Invoke(elementId);
        }

        [JSInvokable]
        public void InvokeOnGlobalFocus(string elementId)
        {
            OnGlobalFocus?.Invoke(elementId);
        }

        [JSInvokable]
        public void InvokeNavbarHeightChanged(int height)
        {
            _lastNavbarHeight = height;
            _navbarHeightSubscribers?.Invoke(height);
        }

        protected virtual async ValueTask DisposeAsyncCore()
        {
            try
            {
                if (Initialized && _runtime is not null)
                    await _runtime.InvokeVoidAsync("disposeEventHandlers");
            }
            catch { /* ignore */ }

            _dotNetRef?.Dispose();
            _dotNetRef = null;
            Initialized = false;
        }

        public async ValueTask DisposeAsync()
        {
            await DisposeAsyncCore().ConfigureAwait(false);
            GC.SuppressFinalize(this);
        }
    }
}
