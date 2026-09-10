# version numbered upgrade steps

Each `.ldif.j2` template here is named after the product version it upgrades an installation *to*, and
the LDAP tree upgrade applies every file whose version is above the installed one. Add a new
file for a new version; never change a released one, because it has already run on
existing installations.

The files below `minimum_upgrade_source_version` (`inventory/group_vars/all.yml`, 8.0)
were removed in 9.5.0. The installer refuses an upgrade that starts below that version
(`roles/common/tasks/validate-upgrade-source-version.yml`), so those steps could never run
again. This README is what keeps the directory itself in place while it holds no upgrade
file: the `fileglob` lookup in `roles/middleware/tasks/upgrade_ldap_tree.yml` warn on every run about a path that
does not exist.
