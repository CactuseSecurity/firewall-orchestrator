#!/usr/bin/python3
"""Convert log CSV files from a Git repository into FWO log-import JSON."""

import argparse
import csv
import json
import logging
import sys
import urllib.parse
from collections.abc import Mapping
from dataclasses import dataclass, field
from datetime import datetime, timezone
from pathlib import Path
from typing import TextIO, cast

from scripts.customizing.fwo_custom_lib.basic_helpers import (
    get_logger,
    read_custom_config,
    read_custom_config_with_default,
)
from scripts.customizing.fwo_custom_lib.git_helpers import (
    commit_and_push_deletions,
    parse_git_depth_arg,
    update_git_repo,
)

DEFAULT_CONFIG_FILE: str = "/usr/local/fworch/etc/secrets/customizingConfig.json"
DEFAULT_REPOSITORY_DIRECTORY: str = "/usr/local/fworch/etc/logDataRepo"
START_PATH_CONFIG_KEY: str = "logDataGitRepoStartPath"
CSV_PATTERN: str = "*.csv"
OUTPUT_FILE: Path = Path(__file__).with_suffix(".json")
# the manifest is deliberately kept outside the cloned repository: it decides which files the
# acknowledgement deletes and pushes, so it must not be something the log repository can provide
MANIFEST_FILE: Path = OUTPUT_FILE.with_name(".fwo-log-import-manifest.json")
MAX_PENDING_REUSES: int = 3
MAX_REPORTED_EXAMPLE_ROWS: int = 5
REUSES_KEY: str = "reuses"
ACKNOWLEDGE_FAILURES_KEY: str = "acknowledge_failures"
COMMIT_MESSAGE: str = "chore: remove imported log data"
REQUIRED_COLUMNS: set[str] = {"App ID", "Log count", "Src IP", "Dst IP", "Port"}
OPTIONAL_COLUMNS: dict[str, str] = {"Log timestamp": "log_time", "Rule name": "rule_name"}
PORT_PROTOCOLS: tuple[int, int] = (6, 17)
LogDataEntry = dict[str, str | int | None]


@dataclass
class RejectedFile:
    """
    What could not be imported from one CSV file.

    A file with an unconvertible row is kept in the repository as a whole, so the reason has to
    survive the rejection of the file: the summary at the end of the run is the only place which
    reports how much data is still waiting and which lines hold it back.
    """

    reason: str = ""
    row_count: int = 0
    examples: list[str] = field(default_factory=list[str])

    def add_row(self, line_number: int, reason: str) -> None:
        """Count one line which cannot be imported, keeping the first few as examples."""
        self.row_count += 1
        if len(self.examples) < MAX_REPORTED_EXAMPLE_ROWS:
            self.examples.append(f"line {line_number}: {reason}")


@dataclass
class ConversionResult:
    """What one run made of the CSV files found in the log data repository."""

    entries: list[LogDataEntry] = field(default_factory=list[LogDataEntry])
    converted_files: list[Path] = field(default_factory=list[Path])
    rejected_files: dict[str, RejectedFile] = field(default_factory=dict[str, RejectedFile])


def get_optional_value(config_file: str, key: str, default: str, logger: logging.Logger) -> str:
    """Read an optional string setting, returning its default when it is absent or invalid."""
    configured_value: object = read_custom_config_with_default(config_file, key, default, logger)
    if not isinstance(configured_value, str):
        logger.warning("%s must be a string in config file %s; using the default", key, config_file)
        return default
    return configured_value


def get_csv_search_directory(config_file: str, repository_directory: Path, logger: logging.Logger) -> Path | None:
    """
    Return the configured CSV directory, refusing paths outside the cloned repository.

    The returned directory keeps the repository directory as its prefix instead of being resolved:
    the files found below it are reported and acknowledged relative to the repository directory, and
    a resolved prefix is no prefix of it any more as soon as the configured repository directory is
    a symbolic link or is not written out in its shortest form. Only the containment check resolves,
    because a symbolic link inside the repository must not be able to lead the search out of it.
    """
    configured_start_path: str = get_optional_value(config_file, START_PATH_CONFIG_KEY, "", logger).strip()
    if not configured_start_path:
        return repository_directory
    start_path: Path = Path(configured_start_path)
    if start_path.is_absolute():
        logger.error("%s must be a repository-relative directory", START_PATH_CONFIG_KEY)
        return None
    search_directory: Path = repository_directory / start_path
    resolved_repository_directory: Path = repository_directory.resolve()
    resolved_search_directory: Path = search_directory.resolve()
    if not resolved_search_directory.is_relative_to(resolved_repository_directory):
        logger.error("%s must name a directory within the log data repository", START_PATH_CONFIG_KEY)
        return None
    if not resolved_search_directory.is_dir():
        logger.error("%s does not name an existing directory in the log data repository", START_PATH_CONFIG_KEY)
        return None
    return search_directory


