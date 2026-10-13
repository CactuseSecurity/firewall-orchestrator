using FWO.Ui.Services;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.Extensions.Caching.Memory;
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
