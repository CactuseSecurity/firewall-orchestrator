using FWO.Logging;

namespace FWO.Ui.Services
{
    /// <summary>
    /// Asks all sessions (Blazor circuits) of this UI server to check at once whether they are still live.
    /// Registered as singleton: a change that ends sessions of other users, such as deleting a user or removing it
    /// from a role, raises it so that those sessions end immediately instead of at their next periodic check.
    /// </summary>
    public class UiSessionCheckSignal
    {
        /// <summary>
        /// Raised when the sessions have to check whether they are still live.
        /// </summary>
        public event Action? SessionCheckRequested;

        /// <summary>
        /// Asks all sessions to check whether they are still live. A failing subscriber does not keep the others
        /// from being asked.
        /// </summary>
        public void RequestSessionCheck()
        {
            Action? handlers = SessionCheckRequested;
            if (handlers == null)
            {
                return;
            }

            foreach (Action handler in handlers.GetInvocationList().Cast<Action>())
            {
                try
                {
                    handler();
                }
                catch (Exception exception)
                {
                    Log.WriteError(nameof(UiSessionCheckSignal), "A session could not be asked to check whether it is still live.", exception);
                }
            }
        }
    }
}
