# Command line parameters importer scripts

These can be used for testing a specific import on the command line.

## legacy importer fworch-importer-single.pl

```console
-show-only-first-import-error
-fullauditlog
-clear-all-rules     - remove only rules from DB but keep objects
-clear-management    - remove all rules and objects from the database
-no-md5-checks       - do not check checksums of config files but import even no changes were made
-do-not-copy         - do not copy files from management system but use existing local config file(s)
-direct-from-csv     - neither copy nor parse config but simply import from existing CSV files
need to change import id in all csv-files to next id, e.g.:
                      sudo find . -name "*.csv" -exec sed -i 's/12192714/12192718/' '{}' \;
-use-scp             - use scp for copying config for testing ignoring product specific interfaces
                      (when you do not have a test device handy)
-no-cleanup          - do not delete temporary files (.config, .csv) after import completed (for debugging purposes)
-debug=x             - set debug level for import to x
-configfile=/path/to/file - import directly from local (R8x) config file, do not copy
```

## python importer

```console
user@test:~$ ./import-mgm.py --help
usage: import-mgm.py [-h] -m management_id [-c] [-f] [-d debug_level] [-v] [-s] [-l api_limit] [-i config_file_input] [-n config_file_normalized_input]

Read configuration from FW management via API calls

optional arguments:
  -h, --help            show this help message and exit
  -m management_id, --mgm_id management_id
                        FWORCH DB ID of the management server to import
  -c, --clear           If set the import will delete all data for the given management instead of importing
  -f, --force           If set the import will be attempted without checking for changes before
  -d debug_level, --debug debug_level
                        Debug Level: 0=off, 1=send debug to console, 2=send debug to file, 3=save noramlized config file; 4=additionally save native config file; default=0. config
                        files are saved to $FWORCH/tmp/import dir
  -v, --verify_certificates
                        verify certificates
  -s, --suppress_certificate_warnings
                        suppress certificate warnings
  -l api_limit, --limit api_limit
                        The maximal number of returned results per HTTPS Connection; default=150
  -i config_file_input, --in_file config_file_input
                        if set, the config will not be fetched from firewall but read from native json config file specified here; may also be an url.
  -n config_file_normalized_input, --normalized_in_file config_file_normalized_input
                        if set, the config will not be fetched from firewall but read from normalized json config file specified here; may also be an url.
user@test:~$ 
```

# rework 2025

## data collected from autodiscovery (sync per management)

```json
{
  "management": {
    "uid": "xxxx",  # either name or uid can be fetched from API
    "name": "xxx", 
    "type": "cpr8x, fortimanager, fortiOS, ...", # not read from sync but can be configured in UI/DB
    "do_not_import": false,   # not read from sync but can be configured in UI/DB
    "hide_in_ui": false,      # not read from sync but can be configured in UI/DB
    "gateways": [
      {
        "uid": "xxx",
        "name": "abc",
        "type": "cp-gw|fortigate|...",
        "do_not_import": false,   # not read from sync but can be configured in UI/DB
        "hide_in_ui": false       # not read from sync but can be configured in UI/DB
      }
    ],
    "managements": [] # for sub managements, content: see above
} 
```
- auto discovery does not store any information about rulebases!
- this is the only data stored (by initial sync of a management) in the database
- this means we cannot identify any rulebase changes, pro: these changes will be ignored
- if we cannot sync a firewall, e.g. palo, we need more fields like policy name, ...


## interface for file import (json data structure, normalized config)

```json
{
  "global_rulebases": [],
  "global_network_objects": [],
  "global_network_services": [],
  "global_users": [],
  "sub-managements": [
    {
      "uid": "xxx",
      "name": "abc",
      # type is not imported because we always assume normalized format, 
      "gateways": [
        {
          "uid": "xxx",
          "name": "xxx",
          "type": "",
          "initial_rulebase_uid": "uidxy",
          "routing": [],
          "interfaces": [],
        }
      ],
      "rulebases": [
        {
          "uid": "xxx",
          "name": "xxx",
          "type": "access|nat",
          "rb_order_no": 1,
          "rules": [
            ...       
            "src_ref": "uid1234"
          ]
        }
      ],
      "rulebase_link": [
        {
          "rulebase_from_uid": "xxx", # when null, this is the initial rulebase for a gateway
          "rule_from_uid": "xxx",     # when null, this is the initial rulebase for a gateway
          "rulebase_to_uid": "xxx",
          "link_type": "layer|ordered|...",
          "gateway-uid": "xxx"
        },
        ...more links
      ]
      "network_objects": []
      "network_services": []
      "users": []
    },
    #...more managements
  ]
}
```

