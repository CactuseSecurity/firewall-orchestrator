import csv
import logging
import re
from dataclasses import dataclass
from typing import Any

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
    composite_id_columns: tuple[int, ...] | None
    composite_id_delimiter: str
    composite_id_max_lengths: tuple[int, ...] | None
    app_owner_tiso_column: int
    app_owner_kwita_column: int
    owner_lifecycle_state_column: int
    fallback_owner_lifecycle: str
    criticality_column: int
    criticality_recert_period_mapping: dict[str, int] | None
    responsibles_columns: dict[str, tuple[int, ...]] | None
    level_two_responsible_pattern: str | None
    included_owners_column_no: int
    include_values: list[str] | None
    ldap_path: str
    import_source_string: str
    owner_cls: type[Owner]
    valid_app_id_prefixes: list[str]
    logger: logging.Logger
    debug_level: int


@dataclass(frozen=True)
class ExtractAppDataCsvOptions:
    base_dir: str = "."
    recert_active_app_list: list[str] | None = None
    default_recert_active_state: bool = False
    column_patterns: dict[str, str] | None = None
    valid_app_id_prefixes: list[str] | None = None
    included_owners_column: str | None = None
    include_values: list[str] | None = None
    csv_separator: str = ","
    composite_id_fields: tuple[str, ...] | None = None
    composite_id_fields_delimiter_str: str = ""
    composite_id_fields_max_length: list[int] | None = None
    fallback_owner_lifecycle: str = "unknown"
    criticality_column_header: str | None = None
    criticality_recert_period_mapping: dict[str, int] | None = None
    responsibles_columns_headers: dict[str, tuple[str, ...]] | None = None
    level_two_responsible_pattern: str | None = None


def _resolve_extract_options(
    options: ExtractAppDataCsvOptions | None,
    legacy_kwargs: dict[str, Any],
) -> ExtractAppDataCsvOptions:
    if options is None and not legacy_kwargs:
        return ExtractAppDataCsvOptions()

    valid_keys: set[str] = set(ExtractAppDataCsvOptions.__annotations__.keys())
    invalid_keys: set[str] = set(legacy_kwargs) - valid_keys
    if invalid_keys:
        invalid_keys_list: str = ", ".join(sorted(invalid_keys))
        raise TypeError(f"unknown extract_app_data_from_csv options: {invalid_keys_list}")

    if options is None:
        return ExtractAppDataCsvOptions(**legacy_kwargs)

    merged_kwargs: dict[str, Any] = {field_name: getattr(options, field_name) for field_name in valid_keys}
    merged_kwargs.update(legacy_kwargs)
    return ExtractAppDataCsvOptions(**merged_kwargs)


def _get_composite_id_max_lengths(
    options: ExtractAppDataCsvOptions,
    csv_file: str,
    logger: logging.Logger,
) -> tuple[int, ...] | None:
    if options.composite_id_fields_max_length is None:
        return None
    if options.composite_id_fields is None:
        logger.warning(
            "ignoring compositeIdFieldsMaxLength because compositeIdFields is not configured for %s",
            csv_file,
        )
        return None
    if len(options.composite_id_fields_max_length) != len(options.composite_id_fields):
        logger.warning(
            "skipping csv file %s because compositeIdFields and compositeIdFieldsMaxLength count differ",
            csv_file,
        )
        return None
    if any(value < 0 for value in options.composite_id_fields_max_length):
        logger.warning(
            "skipping csv file %s because compositeIdFieldsMaxLength contains negative values",
            csv_file,
        )
        return None
    return tuple(options.composite_id_fields_max_length)


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


def _build_app_id(line: list[str], context: OwnerLineParserContext) -> str:
    if context.composite_id_columns:
        composite_values: list[str] = []
        for index, column in enumerate(context.composite_id_columns):
            value: str = line[column].strip() if len(line) > column else ""
            if context.composite_id_max_lengths is not None:
                value = value[: context.composite_id_max_lengths[index]]
            composite_values.append(value)
        return context.composite_id_delimiter.join(composite_values)
    if context.app_id_column < 0 or len(line) <= context.app_id_column:
        return ""
    return line[context.app_id_column].strip()


