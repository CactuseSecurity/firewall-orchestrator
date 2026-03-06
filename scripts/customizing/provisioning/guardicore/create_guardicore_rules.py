#!/usr/bin/python3
"""Create Guardicore policy rules from FWO active connections."""

from __future__ import annotations

import argparse
import json
import logging
import re
import sys
from collections import Counter
from dataclasses import dataclass
from typing import Any, cast

import requests

try:
    from scripts.customizing.provisioning.guardicore.guardicore_lib import (
        HTTP_CONTENT_TYPE_JSON,
        FwoConfig,
        GuardicoreConfig,
        apply_ssl_settings,
        extract_label_items,
        login_fwo,
        login_guardicore,
        resolve_ssl_verification_settings,
        run_graphql_query,
    )
except ModuleNotFoundError:
    from guardicore_lib import (  # type: ignore[import-not-found]
        HTTP_CONTENT_TYPE_JSON,
        FwoConfig,
        GuardicoreConfig,
        apply_ssl_settings,
        extract_label_items,
        login_fwo,
        login_guardicore,
        resolve_ssl_verification_settings,
        run_graphql_query,
    )

DEFAULT_GUARDICORE_API_V4_BASE_ENDPOINT: str = "/api/v4.0/"
DEFAULT_GUARDICORE_LABELS_LIST_ENDPOINT: str = f"{DEFAULT_GUARDICORE_API_V4_BASE_ENDPOINT}labels"
DEFAULT_GUARDICORE_RULES_CREATE_ENDPOINT: str = f"{DEFAULT_GUARDICORE_API_V4_BASE_ENDPOINT}visibility/policy/rules"
DEFAULT_GUARDICORE_REVISIONS_CREATE_ENDPOINT: str = (
    f"{DEFAULT_GUARDICORE_API_V4_BASE_ENDPOINT}visibility/policy/revisions"
)
DEFAULT_GUARDICORE_KEY_APPROLE: str = "AppRole"
DEFAULT_FWO_ROLE: str = "reporter"
DEFAULT_TIMEOUT_SECONDS: int = 60
GUARDICORE_LABELS_PAGE_SIZE: int = 1000
MAX_GUARDICORE_LABEL_PAGES: int = 10000
PROTO_ID_ICMP: int = 1
PROTO_ID_TCP: int = 6
PROTO_ID_UDP: int = 17


class GuardicoreRuleProvisioningError(Exception):
    """Raised when provisioning Guardicore rules fails."""


@dataclass(frozen=True)
class RuleBuildResult:
    payloads: list[dict[str, Any]]
    skip_reason: str | None


@dataclass(frozen=True)
class AppRoleResolution:
    label_ids: list[str]
    missing_labels: list[str]


@dataclass(frozen=True)
class AppRoleMapStats:
    total_approle_labels: int
    unique_label_ids: int
    unique_full_value_keys: int
    unique_role_name_keys: int
    unique_role_id_keys: int
    unique_map_keys: int
    pages_fetched: int
    raw_label_objects_seen: int
    label_candidates_seen: int
    approle_candidates_seen: int
    approle_duplicate_label_id_candidates: int
    approle_duplicate_full_value_candidates: int
    role_id_extractions_seen: int
    role_id_duplicate_candidates: int
    role_name_extractions_seen: int
    role_name_duplicate_candidates: int
    top_repeated_full_values: list[tuple[str, int]]
    pagination_mode: str


def parse_app_ids(value: str) -> list[str]:
    """Parse and validate a JSON array of app IDs."""
    try:
        parsed = json.loads(value)
    except json.JSONDecodeError as exc:
        raise argparse.ArgumentTypeError('--app-ids must be valid JSON, e.g. ["APP-1234", "APP-2345"]') from exc

    if not isinstance(parsed, list):
        raise argparse.ArgumentTypeError("--app-ids must be a JSON array of strings")

    app_ids: list[str] = []
    for item in cast("list[object]", parsed):
        if not isinstance(item, str):
            raise argparse.ArgumentTypeError("--app-ids must be a JSON array of strings")
        app_ids.append(item)

    if not app_ids:
        raise argparse.ArgumentTypeError("--app-ids must include at least one app id")

    return app_ids


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Create Guardicore rules from FWO active connections")
    parser.add_argument("--fwo-graphql-url", required=True, help="Hasura GraphQL endpoint URL")
    parser.add_argument("--fwo-jwt", help="JWT for FWO GraphQL calls (skips login if provided)")
    parser.add_argument("--fwo-user", help="FWO username (used if --fwo-jwt not provided)")
    parser.add_argument("--fwo-password", help="FWO password (used if --fwo-jwt not provided)")
    parser.add_argument("--fwo-middleware-url", help="FWO middleware base URL for login")
    parser.add_argument("--fwo-role", default=DEFAULT_FWO_ROLE, help="Hasura role for the GraphQL call")
    parser.add_argument(
        "--app-ids",
        type=parse_app_ids,
        help='Optional JSON array of external app IDs used to filter connections, e.g. ["APP-1234","APP-2345"]',
    )
    parser.add_argument(
        "--guardicore-url",
        required=True,
        help="Guardicore base URL, e.g. https://x.y.z",
    )
    parser.add_argument("--guardicore-token", help="Bearer token for Guardicore API")
    parser.add_argument("--guardicore-user", help="Guardicore username (used if --guardicore-token not provided)")
    parser.add_argument(
        "--guardicore-password",
        help="Guardicore password (used if --guardicore-token not provided)",
    )
    parser.add_argument(
        "--default-ip-protocol",
        default="TCP",
        help="Fallback ip protocol if no service protocol can be derived",
    )
    parser.add_argument(
        "--section-position",
        default="ALLOW",
        help="Guardicore section_position value",
    )
    parser.add_argument(
        "--action",
        default="ALLOW",
        help="Guardicore action value",
    )
    parser.add_argument(
        "--dry-run",
        action="store_true",
        help="Print generated payloads instead of calling Guardicore",
    )
    parser.add_argument(
        "--publish-comments",
        default="published rules by NeMo",
        help="Comment sent when publishing created rulesets",
    )
    parser.add_argument(
        "--fwo-ca-cert",
        help="Path to a CA bundle for FWO API calls",
    )
    parser.add_argument(
        "--guardicore-ca-cert",
        help="Path to a CA bundle for Guardicore API calls",
    )
    parser.add_argument(
        "--fwo-insecure",
        action="store_true",
        help="Disable SSL verification for FWO API calls only",
    )
    parser.add_argument(
        "--guardicore-insecure",
        action="store_true",
        help="Disable SSL verification for Guardicore API calls only",
    )
    parser.add_argument(
        "--insecure",
        action="store_true",
        help="Disable SSL verification for both endpoints",
    )
    parser.add_argument("--timeout", type=int, default=DEFAULT_TIMEOUT_SECONDS, help="HTTP timeout in seconds")
    return parser.parse_args()