def find_csv_files(search_directory: Path, logger: logging.Logger) -> list[Path] | None:
    """Find CSV files without allowing symbolic links to escape the selected directory."""
    resolved_search_directory: Path = search_directory.resolve()
    csv_files: list[Path] = sorted(search_directory.rglob(CSV_PATTERN))
    for csv_file in csv_files:
        if not csv_file.resolve().is_relative_to(resolved_search_directory):
            logger.error(
                "CSV file %s resolves outside the selected log data search directory",
                csv_file.relative_to(search_directory),
            )
            return None
    return csv_files


def parse_optional_int(value: str) -> int | None:
    stripped_value: str = value.strip()
    return int(stripped_value) if stripped_value else None


def convert_csv_file(
    csv_file: Path,
    repository_directory: Path,
    rejected: RejectedFile,
    logger: logging.Logger,
) -> list[LogDataEntry]:
    """Convert every row of one CSV file, rejecting the complete file if any row is invalid."""
    converted: list[LogDataEntry] = []
    with csv_file.open(newline="", encoding="utf-8-sig") as file_handle:
        typed_file_handle: TextIO = file_handle
        reader: csv.DictReader[str] = csv.DictReader(typed_file_handle)
        if reader.fieldnames is None or not REQUIRED_COLUMNS.issubset(reader.fieldnames):
            raise ValueError(f"{csv_file} is missing one or more mandatory columns")
        for line_number, row in enumerate(reader, start=2):
            converted_row: LogDataEntry | None = convert_row_or_log_error(
                row, csv_file, repository_directory, line_number, rejected, logger
            )
            if converted_row is not None:
                converted.append(converted_row)
    if rejected.row_count > 0:
        raise ValueError(f"{rejected.row_count} row(s) could not be converted")
    return converted


def convert_csv_file_or_log_error(
    csv_file: Path,
    repository_directory: Path,
    rejected: RejectedFile,
    logger: logging.Logger,
) -> list[LogDataEntry] | None:
    """Convert one file, an unusable file is reported and skipped so the other files still import."""
    try:
        return convert_csv_file(csv_file, repository_directory, rejected, logger)
    except (OSError, UnicodeDecodeError, ValueError) as exception:
        logger.warning("ignoring %s: %s", csv_file.relative_to(repository_directory), exception)
        rejected.reason = str(exception)
        return None


def convert_repository_files(
    csv_files: list[Path],
    repository_directory: Path,
    logger: logging.Logger,
) -> ConversionResult:
    """Convert every CSV file found in the repository, keeping track of the skipped ones."""
    result: ConversionResult = ConversionResult()
    for csv_file in csv_files:
        rejected: RejectedFile = RejectedFile()
        file_entries: list[LogDataEntry] | None = convert_csv_file_or_log_error(
            csv_file, repository_directory, rejected, logger
        )
        if file_entries is None:
            result.rejected_files[csv_file.relative_to(repository_directory).as_posix()] = rejected
            continue
        result.entries.extend(file_entries)
        result.converted_files.append(csv_file)
    return result


def report_conversion_summary(result: ConversionResult, logger: logging.Logger) -> None:
    """
    Report what the run imported and what it had to leave behind.

    The middleware reads the output of this script into its own log, so this summary is what an
    operator finds in middleware.log. The per-row warnings alone do not say how much data is still
    waiting in the repository, and a file which keeps being skipped is invisible between the runs.
    """
    logger.info("converted %s CSV files into %s log entries", len(result.converted_files), len(result.entries))
    if not result.rejected_files:
        return
    rejected_row_count: int = sum(rejected.row_count for rejected in result.rejected_files.values())
    logger.info(
        "%s CSV file(s) with %s not importable line(s) were kept in the log data repository for the next run",
        len(result.rejected_files),
        rejected_row_count,
    )
    for source_name, rejected in sorted(result.rejected_files.items()):
        logger.info("not imported from %s: %s", source_name, describe_rejected_file(rejected))


def describe_rejected_file(rejected: RejectedFile) -> str:
    """Describe one skipped file, naming up to MAX_REPORTED_EXAMPLE_ROWS of its lines as examples."""
    if not rejected.examples:
        return rejected.reason
    examples: str = "; ".join(rejected.examples)
    remaining_row_count: int = rejected.row_count - len(rejected.examples)
    if remaining_row_count > 0:
        return f"{rejected.row_count} not importable line(s), for example {examples}; {remaining_row_count} more"
    return f"{rejected.row_count} not importable line(s): {examples}"


