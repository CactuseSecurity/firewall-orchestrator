#!/usr/bin/python3
# revision history:
__version__ = "2026-04-07-01"

# breaking change: /usr/local/fworch needs to be in the python path
# just add "export PYTHONPATH="$PYTHONPATH:/usr/local/fworch/"" to /etc/environment
# 2025-11-25-01, initial version
# 2025-12-02-01, adding new fields to the interface:
#   - daysUntilFirstRecertification: int|None (if not set, we assume the same intervals as for normal recertification),
#   - recertificationActive: bool
# 2026-02-19-01:
#   - setting fields recertificationActive for all apps to true or false
#   - importing owner_lifecycle_state from csv file if column pattern is given in config; if not, owner_lifecycle_state will be set to "unknown" for all apps
#   - enhancing import of owner_responsibles from csv file (allowing for multiple columns for different levels of responsibility)
#   - importing criticality from csv file if column pattern is given in config; if not, criticality will be set to "unknown" for all apps
#   - allowing for composite fields with delimiter string to allow concatenation of two columns into one field
#   - in UI-Settings: allow passing of multiple script parameters via multiple text fields (should be limited to non-sensitive parameters, as they will be visible to all users with access to the UI-Settings)
# 2026-03-16-01:
#   - allowing multiple filter columns with per-column include values
# 2026-03-17-01:
#   - allowing csv delimiter override via cli argument
# 2026-03-20-01:
#   - generalizing fallback responsible pattern to grouped add_users_by_pattern entries
# 2026-04-07-01:
#   - adding optional additional_information import from configured owner CSV columns

# reads the main app data from multiple csv files contained in a git repo
# users will reside in external ldap groups with standardized names
# only the main responsible person per app is taken from the csv files
# here app servers will only have ip addresses (no names)

# dependencies:
#   a) package python3-git must be installed
#   b) requires the config items listed in the aprser help to be present in config file /usr/local/orch/etc/secrets/customizingConfig.json

import argparse
import logging
import re
import shlex
import sys
from pathlib import Path

import urllib3

from scripts.customizing.fwo_custom_lib.app_data_basics import (
    transform_app_list_to_dict,
    write_owners_to_json,
)
from scripts.customizing.fwo_custom_lib.app_data_models import Appip, Owner
from scripts.customizing.fwo_custom_lib.basic_helpers import (
    get_logger,
    read_custom_config,
    read_custom_config_with_default,
)
from scripts.customizing.fwo_custom_lib.git_helpers import (
    cleanup_repo_target_dir,
    parse_git_depth_arg,
    read_file_from_git_repo,
    update_git_repo,
)
from scripts.customizing.fwo_custom_lib.read_app_data_csv import (
    extract_app_data_from_csv,
    extract_ip_data_from_csv,
    parse_csv_separator_arg,
)
from scripts.customizing.fwo_custom_lib.responsibles_config import (
    parse_responsibles_columns as parse_responsibles_columns_from_lib,
)
from scripts.customizing.fwo_custom_lib.responsibles_config import (
    resolve_responsibles_columns_headers as resolve_responsibles_columns_headers_from_lib,
)

base_dir: str = "/usr/local/fworch/"
base_dir_etc: str = base_dir + "etc/"
default_config_file_name: str = base_dir_etc + "secrets/customizingConfig.json"
default_import_source_string: str = "tufinRlm"


def parse_bool_arg(value: str) -> bool:
    normalized_value: str = value.strip().lower()
    if normalized_value in ("true", "1", "yes", "y"):
        return True
    if normalized_value in ("false", "0", "no", "n"):
        return False
    raise argparse.ArgumentTypeError(f"invalid boolean value: {value}")


