using FWO.Ui.Services;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    internal class CircuitOptionsConfiguratorTest
    {
        private const string kCircuitSecret = "circuit-secret";

        [Test]
        public void Configure_KeepsNoPersistedCircuits()
        {
            CircuitOptions options = new();

            CircuitOptionsConfigurator.Configure(options);

            Assert.That(options.PersistedCircuitInMemoryMaxRetained, Is.EqualTo(0));
        }

        [Test]
        public void AddFwoServerSideBlazor_RegistersCircuitConfiguration()
        {
            ServiceCollection services = new();

            IServerSideBlazorBuilder builder = services.AddFwoServerSideBlazor();

            // resolving the options would need a full web host environment, so the registered
            // configuration instances are applied directly
            CircuitOptions options = new();
            foreach (IConfigureOptions<CircuitOptions> configureOptions in services
                .Where(descriptor => descriptor.ServiceType == typeof(IConfigureOptions<CircuitOptions>))
                .Select(descriptor => descriptor.ImplementationInstance)
                .OfType<IConfigureOptions<CircuitOptions>>())
            {
                configureOptions.Configure(options);
            }

            Assert.Multiple(() =>
            {
                Assert.That(builder.Services, Is.SameAs(services));
                Assert.That(options.PersistedCircuitInMemoryMaxRetained, Is.EqualTo(CircuitOptionsConfigurator.kPersistedCircuitsInMemory));
            });
        }

        /// <summary>
        /// The framework keeps persisted circuits in a memory cache limited to PersistedCircuitInMemoryMaxRetained
        /// entries of size 1. This guards the assumption that such a cache does not keep any circuit.
        /// </summary>
        [Test]
        public void PersistedCircuitCache_WithConfiguredLimit_DoesNotStoreCircuit()
        {
            using MemoryCache cache = new(new MemoryCacheOptions { SizeLimit = CircuitOptionsConfigurator.kPersistedCircuitsInMemory });

            cache.Set(kCircuitSecret, new object(), new MemoryCacheEntryOptions { Size = 1 });

            Assert.That(cache.TryGetValue(kCircuitSecret, out _), Is.False);
        }
    }
}
