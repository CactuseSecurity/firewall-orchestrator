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

DEFAULT_GUARDICORE_LABELS_ENDPOINT: str = "/api/v3.0/api/automation_api/v1/labels/bulk"
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


def build_graphql_query() -> str:
    """Build GraphQL query using variable-based app filtering."""
    return """
query getARsAndAZs($appFilter: owner_bool_exp!) {
  owner(where: {_or:[
    {common_service_possible:{_eq:true}}
    {_and: [{nwgroups: {group_type: {_eq: 20}}}, $appFilter]}
    {_and: [{nwgroups: {group_type: {_eq: 21}}}, $appFilter]}
  ]}) {
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


def build_graphql_variables(app_ids: list[str] | None = None) -> dict[str, Any]:
    """Build GraphQL variables for optional app-id filtering."""
    app_filter: dict[str, Any] = {}
    if app_ids is not None:
        app_filter = {"app_id_external": {"_in": app_ids}}
    return {"appFilter": app_filter}


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
    endpoint = base_url.rstrip("/") + "/api/v3.0/authenticate"

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
    return value.split("/")[0] if "/" in value else value


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


def build_label_from_group(nwgroup: dict[str, Any], include_empty: bool) -> LabelItem | None:
    id_string = nwgroup.get("id_string")
    if not id_string:
        return None
    key = label_key_from_id(id_string)
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
            label = build_label_from_group(nwgroup, include_empty)
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


def post_guardicore_labels(config: GuardicoreConfig, payload: list[dict[str, Any]]) -> None:
    headers = {
        "Authorization": f"Bearer {config.token}",
        "Content-Type": HTTP_CONTENT_TYPE_JSON,
    }
    endpoint = config.base_url.rstrip("/") + DEFAULT_GUARDICORE_LABELS_ENDPOINT

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
            build_graphql_variables(args.app_ids),
        )
        labels = build_labels_from_response(response, include_empty=args.include_empty)

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

        total_sent = 0
        for batch in chunked(labels, args.batch_size):
            payload = to_guardicore_payload(batch)
            if args.dry_run:
                logger.info("Dry run payload: %s", json.dumps(payload, indent=2))
            else:
                post_guardicore_labels(guardicore_config, payload)
            total_sent += len(batch)

        logger.info("Processed %s label(s).", total_sent)
        return 0

    except GuardicoreProvisioningError:
        logger.exception("Guardicore provisioning failed.")
        return 1


if __name__ == "__main__":
    sys.exit(main())
