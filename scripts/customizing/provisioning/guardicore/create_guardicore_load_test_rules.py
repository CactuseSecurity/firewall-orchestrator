#!/usr/bin/python3
"""Create a simple Guardicore load-test policy between two IP labels."""

from __future__ import annotations

import argparse
import logging
import sys
import time
from typing import Any, cast

import requests

try:
    from scripts.customizing.provisioning.guardicore.guardicore_lib import (
        HTTP_CONTENT_TYPE_JSON,
        GuardicoreConfig,
        JsonDict,
        apply_ssl_settings,
        login_guardicore,
    )
except ModuleNotFoundError:
    from guardicore_lib import (  # type: ignore[import-not-found]
        HTTP_CONTENT_TYPE_JSON,
        GuardicoreConfig,
        JsonDict,
        apply_ssl_settings,
        login_guardicore,
    )

DEFAULT_GUARDICORE_API_V4_BASE_ENDPOINT: str = "/api/v4.0/"
DEFAULT_GUARDICORE_LABELS_BULK_ENDPOINT: str = f"{DEFAULT_GUARDICORE_API_V4_BASE_ENDPOINT}labels/bulk"
DEFAULT_GUARDICORE_RULES_CREATE_ENDPOINT: str = f"{DEFAULT_GUARDICORE_API_V4_BASE_ENDPOINT}visibility/policy/rules"
DEFAULT_GUARDICORE_REVISIONS_CREATE_ENDPOINT: str = (
    f"{DEFAULT_GUARDICORE_API_V4_BASE_ENDPOINT}visibility/policy/revisions"
)
DEFAULT_LABEL_KEY: str = "LoadTest"
DEFAULT_LABEL_FIELD: str = "numeric_ip_addresses"
DEFAULT_TIMEOUT_SECONDS: int = 60
EXPECTED_CREATED_LABEL_COUNT: int = 2
LOAD_TEST_PORTS: list[int] = [*range(1000, 2000), 22]


class GuardicoreLoadTestError(Exception):
    """Raised when the Guardicore load-test script fails."""


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Create Guardicore load-test rules for two IP labels")
    parser.add_argument("--guardicore-url", required=True, help="Guardicore base URL, e.g. https://x.y.z")
    parser.add_argument("--guardicore-user", required=True, help="Guardicore username")
    parser.add_argument("--guardicore-password", required=True, help="Guardicore password")
    parser.add_argument("--ip-a", required=True, help="Source IP address")
    parser.add_argument("--label-a", required=True, help="Source label value")
    parser.add_argument("--ip-b", required=True, help="Destination IP address")
    parser.add_argument("--label-b", required=True, help="Destination label value")
    parser.add_argument("--guardicore-ca-cert", help="Path to a CA bundle for Guardicore API calls")
    parser.add_argument("--guardicore-insecure", action="store_true", help="Disable SSL verification")
    parser.add_argument("--insecure", action="store_true", help="Disable SSL verification")
    parser.add_argument("--timeout", type=int, default=DEFAULT_TIMEOUT_SECONDS, help="HTTP timeout in seconds")
    return parser.parse_args()


def build_guardicore_config(args: argparse.Namespace, token: str) -> GuardicoreConfig:
    return GuardicoreConfig(
        base_url=args.guardicore_url,
        token=token,
        verify_ssl=False,
        timeout_seconds=args.timeout,
    )


def post_guardicore_json(
    config: GuardicoreConfig,
    endpoint_suffix: str,
    payload: JsonDict | list[JsonDict],
    error_message: str,
) -> JsonDict:
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
            raise GuardicoreLoadTestError(f"{error_message}: {exc}") from exc

    try:
        result: Any = response.json()
    except ValueError:
        return {}
    if not isinstance(result, dict):
        return {}
    return cast("JsonDict", result)


