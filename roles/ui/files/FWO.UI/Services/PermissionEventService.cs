using FWO.Api.Data;

namespace FWO.Ui.Services
{
    public class PermissionEventService
    {
        public event EventHandler<UiUser>? OnPermissionChanged;

        public PermissionEventService()
        {

        }

        public void PermissionsChanged(UiUser user)
        {
            OnPermissionChanged?.Invoke(this, user);
        }
    }
}