def _get_recert_period_days(line: list[str], kwita_column: int) -> int:
    kwita: str = line[kwita_column] if kwita_column >= 0 else ""
    return 365 if kwita == "" or kwita.lower() == "false" else 182


def _get_owner_lifecycle_state(
    line: list[str], owner_lifecycle_state_column: int, fallback_owner_lifecycle: str
) -> str:
    if owner_lifecycle_state_column < 0:
        return fallback_owner_lifecycle
    owner_lifecycle_state: str = (
        line[owner_lifecycle_state_column].strip() if len(line) > owner_lifecycle_state_column else ""
    )
    return owner_lifecycle_state or fallback_owner_lifecycle


def _get_criticality(line: list[str], criticality_column: int) -> str:
    if criticality_column < 0:
        return ""
    return line[criticality_column].strip() if len(line) > criticality_column else ""


def _get_responsibles(line: list[str], responsibles_columns: dict[str, tuple[int, ...]] | None) -> dict[str, list[str]]:
    if not responsibles_columns:
        return {}
    responsibles: dict[str, list[str]] = {}
    responsible_level: str
    column_indexes: tuple[int, ...]
    for responsible_level, column_indexes in responsibles_columns.items():
        responsibles[responsible_level] = [
            line[column_index].strip() if len(line) > column_index else "" for column_index in column_indexes
        ]
    return responsibles


def _split_app_id_external(app_id_external: str) -> tuple[str, str]:
    if "-" in app_id_external:
        app_prefix, app_id = app_id_external.split("-", 1)
        return app_prefix, app_id
    if "_" in app_id_external:
        app_prefix, app_id = app_id_external.split("_", 1)
        return app_prefix, app_id
    prefix_end: int = 0
    while prefix_end < len(app_id_external) and app_id_external[prefix_end].isalpha():
        prefix_end += 1
    if 0 < prefix_end < len(app_id_external):
        app_prefix = app_id_external[:prefix_end]
        app_id = app_id_external[prefix_end:].lstrip("-_")
        if app_id != "":
            return app_prefix, app_id
    return app_id_external, app_id_external


def _build_level_two_responsible(app_id_external: str, level_two_responsible_pattern: str) -> str:
    app_prefix, app_id = _split_app_id_external(app_id_external)
    return level_two_responsible_pattern.replace("@@AppPrefix@@", app_prefix).replace("@@AppId@@", app_id)


def _build_default_responsibles(
    app_id_external: str,
    main_user_dn: str,
    level_two_responsible_pattern: str,
) -> dict[str, list[str]]:
    return {
        "1": [main_user_dn] if main_user_dn else [],
        "2": [_build_level_two_responsible(app_id_external, level_two_responsible_pattern)],
    }


def _build_responsibles_dns(
    responsibles: dict[str, list[str]],
    ldap_path: str,
    logger: logging.Logger,
) -> dict[str, list[str]]:
    responsibles_dns: dict[str, list[str]] = {}
    responsible_level: str
    user_ids: list[str]
    for responsible_level, user_ids in responsibles.items():
        dns: list[str] = []
        raw_user_value: str
        for raw_user_value in user_ids:
            split_user_ids: list[str] = [
                user_id.strip() for user_id in re.split(r"[,\n;]+", raw_user_value) if user_id.strip()
            ]
            if len(split_user_ids) == 0 and raw_user_value.strip():
                split_user_ids = [raw_user_value.strip()]
            user_id: str
            for user_id in split_user_ids:
                dn: str = build_dn(user_id, ldap_path, logger)
                if dn:
                    dns.append(dn)
        responsibles_dns[responsible_level] = dns
    return responsibles_dns