def extract_created_label_ids(response: JsonDict) -> tuple[str, str]:
    succeeded: list[str] | None = response.get("succeeded")
    if not isinstance(succeeded, list) or len(succeeded) < EXPECTED_CREATED_LABEL_COUNT:
        raise GuardicoreLoadTestError(
            f"Guardicore label creation did not return {EXPECTED_CREATED_LABEL_COUNT} label ids: {response}"
        )

    label_ids = [label_id for label_id in succeeded if label_id]
    if len(label_ids) < EXPECTED_CREATED_LABEL_COUNT:
        raise GuardicoreLoadTestError(f"Guardicore label creation returned invalid label ids: {response}")
    return label_ids[0], label_ids[1]


def build_ip_label_payload(label_value: str, ip_address: str) -> JsonDict:
    return {
        "key": DEFAULT_LABEL_KEY,
        "value": label_value,
        "criteria": [
            {
                "field": DEFAULT_LABEL_FIELD,
                "op": "SUBNET",
                "argument": ip_address,
            }
        ],
    }


# Step 1. Auth
def auth(args: argparse.Namespace) -> str:
    return login_guardicore(
        args.guardicore_user,
        args.guardicore_password,
        args.guardicore_url,
        verify_ssl=False,
        timeout=args.timeout,
        error_cls=GuardicoreLoadTestError,
    )


# Step 2. Create labels for ips
def create_labels_for_ips(
    config: GuardicoreConfig, ip_a: str, label_a: str, ip_b: str, label_b: str
) -> tuple[str, str]:
    payload = [
        build_ip_label_payload(label_a, ip_a),
        build_ip_label_payload(label_b, ip_b),
    ]
    response = post_guardicore_json(
        config,
        DEFAULT_GUARDICORE_LABELS_BULK_ENDPOINT,
        payload,
        "Guardicore label creation failed",
    )
    return extract_created_label_ids(response)


def build_rule_payload(source_label_id: str, destination_label_id: str, port: int, ruleset_name: str) -> JsonDict:
    return {
        "ruleset_name": ruleset_name,
        "action": "ALLOW",
        "section_position": "ALLOW",
        "ip_protocols": ["TCP"],
        "ports": [port],
        "port_ranges": [],
        "source": {
            "labels": {
                "or_labels": [{"and_labels": [source_label_id]}],
            }
        },
        "destination": {
            "labels": {
                "or_labels": [{"and_labels": [destination_label_id]}],
            }
        },
    }


# Step 3. create rules
def create_rules(config: GuardicoreConfig, source_label_id: str, destination_label_id: str) -> int:
    ruleset_name = f"load-test-{source_label_id}-to-{destination_label_id}"
    for port in LOAD_TEST_PORTS:
        payload = build_rule_payload(source_label_id, destination_label_id, port, ruleset_name)
        post_guardicore_json(
            config,
            DEFAULT_GUARDICORE_RULES_CREATE_ENDPOINT,
            payload,
            f"Guardicore rule creation failed for tcp/{port}",
        )
    return len(LOAD_TEST_PORTS)


# Step 4. publish
def publish(config: GuardicoreConfig) -> None:
    post_guardicore_json(
        config,
        DEFAULT_GUARDICORE_REVISIONS_CREATE_ENDPOINT,
        {"comments": "published guardicore load test rules"},
        "Guardicore publish failed",
    )


def main() -> int:
    logging.basicConfig(level=logging.INFO, format="%(levelname)s: %(message)s")
    logger = logging.getLogger(__name__)
    args = parse_args()

    start_time = time.time()

    try:
        logger.info("Step 1/4: Auth")
        token = auth(args)
        config = build_guardicore_config(args, token)

        logger.info("Step 2/4: Create labels for ips")
        source_label_id, destination_label_id = create_labels_for_ips(
            config, args.ip_a, args.label_a, args.ip_b, args.label_b
        )

        logger.info("Step 3/4: Create rules")
        created_rules = create_rules(config, source_label_id, destination_label_id)

        logger.info("Step 4/4: Publish")
        publish(config)

        stop_time = time.time()
        elapsed_time = stop_time - start_time

        logger.info("Done. Created %s load-test rules in %.2f seconds.", created_rules, elapsed_time)
        return 0
    except GuardicoreLoadTestError:
        logger.exception("Guardicore load-test rule creation failed.")
        return 1


if __name__ == "__main__":
    sys.exit(main())
