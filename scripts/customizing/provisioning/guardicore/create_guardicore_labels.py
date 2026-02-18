#!/usr/bin/python3
"""Create Guardicore labels from FWO GraphQL data."""

from __future__ import annotations

import argparse
import json
import logging
import sys
from dataclasses import dataclass
from typing import TYPE_CHECKING, Any, cast

if TYPE_CHECKING:
    from collections.abc import Iterable

import requests
import urllib3

DEFAULT_GUARDICORE_API_V4_BASE_ENDPOINT: str = "/api/v4.0/"
DEFAULT_GUARDICORE_AUTH_ENDPOINT: str = "/api/v3.0/authenticate"
DEFAULT_GUARDICORE_LABELS_BULK_ENDPOINT: str = f"{DEFAULT_GUARDICORE_API_V4_BASE_ENDPOINT}labels/bulk"
DEFAULT_GUARDICORE_LABELS_LIST_ENDPOINT: str = f"{DEFAULT_GUARDICORE_API_V4_BASE_ENDPOINT}labels"
DEFAULT_GUARDICORE_LABELS_LIST_FIELDS: str = "id,key,value,dynamic_criteria"
DEFAULT_GUARDICORE_LABELS_PAGE_SIZE: int = 1000
DEFAULT_GUARDICORE_FIELD: str = "numeric_ip_addresses"
DEFAULT_GUARDICORE_KEY_APPZONE: str = "AppZone"
DEFAULT_GUARDICORE_KEY_APPROLE: str = "AppRole"
HTTP_CONTENT_TYPE_JSON: str = "application/json"
HTTP_OK: int = 200


def parse_app_ids(value: str) -> list[str]:
    """Parse and validate a JSON array of app IDs."""
    try:
        parsed = json.loads(value)
    except json.JSONDecodeError as exc:
        raise argparse.ArgumentTypeError('--app-ids must be valid JSON, e.g. ["APP-1234", "APP-2345"]') from exc

    if not isinstance(parsed, list):
        raise argparse.ArgumentTypeError("--app-ids must be a JSON array of strings")

    parsed_list = cast("list[object]", parsed)
    app_ids: list[str] = []
    for item in parsed_list:
        if not isinstance(item, str):
            raise argparse.ArgumentTypeError("--app-ids must be a JSON array of strings")
        app_ids.append(item)
    return app_ids


def parse_group_types(value: str) -> list[int]:
    """Parse and validate a JSON array of group types."""
    try:
        parsed = json.loads(value)
    except json.JSONDecodeError as exc:
        raise argparse.ArgumentTypeError("--include-group-types must be valid JSON, e.g. [20, 21]") from exc

    if not isinstance(parsed, list):
        raise argparse.ArgumentTypeError("--include-group-types must be a JSON array of integers")

    parsed_list = cast("list[object]", parsed)
    group_types: list[int] = []
    for item in parsed_list:
        if not isinstance(item, int):
            raise argparse.ArgumentTypeError("--include-group-types must be a JSON array of integers")
        group_types.append(item)

    if not group_types:
        raise argparse.ArgumentTypeError("--include-group-types must include at least one group type")

    return group_types


def build_graphql_query() -> str:
    """Build GraphQL query using a variable-based owner filter."""
    return """
query getARsAndAZs($ownerFilter: owner_bool_exp!) {
  owner(where: $ownerFilter) {
    app_id_external
    name
    common_service_possible
    nwgroups {
      name
      id_string
      app_id
      nwobject_nwgroups {
        owner_network {
          name
          ip
          ip_end
        }
      }
    }
  }
}
""".strip()


def build_graphql_variables(
    app_ids: list[str] | None = None,
    include_common_services: bool = False,
    include_group_types: list[int] | None = None,
) -> dict[str, Any]:
    """Build GraphQL variables for app-id filtering and optional owner scopes."""
    app_filter: dict[str, Any] = {}
    if app_ids is not None:
        app_filter = {"app_id_external": {"_in": app_ids}}

    group_types = include_group_types if include_group_types is not None else [20, 21]
    owners_or_clauses: list[dict[str, Any]] = []
    and_conditions: list[dict[str, Any]] = [{"nwgroups": {"group_type": {"_in": group_types}}}]
    if app_filter:
        and_conditions.append(app_filter)
    owners_or_clauses.append({"_and": and_conditions})

    if include_common_services:
        owners_or_clauses.append({"common_service_possible": {"_eq": True}})

    return {"ownerFilter": {"_or": owners_or_clauses}}