def _get_recert_period_days_for_criticality(
    criticality: str,
    criticality_recert_period_mapping: dict[str, int] | None,
) -> int | None:
    if not criticality_recert_period_mapping:
        return None
    for criticality_prefix, period_days in criticality_recert_period_mapping.items():
        if criticality.startswith(criticality_prefix):
            return period_days
    return None


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


def _find_required_header_index_by_name(
    headers: list[str],
    header_name: str,
    csv_file_name: str,
    logger: logging.Logger,
) -> int:
    normalized_target: str = header_name.strip().casefold()
    for i, header in enumerate(headers):
        if header.strip().casefold() == normalized_target:
            return i
    logger.error(
        "missing required composite id header %s in %s; headers=%s",
        header_name,
        csv_file_name,
        headers,
    )
    raise ValueError(f"missing required composite id header {header_name}")


def _find_composite_id_columns(
    headers: list[str],
    composite_id_fields: tuple[str, ...] | None,
    csv_file_name: str,
    logger: logging.Logger,
) -> tuple[int, ...] | None:
    if not composite_id_fields:
        return None
    return tuple(
        _find_required_header_index_by_name(headers, header_name, csv_file_name, logger)
        for header_name in composite_id_fields
    )


def _find_responsibles_columns(
    headers: list[str],
    responsibles_columns_headers: dict[str, tuple[str, ...]] | None,
    csv_file_name: str,
    logger: logging.Logger,
) -> dict[str, tuple[int, ...]] | None:
    if not responsibles_columns_headers:
        return None
    responsibles_columns: dict[str, tuple[int, ...]] = {}
    responsible_level: str
    responsible_headers: tuple[str, ...]
    for responsible_level, responsible_headers in responsibles_columns_headers.items():
        responsibles_columns[responsible_level] = tuple(
            _find_required_header_index_by_name(headers, header_name, csv_file_name, logger)
            for header_name in responsible_headers
        )
    return responsibles_columns


