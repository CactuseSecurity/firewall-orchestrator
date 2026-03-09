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
        JsonDict,
        JsonList,
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
        JsonDict,
        JsonList,
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
DEFAULT_GUARDICORE_KEY_NETWORKAREA: str = "NetworkArea"
DEFAULT_FWO_ROLE: str = "reporter"
DEFAULT_TIMEOUT_SECONDS: int = 60
GUARDICORE_LABELS_PAGE_SIZE: int = 1000
MAX_GUARDICORE_LABEL_PAGES: int = 10000
MAX_STAGNANT_PAGES: int = 2
PROTO_ID_ICMP: int = 1
PROTO_ID_TCP: int = 6
PROTO_ID_UDP: int = 17


class GuardicoreRuleProvisioningError(Exception):
    """Raised when provisioning Guardicore rules fails."""


@dataclass(frozen=True)
class RuleBuildResult:
    payloads: list[JsonDict]
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


@dataclass
class AppRoleMapBuildState:
    app_role_map: dict[str, list[str]]
    full_value_keys: set[str]
    role_name_keys: set[str]
    role_id_keys: set[str]
    label_ids_seen: set[str]
    approle_full_value_counter: Counter[str]
    role_id_extractions_seen: int = 0
    role_name_extractions_seen: int = 0
    raw_label_objects_seen: int = 0
    label_candidates_seen: int = 0
    approle_candidates_seen: int = 0


