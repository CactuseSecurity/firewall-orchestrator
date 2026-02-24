import csv
import logging
import re
from dataclasses import dataclass

from netaddr import IPAddress, IPNetwork

from scripts.customizing.fwo_custom_lib.app_data_models import Appip, Owner

DEFAULT_VALID_APP_ID_PREFIXES: list[str] = []
DEFAULT_OWNER_HEADER_PATTERNS: dict[str, str] = {
    "name": r".*?:\s*Name",
    "app_id": r".*?:\s*Alfabet-ID$",
    "owner_tiso": r".*?:\s*TISO",
    "owner_kwita": r".*?:\s*kwITA",
    "owner_lifecycle_state": r"^\s*Lifecycle State\s*$",
}
DEFAULT_IP_HEADER_PATTERNS: dict[str, str] = {"app_id": r".*?:\s*Alfabet-ID$", "ip": r".*?:\s*IP"}


@dataclass(frozen=True)
class OwnerLineParserContext:
    app_name_column: int
    app_id_column: int
    app_owner_tiso_column: int
    app_owner_kwita_column: int
    owner_lifecycle_state_column: int
    included_owners_column_no: int
    include_values: list[str] | None
    ldap_path: str
    import_source_string: str
    owner_cls: type[Owner]
    valid_app_id_prefixes: list[str]
    logger: logging.Logger
    debug_level: int


def _is_included_owners_match(line: list[str], context: OwnerLineParserContext) -> bool:
    if context.included_owners_column_no < 0:
        return True
    row_value: str = (
        line[context.included_owners_column_no].strip() if len(line) > context.included_owners_column_no else ""
    )
    allowed_values: set[str] = {value.strip().casefold() for value in (context.include_values or []) if value.strip()}
    if row_value.casefold() in allowed_values:
        return True
    if context.debug_level > 1:
        context.logger.debug(
            "ignoring line from csv file as included owners value does not match: found '%s', expected one of %s'",
            row_value,
            sorted(allowed_values),
        )
    return False


def _has_valid_app_id_prefix(app_id: str, context: OwnerLineParserContext) -> bool:
    return len(context.valid_app_id_prefixes) == 0 or app_id.lower().startswith(tuple(context.valid_app_id_prefixes))


def _get_recert_period_days(line: list[str], kwita_column: int) -> int:
    kwita: str = line[kwita_column] if kwita_column >= 0 else ""
    return 365 if kwita == "" or kwita.lower() == "false" else 182


def _get_owner_lifecycle_state(line: list[str], owner_lifecycle_state_column: int) -> str:
    if owner_lifecycle_state_column < 0:
        return "unknown"
    owner_lifecycle_state: str = (
        line[owner_lifecycle_state_column].strip() if len(line) > owner_lifecycle_state_column else ""
    )
    return owner_lifecycle_state if owner_lifecycle_state else "unknown"


def build_dn(user_id: str, ldap_path: str, logger: logging.Logger) -> str:
    dn: str = ""
    if len(user_id) > 0:
        if "{USERID}" in ldap_path:
            dn = ldap_path.replace("{USERID}", user_id)
        else:
            logger.error("could not find {USERID} parameter in ldap_path %s", ldap_path)
    return dn


def _normalize_headers(headers: list[str]) -> list[str]:
    # Strip whitespace and BOM to make header matching resilient.
    return [h.strip().lstrip("\ufeff") for h in headers]


def _find_header_index(
    headers: list[str],
    pattern: re.Pattern[str],
    column_name: str,
    csv_file_name: str,
    logger: logging.Logger,
    required: bool = True,
) -> int:
    for i, header in enumerate(headers):
        if pattern.search(header):
            return i
    if not required:
        return -1
    logger.error(
        "missing required column %s in %s; headers=%s; pattern=%s",
        column_name,
        csv_file_name,
        headers,
        pattern.pattern,
    )
    raise ValueError(f"missing required column {column_name}")


