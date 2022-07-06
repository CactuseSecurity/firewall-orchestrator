using FWO.Api.Data;

namespace FWO.Ui.Services
{
    public static class JwtEventService
    {
        public static event EventHandler<string>? OnPermissionChanged;

        public static event EventHandler<string>? OnJwtAboutToExpire;

        private static readonly Dictionary<string, Timer> jwtExpiryTimers = new Dictionary<string, Timer>();

        public static void PermissionsChanged(string userDn)
        {
            OnPermissionChanged?.Invoke(null, userDn);
        }

        public static void JwtAboutToExpire(string userDn)
        {
            OnJwtAboutToExpire?.Invoke(null, userDn);
        }

        public static void AddJwtTimer(string userDn, int time)
        {
            jwtExpiryTimers[userDn] = new Timer(_ => JwtAboutToExpire(userDn), null, time, time);
        }
    }
}