def require_login_fields(args: argparse.Namespace) -> None:
    if args.fwo_jwt:
        return
    missing: list[str] = []
    if not args.fwo_user:
        missing.append("--fwo-user")
    if not args.fwo_password:
        missing.append("--fwo-password")
    if not args.fwo_middleware_url:
        missing.append("--fwo-middleware-url")
    if missing:
        raise GuardicoreRuleProvisioningError("Missing arguments for FWO login: " + ", ".join(missing))


def require_guardicore_fields(args: argparse.Namespace) -> None:
    if args.guardicore_token:
        return
    missing: list[str] = []
    if not args.guardicore_user:
        missing.append("--guardicore-user")
    if not args.guardicore_password:
        missing.append("--guardicore-password")
    if missing:
        raise GuardicoreRuleProvisioningError("Missing arguments for Guardicore auth: " + ", ".join(missing))


def get_fwo_jwt(args: argparse.Namespace, fwo_verify: bool | str) -> str:
    if args.fwo_jwt:
        return args.fwo_jwt
    return login_fwo(
        args.fwo_user,
        args.fwo_password,
        args.fwo_middleware_url,
        fwo_verify,
        args.timeout,
        GuardicoreRuleProvisioningError,
    )


def get_guardicore_token(args: argparse.Namespace, guardicore_verify: bool | str) -> str:
    if args.guardicore_token:
        return args.guardicore_token
    return login_guardicore(
        args.guardicore_user,
        args.guardicore_password,
        args.guardicore_url,
        guardicore_verify,
        args.timeout,
        GuardicoreRuleProvisioningError,
    )


def build_graphql_query(filter_by_app_ids: bool) -> str:
    owner_clause = "owner: { app_id_external: { _in: $appIds } }," if filter_by_app_ids else ""
    query = """
query getConnectionsForGuardicore__APPIDS_DECL__ {
  modelling_connection(
    where: {
      removed: { _eq: false },
      is_interface: { _eq: false },
      __OWNER_CLAUSE__
    },
    order_by: {id: asc}
  ) {
    id
    name
    is_published
    requested_on_fw
    owner {
      name
      app_id_external
    }
    source_approles: nwgroup_connections(
      where: {
        connection_field: { _eq: 1 },
        nwgroup: { group_type: { _in: [20, 23] }, is_deleted: { _eq: false } }
      }
    ) {
      nwgroup {
        id
        name
        id_string
      }
    }
    destination_approles: nwgroup_connections(
      where: {
        connection_field: { _eq: 2 },
        nwgroup: { group_type: { _in: [20, 23] }, is_deleted: { _eq: false } }
      }
    ) {
      nwgroup {
        id
        name
        id_string
      }
    }
    used_interface: connection {
      source_approles: nwgroup_connections(
        where: {
          connection_field: { _eq: 1 },
          nwgroup: { group_type: { _in: [20, 23] }, is_deleted: { _eq: false } }
        }
      ) {
        nwgroup {
          id
          name
          id_string
        }
      }
      destination_approles: nwgroup_connections(
        where: {
          connection_field: { _eq: 2 },
          nwgroup: { group_type: { _in: [20, 23] }, is_deleted: { _eq: false } }
        }
      ) {
        nwgroup {
          id
          name
          id_string
        }
      }
      services: service_connections {
        service {
          id
          name
          proto_id
          port
          port_end
          protocol: stm_ip_proto {
            id: ip_proto_id
            name: ip_proto_name
          }
        }
      }
      service_groups: service_group_connections {
        service_group {
          services: service_service_groups {
            service {
              id
              name
              proto_id
              port
              port_end
              protocol: stm_ip_proto {
                id: ip_proto_id
                name: ip_proto_name
              }
            }
          }
        }
      }
    }
    services: service_connections {
      service {
        id
        name
        proto_id
        port
        port_end
        protocol: stm_ip_proto {
          id: ip_proto_id
          name: ip_proto_name
        }
      }
    }
    service_groups: service_group_connections {
      service_group {
        services: service_service_groups {
          service {
            id
            name
            proto_id
            port
            port_end
            protocol: stm_ip_proto {
              id: ip_proto_id
              name: ip_proto_name
            }
          }
        }
      }
    }
  }
}
""".strip()
    app_ids_decl = "($appIds: [String!]!)" if filter_by_app_ids else ""
    return " ".join(query.splitlines()).replace("__APPIDS_DECL__", app_ids_decl).replace("__OWNER_CLAUSE__", owner_clause)