def read_app_data_from_csv(
    csv_file_name: str,
    logger: logging.Logger,
    column_patterns: dict[str, str] | None = None,
    included_owners_column: str | None = None,
    csv_separator: str = ",",
) -> tuple[list[list[str]], int, int, int, int, int, int] | None:
    try:
        header_patterns: dict[str, str] = {**DEFAULT_OWNER_HEADER_PATTERNS, **(column_patterns or {})}
        with open(csv_file_name, newline="", encoding="utf-8") as csv_file_handle:
            reader = csv.reader(csv_file_handle, delimiter=csv_separator)
            headers: list[str] = _normalize_headers(next(reader))  # Get header row first

            name_pattern: re.Pattern[str] = re.compile(header_patterns["name"], re.IGNORECASE)
            app_id_pattern: re.Pattern[str] = re.compile(header_patterns["app_id"], re.IGNORECASE)
            owner_tiso_pattern: re.Pattern[str] = re.compile(header_patterns["owner_tiso"], re.IGNORECASE)
            owner_kwita_pattern: re.Pattern[str] = re.compile(header_patterns["owner_kwita"], re.IGNORECASE)
            owner_lifecycle_state_pattern: re.Pattern[str] = re.compile(
                header_patterns["owner_lifecycle_state"], re.IGNORECASE
            )

            app_name_column: int = _find_header_index(headers, name_pattern, "name", csv_file_name, logger)
            app_id_column: int = _find_header_index(headers, app_id_pattern, "app_id", csv_file_name, logger)
            app_owner_tiso_column: int = _find_header_index(
                headers, owner_tiso_pattern, "owner_tiso", csv_file_name, logger
            )
            app_owner_kwita_column: int = _find_header_index(
                headers, owner_kwita_pattern, "owner_kwita", csv_file_name, logger, required=False
            )
            owner_lifecycle_state_column: int = _find_header_index(
                headers,
                owner_lifecycle_state_pattern,
                "owner_lifecycle_state",
                csv_file_name,
                logger,
                required=False,
            )
            included_owners_column_no: int = -1
            if included_owners_column:
                escaped_included_owners_column: str = re.escape(included_owners_column)
                included_owners_pattern: re.Pattern[str] = re.compile(
                    rf"^\s*{escaped_included_owners_column}\s*$", re.IGNORECASE
                )
                included_owners_column_no = _find_header_index(
                    headers, included_owners_pattern, "included_owners", csv_file_name, logger, required=False
                )
                if included_owners_column_no < 0:
                    logger.warning(
                        "optional filter column '%s' not found in %s; proceeding without included owners filtering",
                        included_owners_column,
                        csv_file_name,
                    )

            apps_from_csv: list[list[str]] = list(reader)  # Read remaining rows
    except ValueError as err:
        logger.warning("skipping csv file %s because %s", csv_file_name, err)
        return None
    except Exception:
        logger.exception("error while trying to read csv file %s", csv_file_name)
        return None

    return (
        apps_from_csv,
        app_name_column,
        app_id_column,
        app_owner_tiso_column,
        app_owner_kwita_column,
        owner_lifecycle_state_column,
        included_owners_column_no,
    )


def parse_app_line(
    line: list[str],
    context: OwnerLineParserContext,
    count_skips: int,
    app_list: list[Owner],
) -> int:
    if not _is_included_owners_match(line, context):
        return count_skips + 1

    app_id: str = line[context.app_id_column]
    if not _has_valid_app_id_prefix(app_id, context):
        if context.debug_level > 1:
            context.logger.info("ignoring line from csv file: %s - inconclusive appId", app_id)
        return count_skips + 1

    app_name: str = line[context.app_name_column]
    app_main_user: str = line[context.app_owner_tiso_column]
    main_user_dn: str = build_dn(app_main_user, context.ldap_path, context.logger)
    recert_period_days: int = _get_recert_period_days(line, context.app_owner_kwita_column)
    owner_lifecycle_state: str = _get_owner_lifecycle_state(line, context.owner_lifecycle_state_column)
    if main_user_dn == "" and context.debug_level > 0:
        context.logger.warning("adding app without main user: %s", app_id)
    app_list.append(
        context.owner_cls(
            app_id_external=app_id,
            name=app_name,
            main_user=main_user_dn,
            recert_period_days=recert_period_days,
            days_until_first_recert=recert_period_days,
            recert_active=False,
            import_source=context.import_source_string,
            owner_lifecycle_state=owner_lifecycle_state,
        )
    )
    return count_skips


