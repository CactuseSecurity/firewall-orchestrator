using FWO.Data;
using FWO.Services.EventMediator;
using FWO.Services.EventMediator.Events;
using FWO.Ui.Services;
using Microsoft.AspNetCore.Components.Server.Circuits;
using NUnit.Framework;
using System.Threading;

namespace FWO.Test
{
    [TestFixture]
    public class CircuitHandlerServiceTest
    {
        [Test]
        public async Task OnConnectionDownAsync_ShouldNotPublishSessionClosedEvent_ButCircuitClosedAsyncShould()
        {
            EventMediator eventMediator = new();
            CircuitHandlerService circuitHandlerService = new(eventMediator)
            {
                User = new UiUser
                {
                    Name = "tester",
                    Dn = "uid=tester,ou=people,dc=example,dc=com"
                }
            };

            int publishCount = 0;
            eventMediator.Subscribe<UserSessionClosedEvent>(nameof(UserSessionClosedEvent), _ => publishCount++);

            await circuitHandlerService.OnConnectionDownAsync(null!, CancellationToken.None);
            Assert.That(publishCount, Is.EqualTo(0));

            await circuitHandlerService.OnCircuitClosedAsync(null!, CancellationToken.None);
            await circuitHandlerService.OnCircuitClosedAsync(null!, CancellationToken.None);

            Assert.That(publishCount, Is.EqualTo(1));
        }
    }
}