def build_graphql_variables(app_ids: list[str] | None) -> dict[str, Any]:
    if app_ids is None:
        return {}
    return {"appIds": app_ids}


def fetch_connections_from_fwo(
    fwo_config: FwoConfig,
    app_ids: list[str] | None,
) -> list[dict[str, Any]]:
    result = run_graphql_query(
        fwo_config,
        build_graphql_query(filter_by_app_ids=app_ids is not None),
        build_graphql_variables(app_ids),
        GuardicoreRuleProvisioningError,
    )
    data_obj = result.get("data")
    if not isinstance(data_obj, dict):
        return []
    data = cast("dict[str, Any]", data_obj)
    modelling_connections_obj = data.get("modelling_connection")
    if not isinstance(modelling_connections_obj, list):
        return []
    modelling_connections = cast("list[Any]", modelling_connections_obj)
    return [cast("dict[str, Any]", item) for item in modelling_connections if isinstance(item, dict)]


def parse_existing_labels(payload: Any) -> list[dict[str, Any]]:
    return extract_label_items(payload)


def extract_id_string_from_label_value(label_value: str) -> str | None:
    match = re.search(r"\(([^()]*)\)\s*$", label_value.strip())
    if not match:
        return None
    id_string = match.group(1).strip()
    return id_string if id_string else None


def extract_nwgroup_name_from_label_value(label_value: str) -> str | None:
    id_match = re.search(r"\(([^()]*)\)\s*$", label_value.strip())
    if not id_match:
        return None

    value_without_id = label_value[: id_match.start()].strip()
    if not value_without_id:
        return None

    if " - " in value_without_id:
        nwgroup_name = value_without_id.rsplit(" - ", 1)[-1].strip()
    else:
        nwgroup_name = value_without_id
    return nwgroup_name if nwgroup_name else None


def normalize_guardicore_label_key(key: str) -> str:
    normalized = key.strip().rstrip(":").casefold()
    return "".join(ch for ch in normalized if ch.isalnum())


def is_guardicore_approle_key(key: Any) -> bool:
    if not isinstance(key, str):
        return False
    return normalize_guardicore_label_key(key) == normalize_guardicore_label_key(DEFAULT_GUARDICORE_KEY_APPROLE)


def is_probable_approle_label(key: Any, label_value: str) -> bool:
    if is_guardicore_approle_key(key):
        return True
    parsed_id_string = extract_id_string_from_label_value(label_value)
    if not parsed_id_string:
        return False
    return parsed_id_string.strip().upper().startswith("AR")


def extract_label_id_key_value_candidates(label_item: dict[str, Any]) -> list[tuple[str, str, str]]:
    candidates: list[tuple[str, str, str]] = []
    candidate_objects: list[dict[str, Any]] = [label_item]

    nested_label = label_item.get("label")
    if isinstance(nested_label, dict):
        candidate_objects.append(cast("dict[str, Any]", nested_label))

    for candidate in candidate_objects:
        candidate_key = candidate.get("key")
        candidate_value = candidate.get("value")
        candidate_id = candidate.get("id")
        if isinstance(candidate_id, int):
            candidate_id = str(candidate_id)
        if not isinstance(candidate_key, str) or not isinstance(candidate_value, str) or not isinstance(candidate_id, str):
            continue
        normalized_value = candidate_value.strip()
        normalized_id = candidate_id.strip()
        if not normalized_value or not normalized_id:
            continue
        candidates.append((normalized_id, candidate_key, normalized_value))
    return candidates