def extract_app_data_from_csv(
    csv_file: str,
    app_list: list[Owner],
    ldap_path: str,
    import_source_string: str,
    owner_cls: type[Owner],
    logger: logging.Logger,
    debug_level: int,
    base_dir: str = ".",
    recert_active_app_list: list[str] | None = None,
    default_recert_active_state: bool = False,
    column_patterns: dict[str, str] | None = None,
    valid_app_id_prefixes: list[str] | None = None,
    included_owners_column: str | None = None,
    include_values: list[str] | None = None,
    csv_separator: str = ",",
) -> None:
    if recert_active_app_list is None:
        recert_active_app_list = []
    if valid_app_id_prefixes is None:
        valid_app_id_prefixes = DEFAULT_VALID_APP_ID_PREFIXES

    csv_file_path: str = base_dir + "/" + csv_file  # add directory to csv files

    csv_data: tuple[list[list[str]], int, int, int, int, int, int] | None = read_app_data_from_csv(
        csv_file_path,
        logger,
        column_patterns,
        included_owners_column,
        csv_separator,
    )
    if csv_data is None:
        return

    (
        apps_from_csv,
        app_name_column,
        app_id_column,
        app_owner_tiso_column,
        app_owner_kwita_column,
        owner_lifecycle_state_column,
        included_owners_column_no,
    ) = csv_data
    parser_context: OwnerLineParserContext = OwnerLineParserContext(
        app_name_column=app_name_column,
        app_id_column=app_id_column,
        app_owner_tiso_column=app_owner_tiso_column,
        app_owner_kwita_column=app_owner_kwita_column,
        owner_lifecycle_state_column=owner_lifecycle_state_column,
        included_owners_column_no=included_owners_column_no,
        include_values=include_values,
        ldap_path=ldap_path,
        import_source_string=import_source_string,
        owner_cls=owner_cls,
        valid_app_id_prefixes=valid_app_id_prefixes,
        logger=logger,
        debug_level=debug_level,
    )

    count_skips: int = 0
    # append all owners from CSV
    for line in apps_from_csv:
        count_skips = parse_app_line(line, parser_context, count_skips, app_list)
    if debug_level > 0:
        logger.info("%s: #total lines %s, skipped: %s", csv_file_path, len(apps_from_csv), count_skips)

    recert_active_app_set: set[str] = set(recert_active_app_list)
    for app in app_list:
        app.recert_active = default_recert_active_state
        if app.app_id_external in recert_active_app_set:
            app.recert_active = True
            # Set initial recertification to standard period of days.
            app.days_until_first_recert = app.recert_period_days


def read_ip_data_from_csv(
    csv_filename: str,
    logger: logging.Logger,
    column_patterns: dict[str, str] | None = None,
    csv_separator: str = ",",
) -> tuple[list[list[str]], int, int] | None:
    try:
        header_patterns: dict[str, str] = {**DEFAULT_IP_HEADER_PATTERNS, **(column_patterns or {})}
        with open(csv_filename, newline="", encoding="utf-8") as csv_file:
            reader = csv.reader(csv_file, delimiter=csv_separator)
            headers: list[str] = _normalize_headers(next(reader))  # Get header row first

            app_id_pattern: re.Pattern[str] = re.compile(header_patterns["app_id"], re.IGNORECASE)
            ip_pattern: re.Pattern[str] = re.compile(header_patterns["ip"], re.IGNORECASE)

            app_id_column_no: int = _find_header_index(headers, app_id_pattern, "app_id", csv_filename, logger)
            ip_column_no: int = _find_header_index(headers, ip_pattern, "ip", csv_filename, logger)

            ip_data: list[list[str]] = list(reader)  # Read remaining rows
    except ValueError as err:
        logger.warning("skipping csv file %s because %s", csv_filename, err)
        return None
    except Exception:
        logger.exception("error while trying to read csv file %s", csv_filename)
        return None

    return ip_data, app_id_column_no, ip_column_no


