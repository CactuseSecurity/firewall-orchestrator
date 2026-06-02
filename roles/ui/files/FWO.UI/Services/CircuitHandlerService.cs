using Microsoft.AspNetCore.Components.Server.Circuits;
using FWO.Logging;
using FWO.Data;
using FWO.Services.EventMediator.Interfaces;
using FWO.Services.EventMediator.Events;

namespace FWO.Ui.Services
{
    public class CircuitHandlerService(IEventMediator eventMediator) : CircuitHandler
    {
        public UiUser? User { get; set; }

        public event Action? ConnectionDown;
        public event Action? ConnectionUp;
        public event Action? CircuitClosed;

        private readonly UserSessionClosedEvent OnUserSessionClosed = new();

        public override Task OnConnectionDownAsync(Circuit circuit, CancellationToken cancellationToken)
        {
            if (User != null)
            {
                Log.WriteAudit($"Session of \"{User.Name}\" closed", $"Session of user \"{User.Name}\" (last logged in) with DN: \"{User.Dn}\" was closed.");

                OnUserSessionClosed.EventArgs = new UserSessionClosedEventArgs
                {
                    UserDn = User.Dn,
                    UserName = User.Name,
                };

                CircuitClosed?.Invoke();
                eventMediator.Publish(nameof(UserSessionClosedEvent), OnUserSessionClosed);
            }

            ConnectionDown?.Invoke();
            return base.OnConnectionDownAsync(circuit, cancellationToken);
        }

        public override Task OnConnectionUpAsync(Circuit circuit, CancellationToken cancellationToken)
        {
            ConnectionUp?.Invoke();
            return base.OnConnectionUpAsync(circuit, cancellationToken);
        }

        public override Task OnCircuitClosedAsync(Circuit circuit, CancellationToken cancellationToken)
        {
            if (User != null)
            {
                Log.WriteAudit($"Session of \"{User.Name}\" closed", $"Session of user \"{User.Name}\" (last logged in) with DN: \"{User.Dn}\" was closed.");

                OnUserSessionClosed.EventArgs = new UserSessionClosedEventArgs
                {
                    UserDn = User.Dn,
                    UserName = User.Name,
                };

                CircuitClosed?.Invoke();
                eventMediator.Publish(nameof(UserSessionClosedEvent), OnUserSessionClosed);
            }

            return base.OnCircuitClosedAsync(circuit, cancellationToken);
        }
    }
}