## check point specific information

### getting routing information from checkpoint gateways

Note: this only works when the user has admin privileges (not just read-only!)

```
curl --request POST \
  --url https://192.168.100.111/web_api/run-script \
  --header 'Content-Type: application/json' \
  --header 'x-chkp-sid:   uvXWp05mPeL-YKAvdyWvzY4YeGGt9GApCyZ2ndwQlOg' \
  --cookie Session=Login \
  --data '{
	"script-name": "show-routing-table",
	"script": "ip route show",
	"targets": [
		"sting-gw"
	]
}'
```
return a task id:
```json
{
	"tasks": [
		{
			"target": "sting-gw",
			"task-id": "345d9cfc-43b3-4a9f-b5c9-97755e075622"
		}
	]
}
```

this must be used to get the routing table (once the task is completed):

```
curl --request POST \
  --url https://192.168.100.111/web_api/show-task \
  --header 'Content-Type: application/json' \
  --header 'x-chkp-sid:   Ir3sw1JejoCx24Cs0uHfebjhWKGaPdcjwyW9Cn86RlE' \
  --cookie Session=Login \
  --data '{
	"task-id": "345d9cfc-43b3-4a9f-b5c9-97755e075622",
	"details-level": "full"
}'
```

returns

```json
{
	"tasks": [
		{
			"uid": "5570f622-47c2-41f9-86e6-dc4d0ebf3584",
			"name": "sting-gw - show-routing-table",
			"type": "CdmTaskNotification",
			"task-id": "345d9cfc-43b3-4a9f-b5c9-97755e075622",
			"task-name": "sting-gw - show-routing-table",
			"status": "succeeded",
			"progress-percentage": 100,
			"task-details": [
				{
					"uid": "698d6899-b963-4d34-aeb7-c3c9fa58f8f4",
					"statusCode": "succeeded",
					"statusDescription": "default via ...",
					"taskNotification": "5570f622-47c2-41f9-86e6-dc4d0ebf3584",
					"gatewayId": "cbdd1e35-b6e9-4ead-b13f-fd6389e34987",
					"gatewayName": "sting-gw",
					"transactionId": 559998982,
					"responseMessage": "xxxx"
        }
			]
		}
	]
}
```
Get the full routing table by base64-decoding the responseMessage.

### interface checkpoint (file) import (json data structure, native config)

 Here, the gateways contain interface information when the command is issued with details-level=full
 Routing info is missing though.
 Just need to add the interfaces sectino for each gateway to the gateways.