def fetch_guardicore_approle_map(config: GuardicoreConfig) -> tuple[dict[str, list[str]], AppRoleMapStats]:
    endpoint = config.base_url.rstrip("/") + DEFAULT_GUARDICORE_LABELS_LIST_ENDPOINT
    headers = {
        "Authorization": f"Bearer {config.token}",
        "Content-Type": HTTP_CONTENT_TYPE_JSON,
    }
    base_params: dict[str, Any] = {
        "expand": "dynamic_assets",
        "fields": "id,key,value,dynamic_criteria",
        "max_results": GUARDICORE_LABELS_PAGE_SIZE,
        # Keep legacy alias for compatibility with older Guardicore releases.
        "limit": GUARDICORE_LABELS_PAGE_SIZE,
    }
    def fetch_with_mode(pagination_mode: str, page_start: int = 0) -> tuple[dict[str, list[str]], AppRoleMapStats]:
        app_role_map: dict[str, list[str]] = {}
        full_value_keys: set[str] = set()
        role_name_keys: set[str] = set()
        role_id_keys: set[str] = set()
        label_ids_seen: set[str] = set()
        approle_full_value_counter: Counter[str] = Counter()
        role_id_extractions_seen = 0
        role_name_extractions_seen = 0
        offset = 0
        page_number = page_start
        page_count = 0
        raw_label_objects_seen = 0
        label_candidates_seen = 0
        approle_candidates_seen = 0
        stagnant_pages = 0

        with requests.Session() as session:
            apply_ssl_settings(session, config.verify_ssl)
            session.headers.update(headers)
            while True:
                page_count += 1
                if page_count > MAX_GUARDICORE_LABEL_PAGES:
                    raise GuardicoreRuleProvisioningError(
                        "Guardicore label query exceeded maximum page limit; aborting to prevent endless pagination loop."
                    )
                previous_unique_label_count = len(label_ids_seen)

                params = dict(base_params)
                if pagination_mode == "offset":
                    params["start_at"] = offset
                    # Keep legacy alias for compatibility with older Guardicore releases.
                    params["offset"] = offset
                elif pagination_mode == "page":
                    params["page"] = page_number
                else:
                    raise GuardicoreRuleProvisioningError(f"Unknown pagination mode: {pagination_mode}")

                try:
                    response = session.get(endpoint, params=params, timeout=config.timeout_seconds)
                    response.raise_for_status()
                except requests.exceptions.RequestException as exc:
                    raise GuardicoreRuleProvisioningError(f"Guardicore label query failed: {exc}") from exc

                result_obj = response.json()
                labels = parse_existing_labels(result_obj)
                raw_label_objects_seen += len(labels)
                for label in labels:
                    seen_candidate_ids: set[str] = set()
                    for label_id, key, label_value in extract_label_id_key_value_candidates(label):
                        label_candidates_seen += 1
                        if not is_probable_approle_label(key, label_value):
                            continue
                        approle_candidates_seen += 1
                        if label_id in seen_candidate_ids:
                            continue
                        seen_candidate_ids.add(label_id)
                        label_ids_seen.add(label_id)
                        approle_full_value_counter[label_value] += 1
                        app_role_map.setdefault(label_value, []).append(label_id)
                        full_value_keys.add(label_value)
                        parsed_id_string = extract_id_string_from_label_value(label_value)
                        if parsed_id_string:
                            role_id_extractions_seen += 1
                            app_role_map.setdefault(parsed_id_string, []).append(label_id)
                            role_id_keys.add(parsed_id_string)
                        parsed_nwgroup_name = extract_nwgroup_name_from_label_value(label_value)
                        if parsed_nwgroup_name:
                            role_name_extractions_seen += 1
                            app_role_map.setdefault(parsed_nwgroup_name, []).append(label_id)
                            role_name_keys.add(parsed_nwgroup_name)

                if not isinstance(result_obj, dict):
                    break
                result = cast("dict[str, Any]", result_obj)
                objects_obj = result.get("objects")
                if not isinstance(objects_obj, list):
                    break
                objects = cast("list[Any]", objects_obj)
                if len(objects) == 0:
                    break

                if len(label_ids_seen) == previous_unique_label_count:
                    stagnant_pages += 1
                else:
                    stagnant_pages = 0
                if stagnant_pages >= 2:
                    break

                if pagination_mode == "offset":
                    total_count_obj = result.get("total_count")
                    current_offset = offset
                    next_offset = current_offset + len(objects)
                    next_offset_obj = result.get("next_offset")
                    if isinstance(next_offset_obj, int) and next_offset_obj > current_offset:
                        next_offset = next_offset_obj
                    offset = next_offset
                    if isinstance(total_count_obj, int):
                        if offset >= total_count_obj:
                            break
                        continue
                    if offset <= current_offset:
                        break
                else:
                    page_number += 1

        top_repeated_full_values = [
            (value, count) for value, count in approle_full_value_counter.most_common(10) if count > 1
        ]
        stats = AppRoleMapStats(
            total_approle_labels=len(label_ids_seen),
            unique_label_ids=len(label_ids_seen),
            unique_full_value_keys=len(full_value_keys),
            unique_role_name_keys=len(role_name_keys),
            unique_role_id_keys=len(role_id_keys),
            unique_map_keys=len(app_role_map),
            pages_fetched=page_count,
            raw_label_objects_seen=raw_label_objects_seen,
            label_candidates_seen=label_candidates_seen,
            approle_candidates_seen=approle_candidates_seen,
            approle_duplicate_label_id_candidates=max(0, approle_candidates_seen - len(label_ids_seen)),
            approle_duplicate_full_value_candidates=max(0, approle_candidates_seen - len(full_value_keys)),
            role_id_extractions_seen=role_id_extractions_seen,
            role_id_duplicate_candidates=max(0, role_id_extractions_seen - len(role_id_keys)),
            role_name_extractions_seen=role_name_extractions_seen,
            role_name_duplicate_candidates=max(0, role_name_extractions_seen - len(role_name_keys)),
            top_repeated_full_values=top_repeated_full_values,
            pagination_mode=pagination_mode if pagination_mode == "offset" else f"{pagination_mode}:{page_start}",
        )
        return app_role_map, stats

    offset_map, offset_stats = fetch_with_mode("offset")
    suspicious_offset_pagination = (
        offset_stats.pages_fetched > 1
        and offset_stats.approle_candidates_seen > 0
        and offset_stats.unique_label_ids * 2 < offset_stats.approle_candidates_seen
    )
    if not suspicious_offset_pagination:
        return offset_map, offset_stats

    page0_map, page0_stats = fetch_with_mode("page", page_start=0)
    page1_map, page1_stats = fetch_with_mode("page", page_start=1)
    best_map, best_stats = max(
        [(offset_map, offset_stats), (page0_map, page0_stats), (page1_map, page1_stats)],
        key=lambda item: item[1].unique_label_ids,
    )
    return best_map, best_stats


