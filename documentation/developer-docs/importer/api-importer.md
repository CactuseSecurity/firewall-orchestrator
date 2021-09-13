# Importing firewall configs via API

## Import data interface

The main import script is /usr/local/fworch/importer/import_mgm.py. Within this file we have the following calls which deal with firewall product specific stuff:

```python
 # import product-specific importer module:
fw_module_name = mgm_details['deviceType']['name'].lower().replace(
    ' ', '') + mgm_details['deviceType']['version']+'.fwcommon'
fw_module = importlib.import_module(fw_module_name)

# get config from FW API and write it into config2import
fw_module.get_config(
    config2import, current_import_id, base_dir, mgm_details, secret_filename, rulebase_string, config_filename, debug_level)

# now we import config2import via the FWO API:
error_count += fwo_api.import_json_config(fwo_api_base_url, jwt, args.mgm_id, {
    "importId": current_import_id, "mgmId": args.mgm_id, "config": config2import})
```

So the following rules apply:

- Create a sub-directory beneath /usr/local/fworch/importer/ called "device_type.name" + "device_type.version"
- Within this directory there has be a module called 'fwcommon.py' containing a function get_config using the parameters above
- The config needs to be returned in the config2import variable as a json dict using the following syntax:
```json
{
  "rules": [
    {
      "rule_dst": "sting-gw",
      "rule_num": 0,
      "rule_src": "test-ext-vpn-gw|test-interop-device|BeeW10|wsus",
      "rule_svc": "IPSEC",
      "rule_uid": "828b0f42-4b18-4352-8bdf-c9c864d692eb",
      "rule_name": "",
      "rule_time": "Any",
      "control_id": 1074,
      "rule_track": "Log",
      "rule_action": "Drop",
      "rule_comment": "test comment",
      "rule_dst_neg": false,
      "rule_implied": false,
      "rule_src_neg": false,
      "rule_svc_neg": false,
      "rule_disabled": true,
      "rule_dst_refs": "cbdd1e35-b6e9-4ead-b13f-fd6389e34987",
      "rule_src_refs": "a580c5a3-379c-479b-b49d-487faba2442e|98bc04fc-b88b-4283-83ad-7b6899bc1876|2ad18398-e004-4324-af79-634be66941d6|2661ec9f-293f-4c82-8150-4bb6c883ca79",
      "rule_svc_refs": "97aeb475-9aea-11d5-bd16-0090272ccb30",
      "rulebase_name": "FirstLayer shared with inline layer",
      "rule_installon": "Policy Targets",
      "parent_rule_uid": "",
      "rule_last_change_admin": "tim-admin"
    }
  ],
  "user_objects": [
    {
      "user_typ": "simple",
      "user_uid": "aae47c39-f416-4b32-801d-af53adfa1939",
      "user_name": "test-user1",
      "control_id": 1074,
      "user_color": "black",
      "user_comment": ""
    },
    {
      "user_typ": "group",
      "user_uid": "227d1a80-cc1e-4cd4-9576-4d46f271402f",
      "user_name": "test-group",
      "control_id": 1074,
      "user_color": "black",
      "user_comment": ""
    }
  ],
  "network_objects": [
    {
      "obj_ip": "22.55.200.192/26",
      "obj_typ": "network",
      "obj_uid": "5368caf0-d192-457b-9c86-5d5f9e5dc199",
      "obj_name": "Net_22.55.200.192-2",
      "obj_color": "black",
      "control_id": 1074,
      "obj_ip_end": "22.55.200.192/26",
      "obj_comment": null,
      "obj_member_refs": null,
      "obj_member_names": null
    }
  ],
  "service_objects": [
    {
      "rpc_nr": null,
      "svc_typ": "simple",
      "svc_uid": "97aeb44f-9aea-11d5-bd16-0090272ccb30",
      "ip_proto": "6",
      "svc_name": "AOL",
      "svc_port": "5190",
      "svc_color": "red",
      "control_id": 1074,
      "svc_comment": "AOL Instant Messenger. Also used by: ICQ & Apple iChat",
      "svc_timeout": "3600",
      "svc_port_end": "5190",
      "svc_member_refs": null,
      "svc_member_names": null
    }
  ],
  "zone_objects": [
    {
      "zone_name": "test-zone",
      "svc_typ": "simple",
      "zone_uid": "98aeb44f-9aea-11d5-bd16-0090272ccb30",
      "control_id": 1074,
      "zone_comment": "just a test"
    }
  ]}
```
If there are no (e.g. user or zone) objects in the config, the xxx_objects arrays can simply be ommited.

## testing

We recommend using a tool like insomnia for testing API stuff.