```json
{
  "users": {},
  "object_tables": [
    {
      "object_type": "hosts",
      "object_chunks": []
    },
    {
      "object_type": "networks",
      "object_chunks": []
    }, 
    {
      "object_type": "groups",
      "object_chunks": []
    },
    {
      "object_type": "gateways-and-servers",   
      "object_chunks": [
        {
          "objects": [
            {
              "uid": "cbdd1e35-b6e9-4ead-b13f-fd6389e34987",
              "name": "sting-gw",
              "type": "simple-gateway",
              "domain": {
                "uid": "41e821a0-3720-11e3-aa6e-0800200c9fde",
                "name": "SMC User",
                "domain-type": "domain"
              },
              "policy": {
                "access-policy-revision": {
                  "uid": "ab620dc5-b80c-45c6-85ee-11f4faa9f5aa",
                  "name": "xxx",
                  "type": "session",
                  "state": "published",
                  "publish-time": {
                    "posix": 1736767952039,
                    "iso-8601": "2025-01-13T12:32+0100"
                  }
                },
              },
              "operating-system": "Gaia",
              "hardware": "Open server",
              "version": "R82",
              "ipv4-address": "192.168.10.90",
              "interfaces": [
                {
                  "interface-name": "eth5",
                  "ipv4-address": "91.26.19.194",
                  "ipv4-network-mask": "255.255.255.240",
                  "ipv4-mask-length": 28,
                  "dynamic-ip": false,
                  "topology": {
                    "leads-to-internet": true
                  }
                },
                {
                  "interface-name": "eth2",
                  "ipv4-address": "213.157.1.55",
                  "ipv4-network-mask": "255.255.255.240",
                  "ipv4-mask-length": 28,
                  "dynamic-ip": false,
                  "topology": {
                    "leads-to-internet": false,
                    "ip-address-behind-this-interface": "specific",
                    "leads-to-specific-network": {
                      "uid": "a4944c2e-1801-408f-8fd9-48f873123d5d",
                      "name": "sting-gw_eth2",
                      "type": "group"
                    },
                    "leads-to-dmz": false
                  }
                },
                {
                  "interface-name": "eth3",
                  "ipv4-address": "192.168.20.1",
                  "ipv4-network-mask": "255.255.255.192",
                  "ipv4-mask-length": 26,
                  "dynamic-ip": false,
                  "topology": {
                    "leads-to-internet": false,
                    "ip-address-behind-this-interface": "network defined by the interface ip and net mask",
                    "leads-to-dmz": false
                  }
                }
              ],
              "network-security-blades": {
                "firewall": true,
                "site-to-site-vpn": true,
                "application-control": true,
                "content-awareness": true,
                "identity-awareness": true,
                "monitoring": true,
                "url-filtering": true,
                "anti-bot": true,
                "anti-virus": true,
                "ips": true
              },
            },
            {
              "uid": "d7a49524-b877-4b2a-b949-a736d86e2baa",
              "name": "sting-mgmt",
              "type": "checkpoint-host",
              "domain": {
                "uid": "41e821a0-3720-11e3-aa6e-0800200c9fde",
                "name": "SMC User",
                "domain-type": "domain"
              },
              "ipv4-address": "192.168.100.111",
              "interfaces": [
              ],
              "version": "R82",
              "os": "Gaia",
              "hardware": "Open server",
              "sic-name": "CN=cp_mgmt_sting-mgmt,O=sting.cactus-es.de..jnbkhk",
              "sic-state": "communicating",
              "management-blades": {
                "network-policy-management": true,
                "user-directory": false,
                "compliance": false,
                "logging-and-status": true,
                "smart-event-server": true,
                "smart-event-correlation": true,
                "endpoint-policy": false,
                "secondary": false,
                "identity-logging": true
              },
              "firewall": false,
              "read-only": true,
              "available-actions": {
                "edit": "false",
                "delete": "false",
                "clone": "not_supported"
              }
            }
          ],
          "from": 1,
          "to": 2,
          "total": 2
        }
      ]
    },
      ...
  ],
  "gateways": [
    {
      "dev_name": "sting-gw",
      "dev_uid": "sting-gw-uid",  # see gateways and servers
      "rulebase_links":
      [
        {
          "from_rulebase_uid": "sting-big-gw-top-level-uid",
          "to_rulebase_uid": "sting-first-ordered-layer-uid",
          "from_rule_uid": "9cd8ce05-32db-483a-a44a-0c9131cca022",
          "link_type": "ordered"
        },
        {
          "from_rulebase_uid": "inline-layer-1-uid",
          "to_rulebase_uid": "inline-layer-2-uid",
          "from_rule_uid": "52f5f517-6801-4741-84b4-13fe26843433",
          "link_type": "inline"
        },
        {
          "from_rulebase_uid": "inline-layer-1-uid",
          "to_rulebase_uid": "inline-layer-2-uid",
          "from_rule_uid": "52f5f517-6801-4741-84b4-13fe26843433",
          "link_type": "inline"
        },
        {
          "from_rulebase_uid": "global1-uid",
          "to_rulebase_uid": "local-uid",
          "from_rule_uid": "7288ebb9-d951-4139-9ac2-980cf34a60af",
          "link_type": "local"
        },
        {
          "from_rulebase_uid": null,
          "to_rulebase_uid": "sting-first-ordered-layer-uid",
          "from_rule_uid": null,
          "link_type": "inital"
        }
      ],
      "routing": [],
      "interfaces": [
        {
          "interface-name": "eth5",
          "ipv4-address": "91.26.19.194",
          "ipv4-network-mask": "255.255.255.240",
          "ipv4-mask-length": 28,
          "dynamic-ip": false,
          "topology": {
            "leads-to-internet": true
          }
        },
        {
          "interface-name": "eth2",
          "ipv4-address": "213.157.1.55",
          "ipv4-network-mask": "255.255.255.240",
          "ipv4-mask-length": 28,
          "dynamic-ip": false,
          "topology": {
            "leads-to-internet": false,
            "ip-address-behind-this-interface": "specific",
            "leads-to-specific-network": {
              "uid": "a4944c2e-1801-408f-8fd9-48f873123d5d",
              "name": "sting-gw_eth2",
              "type": "group",
              "domain": {
                "uid": "41e821a0-3720-11e3-aa6e-0800200c9fde",
                "name": "SMC User",
                "domain-type": "domain"
              },
              "icon": "General/group",
              "color": "black"
            },
            "leads-to-dmz": false
          }
        },
        {
          "interface-name": "eth3",
          "ipv4-address": "192.168.20.1",
          "ipv4-network-mask": "255.255.255.192",
          "ipv4-mask-length": 26,
          "dynamic-ip": false,
          "topology": {
            "leads-to-internet": false,
            "ip-address-behind-this-interface": "network defined by the interface ip and net mask",
            "leads-to-dmz": false
          }
        },
        {
          "interface-name": "eth0",
          "ipv4-address": "192.168.10.90",
          "ipv4-network-mask": "255.255.255.0",
          "ipv4-mask-length": 24,
          "dynamic-ip": false,
          "topology": {
            "leads-to-internet": false,
            "ip-address-behind-this-interface": "network defined by the interface ip and net mask",
            "leads-to-dmz": false
          }
        },
        {
          "interface-name": "eth1",
          "ipv4-address": "5.7.21.44",
          "ipv4-network-mask": "255.255.255.248",
          "ipv4-mask-length": 29,
          "dynamic-ip": false,
          "topology": {
            "leads-to-internet": true
          }
        },
        {
          "interface-name": "eth6",
          "ipv4-address": "192.168.5.3",
          "ipv4-network-mask": "255.255.255.0",
          "ipv4-mask-length": 24,
          "dynamic-ip": false,
          "topology": {
            "leads-to-internet": true
          }
        },
        {
          "interface-name": "eth4",
          "ipv4-address": "192.168.15.1",
          "ipv4-network-mask": "255.255.255.0",
          "ipv4-mask-length": 24,
          "dynamic-ip": false,
          "topology": {
            "leads-to-internet": false,
            "ip-address-behind-this-interface": "network defined by the interface ip and net mask",
            "leads-to-dmz": false
          }
        }
      ]
    },
    {
      "dev_name": "small-gw",
      "dev_uid": "small-gw-uid",
      "rulebase_links":
      [
        {
          "from_rulebase_uid": null,
          "to_rulebase_uid": "inline-layer-1-uid",
          "from_rule_uid": null,
          "link_type": "inital"
        }
      ],
      "routing": [],
      "interfaces": []
    }
  ],
  "rulebases": [
    {
      "name": "sting-first-ordered-layer",
      "uid": "sting-first-ordered-layer-uid",
      "chunks": [
        {
          "uid": "sting-first-ordered-layer-uid",
          "name": "sting-first-ordered-layer",
          "rulebase": [
            {
              "uid": "dd12a686-256e-418c-8d8f-a7eb37fc7e5f",
              "name": "Block attacks",
              "type": "access-section",
              "from": 1,
              "to": 2,
              "rulebase": [...],
            }
        }
      ]
    },
    {
      "name": "inline-layer-2",
      "uid": "inline-layer-2-uid",
      "chunks": [
      ],
    },

  ],
  "nat_rulebases": [
    {
      "nat_rule_chunks": [...]
    }
  ]
}
```
