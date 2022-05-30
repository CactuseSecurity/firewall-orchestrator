using FWO.Api.Data;

namespace FWO.Ui.Services
{
    public static class JwtEventService
    {
        public static event EventHandler<string>? OnPermissionChanged;

        public static void PermissionsChanged(string userDn)
        {
            OnPermissionChanged?.Invoke(null, userDn);
        }
    }
}
