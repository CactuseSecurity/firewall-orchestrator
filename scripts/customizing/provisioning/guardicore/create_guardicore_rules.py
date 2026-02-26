#!/usr/bin/python3
"""Create Guardicore policy rules from FWO active connections."""

from __future__ import annotations

import argparse
import json
import logging
import re
import sys
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
    missing_names: list[str]


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
        required=True,
        type=parse_app_ids,
        help='JSON array of external app IDs used to filter connections, e.g. ["APP-1234","APP-2345"]',
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


def build_graphql_query() -> str:
    query = """
query getConnectionsForGuardicore($appIds: [String!]!) {
  modelling_connection(
    where: {
      removed: { _eq: false },
      is_interface: { _eq: false },
      owner: { app_id_external: { _in: $appIds } }
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
    return " ".join(query.splitlines())


def build_graphql_variables(app_ids: list[str]) -> dict[str, Any]:
    return {"appIds": app_ids}


def fetch_connections_from_fwo(
    fwo_config: FwoConfig,
    app_ids: list[str],
) -> list[dict[str, Any]]:
    result = run_graphql_query(
        fwo_config,
        build_graphql_query(),
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


def fetch_guardicore_approle_map(config: GuardicoreConfig) -> dict[str, list[str]]:
    endpoint = config.base_url.rstrip("/") + DEFAULT_GUARDICORE_LABELS_LIST_ENDPOINT
    headers = {
        "Authorization": f"Bearer {config.token}",
        "Content-Type": HTTP_CONTENT_TYPE_JSON,
    }
    base_params: dict[str, Any] = {
        "fields": "id,key,value",
        "limit": GUARDICORE_LABELS_PAGE_SIZE,
        "offset": 0,
    }

    app_role_map: dict[str, list[str]] = {}
    offset = 0

    with requests.Session() as session:
        apply_ssl_settings(session, config.verify_ssl)
        session.headers.update(headers)
        while True:
            params = dict(base_params)
            params["offset"] = offset
            try:
                response = session.get(endpoint, params=params, timeout=config.timeout_seconds)
                response.raise_for_status()
            except requests.exceptions.RequestException as exc:
                raise GuardicoreRuleProvisioningError(f"Guardicore label query failed: {exc}") from exc

            result_obj = response.json()
            labels = parse_existing_labels(result_obj)
            for label in labels:
                key = label.get("key")
                value = label.get("value")
                label_id = label.get("id")
                if not isinstance(key, str) or not isinstance(value, str) or not isinstance(label_id, str):
                    continue
                if key.strip() != DEFAULT_GUARDICORE_KEY_APPROLE:
                    continue
                label_value = value.strip()
                if not label_value:
                    continue
                app_role_map.setdefault(label_value, []).append(label_id)
                parsed_id_string = extract_id_string_from_label_value(label_value)
                if parsed_id_string:
                    app_role_map.setdefault(parsed_id_string, []).append(label_id)

            if not isinstance(result_obj, dict):
                break
            result = cast("dict[str, Any]", result_obj)
            objects_obj = result.get("objects")
            if not isinstance(objects_obj, list):
                break
            objects = cast("list[Any]", objects_obj)
            if len(objects) == 0:
                break
            total_count_obj = result.get("total_count")
            offset += len(objects)
            if isinstance(total_count_obj, int):
                if offset >= total_count_obj:
                    break
                continue
            if len(objects) < GUARDICORE_LABELS_PAGE_SIZE:
                break

    return app_role_map


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

    return services


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


def resolve_approle_labels(
    connection_approles: list[dict[str, Any]],
    approle_id_map: dict[str, list[str]],
) -> AppRoleResolution:
    label_ids: set[str] = set()
    missing_names: set[str] = set()

    for connection_approle in connection_approles:
        nwgroup = connection_approle.get("nwgroup")
        if not isinstance(nwgroup, dict):
            continue
        nwgroup_dict = cast("dict[str, Any]", nwgroup)
        id_string = nwgroup_dict.get("id_string")
        name = nwgroup_dict.get("name")
        if not isinstance(name, str) or not name.strip():
            continue

        matched_label_ids: list[str] | None = None
        if isinstance(id_string, str) and id_string.strip():
            matched_label_ids = approle_id_map.get(id_string.strip())
        if not matched_label_ids:
            matched_label_ids = approle_id_map.get(name.strip())
        if matched_label_ids:
            label_ids.update(matched_label_ids)
        else:
            missing_names.add(name.strip())

    return AppRoleResolution(label_ids=sorted(label_ids), missing_names=sorted(missing_names))


def to_guardicore_or_labels(label_ids: list[str]) -> list[dict[str, list[str]]]:
    return [{"and_labels": [label_id]} for label_id in label_ids]


def build_rule_payload(
    connection: dict[str, Any],
    approle_id_map: dict[str, list[str]],
    default_ip_protocol: str,
    action: str,
    section_position: str,
) -> RuleBuildResult:
    source_approles_raw = connection.get("source_approles")
    destination_approles_raw = connection.get("destination_approles")
    source_approles: list[dict[str, Any]] = []
    destination_approles: list[dict[str, Any]] = []
    if isinstance(source_approles_raw, list):
        source_approles_list = cast("list[Any]", source_approles_raw)
        source_approles = [cast("dict[str, Any]", item) for item in source_approles_list if isinstance(item, dict)]
    if isinstance(destination_approles_raw, list):
        destination_approles_list = cast("list[Any]", destination_approles_raw)
        destination_approles = [
            cast("dict[str, Any]", item) for item in destination_approles_list if isinstance(item, dict)
        ]

    source_resolution = resolve_approle_labels(
        source_approles,
        approle_id_map,
    )
    destination_resolution = resolve_approle_labels(
        destination_approles,
        approle_id_map,
    )

    missing = source_resolution.missing_names + destination_resolution.missing_names
    if missing:
        return RuleBuildResult(payloads=[], skip_reason=f"missing Guardicore AppRole labels: {sorted(set(missing))}")

    if not source_resolution.label_ids or not destination_resolution.label_ids:
        return RuleBuildResult(payloads=[], skip_reason="missing source/destination AppRole labels")

    services = extract_services(connection)
    ports_by_protocol = collect_ports_and_protocols_by_protocol(services, default_ip_protocol)

    connection_id = connection.get("id")
    ruleset_name = f"FWOC{connection_id}"
    payloads: list[dict[str, Any]] = []
    for protocol, (ports, port_ranges) in ports_by_protocol.items():
        payloads.append(
            {
                "ruleset_name": ruleset_name,
                "ports": ports,
                "port_ranges": port_ranges,
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
    payload = {"rulesets": rulesets, "comments": comments}

    with requests.Session() as session:
        apply_ssl_settings(session, config.verify_ssl)
        session.headers.update(headers)
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
        logger.info("Fetched %s filtered FWO connections.", len(connections))

        approle_id_map = fetch_guardicore_approle_map(guardicore_config)
        logger.info("Fetched %s Guardicore AppRole labels.", len(approle_id_map))

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
                json.dumps({"rulesets": sorted_rulesets, "comments": args.publish_comments}),
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
