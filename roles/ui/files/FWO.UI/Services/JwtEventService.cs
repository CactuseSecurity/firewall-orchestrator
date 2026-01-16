using FWO.Basics;
using FWO.Config.Api;

namespace FWO.Ui.Services
{
    public static class JwtEventService
    {
        public static event EventHandler<string>? OnPermissionChanged;

        public static event EventHandler<string>? OnJwtExpired;

        public static void PermissionsChanged(string userDn)
        {
            OnPermissionChanged?.Invoke(null, userDn);
        }

        public static void JwtExpired(string userDn)
        {
            OnJwtExpired?.Invoke(null, userDn);
        }
    }
}