def normalize_protocol_name(protocol_name: str | None) -> str | None:
    if not protocol_name:
        return None
    normalized = protocol_name.upper()
    if normalized in {"TCP", "UDP", "ICMP", "SCTP", "GRE", "ESP", "AH"}:
        return normalized
    if normalized == "IP":
        return None
    return normalized


def protocol_from_proto_id(proto_id: int | None) -> str | None:
    if proto_id == PROTO_ID_TCP:
        return "TCP"
    if proto_id == PROTO_ID_UDP:
        return "UDP"
    if proto_id == PROTO_ID_ICMP:
        return "ICMP"
    return None


def parse_protocol_id(value: Any) -> int | None:
    if isinstance(value, int):
        return value
    if isinstance(value, str) and value.isdigit():
        return int(value)
    return None


def get_service_protocol(service: dict[str, Any], default_protocol: str) -> str:
    protocol_object = service.get("protocol")
    protocol_name: str | None = None
    protocol_id: int | None = None
    if isinstance(protocol_object, dict):
        protocol_dict = cast("dict[str, Any]", protocol_object)
        protocol_raw_name = protocol_dict.get("name")
        if isinstance(protocol_raw_name, str):
            protocol_name = protocol_raw_name
        protocol_id = parse_protocol_id(protocol_dict.get("id"))

    normalized = normalize_protocol_name(protocol_name)
    if normalized:
        return normalized

    service_proto_id = parse_protocol_id(service.get("proto_id"))
    if service_proto_id is not None:
        from_proto_id = protocol_from_proto_id(service_proto_id)
        if from_proto_id:
            return from_proto_id

    if protocol_id is not None:
        from_proto_id = protocol_from_proto_id(protocol_id)
        if from_proto_id:
            return from_proto_id

    return default_protocol.upper()


def extract_services(connection: dict[str, Any]) -> list[dict[str, Any]]:
    services: list[dict[str, Any]] = []

    direct_services = connection.get("services", [])
    if isinstance(direct_services, list):
        direct_services_list = cast("list[Any]", direct_services)
        for direct_service in direct_services_list:
            if not isinstance(direct_service, dict):
                continue
            direct_service_dict = cast("dict[str, Any]", direct_service)
            service = direct_service_dict.get("service")
            if isinstance(service, dict):
                services.append(cast("dict[str, Any]", service))

    service_groups = connection.get("service_groups", [])
    if isinstance(service_groups, list):
        service_groups_list = cast("list[Any]", service_groups)
        for service_group_wrapper in service_groups_list:
            if not isinstance(service_group_wrapper, dict):
                continue
            service_group_wrapper_dict = cast("dict[str, Any]", service_group_wrapper)
            service_group = service_group_wrapper_dict.get("service_group")
            if not isinstance(service_group, dict):
                continue
            service_group_dict = cast("dict[str, Any]", service_group)
            grouped_services = service_group_dict.get("services", [])
            if not isinstance(grouped_services, list):
                continue
            grouped_services_list = cast("list[Any]", grouped_services)
            for grouped_service_wrapper in grouped_services_list:
                if not isinstance(grouped_service_wrapper, dict):
                    continue
                grouped_service_wrapper_dict = cast("dict[str, Any]", grouped_service_wrapper)
                service = grouped_service_wrapper_dict.get("service")
                if isinstance(service, dict):
                    services.append(cast("dict[str, Any]", service))

    if services:
        return services

    used_interface = connection.get("used_interface")
    if isinstance(used_interface, dict):
        used_interface_dict = cast("dict[str, Any]", used_interface)
        interface_services = used_interface_dict.get("services")
        if isinstance(interface_services, list):
            interface_services_list = cast("list[Any]", interface_services)
            for direct_service in interface_services_list:
                if not isinstance(direct_service, dict):
                    continue
                direct_service_dict = cast("dict[str, Any]", direct_service)
                service = direct_service_dict.get("service")
                if isinstance(service, dict):
                    services.append(cast("dict[str, Any]", service))

        interface_service_groups = used_interface_dict.get("service_groups")
        if isinstance(interface_service_groups, list):
            interface_service_groups_list = cast("list[Any]", interface_service_groups)
            for service_group_wrapper in interface_service_groups_list:
                if not isinstance(service_group_wrapper, dict):
                    continue
                service_group_wrapper_dict = cast("dict[str, Any]", service_group_wrapper)
                service_group = service_group_wrapper_dict.get("service_group")
                if not isinstance(service_group, dict):
                    continue
                service_group_dict = cast("dict[str, Any]", service_group)
                grouped_services = service_group_dict.get("services", [])
                if not isinstance(grouped_services, list):
                    continue
                grouped_services_list = cast("list[Any]", grouped_services)
                for grouped_service_wrapper in grouped_services_list:
                    if not isinstance(grouped_service_wrapper, dict):
                        continue
                    grouped_service_wrapper_dict = cast("dict[str, Any]", grouped_service_wrapper)
                    service = grouped_service_wrapper_dict.get("service")
                    if isinstance(service, dict):
                        services.append(cast("dict[str, Any]", service))

    return services