def parse_criticality_recert_period_mapping(mapping_entries: list[str]) -> dict[str, int]:
    mapping: dict[str, int] = {}
    for mapping_entry in mapping_entries:
        if ":" not in mapping_entry:
            raise argparse.ArgumentTypeError(
                f"invalid criticalityRecertPeriodMapping entry '{mapping_entry}', expected PREFIX:DAYS"
            )
        criticality_prefix, recert_days_str = mapping_entry.split(":", 1)
        criticality_prefix = criticality_prefix.strip()
        recert_days_str = recert_days_str.strip()
        if criticality_prefix == "" or recert_days_str == "":
            raise argparse.ArgumentTypeError(
                f"invalid criticalityRecertPeriodMapping entry '{mapping_entry}', expected PREFIX:DAYS"
            )
        try:
            recert_days: int = int(recert_days_str)
        except ValueError as err:
            raise argparse.ArgumentTypeError(
                f"invalid recertification period '{recert_days_str}' in mapping entry '{mapping_entry}'"
            ) from err
        if recert_days < 0:
            raise argparse.ArgumentTypeError(
                f"invalid recertification period '{recert_days_str}' in mapping entry '{mapping_entry}'"
            )
        mapping[criticality_prefix] = recert_days
    return mapping


def build_git_repo_url(
    repo_url_without_protocol: str | None,
    git_username: str | None,
    git_password: str | None,
    logger: logging.Logger,
    repo_purpose: str,
) -> str | None:
    if not repo_url_without_protocol:
        logger.warning("%s git repo url missing in config; skipping repository access", repo_purpose)
        return None

    normalized_repo_url: str = repo_url_without_protocol.removeprefix("https://")
    normalized_repo_url = normalized_repo_url.removeprefix("http://")
    if git_username and git_password:
        return f"https://{git_username}:{git_password}@{normalized_repo_url}"

    if git_username or git_password:
        logger.warning("%s git credentials incomplete in config; using anonymous repository access", repo_purpose)

    return f"https://{normalized_repo_url}"


def _expand_responsibles_entries(entries: list[str]) -> list[str]:
    expanded: list[str] = []
    entry_value: str
    for entry_value in entries:
        if '"' in entry_value or "'" in entry_value:
            expanded.extend(shlex.split(entry_value))
        else:
            expanded.append(entry_value)
    return expanded


def _parse_level_mapping(entry_value: str) -> tuple[str, str] | None:
    split_entry: list[str] = entry_value.split(":", 1)
    if len(split_entry) != 2:  # noqa: PLR2004
        return None
    return split_entry[0].strip(), split_entry[1].strip()


def parse_add_users_by_pattern(entries: list[str]) -> dict[str, str]:
    add_users_by_pattern: dict[str, str] = {}
    expanded_entries: list[str] = _expand_responsibles_entries(entries)
    entry: str
    for entry in expanded_entries:
        level_mapping: tuple[str, str] | None = _parse_level_mapping(entry)
        if level_mapping is None:
            raise argparse.ArgumentTypeError(f"invalid add_users_by_pattern entry '{entry}', expected LEVEL:PATTERN")
        level, pattern = level_mapping
        if level == "" or pattern == "":
            raise argparse.ArgumentTypeError(f"invalid add_users_by_pattern entry '{entry}', expected LEVEL:PATTERN")
        add_users_by_pattern[level] = pattern
    if not add_users_by_pattern:
        raise argparse.ArgumentTypeError("add_users_by_pattern must contain at least one LEVEL:PATTERN mapping")
    return add_users_by_pattern


def parse_responsibles_columns(columns_entries: list[str]) -> dict[str, tuple[str, ...]]:
    return parse_responsibles_columns_from_lib(columns_entries)


def parse_additional_information_columns(columns_entries: list[str]) -> dict[str, str]:
    additional_information_columns: dict[str, str] = {}
    expanded_entries: list[str] = _expand_responsibles_entries(columns_entries)
    entry: str
    for entry in expanded_entries:
        key_mapping: tuple[str, str] | None = _parse_level_mapping(entry)
        if key_mapping is None:
            raise argparse.ArgumentTypeError(
                f"invalid additionalInformationColumns entry '{entry}', expected KEY:HEADER"
            )
        dump_key, header_name = key_mapping
        if dump_key == "" or header_name == "":
            raise argparse.ArgumentTypeError(
                f"invalid additionalInformationColumns entry '{entry}', expected KEY:HEADER"
            )
        additional_information_columns[dump_key] = header_name
    if not additional_information_columns:
        raise argparse.ArgumentTypeError("additionalInformationColumns must contain at least one KEY:HEADER mapping")
    return additional_information_columns


def resolve_responsibles_columns_headers(
    config_file: str,
    cli_responsibles_columns: list[str] | None,
    logger: logging.Logger,
) -> dict[str, tuple[str, ...]] | None:
    return resolve_responsibles_columns_headers_from_lib(config_file, logger, cli_responsibles_columns)


