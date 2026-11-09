using Microsoft.AspNetCore.Components.Server;

namespace FWO.Ui.Services
{
    /// <summary>
    /// Configures the Blazor Server circuits of the UI.
    /// </summary>
    public static class CircuitOptionsConfigurator
    {
        /// <summary>
        /// Number of circuits whose state is kept in memory after they have been evicted. Zero keeps none.
        /// </summary>
        public const int kPersistedCircuitsInMemory = 0;

        /// <summary>
        /// Registers the Blazor Server services with the UI specific circuit settings.
        /// </summary>
        /// <param name="services">Service collection of the UI.</param>
        /// <returns>The builder for further Blazor Server configuration.</returns>
        public static IServerSideBlazorBuilder AddFwoServerSideBlazor(this IServiceCollection services)
        {
            return services.AddServerSideBlazor(Configure);
        }

        /// <summary>
        /// Applies the UI specific circuit settings.
        /// </summary>
        /// <remarks>
        /// Since .NET 10 a circuit that is evicted after a longer disconnect (e.g. sleeping laptop, network outage)
        /// is persisted, and the client resumes it instead of reloading the page. The UI is hosted via _Host.cshtml
        /// and the component tag helper, so the persisted state does not contain any root component. The resumed
        /// circuit therefore renders nothing: the old page stays visible, all its event handlers are gone and
        /// navigation only changes the address line until the page is reloaded manually.
        /// Keeping no persisted circuits makes the resume fail, so the client falls back to reloading the page,
        /// which is the behaviour of .NET 9.
        /// This only works as long as no HybridCache service is registered: the framework then persists circuits
        /// via HybridCache, which ignores PersistedCircuitInMemoryMaxRetained, and the freeze returns.
        /// </remarks>
        /// <param name="options">Circuit options to adapt.</param>
        public static void Configure(CircuitOptions options)
        {
            options.PersistedCircuitInMemoryMaxRetained = kPersistedCircuitsInMemory;
        }
    }
}
