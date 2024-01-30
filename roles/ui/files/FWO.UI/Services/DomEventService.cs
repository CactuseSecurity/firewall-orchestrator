using Microsoft.JSInterop;

namespace FWO.Ui.Services
{
	public class DomEventService
	{
		public event Action<string>? OnGlobalScroll;
		public event Action<string>? OnGlobalClick;
		public event Action? OnGlobalResize;

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

		public async Task Initialize(IJSRuntime runtime)
		{
			if (!Initialized)
			{
				await runtime.InvokeVoidAsync("globalScroll", DotNetObjectReference.Create(this));
				await runtime.InvokeVoidAsync("globalResize", DotNetObjectReference.Create(this));
				await runtime.InvokeVoidAsync("globalClick", DotNetObjectReference.Create(this));
				Initialized = true;
			}
		}
	}
}