def extract_connection_approles(connection: dict[str, Any], field_name: str) -> list[dict[str, Any]]:
    approles_raw = connection.get(field_name)
    approles: list[dict[str, Any]] = []
    if isinstance(approles_raw, list):
        approles_list = cast("list[Any]", approles_raw)
        approles = [cast("dict[str, Any]", item) for item in approles_list if isinstance(item, dict)]
    if approles:
        return approles

    used_interface = connection.get("used_interface")
    if not isinstance(used_interface, dict):
        return []
    used_interface_dict = cast("dict[str, Any]", used_interface)
    interface_approles_raw = used_interface_dict.get(field_name)
    if not isinstance(interface_approles_raw, list):
        return []
    interface_approles_list = cast("list[Any]", interface_approles_raw)
    return [cast("dict[str, Any]", item) for item in interface_approles_list if isinstance(item, dict)]


def collect_ports_and_protocols(
    services: list[dict[str, Any]],
    default_protocol: str,
) -> tuple[list[int], list[dict[str, int]], list[str]]:
    ports: set[int] = set()
    port_ranges: set[tuple[int, int]] = set()
    protocols: set[str] = set()

    for service in services:
        protocol_object = service.get("protocol")
        protocol_name: str | None = None
        if isinstance(protocol_object, dict):
            protocol_dict = cast("dict[str, Any]", protocol_object)
            protocol_raw_name = protocol_dict.get("name")
            if isinstance(protocol_raw_name, str):
                protocol_name = protocol_raw_name
        protocol = normalize_protocol_name(protocol_name)
        if protocol:
            protocols.add(protocol)

        port = service.get("port")
        port_end = service.get("port_end")
        if isinstance(port, int) and port > 0:
            if isinstance(port_end, int) and port_end > port:
                port_ranges.add((port, port_end))
            else:
                ports.add(port)

    if not protocols:
        protocols.add(default_protocol.upper())

    sorted_ports = sorted(ports)
    sorted_ranges = [{"start": start, "end": end} for (start, end) in sorted(port_ranges)]
    sorted_protocols = sorted(protocols)
    return sorted_ports, sorted_ranges, sorted_protocols


def collect_ports_and_protocols_by_protocol(
    services: list[dict[str, Any]],
    default_protocol: str,
) -> dict[str, tuple[list[int], list[dict[str, int]]]]:
    grouped_ports: dict[str, set[int]] = {}
    grouped_ranges: dict[str, set[tuple[int, int]]] = {}

    for service in services:
        protocol = get_service_protocol(service, default_protocol)

        grouped_ports.setdefault(protocol, set())
        grouped_ranges.setdefault(protocol, set())

        port = service.get("port")
        port_end = service.get("port_end")
        if isinstance(port, int) and port > 0:
            if isinstance(port_end, int) and port_end > port:
                grouped_ranges[protocol].add((port, port_end))
            else:
                grouped_ports[protocol].add(port)

    if not grouped_ports and not grouped_ranges:
        protocol = default_protocol.upper()
        grouped_ports[protocol] = set()
        grouped_ranges[protocol] = set()

    result: dict[str, tuple[list[int], list[dict[str, int]]]] = {}
    for protocol in sorted(set(grouped_ports.keys()) | set(grouped_ranges.keys())):
        protocol_ports = sorted(grouped_ports.get(protocol, set()))
        protocol_ranges = [{"start": start, "end": end} for (start, end) in sorted(grouped_ranges.get(protocol, set()))]
        result[protocol] = (protocol_ports, protocol_ranges)
    return result


def format_approle_identifier(name: Any, id_string: Any) -> str | None:
    name_text = name.strip() if isinstance(name, str) else ""
    id_text = id_string.strip() if isinstance(id_string, str) else ""
    if name_text and id_text:
        return f"{name_text} ({id_text})"
    if name_text:
        return name_text
    if id_text:
        return f"(id: {id_text})"
    return None


def collect_approle_identifiers(connection_approles: list[dict[str, Any]]) -> list[str]:
    identifiers: set[str] = set()
    for connection_approle in connection_approles:
        nwgroup = connection_approle.get("nwgroup")
        if not isinstance(nwgroup, dict):
            continue
        nwgroup_dict = cast("dict[str, Any]", nwgroup)
        identifier = format_approle_identifier(nwgroup_dict.get("name"), nwgroup_dict.get("id_string"))
        if identifier:
            identifiers.add(identifier)
    return sorted(identifiers)


def build_missing_approle_warning_details(connection: dict[str, Any]) -> str:
    source_approles = extract_connection_approles(connection, "source_approles")
    destination_approles = extract_connection_approles(connection, "destination_approles")

    source_identifiers = collect_approle_identifiers(source_approles)
    destination_identifiers = collect_approle_identifiers(destination_approles)
    connection_json = json.dumps(connection, sort_keys=True, ensure_ascii=True)
    return (
        f"all_source_approles={source_identifiers}, "
        f"all_destination_approles={destination_identifiers}, "
        f"connection={connection_json}"
    )


def resolve_approle_labels(
    connection_approles: list[dict[str, Any]],
    approle_id_map: dict[str, list[str]],
) -> AppRoleResolution:
    label_ids: set[str] = set()
    missing_labels: set[str] = set()

    for connection_approle in connection_approles:
        nwgroup = connection_approle.get("nwgroup")
        if not isinstance(nwgroup, dict):
            continue
        nwgroup_dict = cast("dict[str, Any]", nwgroup)
        id_string = nwgroup_dict.get("id_string")
        name = nwgroup_dict.get("name")

        matched_label_ids: list[str] | None = None
        if isinstance(id_string, str) and id_string.strip():
            matched_label_ids = approle_id_map.get(id_string.strip())
        if not matched_label_ids and isinstance(name, str) and name.strip():
            matched_label_ids = approle_id_map.get(name.strip())
        if matched_label_ids:
            label_ids.update(matched_label_ids)
        else:
            identifier = format_approle_identifier(name, id_string)
            if identifier:
                missing_labels.add(identifier)

    return AppRoleResolution(label_ids=sorted(label_ids), missing_labels=sorted(missing_labels))


