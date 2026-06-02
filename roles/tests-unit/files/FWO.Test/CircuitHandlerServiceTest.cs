using FWO.Services.EventMediator;
using FWO.Ui.Services;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    internal class CircuitHandlerServiceTest
    {
        [Test]
        public async Task OnConnectionDownAsync_RaisesConnectionDownEvent()
        {
            EventMediator eventMediator = new();
            CircuitHandlerService circuitHandler = new(eventMediator);
            int downCount = 0;
            circuitHandler.ConnectionDown += () => downCount++;

            await circuitHandler.OnConnectionDownAsync(null!, CancellationToken.None);

            Assert.That(downCount, Is.EqualTo(1));
        }

        [Test]
        public async Task OnConnectionUpAsync_RaisesConnectionUpEvent()
        {
            EventMediator eventMediator = new();
            CircuitHandlerService circuitHandler = new(eventMediator);
            int upCount = 0;
            circuitHandler.ConnectionUp += () => upCount++;

            await circuitHandler.OnConnectionUpAsync(null!, CancellationToken.None);

            Assert.That(upCount, Is.EqualTo(1));
        }
    }
}
