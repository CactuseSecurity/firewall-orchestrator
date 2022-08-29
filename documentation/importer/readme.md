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
fworch@fwodemo:~/importer$ ./import-mgm.py --help
usage: import-mgm.py [-h] -m management_id [-c] [-f] [-d debug_level]
                     [-x proxy_string] [-s ssl_verification_mode]
                     [-l api_limit] [-i config_file_input]

Read configuration from FW management via API calls

optional arguments:
  -h, --help            show this help message and exit
  -m management_id, --mgm_id management_id
                        FWORCH DB ID of the management server to import
  -c, --clear           If set the import will delete all data for the given
                        management instead of importing
  -f, --force           If set the import will be attempted without checking
                        for changes before
  -d debug_level, --debug debug_level
                        Debug Level: 0=off, 1=send debug to console, 2=send
                        debug to file, 3=save noramlized config file;
                        4=additionally save native config file; default=0.
                        config files are saved to $FWORCH/tmp/import dir
  -x proxy_string, --proxy proxy_string
                        proxy server string to use, e.g. http://1.2.3.4:8080
  -s ssl_verification_mode, --ssl ssl_verification_mode
                        [ca]certfile, if value not set, ssl check is off";
                        default=empty/off
  -l api_limit, --limit api_limit
                        The maximal number of returned results per HTTPS
                        Connection; default=150
  -i config_file_input, --in_file config_file_input
                        if set, the config will not be fetched from firewall
                        but read from native json config file specified here;
                        may also be an url.
fworch@fwodemo:~/importer$ 
```