def parse_ip(
    line: list[str],
    app_id: str,
    ip_column_no: int,
    app_dict: dict[str, Owner],
    count_skips: int,
    app_ip_cls: type[Appip],
    logger: logging.Logger,
    debug_level: int,
) -> int:
    app_server_ip_string: str = line[ip_column_no]
    if app_server_ip_string != "":
        try:
            ip_range: IPNetwork = IPNetwork(app_server_ip_string)
        except Exception:
            if debug_level > 1:
                logger.warning(
                    "error parsing IP/network %s for app %s, skipping this entry",
                    app_server_ip_string,
                    app_id,
                )
            count_skips += 1
            return count_skips
        if ip_range.size > 1:
            ip_type: str = "network"
        else:
            ip_type = "host"

        ip_start: IPAddress = IPAddress(ip_range.first)
        ip_end: IPAddress = IPAddress(ip_range.last)
        ip_obj_name: str = f"{ip_type}_{app_server_ip_string}".replace("/", "_")
        app_server_ip: Appip = app_ip_cls(
            app_id_external=app_id, ip_start=ip_start, ip_end=ip_end, ip_type=ip_type, name=ip_obj_name
        )
        if app_server_ip not in app_dict[app_id].app_servers:
            app_dict[app_id].app_servers.append(app_server_ip)
    else:
        count_skips += 1

    return count_skips


def parse_single_ip_line(
    line: list[str],
    app_id_column_no: int,
    ip_column_no: int,
    app_dict: dict[str, Owner],
    valid_app_id_prefixes: list[str],
    app_ip_cls: type[Appip],
    logger: logging.Logger,
    debug_level: int,
) -> int:
    count_skips: int = 0
    if len(line) - 1 < app_id_column_no:
        return 1

    app_id: str = line[app_id_column_no]
    app_id_prefix: str = app_id.split("-", maxsplit=1)[0].lower() + "-"
    # TODO: deal with apps without prefix'

    if len(valid_app_id_prefixes) == 0 or app_id_prefix in valid_app_id_prefixes:
        if app_id in app_dict:
            count_skips = parse_ip(line, app_id, ip_column_no, app_dict, count_skips, app_ip_cls, logger, debug_level)
        else:
            if debug_level > 1:
                logger.debug(
                    "ignoring line from csv file as the app_id is not part of the app_list: %s inactive?",
                    app_id,
                )
            return 1
    else:
        if debug_level > 1:
            logger.info("ignoring line from csv file: %s - inconclusive appId", app_id)
        return 1
    return count_skips


def extract_ip_data_from_csv(
    csv_filename: str,
    app_dict: dict[str, Owner],
    app_ip_cls: type[Appip],
    logger: logging.Logger,
    debug_level: int,
    base_dir: str,
    valid_app_id_prefixes: list[str] | None = None,
    column_patterns: dict[str, str] | None = None,
    csv_separator: str = ",",
) -> None:
    if valid_app_id_prefixes is None:
        valid_app_id_prefixes = DEFAULT_VALID_APP_ID_PREFIXES

    csv_file_path: str = base_dir + "/" + csv_filename  # add directory to csv files

    ip_data_result: tuple[list[list[str]], int, int] | None = read_ip_data_from_csv(
        csv_file_path, logger, column_patterns, csv_separator
    )
    if ip_data_result is None:
        return
    ip_data, app_id_column_no, ip_column_no = ip_data_result

    count_skips: int = 0
    for line in ip_data:
        count_skips += parse_single_ip_line(
            line, app_id_column_no, ip_column_no, app_dict, valid_app_id_prefixes, app_ip_cls, logger, debug_level
        )
    if debug_level > 0:
        logger.info("%s: #total lines %s, skipped: %s", csv_file_path, len(ip_data), count_skips)
