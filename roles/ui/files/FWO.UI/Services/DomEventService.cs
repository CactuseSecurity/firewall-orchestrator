using Microsoft.JSInterop;

namespace FWO.Ui.Services
{
	public class DomEventService
	{
		public event Action<string>? OnGlobalScroll;
		public event Action? OnGlobalResize;

		public bool Initialized { get; private set; } = false;

		[JSInvokable]
		public void InvokeOnGlobalScroll(string elementId)
		{
			OnGlobalScroll?.Invoke(elementId);
		}

		[JSInvokable]
		public void InvokeOnGlobalResize()
		{
			OnGlobalResize?.Invoke();
		}

		public async Task Initialize(IJSRuntime runtime)
		{
			if (!Initialized)
			{
				await runtime.InvokeVoidAsync("globalScroll", DotNetObjectReference.Create(this));
				await runtime.InvokeVoidAsync("globalResize", DotNetObjectReference.Create(this));
				Initialized = true;
			}
		}
	}
}