def to_guardicore_or_labels(label_ids: list[str]) -> list[dict[str, list[str]]]:
    return [{"and_labels": [label_id]} for label_id in label_ids]


def strip_app_id_prefix(app_id_external: str) -> str:
    app_id = app_id_external.strip()
    if not app_id:
        return ""
    first_digit_match = re.search(r"\d", app_id)
    if first_digit_match:
        return app_id[first_digit_match.start() :]
    return app_id


def build_guardicore_ruleset_name(connection: dict[str, Any]) -> str:
    connection_id = connection.get("id")
    default_ruleset_name = f"FWOC{connection_id}"
    owner = connection.get("owner")
    if not isinstance(owner, dict):
        return default_ruleset_name

    owner_dict = cast("dict[str, Any]", owner)
    app_id_external = owner_dict.get("app_id_external")
    if not isinstance(app_id_external, str) or not app_id_external.strip():
        return default_ruleset_name

    normalized_app_id = strip_app_id_prefix(app_id_external)
    if not normalized_app_id:
        return default_ruleset_name
    return f"FWOA{normalized_app_id} {default_ruleset_name}"


def build_rule_payload(
    connection: dict[str, Any],
    approle_id_map: dict[str, list[str]],
    default_ip_protocol: str,
    action: str,
    section_position: str,
) -> RuleBuildResult:
    source_approles = extract_connection_approles(connection, "source_approles")
    destination_approles = extract_connection_approles(connection, "destination_approles")

    source_resolution = resolve_approle_labels(
        source_approles,
        approle_id_map,
    )
    destination_resolution = resolve_approle_labels(
        destination_approles,
        approle_id_map,
    )

    source_missing = source_resolution.missing_labels
    destination_missing = destination_resolution.missing_labels
    if source_missing or destination_missing:
        return RuleBuildResult(
            payloads=[],
            skip_reason=(
                "missing Guardicore AppRole labels: "
                f"source={source_missing}, destination={destination_missing}"
            ),
        )

    if not source_resolution.label_ids or not destination_resolution.label_ids:
        source_identifiers = collect_approle_identifiers(source_approles)
        destination_identifiers = collect_approle_identifiers(destination_approles)
        return RuleBuildResult(
            payloads=[],
            skip_reason=(
                "missing source/destination AppRole labels: "
                f"source={source_identifiers}, destination={destination_identifiers}"
            ),
        )

    services = extract_services(connection)
    ports_by_protocol = collect_ports_and_protocols_by_protocol(services, default_ip_protocol)

    ruleset_name = build_guardicore_ruleset_name(connection)
    payloads: list[dict[str, Any]] = []
    skipped_protocols: set[str] = set()
    for protocol, (ports, port_ranges) in ports_by_protocol.items():
        if protocol == "ESP":
            skipped_protocols.add(protocol)
            continue
        payload: dict[str, Any] = {
            "ruleset_name": ruleset_name,
            "ip_protocols": [protocol],
            "action": action,
            "section_position": section_position,
            "source": {
                "labels": {
                    "or_labels": to_guardicore_or_labels(source_resolution.label_ids),
                }
            },
            "destination": {
                "labels": {
                    "or_labels": to_guardicore_or_labels(destination_resolution.label_ids),
                }
            },
        }
        if protocol != "ICMP":
            payload["ports"] = ports
            payload["port_ranges"] = port_ranges
        payloads.append(payload)
    if not payloads and skipped_protocols:
        return RuleBuildResult(
            payloads=[],
            skip_reason=f"unsupported Guardicore ip_protocols: {sorted(skipped_protocols)}",
        )
    return RuleBuildResult(payloads=payloads, skip_reason=None)


def post_guardicore_rule(config: GuardicoreConfig, payload: dict[str, Any]) -> None:
    endpoint = config.base_url.rstrip("/") + DEFAULT_GUARDICORE_RULES_CREATE_ENDPOINT
    headers = {
        "Authorization": f"Bearer {config.token}",
        "Content-Type": HTTP_CONTENT_TYPE_JSON,
    }

    with requests.Session() as session:
        apply_ssl_settings(session, config.verify_ssl)
        session.headers.update(headers)
        try:
            response = session.post(endpoint, json=payload, timeout=config.timeout_seconds)
            response.raise_for_status()
        except requests.exceptions.RequestException as exc:
            raise GuardicoreRuleProvisioningError(f"Guardicore rule creation failed: {exc}") from exc


def post_guardicore_revision(config: GuardicoreConfig, rulesets: list[str], comments: str) -> None:
    if not rulesets:
        return

    endpoint = config.base_url.rstrip("/") + DEFAULT_GUARDICORE_REVISIONS_CREATE_ENDPOINT
    headers = {
        "Authorization": f"Bearer {config.token}",
        "Content-Type": HTTP_CONTENT_TYPE_JSON,
    }
    with requests.Session() as session:
        apply_ssl_settings(session, config.verify_ssl)
        session.headers.update(headers)
        payload = {"comments": comments}
        try:
            response = session.post(endpoint, json=payload, timeout=config.timeout_seconds)
            response.raise_for_status()
        except requests.exceptions.RequestException as exc:
            raise GuardicoreRuleProvisioningError(f"Guardicore publish failed: {exc}") from exc


