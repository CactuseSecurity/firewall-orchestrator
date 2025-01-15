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
      "uid": "xxx",
      "name": "abc",
      "type": "cp-gw|fortigate|...",
      "do_not_import": false,   # not read from sync but can be configured in UI/DB
      "hide_in_ui": false       # not read from sync but can be configured in UI/DB
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
    ...more managements
      ]

  ],
]
```