class GuardicoreProvisioningError(Exception):
    """Raised when Guardicore provisioning fails."""


@dataclass(frozen=True)
class Criteria:
    field: str
    op: str
    argument: str


@dataclass(frozen=True)
class LabelItem:
    key: str
    value: str
    criteria: list[Criteria]


@dataclass(frozen=True)
class GuardicoreConfig:
    base_url: str
    token: str
    verify_ssl: bool | str
    timeout_seconds: int


@dataclass(frozen=True)
class FwoConfig:
    graphql_url: str
    jwt: str
    verify_ssl: bool | str
    timeout_seconds: int
    role: str


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Create Guardicore labels from FWO GraphQL output")
    parser.add_argument("--fwo-graphql-url", required=True, help="Hasura GraphQL endpoint URL")
    parser.add_argument("--fwo-jwt", help="JWT for FWO GraphQL calls (skips login if provided)")
    parser.add_argument("--fwo-user", help="FWO username (used if --fwo-jwt not provided)")
    parser.add_argument("--fwo-password", help="FWO password (used if --fwo-jwt not provided)")
    parser.add_argument("--fwo-middleware-url", help="FWO middleware base URL for login")
    parser.add_argument("--fwo-role", default="reporter", help="Hasura role for the GraphQL call")
    parser.add_argument(
        "--guardicore-url",
        required=True,
        help="Guardicore base URL, e.g. https://x.y.z",
    )
    parser.add_argument("--guardicore-user", required=True, help="Guardicore username")
    parser.add_argument("--guardicore-password", required=True, help="Guardicore password")
    parser.add_argument(
        "--guardicore-ca-cert",
        help="Path to a CA bundle for the Guardicore API (useful for self-signed certs)",
    )
    parser.add_argument(
        "--guardicore-insecure",
        action="store_true",
        help="Disable SSL verification for Guardicore API calls only (not recommended)",
    )
    parser.add_argument(
        "--batch-size",
        type=int,
        default=100,
        help="Number of labels per Guardicore bulk call",
    )
    parser.add_argument(
        "-a",
        "--app-ids",
        type=parse_app_ids,
        help='Optional JSON array of app-ids to filter, e.g. ["APP-1234","APP-2345"]',
    )
    parser.add_argument(
        "--include-common-services",
        action="store_true",
        help="Include labels belonging to owners with common_service_possible=true",
    )
    parser.add_argument(
        "--include-group-types",
        type=parse_group_types,
        default=[20, 21],
        help="JSON array of group types to include, e.g. [20,21] (default: [20,21])",
    )
    parser.add_argument("--include-empty", action="store_true", help="Include labels with no criteria")
    parser.add_argument(
        "--dry-run",
        action="store_true",
        help="Print payload instead of calling Guardicore",
    )
    parser.add_argument(
        "--fwo-ca-cert",
        help="Path to a CA bundle for the FWO API (useful for self-signed certs)",
    )
    parser.add_argument(
        "--fwo-insecure",
        action="store_true",
        help="Disable SSL verification for FWO API calls only (not recommended)",
    )
    parser.add_argument(
        "--insecure",
        action="store_true",
        help="Disable SSL verification (not recommended)",
    )
    parser.add_argument("--timeout", type=int, default=60, help="HTTP timeout in seconds")
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
        raise GuardicoreProvisioningError("Missing arguments for login: " + ", ".join(missing))


def require_guardicore_fields(args: argparse.Namespace) -> None:
    missing: list[str] = []
    if not args.guardicore_user:
        missing.append("--guardicore-user")
    if not args.guardicore_password:
        missing.append("--guardicore-password")
    if missing:
        raise GuardicoreProvisioningError("Missing arguments for Guardicore login: " + ", ".join(missing))


def apply_ssl_settings(session: requests.Session, verify_setting: bool | str) -> None:
    session.verify = verify_setting
    if verify_setting is False:
        urllib3.disable_warnings(urllib3.exceptions.InsecureRequestWarning)


