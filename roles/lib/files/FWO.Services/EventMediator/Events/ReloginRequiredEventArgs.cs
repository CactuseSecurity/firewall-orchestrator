using FWO.Services.EventMediator.Interfaces;

namespace FWO.Services.EventMediator.Events
{
    /// <summary>
    /// Identifies the user session that now requires an explicit re-login.
    /// </summary>
    public class ReloginRequiredEventArgs(string userDn = "") : IEventArgs
    {
        /// <summary>
        /// Distinguished name of the affected user.
        /// </summary>
        public string UserDn { get; set; } = userDn;
    }
}
