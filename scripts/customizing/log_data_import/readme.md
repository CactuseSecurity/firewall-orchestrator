# Log data import

`import_log_data_from_git.py` is a standalone customization script for the middleware's independent log-data scheduler. It reads every CSV file in the configured Git repository, writes the normalized JSON file beside the script, and leaves the source CSV files untouched until the middleware completes the database import.

Configure these keys in `customizingConfig.json`:

- `logDataGitRepo`, `logDataGitUser`, `logDataGitPassword` (required)
- `logDataGitRepoTargetDir` (optional; default `/usr/local/fworch/etc/logDataRepo`)
- `logDataGitBranch` (optional)
- `logDataGitRepoStartPath` (optional): repository-relative directory from which CSV files are searched recursively. If omitted or empty, the whole repository is searched. Symbolic links to CSV files outside the selected directory are rejected.

The script uses `GitPython`, which is not part of the system python. The installer creates a virtual environment for the customizing scripts at `/usr/local/fworch/scripts/customizing-venv` from `scripts/requirements.txt` and rewrites the shebangs of the deployed scripts to that environment, so the middleware picks up the dependencies when it executes the script. Custom scripts placed below `scripts/customizing` are treated the same way; add their dependencies to `scripts/requirements.txt`.

Set `importLogDataPath` to the extensionless full path of this script. The scheduler runs the script, then reads its sibling `.json` file. After a successful import it runs the script with `--acknowledge-import`; that deletes the source CSV files, commits their removal, pushes it to `origin`, and deletes the generated JSON file.

The mandatory CSV headers are `App ID`, `Log count`, `Src IP`, `Dst IP`, and `Port`. Optional headers are `Protocol`, `Action`, `Log timestamp`, and `Rule name`. `Protocol` uses IP protocol numbers. A nonempty port is permitted only with TCP (`6`) or UDP (`17`).

If any row cannot be converted, the complete CSV file is excluded from the generated JSON and from acknowledgement. Other valid CSV files in the same run are still imported and removed. The rejected file remains in the repository so it can be corrected and retried without losing the application IDs needed by replacement mode.

At the end of a run the script logs an `INFO` summary of what it converted and what it had to leave behind: the number of skipped CSV files, the total number of lines which could not be imported, and one line per skipped file naming up to five of its not importable lines with their line number and reason. A file rejected before its rows were read - because a mandatory column is missing or the file cannot be read - is reported with that reason instead of example lines. The middleware reads the script output into its own log, so the summary appears in `middleware.log`.

The database keeps one row per application, source, destination and service. Repeated entries for the same flow are not stored a second time: entries which are repeated inside one import file are merged into a single entry with the summed log count, and an entry which is already stored is updated with the imported log count, action, log time and rule name instead of being inserted again. An entry without a protocol or without a port counts as one distinct flow, not as a wildcard.

The generated JSON interface is:

```json
{"logs":[{"app_id":"APP-1","log_count":42,"source":"192.0.2.1","destination":"198.51.100.10","protocol":6,"port":443,"action":"accept","log_time":"2026-07-28T10:30:00Z","rule_name":"web"}]}
```