def login_fwo(user: str, password: str, middleware_url: str, verify_ssl: bool | str, timeout: int) -> str:
    payload: dict[str, str] = {"Username": user, "Password": password}
    headers: dict[str, str] = {"Content-Type": HTTP_CONTENT_TYPE_JSON}
    endpoint = middleware_url.rstrip("/") + "/api/AuthenticationToken/Get"

    with requests.Session() as session:
        apply_ssl_settings(session, verify_ssl)
        try:
            response = session.post(endpoint, json=payload, headers=headers, timeout=timeout)
        except requests.exceptions.RequestException as exc:
            raise GuardicoreProvisioningError(f"FWO login failed for {endpoint}: {exc}") from exc

    if response.status_code != HTTP_OK:
        raise GuardicoreProvisioningError(f"FWO login failed with status {response.status_code}: {response.text}")
    return response.text


def login_guardicore(user: str, password: str, base_url: str, verify_ssl: bool | str, timeout: int) -> str:
    payload: dict[str, str] = {"username": user, "password": password}
    headers: dict[str, str] = {"Content-Type": HTTP_CONTENT_TYPE_JSON}
    endpoint = base_url.rstrip("/") + DEFAULT_GUARDICORE_AUTH_ENDPOINT

    with requests.Session() as session:
        apply_ssl_settings(session, verify_ssl)
        try:
            response = session.post(endpoint, json=payload, headers=headers, timeout=timeout)
            response.raise_for_status()
        except requests.exceptions.RequestException as exc:
            raise GuardicoreProvisioningError(f"Guardicore login failed for {endpoint}: {exc}") from exc

    try:
        result = response.json()
    except ValueError as exc:
        raise GuardicoreProvisioningError("Guardicore login response was not valid JSON.") from exc

    for token_key in ("access_token", "token", "jwt", "accessToken"):
        token = result.get(token_key)
        if token:
            return token

    raise GuardicoreProvisioningError(f"Guardicore login response did not include a token: {result}")


def run_graphql_query(config: FwoConfig, query: str, variables: dict[str, Any]) -> dict[str, Any]:
    headers: dict[str, str] = {
        "Content-Type": HTTP_CONTENT_TYPE_JSON,
        "Authorization": f"Bearer {config.jwt}",
        "x-hasura-role": config.role,
    }
    payload: dict[str, Any] = {"query": query, "variables": variables}

    with requests.Session() as session:
        apply_ssl_settings(session, config.verify_ssl)
        session.headers.update(headers)
        try:
            response = session.post(config.graphql_url, json=payload, timeout=config.timeout_seconds)
            response.raise_for_status()
        except requests.exceptions.RequestException as exc:
            raise GuardicoreProvisioningError(f"GraphQL query failed: {exc}") from exc

    result = response.json()
    if "errors" in result:
        raise GuardicoreProvisioningError(f"GraphQL returned errors: {result['errors']}")
    return result


def normalize_ip(value: str) -> str:
    return value.split("/")[0]


def criteria_from_network(ip: str | None, ip_end: str | None) -> Criteria | None:
    if not ip or not ip_end:
        return None
    if ip == ip_end:
        return Criteria(field=DEFAULT_GUARDICORE_FIELD, op="SUBNET", argument=ip)
    return Criteria(
        field=DEFAULT_GUARDICORE_FIELD,
        op="RANGE",
        argument=f"{normalize_ip(ip)}-{normalize_ip(ip_end)}",
    )


def label_key_from_id(id_string: str) -> str | None:
    if id_string.startswith("AZ"):
        return DEFAULT_GUARDICORE_KEY_APPZONE
    if id_string.startswith("AR"):
        return DEFAULT_GUARDICORE_KEY_APPROLE
    return None


def extract_criteria(nwgroup: dict[str, Any]) -> list[Criteria]:
    criteria_set: dict[tuple[str, str, str], Criteria] = {}
    for nwobject in nwgroup.get("nwobject_nwgroups", []):
        owner_network = nwobject.get("owner_network", {})
        criteria = criteria_from_network(owner_network.get("ip"), owner_network.get("ip_end"))
        if not criteria:
            continue
        criteria_set[(criteria.field, criteria.op, criteria.argument)] = criteria
    return list(criteria_set.values())