def main() -> int:
    logging.basicConfig(level=logging.INFO, format="%(levelname)s: %(message)s")
    logger = logging.getLogger(__name__)
    args = parse_args()

    try:
        require_login_fields(args)
        require_guardicore_fields(args)
    except GuardicoreRuleProvisioningError:
        logger.exception("Argument validation failed.")
        return 2

    try:
        fwo_verify, guardicore_verify = resolve_ssl_verification_settings(args)
        fwo_jwt = get_fwo_jwt(args, fwo_verify)
        guardicore_token = get_guardicore_token(args, guardicore_verify)

        fwo_config = FwoConfig(
            graphql_url=args.fwo_graphql_url,
            jwt=fwo_jwt,
            verify_ssl=fwo_verify,
            timeout_seconds=args.timeout,
            role=args.fwo_role,
        )
        guardicore_config = GuardicoreConfig(
            base_url=args.guardicore_url,
            token=guardicore_token,
            verify_ssl=guardicore_verify,
            timeout_seconds=args.timeout,
        )

        connections = fetch_connections_from_fwo(
            fwo_config,
            app_ids=args.app_ids,
        )
        if args.app_ids is None:
            logger.info("Fetched %s FWO connections for all apps.", len(connections))
        else:
            logger.info("Fetched %s filtered FWO connections.", len(connections))

        approle_id_map, approle_map_stats = fetch_guardicore_approle_map(guardicore_config)
        logger.info(
            "Fetched Guardicore AppRole labels: total=%s, unique_label_ids=%s, full_value_keys=%s, "
            "role_name_keys=%s, role_id_keys=%s, total_map_keys=%s, pages_fetched=%s, "
            "raw_label_objects_seen=%s, label_candidates_seen=%s, approle_candidates_seen=%s, pagination_mode=%s.",
            approle_map_stats.total_approle_labels,
            approle_map_stats.unique_label_ids,
            approle_map_stats.unique_full_value_keys,
            approle_map_stats.unique_role_name_keys,
            approle_map_stats.unique_role_id_keys,
            approle_map_stats.unique_map_keys,
            approle_map_stats.pages_fetched,
            approle_map_stats.raw_label_objects_seen,
            approle_map_stats.label_candidates_seen,
            approle_map_stats.approle_candidates_seen,
            approle_map_stats.pagination_mode,
        )
        logger.info(
            "AppRole map diagnostics: non_approle_candidates=%s, duplicate_label_id_candidates=%s, "
            "duplicate_full_value_candidates=%s, role_id_extractions=%s, duplicate_role_id_candidates=%s, "
            "role_name_extractions=%s, duplicate_role_name_candidates=%s.",
            max(0, approle_map_stats.label_candidates_seen - approle_map_stats.approle_candidates_seen),
            approle_map_stats.approle_duplicate_label_id_candidates,
            approle_map_stats.approle_duplicate_full_value_candidates,
            approle_map_stats.role_id_extractions_seen,
            approle_map_stats.role_id_duplicate_candidates,
            approle_map_stats.role_name_extractions_seen,
            approle_map_stats.role_name_duplicate_candidates,
        )
        if approle_map_stats.top_repeated_full_values:
            logger.info("Top repeated AppRole full values: %s", approle_map_stats.top_repeated_full_values)

        created = 0
        skipped = 0
        created_rulesets: set[str] = set()
        for connection in connections:
            result = build_rule_payload(
                connection=connection,
                approle_id_map=approle_id_map,
                default_ip_protocol=args.default_ip_protocol,
                action=args.action,
                section_position=args.section_position,
            )
            if not result.payloads:
                skipped += 1
                if result.skip_reason and "AppRole" in result.skip_reason:
                    logger.warning(
                        "Skipping connection id=%s name=%s: %s; %s",
                        connection.get("id"),
                        connection.get("name"),
                        result.skip_reason,
                        build_missing_approle_warning_details(connection),
                    )
                else:
                    logger.warning(
                        "Skipping connection id=%s name=%s: %s",
                        connection.get("id"),
                        connection.get("name"),
                        result.skip_reason,
                    )
                continue

            for payload in result.payloads:
                if args.dry_run:
                    logger.info("Dry run payload for connection id=%s: %s", connection.get("id"), json.dumps(payload))
                else:
                    post_guardicore_rule(guardicore_config, payload)
                ruleset_name = payload.get("ruleset_name")
                if isinstance(ruleset_name, str) and ruleset_name:
                    created_rulesets.add(ruleset_name)
            created += len(result.payloads)

        sorted_rulesets = sorted(created_rulesets)
        if args.dry_run:
            logger.info(
                "Dry run publish payload: %s",
                json.dumps({"comments": args.publish_comments}),
            )
        else:
            post_guardicore_revision(guardicore_config, sorted_rulesets, args.publish_comments)

        logger.info("Done. Created %s rule(s), skipped %s connection(s).", created, skipped)
        return 0

    except GuardicoreRuleProvisioningError:
        logger.exception("Guardicore rule provisioning failed.")
        return 1


if __name__ == "__main__":
    sys.exit(main())