def convert_row_or_log_error(
    row: Mapping[str, str | None],
    csv_file: Path,
    repository_directory: Path,
    line_number: int,
    rejected: RejectedFile,
    logger: logging.Logger,
) -> LogDataEntry | None:
    """Convert one row, recording an unconvertible one for the summary of the run."""
    try:
        return convert_row(row)
    except (TypeError, ValueError) as exception:
        logger.warning("ignoring %s line %s: %s", csv_file.relative_to(repository_directory), line_number, exception)
        rejected.add_row(line_number, str(exception))
        return None


def convert_row(row: Mapping[str, str | None]) -> LogDataEntry:
    app_id: str = (row.get("App ID") or "").strip()
    source: str = (row.get("Src IP") or "").strip()
    destination: str = (row.get("Dst IP") or "").strip()
    log_count: int = int((row.get("Log count") or "").strip())
    if not app_id or not source or not destination or log_count < 1:
        raise ValueError("App ID, Log count, Src IP and Dst IP must be present")
    protocol, port = parse_service(row)
    result: LogDataEntry = {
        "app_id": app_id,
        "log_count": log_count,
        "source": source,
        "destination": destination,
        "protocol": protocol,
        "port": port,
        "action": (row.get("Action") or "accept").strip() or "accept",
    }
    add_optional_columns(result, row)
    return result


def parse_service(row: Mapping[str, str | None]) -> tuple[int | None, int | None]:
    """
    Read protocol and port of the logged flow.

    A port of a named protocol other than TCP or UDP is rejected here. A port without any
    protocol is passed on: whether it is imported is decided by the allowLogDataPortWithoutProtocol
    setting, since some log exports carry ports without naming the protocol.
    """
    protocol: int | None = parse_optional_int(row.get("Protocol") or "")
    port: int | None = parse_optional_int(row.get("Port") or "")
    if port is not None and protocol is not None and protocol not in PORT_PROTOCOLS:
        raise ValueError(f"Port is only valid with Protocol {' or '.join(str(p) for p in PORT_PROTOCOLS)}")
    return protocol, port


def add_optional_columns(result: LogDataEntry, row: Mapping[str, str | None]) -> None:
    """Copy the columns which may be missing in the source file into the converted entry."""
    for csv_column, json_column in OPTIONAL_COLUMNS.items():
        value: str = (row.get(csv_column) or "").strip()
        if value:
            result[json_column] = normalize_log_time(value) if json_column == "log_time" else value


def normalize_log_time(value: str) -> str:
    """
    Convert a log timestamp into the ISO 8601 form the importer can deserialize.

    A value which is not a timestamp is rejected here: the importer reads the whole generated
    file at once, so one unparsable timestamp would make it discard every entry of the file.
    """
    try:
        parsed_time: datetime = datetime.fromisoformat(value.replace("Z", "+00:00"))
    except ValueError as exception:
        raise ValueError(f"'{value}' is not a valid log timestamp") from exception
    if parsed_time.tzinfo is None:
        # without an offset the importer would read the value in the timezone of the middleware
        parsed_time = parsed_time.replace(tzinfo=timezone.utc)
    return parsed_time.isoformat()


def write_import_file(entries: list[LogDataEntry], csv_files: list[Path], repository_directory: Path) -> None:
    import_time: str = datetime.now(timezone.utc).isoformat()
    write_json_file(OUTPUT_FILE, {"import_time": import_time, "logs": entries})
    relative_files: list[str] = [csv_file.relative_to(repository_directory).as_posix() for csv_file in csv_files]
    manifest: dict[str, object] = {"csv_files": relative_files, REUSES_KEY: 0, ACKNOWLEDGE_FAILURES_KEY: 0}
    write_json_file(MANIFEST_FILE, manifest)


def write_json_file(target_file: Path, content: dict[str, object]) -> None:
    """
    Write a JSON file so it is either complete or unchanged.

    The generated import and its manifest are read by the following run and by the middleware. A
    run stopped in the middle of a write - by the import script timeout or a full disk - would
    otherwise leave a truncated file behind which no later run can read.
    """
    temporary_file: Path = target_file.with_name(f"{target_file.name}.tmp")
    temporary_file.write_text(json.dumps(content, indent=2), encoding="utf-8")
    temporary_file.replace(target_file)