def resolve_local_repo_base_dir(
    config_file: str,
    cli_local_repo_base_dir: str | None,
    logger: logging.Logger,
) -> str:
    if cli_local_repo_base_dir is not None:
        return cli_local_repo_base_dir
    return read_custom_config_with_default(config_file, "localRepoBaseDir", base_dir_etc, logger)


def apply_owner_column_overrides(
    owner_header_patterns: dict[str, str],
    lifecycle_state_column: str,
) -> dict[str, str]:
    updated_patterns: dict[str, str] = dict(owner_header_patterns)
    if lifecycle_state_column.strip():
        normalized_lifecycle_state_column: str = lifecycle_state_column.strip()
        escaped_lifecycle_state_column: str = re.escape(normalized_lifecycle_state_column)
        updated_patterns["owner_lifecycle_state"] = rf"^\s*{escaped_lifecycle_state_column}\s*$"
    return updated_patterns


def parse_included_owners_filters(
    filter_columns: list[str] | None,
    include_values_groups: list[list[str]] | None,
) -> dict[str, tuple[str, ...]] | None:
    if not filter_columns:
        return None
    if include_values_groups is None:
        include_values_groups = [["Ja"]]

    normalized_filter_columns: list[str] = [column.strip() for column in filter_columns if column.strip()]
    if len(normalized_filter_columns) == 0:
        return None
    if len(include_values_groups) == 1:
        include_values_groups = include_values_groups * len(normalized_filter_columns)
    if len(include_values_groups) != len(normalized_filter_columns):
        raise argparse.ArgumentTypeError("number of --includeValues groups must match --filterColumn occurrences")

    included_owners_filters: dict[str, tuple[str, ...]] = {}
    index: int
    filter_column: str
    for index, filter_column in enumerate(normalized_filter_columns):
        normalized_include_values: tuple[str, ...] = tuple(
            value.strip() for value in include_values_groups[index] if value.strip()
        )
        if len(normalized_include_values) == 0:
            raise argparse.ArgumentTypeError(
                f"--includeValues for filter column '{filter_column}' must contain at least one non-empty value"
            )
        included_owners_filters[filter_column] = normalized_include_values
    return included_owners_filters


