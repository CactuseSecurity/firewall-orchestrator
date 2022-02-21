# Importing firewall configs via API

## Code structure and entry point

We use the FWO API to write the whole firewall config as JSON into the import_config table. The format is described below.
The main import script is [/roles/importer/import_mgm.py](/roles/importer/import_mgm.py).
It does not need to be changed when adding new import modules.
Within this file we have the following calls which deal with firewall product specific stuff:
```python
 # import product-specific importer module:
fw_module = importlib.import_module("." + fw_module_name, pkg_name)

# get config from FW API and write it into config2import if it has changed:

    config_changed_since_last_import = fw_module.has_config_changed(mgm_details, debug_level=debug_level, ssl_verification=ssl, proxy=proxy, force=force)

    if config_changed_since_last_import:
        get_config_response = fw_module.get_config( 
                        config2import, full_config_json,  current_import_id, mgm_details, debug_level=debug_level, 
                        ssl_verification=ssl, proxy=proxy, limit=limit, force=force, jwt=jwt)

# now we import config2import via the FWO API:
error_count += fwo_api.import_json_config(fwo_api_base_url, jwt, args.mgm_id, {
    "importId": current_import_id, "mgmId": args.mgm_id, "config": config2import})
```
The above describes the new 2021 import method. In addition we also have some legacy import modules. 
Regarding legacy import modules see [/documentation/developer-docs/importer](/documentation/developer-docs/importer/legacy-importer-csv-interface.md).
For an overview see next section.

### Import module status
- CPR8x - Currently CPR8x can be importer both via legacy (perl with CSV) and python importer (API2API)
- FortiManager - can be importer using the new API-based method
- FortiGate - can be importer via old legacy method (ssh-based)
- Barracuda - can be importer via old legacy method (ssh-based)
- Juniper JunOS - can be importer via old legacy method (ssh-based)
- Juniper ScreenOS - can be importer via old legacy method (ssh-based)
- Cisco ASA/Pix - can be importer via old legacy method (ssh-based) - work in progress - needs to be tested first.

## Adding a new import module

- It is highly recommend to stop the import process before making any changes (don't forget to restart it after successful integration):
```bash
  sudo systemctl stop fworch-importer
```
- create  a new firewall type by adding it to table stm_dev_type. To make this change permanent, this has to be added to [/roles/database/files/sql/creation/fworch-fill-stm.sql](/roles/database/files/sql/creation/fworch-fill-stm.sql) as follows:
```sql
insert into stm_dev_typ (dev_typ_name,dev_typ_version,dev_typ_manufacturer) VALUES ('<new FW model>','<version>','<name of the new FW model''s manufacturer>');
```
- Note that the version should be short and open ended, e.g. "7ff". If there is a major breaking change, a new version, e.g. 10ff will have to be created.
- For a smooth upgrade path for existing installations, a new FWO version needs to be created and sql statement above also needs to be added to the upgrade script. For upgrade information see (documentation/developer-docs/installer/upgrading.md).
- Create a sub-directory beneath /usr/local/fworch/importer/ called "dev_typ_name" + "dev_typ_version"
- Within this directory there has be a module called 'fwcommon.py' containing a function get_config using the parameters above
- The config needs to be returned in the config2import variable as a json dict using the syntax described in [/documentation/developer-docs/importer/FWO-import-api.md](/documentation/developer-docs/importer/FWO-import-api.md)
- For testing the new import module, you need to add a management and a device via the UI (see help section for details on this).

### Data handling rules
- If there are no (e.g. user or zone) objects of a kind in the config, the respective *_objects arrays can simply be ommited.
- Group delimiter is the pipe (|).
- All non-integer values need to be enclosed in quotes (").
- All values need to be sanitized, removing quotes (single and double) as well as replacing line breaks with space chars.

## Testing & Tools

We recommend using a tool like insomnia for testing API stuff.

## Debugging python

importer files end up in different directories during installation process (not the same as in the source/installer code). For debugging use something like:

```bash
sudo ln -s /home/tim/dev/tpur-fwo-june/firewall-orchestrator/roles/importer/files/importer /usr/local/fworch/importer
```
or the following in vscode

```console
sys.path.append(r"/home/tim/dev/tpur-fwo-june/firewall-orchestrator/roles/importer/files/importer")
```
