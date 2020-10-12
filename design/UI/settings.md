# content of settings page in UI
graphical descriptions, see <https://xfer.cactus.de/index.php/f/18376>
- all values that do not have a default value and are not nullable will be mandatory (!)

- settings
  - manage user settings (for currently logged in user only)
    - select UI lanaguage (additionally offer language selection on top level menu with little flags)
    - change password (only for local users!)
  - manage devices (managements + devices) - for API queries see <https://github.com/CactuseSecurity/firewall-orchestrator/blob/master/design/UI/settings-management.md>
    - add management
    - modify management (enable/disable import of management, change details, ...)
    - add device
    - modify device (enable/disable import of device, change details, ...)
  - manage users and roles
    - manage authentication servers (external ldap)
      - assign tenants to (external) users (currently simply done by using tenant level, might have to be enhanced in phase 2)
    - manage users
    - manage roles
      - assign roles to (external) users
    - manage tenants

----- phase 2 -----

  - manage workflows
  - manage config backups
  - manage reporting
    - manage reporting scheduling (sending results via email)
