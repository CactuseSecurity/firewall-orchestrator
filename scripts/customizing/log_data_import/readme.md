# Log data import

`import_log_data_from_git.py` is a standalone customization script for the middleware's independent log-data scheduler. It reads every CSV file in the configured Git repository, writes the normalized JSON file beside the script, and leaves the source CSV files untouched until the middleware completes the database import.

Configure these keys in `customizingConfig.json`:

- `logDataGitRepo`, `logDataGitUser`, `logDataGitPassword` (required)
- `logDataGitRepoTargetDir` (optional; default `/usr/local/fworch/etc/logDataRepo`)
- `logDataGitBranch` (optional)

Set `importLogDataPath` to the extensionless full path of this script. The scheduler runs the script, then reads its sibling `.json` file. After a successful import it runs the script with `--acknowledge-import`; that deletes the source CSV files, commits their removal, pushes it to `origin`, and deletes the generated JSON file.

The mandatory CSV headers are `App ID`, `Log count`, `Src IP`, `Dst IP`, and `Port`. Optional headers are `Protocol`, `Action`, `Log timestamp`, and `Rule name`. `Protocol` uses IP protocol numbers. A nonempty port is permitted only with TCP (`6`) or UDP (`17`).

The generated JSON interface is:

```json
{"logs":[{"app_id":"APP-1","log_count":42,"source":"192.0.2.1","destination":"198.51.100.10","protocol":6,"port":443,"action":"accept","log_time":"2026-07-28T10:30:00Z","rule_name":"web"}]}
```