def normalize_option_value_args(argv: list[str], option_names: tuple[str, ...]) -> list[str]:
    normalized_argv: list[str] = []
    option_names_set: set[str] = set(option_names)
    index: int = 0
    while index < len(argv):
        current_arg: str = argv[index]
        if current_arg in option_names_set and index + 1 < len(argv):
            normalized_argv.append(f"{current_arg}={argv[index + 1]}")
            index += 2
            continue
        normalized_argv.append(current_arg)
        index += 1
    return normalized_argv


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="Read configuration from FW management via API calls")
    parser.add_argument(
        "-c",
        "--config",
        default=default_config_file_name,
        help="Filename of custom config file for modelling imports, default file="
        + default_config_file_name
        + ',\
                        sample config file content: \
                        { \
                            "ldapPath": "dc=example,dc=de", \
                            "cmdbGitRepoUrl": "github.example.de/cmdb/app-export", \
                            "cmdbGitUsername": "git-user-1", \
                            "gitPassword": "gituser-1-pwd", \
                            "csvOwnerFilePattern": "NeMo_???_meta.csv", \
                            "csvAppServerFilePattern": "NeMo_???_IP_.*?.csv", \
                            "gitRepoOwnersWithActiveRecert": "github.example.de/FWO", \
                            "gitFileOwnersWithActiveRecert": "isolated-apps.txt", \
                            "csvSeparator": ";", \
                            "validAppIdPrefixes": ["app-", "com-"], \
                            "importSource": "tufinRlm" \
                        } \
                        ',
    )
    parser.add_argument(
        "-s",
        "--suppress_certificate_warnings",
        action="store_true",
        default=True,
        help="suppress certificate warnings",
    )
    parser.add_argument(
        "-f",
        "--import_from_folder",
        help="if set, will try to read csv files from given folder instead of git repo",
    )
    parser.add_argument(
        "--local_repo_base_dir",
        default=None,
        help="base directory for local git checkouts; defaults to config key localRepoBaseDir or /usr/local/fworch/etc/",
    )
    parser.add_argument(
        "-l",
        "--limit",
        metavar="api_limit",
        default="150",
        help="The maximal number of returned results per HTTPS Connection; default=50",
    )
    parser.add_argument(
        "--csvSeparator",
        type=parse_csv_separator_arg,
        default=None,
        help="csv delimiter used for owner and ip csv files; allowed values are ',' and ';'; defaults to config value",
    )
    parser.add_argument("-d", "--debug", default=0, help="debug level, default=0")
    parser.add_argument(
        "--defaultRecertificationActiveState",
        dest="default_recertification_active_state",
        type=parse_bool_arg,
        default=False,
        help="default recertificationActive state for owners without specific data (true|false), default=false",
    )
    parser.add_argument(
        "--filterColumn",
        dest="filter_columns",
        action="append",
        default=None,
        help='owner CSV column header used for filtering; repeat to require matches in multiple columns, default="Aktive Firewallregel"; set to empty string to disable',
    )
    parser.add_argument(
        "--includeValues",
        "--includeValue",
        dest="include_values_groups",
        action="append",
        nargs="+",
        default=None,
        help='list of values to include for the preceding --filterColumn; repeat per filter column, default=["Ja"]',
    )
    parser.add_argument(
        "--lifecycleState",
        default="Lifecycle State",
        help='owner CSV column header for lifecycle state import, default="Lifecycle State"',
    )
    parser.add_argument(
        "--fallback_owner_lifecycle",
        default="unknown",
        help='default owner lifecycle state used when no --lifecycleState column is configured, default="unknown"',
    )
    parser.add_argument(
        "--compositeIdFields",
        nargs="+",
        default=None,
        help="list of owner CSV headers used to build a composite app_id_external",
    )
    parser.add_argument(
        "--compositeIdFieldsDelimiterStr",
        default="",
        help="delimiter string used between composite id field values",
    )
    parser.add_argument(
        "--compositeIdFieldsMaxLength",
        nargs="+",
        type=int,
        default=None,
        help="list of max lengths per composite id field; values are truncated before joining",
    )
    parser.add_argument(
        "--criticalityColumnHeader",
        default=None,
        help="owner CSV header used to import criticality; if omitted, criticality is not included in output",
    )
    parser.add_argument(
        "--criticalityRecertPeriodMapping",
        nargs="+",
        default=None,
        help='list of mappings PREFIX:DAYS, e.g. "1:360 2:360 3:180"; if criticality starts with PREFIX, recert_period_days is set to DAYS',
    )
    parser.add_argument(
        "--responsiblesColumns",
        nargs="+",
        default=None,
        help='grouped mapping LEVEL:HEADER [HEADER ...]; each HEADER may be an exact column name or a regex matching zero or one CSV column, e.g. 1:"^UserID$" "^UserID Vertreter$" 2:"^UserIDs Mitwirkende$"; ambiguous matches are rejected',
    )
    parser.add_argument(
        "--additionalInformationColumns",
        nargs="+",
        default=None,
        help='grouped mapping KEY:HEADER used to import extra owner metadata from owner CSV files, e.g. cost_center:"Cost Center" owner_type:"Owner Type"',
    )
    parser.add_argument(
        "--add_users_by_pattern",
        nargs="+",
        default=None,
        help='grouped mapping LEVEL:PATTERN used to append responsibles after CSV import, e.g. 1:"ROLE_@@AppId@@" 2:"A_@@AppPrefix@@_@@AppId@@_FW_RULEMGT"',
    )
    parser.add_argument(
        "--depth",
        type=parse_git_depth_arg,
        default=None,
        help="optional git clone/pull depth; if omitted, no depth is passed to git",
    )

    normalized_argv: list[str] = normalize_option_value_args(sys.argv[1:], ("--compositeIdFieldsDelimiterStr",))
    args: argparse.Namespace = parser.parse_args(normalized_argv)

    if args.suppress_certificate_warnings:
        urllib3.disable_warnings()

    logger: logging.Logger = get_logger(debug_level_in=2)

    # read config
    ldap_path: str = read_custom_config(args.config, "ldapPath", logger)
    cmdb_git_repo_url_without_protocol: str = read_custom_config(args.config, "cmdbGitRepoUrl", logger)
    cmdb_git_username: str = read_custom_config(args.config, "cmdbGitUsername", logger)
    cmdb_git_password: str = read_custom_config(args.config, "cmdbGitPassword", logger)
    csv_owner_file_pattern: str = read_custom_config(args.config, "csvOwnerFilePattern", logger)
    csv_app_server_file_pattern: str = read_custom_config(args.config, "csvAppServerFilePattern", logger)
    recert_active_repo_url: str | None = read_custom_config_with_default(
        args.config, "gitRepoOwnersWithActiveRecert", None, logger
    )
    recert_active_file_name: str | None = read_custom_config_with_default(
        args.config, "gitFileOwnersWithActiveRecert", None, logger
    )
    owner_header_patterns: dict[str, str] = read_custom_config_with_default(
        args.config, "csvOwnerColumnPatterns", {}, logger
    )
    ip_header_patterns: dict[str, str] = read_custom_config_with_default(args.config, "csvIpColumnPatterns", {}, logger)
    import_source_string: str = read_custom_config_with_default(
        args.config, "importSource", default_import_source_string, logger
    )
    valid_app_id_prefixes: list[str] | None = read_custom_config_with_default(
        args.config, "validAppIdPrefixes", None, logger
    )
    csv_separator: str = (
        args.csvSeparator
        if args.csvSeparator is not None
        else read_custom_config_with_default(args.config, "csvSeparator", ";", logger)
    )
    if args.csvSeparator is None:
        csv_separator = parse_csv_separator_arg(csv_separator)
    default_recert_active_state: bool = args.default_recertification_active_state
    if args.filter_columns is None:
        filter_columns: list[str] = ["Aktive Firewallregel"]
    else:
        filter_columns = args.filter_columns
    try:
        included_owners_filters: dict[str, tuple[str, ...]] | None = parse_included_owners_filters(
            filter_columns,
            args.include_values_groups,
        )
    except argparse.ArgumentTypeError as err:
        error_message: str = str(err)
        parser.error(error_message)
    lifecycle_state_column: str = args.lifecycleState
    fallback_owner_lifecycle: str = args.fallback_owner_lifecycle
    local_repo_base_dir: str = resolve_local_repo_base_dir(args.config, args.local_repo_base_dir, logger)
    composite_id_fields: tuple[str, ...] | None = tuple(args.compositeIdFields) if args.compositeIdFields else None
    composite_id_fields_delimiter_str: str = args.compositeIdFieldsDelimiterStr
    composite_id_fields_max_length: list[int] | None = args.compositeIdFieldsMaxLength
    criticality_column_header: str | None = args.criticalityColumnHeader
    criticality_recert_period_mapping: dict[str, int] | None = None
    if args.criticalityRecertPeriodMapping:
        criticality_recert_period_mapping = parse_criticality_recert_period_mapping(args.criticalityRecertPeriodMapping)
    responsibles_columns_headers: dict[str, tuple[str, ...]] | None = resolve_responsibles_columns_headers(
        args.config, args.responsiblesColumns, logger
    )
    if responsibles_columns_headers is not None:
        logger.debug("resolved responsiblesColumns=%s", responsibles_columns_headers)
    additional_information_columns_headers: dict[str, str] | None = None
    if args.additionalInformationColumns:
        additional_information_columns_headers = parse_additional_information_columns(args.additionalInformationColumns)
    add_users_by_pattern: dict[str, str] | None = None
    if args.add_users_by_pattern:
        add_users_by_pattern = parse_add_users_by_pattern(args.add_users_by_pattern)
    git_depth: int | None = args.depth
    owner_header_patterns = apply_owner_column_overrides(owner_header_patterns, lifecycle_state_column)

    if args.debug:
        debug_level: int = int(args.debug)
    else:
        debug_level = 0

    #############################################
    # 1. get CSV files from github repo
    local_repo_base_path: Path = Path(local_repo_base_dir)
    local_repo_base_path.mkdir(parents=True, exist_ok=True)
    app_data_repo_target_dir: str = str(local_repo_base_path / "cmdb-repo")
    recert_repo_target_dir: str = str(local_repo_base_path / "recert-repo")

    import_from_folder: str | None = args.import_from_folder
    try:
        if import_from_folder:
            base_dir = import_from_folder
            app_data_repo_target_dir = import_from_folder
        else:
            base_dir = app_data_repo_target_dir
            app_data_repo_url: str | None = build_git_repo_url(
                cmdb_git_repo_url_without_protocol,
                cmdb_git_username,
                cmdb_git_password,
                logger,
                "CMDB",
            )
            repo_updated: bool = False
            if app_data_repo_url:
                repo_updated = update_git_repo(app_data_repo_url, app_data_repo_target_dir, logger, depth=git_depth)
            if not repo_updated:
                logger.warning("trying to read csv files from folder given as parameter...")

        #############################################
        # 2. get app list with activated recertification

        if recert_active_repo_url and recert_active_file_name:
            recert_repo_url: str | None = build_git_repo_url(
                recert_active_repo_url,
                cmdb_git_username,
                cmdb_git_password,
                logger,
                "recertification activation",
            )
            recert_activation_data: str | None = None
            if recert_repo_url:
                recert_activation_data = read_file_from_git_repo(
                    recert_repo_url,
                    recert_repo_target_dir,
                    recert_active_file_name,
                    logger,
                    depth=git_depth,
                )
            recert_activation_lines: list[str] = []
            if recert_activation_data:
                recert_activation_lines = recert_activation_data.splitlines()
            recert_active_app_list = recert_activation_lines
            logger.info("found %s apps with active recertification", len(recert_active_app_list))
        else:
            recert_active_app_list: list[str] = []
            logger.info(
                "no recertification activation source configured; skipping activation of recertification import"
            )

        #############################################
        # 3. get app data from CSV files
        app_list: list[Owner] = []
        re_owner_file_pattern: re.Pattern[str] = re.compile(csv_owner_file_pattern)
        for file_path in Path(app_data_repo_target_dir).iterdir():
            if re_owner_file_pattern.match(file_path.name):
                extract_app_data_from_csv(
                    file_path.name,
                    app_list,
                    ldap_path,
                    import_source_string,
                    Owner,
                    logger,
                    debug_level,
                    base_dir=base_dir,
                    recert_active_app_list=recert_active_app_list,
                    default_recert_active_state=default_recert_active_state,
                    column_patterns=owner_header_patterns,
                    valid_app_id_prefixes=valid_app_id_prefixes,
                    included_owners_filters=included_owners_filters,
                    csv_separator=csv_separator,
                    fallback_owner_lifecycle=fallback_owner_lifecycle,
                    composite_id_fields=composite_id_fields,
                    composite_id_fields_delimiter_str=composite_id_fields_delimiter_str,
                    composite_id_fields_max_length=composite_id_fields_max_length,
                    criticality_column_header=criticality_column_header,
                    criticality_recert_period_mapping=criticality_recert_period_mapping,
                    responsibles_columns_headers=responsibles_columns_headers,
                    additional_information_columns_headers=additional_information_columns_headers,
                    add_users_by_pattern=add_users_by_pattern,
                )

        app_dict: dict[str, Owner] = transform_app_list_to_dict(app_list)

        re_app_server_file_pattern: re.Pattern[str] = re.compile(csv_app_server_file_pattern)
        for file_path in Path(app_data_repo_target_dir).iterdir():
            if re_app_server_file_pattern.match(file_path.name):
                if debug_level > 0:
                    logger.info("importing IP data from file %s ...", file_path.name)
                extract_ip_data_from_csv(
                    file_path.name,
                    app_dict,
                    Appip,
                    logger,
                    debug_level,
                    base_dir=base_dir,
                    column_patterns=ip_header_patterns,
                    csv_separator=csv_separator,
                )

        #############################################
        # 4. write owners to json file using the same and basename path as this script, just replacing .py with .json
        write_owners_to_json(app_dict, __file__, logger=logger)

        #############################################
        # 5. Some statistics
        if debug_level > 0:
            logger.info("total #apps: %s", len(app_dict))
            apps_with_ip: int = 0
            for owner in app_dict.values():
                apps_with_ip += 1 if len(owner.app_servers) > 0 else 0
            logger.info("#apps with ip addresses: %s", apps_with_ip)
            total_ips: int = 0
            for owner in app_dict.values():
                total_ips += len(owner.app_servers)
            logger.info("#ip addresses in total: %s", total_ips)
    finally:
        if import_from_folder is None:
            cleanup_repo_target_dir(app_data_repo_target_dir)