def reuse_pending_import(manifest: dict[str, object], logger: logging.Logger) -> int:
    """
    Handle an import whose data was not acknowledged yet.

    Fetching new CSV files while the previous ones are still in the repository would import them
    twice, so the generated file is reused. The run stays successful however often the data was
    kept back: the acknowledgement is only retried when the import continues, so failing here
    would stall the pending import forever, also after the repository became writable again.
    Reuses are counted so a persistent failure can be told apart from a temporary one.
    """
    reuses: int = read_reuses(manifest) + 1
    manifest[REUSES_KEY] = reuses
    write_json_file(MANIFEST_FILE, manifest)
    if reuses > MAX_PENDING_REUSES:
        logger.error(
            "log data of %s was not acknowledged in %s runs; no new log data is imported until %s",
            OUTPUT_FILE,
            reuses,
            describe_pending_cause(manifest),
        )
    else:
        logger.warning("previous log data import is still pending acknowledgement; reusing it (%s)", reuses)
    return 0


def describe_pending_cause(manifest: dict[str, object]) -> str:
    """
    Name what has to happen before new log data is imported again.

    A pending import has two causes which need completely different attention: the deletion of the
    imported CSV files could not be pushed, or the middleware never asked for it because it kept
    the source back - it does that while none of the reported entries can be imported.
    """
    if read_counter(manifest, ACKNOWLEDGE_FAILURES_KEY) > 0:
        return "the deletion of the imported CSV files can be pushed to the log data repository"
    return (
        "the reported entries can be imported; the middleware keeps the source files until then,"
        " check the log data settings and the entries it reported as not importable"
    )


def read_reuses(manifest: dict[str, object]) -> int:
    """Read how often the pending import was kept back, an unusable value counts as never."""
    return read_counter(manifest, REUSES_KEY)


def read_counter(manifest: dict[str, object], key: str) -> int:
    """Read one of the manifest counters, an unusable value counts as never."""
    stored_value: object = manifest.get(key)
    return stored_value if isinstance(stored_value, int) and stored_value > 0 else 0


def record_failed_acknowledgement(manifest: dict[str, object]) -> None:
    """
    Count a failed acknowledgement in the manifest.

    A reuse cannot tell by itself whether the deletion push failed or was never attempted, so the
    acknowledgement leaves its trace here for describe_pending_cause.
    """
    manifest[ACKNOWLEDGE_FAILURES_KEY] = read_counter(manifest, ACKNOWLEDGE_FAILURES_KEY) + 1
    write_json_file(MANIFEST_FILE, manifest)


def report_failed_acknowledgement(manifest: dict[str, object], logger: logging.Logger) -> None:
    """
    Report that the deletion of the imported CSV files could not be pushed.

    This runs while the acknowledgement fails, so the message reaches the log of the middleware,
    which alerts on it. A repeatedly kept back import is reported as a persistent failure: the
    acknowledgement keeps being retried, but no new log data is imported until it succeeds.
    """
    reuses: int = read_reuses(manifest)
    if reuses > MAX_PENDING_REUSES:
        logger.error(
            "deleting the imported CSV files of %s could not be pushed in %s runs; the import is stuck "
            "on this data until the log data repository accepts the deletion",
            OUTPUT_FILE,
            reuses + 1,
        )
    else:
        logger.error(
            "deleting the imported CSV files of %s could not be pushed; the data is kept and imported again",
            OUTPUT_FILE,
        )


def read_manifest() -> dict[str, object]:
    """Read the manifest written by the last import, raising when it is not usable."""
    manifest_data: object = json.loads(MANIFEST_FILE.read_text(encoding="utf-8"))
    if not isinstance(manifest_data, dict):
        raise TypeError("Log data import manifest must be an object")
    return cast("dict[str, object]", manifest_data)


def read_usable_manifest(logger: logging.Logger) -> dict[str, object] | None:
    """
    Read the manifest, reporting an unusable one instead of raising.

    A manifest which cannot be read decides nothing any more, and raising on it would repeat the
    same traceback in every run - exactly the permanent stall the reuse handling exists to prevent.
    It is therefore reported and dropped, so the next run starts a fresh import.
    """
    try:
        return read_manifest()
    except (OSError, ValueError, TypeError):
        logger.exception("log data import manifest %s cannot be read", MANIFEST_FILE)
        return None


def read_csv_file_names(manifest: dict[str, object], logger: logging.Logger) -> list[str] | None:
    """Read the CSV files whose deletion has to be pushed, None when the manifest cannot name them."""
    csv_file_names: object = manifest.get("csv_files", [])
    if not isinstance(csv_file_names, list) or not all(
        isinstance(file_name, str) for file_name in cast("list[object]", csv_file_names)
    ):
        logger.error("log data import manifest %s does not contain a list of CSV file names", MANIFEST_FILE)
        return None
    return cast("list[str]", csv_file_names)