def build_label_from_group(owner: dict[str, Any], nwgroup: dict[str, Any], include_empty: bool) -> LabelItem | None:
    id_string = (
        f"{owner.get('name')} ({owner.get('app_id_external')}) - {nwgroup.get('name')} ({nwgroup.get('id_string')})"
    )
    if not id_string:
        return None
    key = label_key_from_id(str(nwgroup.get("id_string")))
    if not key:
        return None
    criteria_list = extract_criteria(nwgroup)
    if not criteria_list and not include_empty:
        return None
    return LabelItem(key=key, value=id_string, criteria=criteria_list)


def build_labels_from_response(
    response: dict[str, Any],
    include_empty: bool = False,
) -> list[LabelItem]:
    owners: list[dict[str, Any]] = response.get("data", {}).get("owner", [])
    labels: list[LabelItem] = []

    for owner in owners:
        for nwgroup in owner.get("nwgroups", []):
            label = build_label_from_group(owner, nwgroup, include_empty)
            if label:
                labels.append(label)

    return labels


def chunked(items: list[LabelItem], batch_size: int) -> Iterable[list[LabelItem]]:
    for i in range(0, len(items), batch_size):
        yield items[i : i + batch_size]


def to_guardicore_payload(items: list[LabelItem]) -> list[dict[str, Any]]:
    return [
        {
            "key": item.key,
            "value": item.value,
            "criteria": [
                {
                    "field": criteria.field,
                    "op": criteria.op,
                    "argument": criteria.argument,
                }
                for criteria in item.criteria
            ],
        }
        for item in items
    ]


def parse_existing_label_pairs(payload: Any) -> set[tuple[str, str]]:
    """Extract existing key/value pairs from Guardicore label list responses."""
    label_items: list[dict[str, Any]] = []
    if isinstance(payload, list):
        payload_list = cast("list[Any]", payload)
        label_items.extend(cast("dict[str, Any]", item) for item in payload_list if isinstance(item, dict))
    elif isinstance(payload, dict):
        payload_dict = cast("dict[str, Any]", payload)
        for candidate_key in ("objects", "items", "labels", "results", "data"):
            candidate_value = payload_dict.get(candidate_key)
            if isinstance(candidate_value, list):
                candidate_list = cast("list[Any]", candidate_value)
                label_items.extend(cast("dict[str, Any]", item) for item in candidate_list if isinstance(item, dict))
                break
        if not label_items:
            label_items = [payload_dict]

    existing_pairs: set[tuple[str, str]] = set()
    for label_item in label_items:
        key = label_item.get("key")
        value = label_item.get("value")
        nested_label_value = label_item.get("label")
        if (not isinstance(key, str) or not isinstance(value, str)) and isinstance(nested_label_value, dict):
            nested_label = cast("dict[str, Any]", nested_label_value)
            key = nested_label.get("key")
            value = nested_label.get("value")
        if isinstance(key, str) and isinstance(value, str):
            existing_pairs.add((key.strip(), value.strip()))
    return existing_pairs


def fetch_existing_guardicore_labels(config: GuardicoreConfig) -> set[tuple[str, str]]:
    headers = {
        "Authorization": f"Bearer {config.token}",
        "accept": HTTP_CONTENT_TYPE_JSON,
    }
    endpoint = config.base_url.rstrip("/") + DEFAULT_GUARDICORE_LABELS_LIST_ENDPOINT
    base_params = {
        "expand": "dynamic_assets",
        "fields": DEFAULT_GUARDICORE_LABELS_LIST_FIELDS,
        "limit": DEFAULT_GUARDICORE_LABELS_PAGE_SIZE,
    }
    existing_pairs: set[tuple[str, str]] = set()
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
                raise GuardicoreProvisioningError(f"Guardicore label query failed: {exc}") from exc

            try:
                result = response.json()
            except ValueError as exc:
                raise GuardicoreProvisioningError("Guardicore label query response was not valid JSON.") from exc

            existing_pairs.update(parse_existing_label_pairs(result))
            if not isinstance(result, dict):
                break

            result_dict = cast("dict[str, Any]", result)
            objects_value = result_dict.get("objects")
            objects: list[dict[str, Any]] = []
            if isinstance(objects_value, list):
                objects_list = cast("list[Any]", objects_value)
                objects.extend(cast("dict[str, Any]", obj) for obj in objects_list if isinstance(obj, dict))
            if len(objects) == 0:
                break

            total_count = result_dict.get("total_count")
            offset += len(objects)
            if isinstance(total_count, int) and offset >= total_count:
                break
            if isinstance(total_count, int):
                continue
            if len(objects) < DEFAULT_GUARDICORE_LABELS_PAGE_SIZE:
                break

    return existing_pairs


