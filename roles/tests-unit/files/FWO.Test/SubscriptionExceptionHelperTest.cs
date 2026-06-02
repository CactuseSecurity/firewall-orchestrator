using FWO.Ui.Services;
using NUnit.Framework;
using System.Net.WebSockets;

namespace FWO.Test
{
    [TestFixture]
    internal class SubscriptionExceptionHelperTest
    {
        [Test]
        public void ShouldIgnoreDuringCircuitShutdown_IgnoresWebSocketCloseHandshake()
        {
            bool ignore = SubscriptionExceptionHelper.ShouldIgnoreDuringCircuitShutdown(
                new WebSocketException("The remote party closed the WebSocket connection without completing the close handshake."),
                circuitIsClosing: true);

            Assert.That(ignore, Is.True);
        }

        [Test]
        public void ShouldIgnoreDuringCircuitShutdown_DoesNotIgnoreWhenCircuitIsOpen()
        {
            bool ignore = SubscriptionExceptionHelper.ShouldIgnoreDuringCircuitShutdown(
                new WebSocketException("The remote party closed the WebSocket connection without completing the close handshake."),
                circuitIsClosing: false);

            Assert.That(ignore, Is.False);
        }
    }
}
