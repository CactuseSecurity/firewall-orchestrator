using FWO.Services.EventMediator.Interfaces;

namespace FWO.Services.EventMediator.Events
{
    public class JwtExpiredEventArgs(string userDn = "") : IEventArgs
    {
        public string UserDn { get; set; } = userDn;
    }
}
