using Microsoft.AspNetCore.Components.Server.Circuits;
using FWO.Logging;
using FWO.Data;
using FWO.Services.EventMediator.Interfaces;
using FWO.Services.EventMediator.Events;
using System.Threading;

namespace FWO.Ui.Services
{
    public class CircuitHandlerService(IEventMediator eventMediator) : CircuitHandler
    {
        public UiUser? User { get; set; }

        private readonly UserSessionClosedEvent OnUserSessionClosed = new();
        private int sessionClosedPublished;

        public override Task OnConnectionDownAsync(Circuit circuit, CancellationToken cancellationToken)
        {
            LogConnectionDown();

            return base.OnConnectionDownAsync(circuit, cancellationToken);
        }

        public override Task OnCircuitClosedAsync(Circuit circuit, CancellationToken cancellationToken)
        {
            PublishSessionClosedEvent("circuit closed");

            return base.OnCircuitClosedAsync(circuit, cancellationToken);
        }

        private void LogConnectionDown()
        {
            if (User == null)
            {
                return;
            }

            Log.WriteWarning(
                $"Session of \"{User.Name}\" connection down",
                $"Connection for user \"{User.Name}\" with DN: \"{User.Dn}\" was lost. Waiting for circuit close before ending the session.");
        }

        private void PublishSessionClosedEvent(string reason)
        {
            if (User != null)
            {
                if (Interlocked.Exchange(ref sessionClosedPublished, 1) == 1)
                {
                    Log.WriteDebug(
                        nameof(CircuitHandlerService),
                        $"Ignoring duplicate session close notification for user \"{User.Name}\" with DN: \"{User.Dn}\" ({reason}).");

                    return;
                }

                Log.WriteAudit(
                    $"Session of \"{User.Name}\" closed",
                    $"Session of user \"{User.Name}\" (last logged in) with DN: \"{User.Dn}\" was closed after {reason}.");

                OnUserSessionClosed.EventArgs = new UserSessionClosedEventArgs
                {
                    UserDn = User.Dn,
                    UserName = User.Name,
                };

                eventMediator.Publish(nameof(UserSessionClosedEvent), OnUserSessionClosed);
            }
        }
    }
}