def discard_unusable_import(logger: logging.Logger) -> None:
    """
    Drop a pending import which cannot be processed any more.

    Both files are written together, so an unreadable manifest makes the generated import file
    worthless as well. Dropping them costs at most one repeated import of the CSV files still in
    the repository - repeated flows are merged with the stored ones - while keeping them would
    block every following run.
    """
    MANIFEST_FILE.unlink(missing_ok=True)
    OUTPUT_FILE.unlink(missing_ok=True)
    logger.warning("dropped the pending log data import; the log data repository is read again in the next run")


def import_data(config_file: str, depth: int | None, logger: logging.Logger) -> int:
    repository_directory: Path = Path(
        get_optional_value(config_file, "logDataGitRepoTargetDir", DEFAULT_REPOSITORY_DIRECTORY, logger)
    )
    if MANIFEST_FILE.exists() and OUTPUT_FILE.exists():
        pending_manifest: dict[str, object] | None = read_usable_manifest(logger)
        if pending_manifest is not None:
            return reuse_pending_import(pending_manifest, logger)
        discard_unusable_import(logger)

    git_repo: str = read_custom_config(config_file, "logDataGitRepo", logger=logger)
    git_user: str = read_custom_config(config_file, "logDataGitUser", logger=logger)
    git_password: str = read_custom_config(config_file, "logDataGitPassword", logger=logger)
    branch: str = get_optional_value(config_file, "logDataGitBranch", "", logger)
    repo_url: str = f"https://{git_user}:{urllib.parse.quote(git_password, safe='')}@{git_repo}"
    if not update_git_repo(repo_url, str(repository_directory), logger, branch=branch or None, depth=depth):
        return 1
    search_directory: Path | None = get_csv_search_directory(config_file, repository_directory, logger)
    if search_directory is None:
        return 1
    csv_files: list[Path] | None = find_csv_files(search_directory, logger)
    if csv_files is None:
        return 1
    conversion: ConversionResult = convert_repository_files(csv_files, repository_directory, logger)
    write_import_file(conversion.entries, conversion.converted_files, repository_directory)
    report_conversion_summary(conversion, logger)
    return 0


def acknowledge_import(config_file: str, logger: logging.Logger) -> int:
    repository_directory: Path = Path(
        get_optional_value(config_file, "logDataGitRepoTargetDir", DEFAULT_REPOSITORY_DIRECTORY, logger)
    )
    if not MANIFEST_FILE.exists():
        logger.warning("no log data import manifest found; nothing to acknowledge")
        return 0
    manifest: dict[str, object] | None = read_usable_manifest(logger)
    valid_csv_file_names: list[str] | None = None if manifest is None else read_csv_file_names(manifest, logger)
    if manifest is None or valid_csv_file_names is None:
        # without the manifest the acknowledgement does not know what to delete. The data was
        # imported already, so the pending import is dropped and reported instead of retried
        discard_unusable_import(logger)
        return 1
    csv_files: list[Path] = [repository_directory / file_name for file_name in valid_csv_file_names]
    git_user: str = read_custom_config(config_file, "logDataGitUser", logger=logger)
    git_password: str = read_custom_config(config_file, "logDataGitPassword", logger=logger)
    if not commit_and_push_deletions(
        str(repository_directory), csv_files, COMMIT_MESSAGE, logger, git_user, git_password
    ):
        record_failed_acknowledgement(manifest)
        report_failed_acknowledgement(manifest, logger)
        return 1
    MANIFEST_FILE.unlink(missing_ok=True)
    OUTPUT_FILE.unlink(missing_ok=True)
    return 0


def main() -> int:
    parser: argparse.ArgumentParser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--config", default=DEFAULT_CONFIG_FILE, help="Customizing configuration file")
    parser.add_argument("--depth", type=parse_git_depth_arg, default=None, help="Optional Git clone depth")
    parser.add_argument(
        "--acknowledge-import", action="store_true", help="Delete and push successfully imported CSV files"
    )
    arguments: argparse.Namespace = parser.parse_args()
    config_file: str = cast("str", arguments.config)
    depth: int | None = cast("int | None", arguments.depth)
    acknowledge: bool = cast("bool", arguments.acknowledge_import)
    logger: logging.Logger = get_logger(debug_level_in=2)
    return acknowledge_import(config_file, logger) if acknowledge else import_data(config_file, depth, logger)


if __name__ == "__main__":  # pragma: no cover
    sys.exit(main())