@dataclass(frozen=True)
class PageProgress:
    next_offset: int
    next_page_number: int
    should_continue: bool


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
        nwgroup: { group_type: { _eq: 20 }, is_deleted: { _eq: false } }
      }
    ) {
      nwgroup {
        id
        name
        id_string
      }
    }
    source_areas: nwgroup_connections(
      where: {
        connection_field: { _eq: 1 },
        nwgroup: { group_type: { _eq: 23 }, is_deleted: { _eq: false } }
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
        nwgroup: { group_type: { _eq: 20 }, is_deleted: { _eq: false } }
      }
    ) {
      nwgroup {
        id
        name
        id_string
      }
    }
    destination_areas: nwgroup_connections(
      where: {
        connection_field: { _eq: 2 },
        nwgroup: { group_type: { _eq: 23 }, is_deleted: { _eq: false } }
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
          nwgroup: { group_type: { _eq: 20 }, is_deleted: { _eq: false } }
        }
      ) {
        nwgroup {
          id
          name
          id_string
        }
      }
      source_areas: nwgroup_connections(
        where: {
          connection_field: { _eq: 1 },
          nwgroup: { group_type: { _eq: 23 }, is_deleted: { _eq: false } }
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
          nwgroup: { group_type: { _eq: 20 }, is_deleted: { _eq: false } }
        }
      ) {
        nwgroup {
          id
          name
          id_string
        }
      }
      destination_areas: nwgroup_connections(
        where: {
          connection_field: { _eq: 2 },
          nwgroup: { group_type: { _eq: 23 }, is_deleted: { _eq: false } }
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
    return (
        " ".join(query.splitlines()).replace("__APPIDS_DECL__", app_ids_decl).replace("__OWNER_CLAUSE__", owner_clause)
    )


def build_graphql_variables(app_ids: list[str] | None) -> JsonDict:
    if app_ids is None:
        return {}
    return {"appIds": app_ids}


def fetch_connections_from_fwo(
    fwo_config: FwoConfig,
    app_ids: list[str] | None,
) -> list[JsonDict]:
    result = run_graphql_query(
        fwo_config,
        build_graphql_query(filter_by_app_ids=app_ids is not None),
        build_graphql_variables(app_ids),
        GuardicoreRuleProvisioningError,
    )
    data_obj = result.get("data")
    if not isinstance(data_obj, dict):
        return []
    data = cast("JsonDict", data_obj)
    modelling_connections_obj = data.get("modelling_connection")
    if not isinstance(modelling_connections_obj, list):
        return []
    modelling_connections = cast("JsonList", modelling_connections_obj)
    return [cast("JsonDict", item) for item in modelling_connections if isinstance(item, dict)]


def parse_existing_labels(payload: Any) -> list[JsonDict]:
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

    nwgroup_name = value_without_id.rsplit(" - ", 1)[-1].strip() if " - " in value_without_id else value_without_id
    return nwgroup_name if nwgroup_name else None


def normalize_guardicore_label_key(key: str) -> str:
    normalized = key.strip().rstrip(":").casefold()
    return "".join(ch for ch in normalized if ch.isalnum())


def is_guardicore_policy_label_key(key: Any) -> bool:
    if not isinstance(key, str):
        return False
    normalized_key = normalize_guardicore_label_key(key)
    return normalized_key in {
        normalize_guardicore_label_key(DEFAULT_GUARDICORE_KEY_APPROLE),
        normalize_guardicore_label_key(DEFAULT_GUARDICORE_KEY_NETWORKAREA),
    }


def is_probable_policy_label(key: Any, label_value: str) -> bool:
    if is_guardicore_policy_label_key(key):
        return True
    parsed_id_string = extract_id_string_from_label_value(label_value)
    if not parsed_id_string:
        return False
    normalized_id = parsed_id_string.strip().upper()
    return normalized_id.startswith(("AR", "NA"))


def extract_label_id_key_value_candidates(label_item: JsonDict) -> list[tuple[str, str, str]]:
    candidates: list[tuple[str, str, str]] = []
    candidate_objects: list[JsonDict] = [label_item]

    nested_label = label_item.get("label")
    if isinstance(nested_label, dict):
        candidate_objects.append(cast("JsonDict", nested_label))

    for candidate in candidate_objects:
        candidate_key = candidate.get("key")
        candidate_value = candidate.get("value")
        candidate_id = candidate.get("id")
        if isinstance(candidate_id, int):
            candidate_id = str(candidate_id)
        if (
            not isinstance(candidate_key, str)
            or not isinstance(candidate_value, str)
            or not isinstance(candidate_id, str)
        ):
            continue
        normalized_value = candidate_value.strip()
        normalized_id = candidate_id.strip()
        if not normalized_value or not normalized_id:
            continue
        candidates.append((normalized_id, candidate_key, normalized_value))
    return candidates


def build_guardicore_label_query_params(
    base_params: JsonDict,
    pagination_mode: str,
    offset: int,
    page_number: int,
) -> JsonDict:
    params = dict(base_params)
    if pagination_mode == "offset":
        params["start_at"] = offset
        params["offset"] = offset
        return params
    if pagination_mode == "page":
        params["page"] = page_number
        return params
    raise GuardicoreRuleProvisioningError(f"Unknown pagination mode: {pagination_mode}")


def fetch_guardicore_label_query_page(
    session: requests.Session,
    endpoint: str,
    params: JsonDict,
    timeout_seconds: int,
) -> Any:
    try:
        response = session.get(endpoint, params=params, timeout=timeout_seconds)
        response.raise_for_status()
    except requests.exceptions.RequestException as exc:
        raise GuardicoreRuleProvisioningError(f"Guardicore label query failed: {exc}") from exc
    return response.json()


def update_approle_map_state(state: AppRoleMapBuildState, labels: list[JsonDict]) -> None:
    state.raw_label_objects_seen += len(labels)
    for label in labels:
        seen_candidate_ids: set[str] = set()
        for label_id, key, label_value in extract_label_id_key_value_candidates(label):
            state.label_candidates_seen += 1
            if not is_probable_policy_label(key, label_value):
                continue
            state.approle_candidates_seen += 1
            if label_id in seen_candidate_ids:
                continue
            seen_candidate_ids.add(label_id)
            state.label_ids_seen.add(label_id)
            state.approle_full_value_counter[label_value] += 1
            state.app_role_map.setdefault(label_value, []).append(label_id)
            state.full_value_keys.add(label_value)
            parsed_id_string = extract_id_string_from_label_value(label_value)
            if parsed_id_string:
                state.role_id_extractions_seen += 1
                state.app_role_map.setdefault(parsed_id_string, []).append(label_id)
                state.role_id_keys.add(parsed_id_string)
            parsed_nwgroup_name = extract_nwgroup_name_from_label_value(label_value)
            if parsed_nwgroup_name:
                state.role_name_extractions_seen += 1
                state.app_role_map.setdefault(parsed_nwgroup_name, []).append(label_id)
                state.role_name_keys.add(parsed_nwgroup_name)


def extract_page_objects(result_obj: Any) -> tuple[JsonDict | None, list[JsonDict]]:
    if not isinstance(result_obj, dict):
        return None, []
    result = cast("JsonDict", result_obj)
    objects_obj = result.get("objects")
    if not isinstance(objects_obj, list):
        return result, []
    objects = cast("JsonList", objects_obj)
    return result, [cast("JsonDict", obj) for obj in objects if isinstance(obj, dict)]


def determine_page_progress(
    pagination_mode: str,
    result: JsonDict,
    objects: list[JsonDict],
    offset: int,
    page_number: int,
) -> PageProgress:
    if pagination_mode == "page":
        return PageProgress(next_offset=offset, next_page_number=page_number + 1, should_continue=True)

    current_offset = offset
    next_offset = current_offset + len(objects)
    next_offset_obj = result.get("next_offset")
    if isinstance(next_offset_obj, int) and next_offset_obj > current_offset:
        next_offset = next_offset_obj
    total_count_obj = result.get("total_count")
    if isinstance(total_count_obj, int):
        return PageProgress(
            next_offset=next_offset,
            next_page_number=page_number,
            should_continue=next_offset < total_count_obj,
        )
    return PageProgress(
        next_offset=next_offset, next_page_number=page_number, should_continue=next_offset > current_offset
    )


def build_approle_map_stats(
    state: AppRoleMapBuildState,
    page_count: int,
    pagination_mode: str,
    page_start: int,
) -> AppRoleMapStats:
    top_repeated_full_values = [
        (value, count) for value, count in state.approle_full_value_counter.most_common(10) if count > 1
    ]
    return AppRoleMapStats(
        total_approle_labels=len(state.label_ids_seen),
        unique_label_ids=len(state.label_ids_seen),
        unique_full_value_keys=len(state.full_value_keys),
        unique_role_name_keys=len(state.role_name_keys),
        unique_role_id_keys=len(state.role_id_keys),
        unique_map_keys=len(state.app_role_map),
        pages_fetched=page_count,
        raw_label_objects_seen=state.raw_label_objects_seen,
        label_candidates_seen=state.label_candidates_seen,
        approle_candidates_seen=state.approle_candidates_seen,
        approle_duplicate_label_id_candidates=max(0, state.approle_candidates_seen - len(state.label_ids_seen)),
        approle_duplicate_full_value_candidates=max(0, state.approle_candidates_seen - len(state.full_value_keys)),
        role_id_extractions_seen=state.role_id_extractions_seen,
        role_id_duplicate_candidates=max(0, state.role_id_extractions_seen - len(state.role_id_keys)),
        role_name_extractions_seen=state.role_name_extractions_seen,
        role_name_duplicate_candidates=max(0, state.role_name_extractions_seen - len(state.role_name_keys)),
        top_repeated_full_values=top_repeated_full_values,
        pagination_mode=pagination_mode if pagination_mode == "offset" else f"{pagination_mode}:{page_start}",
    )


def fetch_approle_map_with_mode(
    config: GuardicoreConfig,
    endpoint: str,
    headers: dict[str, str],
    base_params: JsonDict,
    pagination_mode: str,
    page_start: int = 0,
) -> tuple[dict[str, list[str]], AppRoleMapStats]:
    state = AppRoleMapBuildState(
        app_role_map={},
        full_value_keys=set(),
        role_name_keys=set(),
        role_id_keys=set(),
        label_ids_seen=set(),
        approle_full_value_counter=Counter(),
    )
    offset = 0
    page_number = page_start
    page_count = 0
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
            previous_unique_label_count = len(state.label_ids_seen)
            params = build_guardicore_label_query_params(base_params, pagination_mode, offset, page_number)
            result_obj = fetch_guardicore_label_query_page(session, endpoint, params, config.timeout_seconds)
            update_approle_map_state(state, parse_existing_labels(result_obj))
            result, objects = extract_page_objects(result_obj)
            if result is None or not objects:
                break
            stagnant_pages = stagnant_pages + 1 if len(state.label_ids_seen) == previous_unique_label_count else 0
            if stagnant_pages >= MAX_STAGNANT_PAGES:
                break
            progress = determine_page_progress(pagination_mode, result, objects, offset, page_number)
            if not progress.should_continue:
                break
            offset = progress.next_offset
            page_number = progress.next_page_number

    return state.app_role_map, build_approle_map_stats(state, page_count, pagination_mode, page_start)


def fetch_guardicore_approle_map(config: GuardicoreConfig) -> tuple[dict[str, list[str]], AppRoleMapStats]:
    endpoint = config.base_url.rstrip("/") + DEFAULT_GUARDICORE_LABELS_LIST_ENDPOINT
    headers = {
        "Authorization": f"Bearer {config.token}",
        "Content-Type": HTTP_CONTENT_TYPE_JSON,
    }
    base_params: JsonDict = {
        "expand": "dynamic_assets",
        "fields": "id,key,value,dynamic_criteria",
        "max_results": GUARDICORE_LABELS_PAGE_SIZE,
        "limit": GUARDICORE_LABELS_PAGE_SIZE,
    }

    offset_map, offset_stats = fetch_approle_map_with_mode(config, endpoint, headers, base_params, "offset")
    suspicious_offset_pagination = (
        offset_stats.pages_fetched > 1
        and offset_stats.approle_candidates_seen > 0
        and offset_stats.unique_label_ids * 2 < offset_stats.approle_candidates_seen
    )
    if not suspicious_offset_pagination:
        return offset_map, offset_stats

    page0_map, page0_stats = fetch_approle_map_with_mode(config, endpoint, headers, base_params, "page", page_start=0)
    page1_map, page1_stats = fetch_approle_map_with_mode(config, endpoint, headers, base_params, "page", page_start=1)
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


def get_service_protocol(service: JsonDict, default_protocol: str) -> str:
    protocol_object = service.get("protocol")
    protocol_name: str | None = None
    protocol_id: int | None = None
    if isinstance(protocol_object, dict):
        protocol_dict = cast("JsonDict", protocol_object)
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


def extract_service_dicts(service_wrappers: Any) -> list[JsonDict]:
    if not isinstance(service_wrappers, list):
        return []
    extracted_services: list[JsonDict] = []
    for wrapper in cast("JsonList", service_wrappers):
        if not isinstance(wrapper, dict):
            continue
        service = cast("JsonDict", wrapper).get("service")
        if isinstance(service, dict):
            extracted_services.append(cast("JsonDict", service))
    return extracted_services


def extract_service_group_services(service_groups: Any) -> list[JsonDict]:
    if not isinstance(service_groups, list):
        return []
    extracted_services: list[JsonDict] = []
    for service_group_wrapper in cast("JsonList", service_groups):
        if not isinstance(service_group_wrapper, dict):
            continue
        service_group = cast("JsonDict", service_group_wrapper).get("service_group")
        if not isinstance(service_group, dict):
            continue
        extracted_services.extend(extract_service_dicts(cast("JsonDict", service_group).get("services", [])))
    return extracted_services


def extract_services_from_container(container: JsonDict) -> list[JsonDict]:
    services = extract_service_dicts(container.get("services", []))
    services.extend(extract_service_group_services(container.get("service_groups", [])))
    return services


def extract_services(connection: JsonDict) -> list[JsonDict]:
    services = extract_services_from_container(connection)
    if services:
        return services
    used_interface = connection.get("used_interface")
    if not isinstance(used_interface, dict):
        return []
    return extract_services_from_container(cast("JsonDict", used_interface))


def extend_group_items(groups: list[JsonDict], container: JsonDict, field_names: list[str]) -> None:
    for field_name in field_names:
        groups_raw = container.get(field_name)
        if not isinstance(groups_raw, list):
            continue
        groups_list = cast("JsonList", groups_raw)
        groups.extend(cast("JsonDict", item) for item in groups_list if isinstance(item, dict))


def extract_connection_policy_groups(connection: JsonDict, side: str) -> list[JsonDict]:
    group_fields = [f"{side}_approles", f"{side}_areas"]
    groups: list[JsonDict] = []
    extend_group_items(groups, connection, group_fields)
    if groups:
        return groups

    used_interface = connection.get("used_interface")
    if not isinstance(used_interface, dict):
        return []
    extend_group_items(groups, cast("JsonDict", used_interface), group_fields)
    return groups


def extract_protocol_name_from_service(service: JsonDict) -> str | None:
    protocol_object = service.get("protocol")
    if not isinstance(protocol_object, dict):
        return None
    protocol_raw_name = cast("JsonDict", protocol_object).get("name")
    return protocol_raw_name if isinstance(protocol_raw_name, str) else None


def update_service_ports(
    ports: set[int],
    port_ranges: set[tuple[int, int]],
    service: JsonDict,
) -> None:
    port = service.get("port")
    port_end = service.get("port_end")
    if not isinstance(port, int) or port <= 0:
        return
    if isinstance(port_end, int) and port_end > port:
        port_ranges.add((port, port_end))
        return
    ports.add(port)


def collect_ports_and_protocols(
    services: list[JsonDict],
    default_protocol: str,
) -> tuple[list[int], list[dict[str, int]], list[str]]:
    ports: set[int] = set()
    port_ranges: set[tuple[int, int]] = set()
    protocols: set[str] = set()

    for service in services:
        protocol = normalize_protocol_name(extract_protocol_name_from_service(service))
        if protocol:
            protocols.add(protocol)
        update_service_ports(ports, port_ranges, service)

    if not protocols:
        protocols.add(default_protocol.upper())

    sorted_ports = sorted(ports)
    sorted_ranges = [{"start": start, "end": end} for (start, end) in sorted(port_ranges)]
    sorted_protocols = sorted(protocols)
    return sorted_ports, sorted_ranges, sorted_protocols


def collect_ports_and_protocols_by_protocol(
    services: list[JsonDict],
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


def collect_approle_identifiers(connection_approles: list[JsonDict]) -> list[str]:
    identifiers: set[str] = set()
    for connection_approle in connection_approles:
        nwgroup = connection_approle.get("nwgroup")
        if not isinstance(nwgroup, dict):
            continue
        nwgroup_dict = cast("JsonDict", nwgroup)
        identifier = format_approle_identifier(nwgroup_dict.get("name"), nwgroup_dict.get("id_string"))
        if identifier:
            identifiers.add(identifier)
    return sorted(identifiers)


def build_missing_approle_warning_details(connection: JsonDict) -> str:
    source_approles = extract_connection_policy_groups(connection, "source")
    destination_approles = extract_connection_policy_groups(connection, "destination")

    source_identifiers = collect_approle_identifiers(source_approles)
    destination_identifiers = collect_approle_identifiers(destination_approles)
    connection_json = json.dumps(connection, sort_keys=True, ensure_ascii=True)
    return (
        f"all_source_approles={source_identifiers}, "
        f"all_destination_approles={destination_identifiers}, "
        f"connection={connection_json}"
    )


def resolve_approle_labels(
    connection_approles: list[JsonDict],
    approle_id_map: dict[str, list[str]],
) -> AppRoleResolution:
    label_ids: set[str] = set()
    missing_labels: set[str] = set()

    for connection_approle in connection_approles:
        nwgroup = connection_approle.get("nwgroup")
        if not isinstance(nwgroup, dict):
            continue
        nwgroup_dict = cast("JsonDict", nwgroup)
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


def build_guardicore_ruleset_name(connection: JsonDict) -> str:
    connection_id = connection.get("id")
    default_ruleset_name = f"FWOC{connection_id}"
    owner = connection.get("owner")
    if not isinstance(owner, dict):
        return default_ruleset_name

    owner_dict = cast("JsonDict", owner)
    app_id_external = owner_dict.get("app_id_external")
    if not isinstance(app_id_external, str) or not app_id_external.strip():
        return default_ruleset_name

    normalized_app_id = strip_app_id_prefix(app_id_external)
    if not normalized_app_id:
        return default_ruleset_name
    return f"FWOA{normalized_app_id} {default_ruleset_name}"


def build_rule_payload(
    connection: JsonDict,
    approle_id_map: dict[str, list[str]],
    default_ip_protocol: str,
    action: str,
    section_position: str,
) -> RuleBuildResult:
    source_approles = extract_connection_policy_groups(connection, "source")
    destination_approles = extract_connection_policy_groups(connection, "destination")

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
                "missing Guardicore AppRole/NetworkArea labels: "
                f"source={source_missing}, destination={destination_missing}"
            ),
        )

    if not source_resolution.label_ids or not destination_resolution.label_ids:
        source_identifiers = collect_approle_identifiers(source_approles)
        destination_identifiers = collect_approle_identifiers(destination_approles)
        return RuleBuildResult(
            payloads=[],
            skip_reason=(
                "missing source/destination AppRole/NetworkArea labels: "
                f"source={source_identifiers}, destination={destination_identifiers}"
            ),
        )

    services = extract_services(connection)
    ports_by_protocol = collect_ports_and_protocols_by_protocol(services, default_ip_protocol)

    ruleset_name = build_guardicore_ruleset_name(connection)
    payloads: list[JsonDict] = []
    skipped_protocols: set[str] = set()
    for protocol, (ports, port_ranges) in ports_by_protocol.items():
        if protocol == "ESP":
            skipped_protocols.add(protocol)
            continue
        payload: JsonDict = {
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


def post_guardicore_rule(config: GuardicoreConfig, payload: JsonDict) -> None:
    post_guardicore_json(
        config,
        DEFAULT_GUARDICORE_RULES_CREATE_ENDPOINT,
        payload,
        "Guardicore rule creation failed",
    )


def post_guardicore_json(
    config: GuardicoreConfig,
    endpoint_suffix: str,
    payload: JsonDict,
    error_message: str,
) -> None:
    endpoint = config.base_url.rstrip("/") + endpoint_suffix
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
            raise GuardicoreRuleProvisioningError(f"{error_message}: {exc}") from exc


def post_guardicore_revision(config: GuardicoreConfig, rulesets: list[str], comments: str) -> None:
    if not rulesets:
        return
    post_guardicore_json(
        config,
        DEFAULT_GUARDICORE_REVISIONS_CREATE_ENDPOINT,
        {"comments": comments},
        "Guardicore publish failed",
    )


def build_runtime_configs(
    args: argparse.Namespace,
    fwo_verify: bool | str,
    guardicore_verify: bool | str,
    fwo_jwt: str,
    guardicore_token: str,
) -> tuple[FwoConfig, GuardicoreConfig]:
    return (
        FwoConfig(
            graphql_url=args.fwo_graphql_url,
            jwt=fwo_jwt,
            verify_ssl=fwo_verify,
            timeout_seconds=args.timeout,
            role=args.fwo_role,
        ),
        GuardicoreConfig(
            base_url=args.guardicore_url,
            token=guardicore_token,
            verify_ssl=guardicore_verify,
            timeout_seconds=args.timeout,
        ),
    )


def log_fetch_summary(logger: logging.Logger, connections: list[JsonDict], app_ids: list[str] | None) -> None:
    if app_ids is None:
        logger.info("Fetched %s FWO connections for all apps.", len(connections))
        return
    logger.info("Fetched %s filtered FWO connections.", len(connections))


def log_approle_map_stats(logger: logging.Logger, approle_map_stats: AppRoleMapStats) -> None:
    logger.info(
        "Fetched Guardicore AppRole/NetworkArea labels: total=%s, unique_label_ids=%s, full_value_keys=%s, "
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
        "Policy label map diagnostics: non_approle_candidates=%s, duplicate_label_id_candidates=%s, "
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
        logger.info("Top repeated policy-label full values: %s", approle_map_stats.top_repeated_full_values)


def log_skipped_connection(logger: logging.Logger, connection: JsonDict, skip_reason: str | None) -> None:
    if skip_reason and "AppRole" in skip_reason:
        logger.warning(
            "Skipping connection id=%s name=%s: %s; %s",
            connection.get("id"),
            connection.get("name"),
            skip_reason,
            build_missing_approle_warning_details(connection),
        )
        return
    logger.warning(
        "Skipping connection id=%s name=%s: %s",
        connection.get("id"),
        connection.get("name"),
        skip_reason,
    )


def apply_rule_payloads(
    args: argparse.Namespace,
    logger: logging.Logger,
    guardicore_config: GuardicoreConfig,
    connection: JsonDict,
    payloads: list[JsonDict],
) -> set[str]:
    created_rulesets: set[str] = set()
    for payload in payloads:
        if args.dry_run:
            logger.info("Dry run payload for connection id=%s: %s", connection.get("id"), json.dumps(payload))
        else:
            post_guardicore_rule(guardicore_config, payload)
        ruleset_name = payload.get("ruleset_name")
        if isinstance(ruleset_name, str) and ruleset_name:
            created_rulesets.add(ruleset_name)
    return created_rulesets


def process_connections(
    args: argparse.Namespace,
    logger: logging.Logger,
    connections: list[JsonDict],
    approle_id_map: dict[str, list[str]],
    guardicore_config: GuardicoreConfig,
) -> tuple[int, int, list[str]]:
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
            log_skipped_connection(logger, connection, result.skip_reason)
            continue
        created_rulesets.update(apply_rule_payloads(args, logger, guardicore_config, connection, result.payloads))
        created += len(result.payloads)
    return created, skipped, sorted(created_rulesets)


def publish_revision_if_needed(
    args: argparse.Namespace,
    logger: logging.Logger,
    guardicore_config: GuardicoreConfig,
    rulesets: list[str],
) -> None:
    if args.dry_run:
        logger.info("Dry run publish payload: %s", json.dumps({"comments": args.publish_comments}))
        return
    post_guardicore_revision(guardicore_config, rulesets, args.publish_comments)


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
        fwo_config, guardicore_config = build_runtime_configs(
            args,
            fwo_verify,
            guardicore_verify,
            fwo_jwt,
            guardicore_token,
        )
        connections = fetch_connections_from_fwo(fwo_config, app_ids=args.app_ids)
        log_fetch_summary(logger, connections, args.app_ids)
        approle_id_map, approle_map_stats = fetch_guardicore_approle_map(guardicore_config)
        log_approle_map_stats(logger, approle_map_stats)
        created, skipped, sorted_rulesets = process_connections(
            args,
            logger,
            connections,
            approle_id_map,
            guardicore_config,
        )
        publish_revision_if_needed(args, logger, guardicore_config, sorted_rulesets)
        logger.info("Done. Created %s rule(s), skipped %s connection(s).", created, skipped)
        return 0
    except GuardicoreRuleProvisioningError:
        logger.exception("Guardicore rule provisioning failed.")
        return 1


if __name__ == "__main__":
    sys.exit(main())