def filter_missing_labels(labels: list[LabelItem], existing_pairs: set[tuple[str, str]]) -> list[LabelItem]:
    """Filter out labels that already exist in Guardicore by key/value."""
    return [label for label in labels if (label.key.strip(), label.value.strip()) not in existing_pairs]


def post_guardicore_labels(config: GuardicoreConfig, payload: list[dict[str, Any]]) -> None:
    if not payload:
        return

    headers = {
        "Authorization": f"Bearer {config.token}",
        "Content-Type": HTTP_CONTENT_TYPE_JSON,
    }
    endpoint = config.base_url.rstrip("/") + DEFAULT_GUARDICORE_LABELS_BULK_ENDPOINT

    with requests.Session() as session:
        apply_ssl_settings(session, config.verify_ssl)
        session.headers.update(headers)
        try:
            response = session.post(endpoint, json=payload, timeout=config.timeout_seconds)
            response.raise_for_status()
        except requests.exceptions.RequestException as exc:
            raise GuardicoreProvisioningError(f"Guardicore API call failed: {exc}") from exc


def main() -> int:
    logging.basicConfig(level=logging.INFO, format="%(levelname)s: %(message)s")
    logger = logging.getLogger(__name__)
    args = parse_args()

    try:
        require_login_fields(args)
        require_guardicore_fields(args)
    except GuardicoreProvisioningError:
        logger.exception("Argument validation failed.")
        return 2

    try:
        verify_ssl = not args.insecure
        if args.fwo_insecure:
            fwo_verify = False
        elif args.fwo_ca_cert:
            fwo_verify = args.fwo_ca_cert
        else:
            fwo_verify = verify_ssl

        if args.guardicore_insecure:
            guardicore_verify = False
        elif args.guardicore_ca_cert:
            guardicore_verify = args.guardicore_ca_cert
        else:
            guardicore_verify = verify_ssl

        if args.fwo_jwt:
            jwt = args.fwo_jwt
        else:
            jwt = login_fwo(
                args.fwo_user,
                args.fwo_password,
                args.fwo_middleware_url,
                fwo_verify,
                args.timeout,
            )

        fwo_config = FwoConfig(
            graphql_url=args.fwo_graphql_url,
            jwt=jwt,
            verify_ssl=fwo_verify,
            timeout_seconds=args.timeout,
            role=args.fwo_role,
        )
        response = run_graphql_query(
            fwo_config,
            build_graphql_query(),
            build_graphql_variables(
                args.app_ids,
                include_common_services=args.include_common_services,
                include_group_types=args.include_group_types,
            ),
        )
        labels = build_labels_from_response(
            response,
            include_empty=args.include_empty,
        )

        if not labels:
            logger.info("No labels to send.")
            return 0

        guardicore_config = GuardicoreConfig(
            base_url=args.guardicore_url,
            token=login_guardicore(
                args.guardicore_user,
                args.guardicore_password,
                args.guardicore_url,
                guardicore_verify,
                args.timeout,
            ),
            verify_ssl=guardicore_verify,
            timeout_seconds=args.timeout,
        )
        existing_label_pairs = fetch_existing_guardicore_labels(guardicore_config)
        labels_to_create = filter_missing_labels(labels, existing_label_pairs)
        skipped_existing = len(labels) - len(labels_to_create)
        if not labels_to_create:
            logger.info("All %s labels already exist. Nothing to create.", len(labels))
            return 0

        total_sent = 0
        for batch in chunked(labels_to_create, args.batch_size):
            payload = to_guardicore_payload(batch)
            if args.dry_run:
                logger.info("Dry run payload: %s", json.dumps(payload, indent=2))
            else:
                post_guardicore_labels(guardicore_config, payload)
            total_sent += len(batch)

        logger.info(
            "Processed %s label(s): created=%s, skipped_existing=%s.",
            len(labels),
            total_sent,
            skipped_existing,
        )
        return 0

    except GuardicoreProvisioningError:
        logger.exception("Guardicore provisioning failed.")
        return 1


if __name__ == "__main__":
    sys.exit(main())
