using Microsoft.JSInterop;
using Newtonsoft.Json.Linq;
using System.Diagnostics;

namespace FWO.Ui.Services
{
	public class DomEventService
	{
        public event Action<string>? OnGlobalScroll;
		public event Action<string>? OnGlobalClick;
		public event Action? OnGlobalResize;

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

        public bool Initialized { get; private set; } = false;

		[JSInvokable]
		public void InvokeOnGlobalScroll(string elementId)
		{
			OnGlobalScroll?.Invoke(elementId ?? "");
		}

		[JSInvokable]
		public void InvokeOnGlobalResize()
		{
			OnGlobalResize?.Invoke();
		}

		[JSInvokable]
		public void InvokeOnGlobalClick(string elementId)
		{
			OnGlobalClick?.Invoke(elementId ?? "");
		}

        [JSInvokable]
        public void InvokeNavbarHeightChanged(int height)
        {
            _lastNavbarHeight = height;
            _navbarHeightSubscribers?.Invoke(height);
        }

        public async Task Initialize(IJSRuntime runtime)
		{
			if (!Initialized)
			{
                try
                {
                    await runtime.InvokeVoidAsync("globalScroll", DotNetObjectReference.Create(this));
                    await runtime.InvokeVoidAsync("globalResize", DotNetObjectReference.Create(this));
                    await runtime.InvokeVoidAsync("globalClick", DotNetObjectReference.Create(this));
                    await runtime.InvokeVoidAsync("observeNavbarHeight", DotNetObjectReference.Create(this));
                    Initialized = true;
                }
                catch(Exception ex)
                {
                    Debug.WriteLine(ex.ToString());
                }				
			}
		}
	}
}
