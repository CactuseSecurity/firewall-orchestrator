# Command line parameters importer scripts

These can be used for testing a specific import on the command line.

## python importer

```console
user@test:~$ ./import_mgm.py --help
usage: import_mgm.py [-h] -m management_id [-c] [-f] [-d debug_level] [-v] [-s] [-l api_limit] [-i config_file_input] [-n config_file_normalized_input] [--rule_hits]

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
  --rule_hits           If set, only the last hits of the rules are imported (rule hit import) instead of the configuration
user@test:~$ 
```

### Rule hit import

The regular import does not read the last hits of the rules. They are imported by a separate rule hit import run:

- The middleware scheduler `ImportRuleHitsJob` (settings: importer settings, "Rule hit import interval" and "Rule hit import start")
  writes a request per imported top-level management into `rule_hit_import_request`.
- The importer main loop runs the rule hit import of a requested management right after its configuration import and
  removes the request. The run holds the import lock of the management (`import_control` row with `import_type_id` 5);
  if another import holds the lock, the management is skipped until the next request.
- Only importer modules implementing `FwCommon.supports_rule_hits` / `get_rule_hits` take part (currently Check Point R8x
  and FortiManager). The hits are stored per gateway in `firewall.rule_hit`; `rule_metadata.rule_last_hit` holds the
  latest hit across all gateways and only moves forward.
- `import_mgm.py -m <mgm_id> --rule_hits` runs the rule hit import of a single management manually.


### Lint Setup

ruff and pre-commit are in requirements.txt

```
pip install -r .\roles\importer\files\importer\requirements.txt
```

```
pre-commit install
```

### Usage

Ruff lint check

```
ruff check
```

Ruff lint check and auto fix

```
ruff check --fix
```

  
Ruff format ( only needed for VS, but also run pre-commit) 

```
ruff format
```
