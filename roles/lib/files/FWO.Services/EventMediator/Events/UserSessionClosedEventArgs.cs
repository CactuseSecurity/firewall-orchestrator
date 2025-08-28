using FWO.Data;
using FWO.Services.EventMediator.Interfaces;

namespace FWO.Services.EventMediator.Events
{
    public class UserSessionClosedEventArgs(string userName = "", string? userDn = "") : IEventArgs
    {
        public string? UserName { get; set; } = userName;
        public string? UserDn { get; set; } = userDn;
    }
}