def read_app_data_from_csv(
    csv_file_name: str,
    logger: logging.Logger,
    column_patterns: dict[str, str] | None = None,
    included_owners_column: str | None = None,
    csv_separator: str = ",",
    composite_id_fields: tuple[str, ...] | None = None,
    criticality_column_header: str | None = None,
    responsibles_columns_headers: dict[str, tuple[str, ...]] | None = None,
) -> (
    tuple[list[list[str]], int, int, tuple[int, ...] | None, int, int, int, int, dict[str, tuple[int, ...]] | None, int]
    | None
):
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
            app_id_column: int = _find_header_index(
                headers, app_id_pattern, "app_id", csv_file_name, logger, required=composite_id_fields is None
            )
            composite_id_columns: tuple[int, ...] | None = _find_composite_id_columns(
                headers, composite_id_fields, csv_file_name, logger
            )
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
            criticality_column: int = -1
            if criticality_column_header:
                criticality_column = _find_required_header_index_by_name(
                    headers, criticality_column_header, csv_file_name, logger
                )
            responsibles_columns: dict[str, tuple[int, ...]] | None = _find_responsibles_columns(
                headers, responsibles_columns_headers, csv_file_name, logger
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
        composite_id_columns,
        app_owner_tiso_column,
        app_owner_kwita_column,
        owner_lifecycle_state_column,
        criticality_column,
        responsibles_columns,
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

    app_id: str = _build_app_id(line, context)
    if app_id == "":
        if context.debug_level > 1:
            context.logger.info("ignoring line from csv file without app_id value")
        return count_skips + 1
    if not _has_valid_app_id_prefix(app_id, context):
        if context.debug_level > 1:
            context.logger.info("ignoring line from csv file: %s - inconclusive appId", app_id)
        return count_skips + 1

    app_name: str = line[context.app_name_column]
    app_main_user: str = line[context.app_owner_tiso_column]
    main_user_dn: str = build_dn(app_main_user, context.ldap_path, context.logger)
    owner_lifecycle_state: str = _get_owner_lifecycle_state(
        line, context.owner_lifecycle_state_column, context.fallback_owner_lifecycle
    )
    criticality: str = _get_criticality(line, context.criticality_column)
    responsibles: dict[str, list[str]]
    if context.responsibles_columns is not None:
        responsibles = _build_responsibles_dns(
            _get_responsibles(line, context.responsibles_columns),
            context.ldap_path,
            context.logger,
        )
    elif context.level_two_responsible_pattern is not None:
        responsibles = _build_default_responsibles(app_id, main_user_dn, context.level_two_responsible_pattern)
    else:
        responsibles = {}
    recert_period_days: int = _get_recert_period_days(line, context.app_owner_kwita_column)
    mapped_recert_period_days: int | None = _get_recert_period_days_for_criticality(
        criticality, context.criticality_recert_period_mapping
    )
    if mapped_recert_period_days is not None:
        recert_period_days = mapped_recert_period_days
    if main_user_dn == "" and context.debug_level > 0:
        context.logger.warning("adding app without main user: %s", app_id)
    criticality_value: str | None = criticality if context.criticality_column >= 0 else None
    responsibles_value: dict[str, list[str]] | None = responsibles or None
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
            criticality=criticality_value,
            responsibles=responsibles_value,
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
    options: ExtractAppDataCsvOptions | None = None,
    **legacy_kwargs: Any,
) -> None:
    resolved_options: ExtractAppDataCsvOptions = _resolve_extract_options(options, legacy_kwargs)
    valid_app_id_prefixes: list[str] = resolved_options.valid_app_id_prefixes or DEFAULT_VALID_APP_ID_PREFIXES
    recert_active_app_list: list[str] = resolved_options.recert_active_app_list or []

    composite_id_max_lengths: tuple[int, ...] | None = _get_composite_id_max_lengths(resolved_options, csv_file, logger)
    if (
        resolved_options.composite_id_fields_max_length is not None
        and resolved_options.composite_id_fields is not None
        and composite_id_max_lengths is None
    ):
        return

    csv_file_path: str = resolved_options.base_dir + "/" + csv_file  # add directory to csv files

    csv_data: (
        tuple[
            list[list[str]],
            int,
            int,
            tuple[int, ...] | None,
            int,
            int,
            int,
            int,
            dict[str, tuple[int, ...]] | None,
            int,
        ]
        | None
    ) = read_app_data_from_csv(
        csv_file_path,
        logger,
        resolved_options.column_patterns,
        resolved_options.included_owners_column,
        resolved_options.csv_separator,
        resolved_options.composite_id_fields,
        resolved_options.criticality_column_header,
        resolved_options.responsibles_columns_headers,
    )
    if csv_data is None:
        return

    (
        apps_from_csv,
        app_name_column,
        app_id_column,
        composite_id_columns,
        app_owner_tiso_column,
        app_owner_kwita_column,
        owner_lifecycle_state_column,
        criticality_column,
        responsibles_columns,
        included_owners_column_no,
    ) = csv_data
    parser_context: OwnerLineParserContext = OwnerLineParserContext(
        app_name_column=app_name_column,
        app_id_column=app_id_column,
        composite_id_columns=composite_id_columns,
        composite_id_delimiter=resolved_options.composite_id_fields_delimiter_str,
        composite_id_max_lengths=composite_id_max_lengths,
        app_owner_tiso_column=app_owner_tiso_column,
        app_owner_kwita_column=app_owner_kwita_column,
        owner_lifecycle_state_column=owner_lifecycle_state_column,
        fallback_owner_lifecycle=resolved_options.fallback_owner_lifecycle,
        criticality_column=criticality_column,
        criticality_recert_period_mapping=resolved_options.criticality_recert_period_mapping,
        responsibles_columns=responsibles_columns,
        level_two_responsible_pattern=resolved_options.level_two_responsible_pattern,
        included_owners_column_no=included_owners_column_no,
        include_values=resolved_options.include_values,
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
        app.recert_active = resolved_options.default_recert_active_state
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
