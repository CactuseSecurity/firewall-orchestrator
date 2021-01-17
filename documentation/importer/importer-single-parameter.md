# Command line parameter fworch-importer-single.pl

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
