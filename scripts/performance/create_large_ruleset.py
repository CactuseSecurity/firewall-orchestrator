#!/usr/bin/env python3
"""Create a large FWO ruleset through the GraphQL API for performance tests."""

from __future__ import annotations

import argparse
import json
import sys
import time
from dataclasses import dataclass
from datetime import UTC, datetime
from getpass import getpass
from pathlib import Path
from typing import Any

import requests
import urllib3

JSON_HEADERS = {"Content-Type": "application/json"}
DEFAULT_PREFIX = "perf"
DEFAULT_BATCH_SIZE = 1000
DEFAULT_TIMEOUT_SECONDS = 120
DEFAULT_DEVICE_TYPE_ID = 9
IMPORT_TYPE_RULE = 1
ACTION_ACCEPT = 1
TRACK_LOG = 1
OBJECT_TYPE_HOST = 3
SERVICE_TYPE_SIMPLE = 1
IP_PROTO_TCP = 6
LINK_TYPE_ORDERED = 2
RULE_NUM_NUMERIC_STEP = 1024
MIN_OBJECTS_FOR_DISTINCT_REFS = 2


class GraphqlError(RuntimeError):
    """Raised when the GraphQL API returns an error."""


@dataclass(frozen=True)
class GraphqlClient:
    """Small GraphQL client for Hasura-style API calls."""

    api_url: str
    headers: dict[str, str]
    timeout: int
    verify: bool

    def call(self, query: str, variables: dict[str, Any] | None = None) -> dict[str, Any]:
        payload = {"query": query, "variables": variables or {}}
        response = requests.post(
            self.api_url,
            headers=self.headers,
            data=json.dumps(payload),
            timeout=self.timeout,
            verify=self.verify,
        )
        response.raise_for_status()
        result = response.json()
        if "errors" in result:
            raise GraphqlError(json.dumps(result["errors"], indent=2))
        return result["data"]


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Create a large normalized ruleset in an FWO database through the GraphQL API."
    )
    parser.add_argument("--api-url", required=True, help="FWO GraphQL API URL, for example https://host/api/v1/graphql")
    parser.add_argument("--middleware-url", help="FWO middleware base URL for login, for example https://host/")
    parser.add_argument("--user", default="admin", help="Middleware login user when --jwt-file is not supplied")
    parser.add_argument("--password-file", help="File containing the middleware login password")
    parser.add_argument("--jwt-file", help="File containing an existing JWT for GraphQL API access")
    parser.add_argument("--admin-secret-file", help="File containing the Hasura admin secret")
    parser.add_argument("--role", default="admin", help="Hasura role to use with JWT authentication")
    parser.add_argument("--insecure", action="store_true", help="Disable TLS certificate verification")
    parser.add_argument("--timeout", type=int, default=DEFAULT_TIMEOUT_SECONDS, help="HTTP timeout in seconds")
    parser.add_argument("--prefix", default=DEFAULT_PREFIX, help="Prefix for generated management/device/object names")
    parser.add_argument("--rules", type=int, required=True, help="Number of access rules to create")
    parser.add_argument("--objects", type=int, default=1000, help="Number of source/destination objects to create")
    parser.add_argument("--services", type=int, default=100, help="Number of TCP services to create")
    parser.add_argument("--batch-size", type=int, default=DEFAULT_BATCH_SIZE, help="Rows per insert mutation")
    parser.add_argument(
        "--device-type-id", type=int, default=DEFAULT_DEVICE_TYPE_ID, help="Existing stm_dev_typ ID to use"
    )
    parser.add_argument("--management-id", type=int, help="Use an existing management instead of creating one")
    parser.add_argument("--device-id", type=int, help="Use an existing device instead of creating one")
    return parser.parse_args()


def read_password(password_file: str | None) -> str:
    if password_file:
        return Path(password_file).read_text(encoding="utf-8").strip()
    return getpass("FWO password: ")


def read_secret(secret_file: str | None) -> str | None:
    if secret_file:
        return Path(secret_file).read_text(encoding="utf-8").strip()
    return None


def login(middleware_url: str, user: str, password: str, timeout: int, verify: bool) -> str:
    url = middleware_url.rstrip("/") + "/api/AuthenticationToken/GetTokenPair"
    response = requests.post(
        url,
        headers=JSON_HEADERS,
        data=json.dumps({"Username": user, "Password": password}),
        timeout=timeout,
        verify=verify,
    )
    response.raise_for_status()
    return response.text


def build_headers(args: argparse.Namespace) -> dict[str, str]:
    headers = dict(JSON_HEADERS)
    admin_secret = read_secret(args.admin_secret_file)
    if admin_secret:
        headers["x-hasura-admin-secret"] = admin_secret
        return headers

    jwt = read_secret(args.jwt_file)
    if jwt is None:
        if not args.middleware_url:
            raise ValueError("Provide --jwt-file, --admin-secret-file, or --middleware-url for login.")
        jwt = login(args.middleware_url, args.user, read_password(args.password_file), args.timeout, not args.insecure)

    headers["Authorization"] = f"Bearer {jwt}"
    headers["x-hasura-role"] = args.role
    return headers


def disable_tls_warnings_if_requested(insecure: bool) -> None:
    if insecure:
        urllib3.disable_warnings(urllib3.exceptions.InsecureRequestWarning)


def chunks(items: list[dict[str, Any]], size: int) -> list[list[dict[str, Any]]]:
    return [items[index : index + size] for index in range(0, len(items), size)]


def current_timestamp() -> str:
    return datetime.now(UTC).replace(microsecond=0).isoformat()


def create_import(client: GraphqlClient, management_id: int) -> int:
    data = client.call(
        """
        mutation createPerfImport($mgmId: Int!, $importTypeId: Int!, $stopTime: timestamp!) {
          insert_import_control(objects: {
            mgm_id: $mgmId
            import_type_id: $importTypeId
            is_initial_import: true
            successful_import: true
            changes_found: true
            policy_changes_found: true
            stop_time: $stopTime
          }) {
            returning { control_id }
          }
        }
        """,
        {"mgmId": management_id, "importTypeId": IMPORT_TYPE_RULE, "stopTime": current_timestamp()},
    )
    return int(data["insert_import_control"]["returning"][0]["control_id"])


def create_management_and_device(client: GraphqlClient, args: argparse.Namespace, run_id: str) -> tuple[int, int]:
    if args.management_id and args.device_id:
        return args.management_id, args.device_id
    if args.management_id or args.device_id:
        raise ValueError("--management-id and --device-id must be supplied together.")

    credential_name = f"{args.prefix}-credential-{run_id}"
    management_name = f"{args.prefix}-management-{run_id}"
    device_name = f"{args.prefix}-gateway-{run_id}"
    credential_data = client.call(
        """
        mutation createPerfCredential($name: String!) {
          insert_import_credential(objects: {
            credential_name: $name
            username: "perf"
            secret: "perf"
            is_key_pair: false
          }) {
            returning { id }
          }
        }
        """,
        {"name": credential_name},
    )
    credential_id = int(credential_data["insert_import_credential"]["returning"][0]["id"])

    management_data = client.call(
        """
        mutation createPerfManagement(
          $name: String!
          $uid: String!
          $devTypeId: Int!
          $credentialId: Int!
        ) {
          insert_management(objects: {
            mgm_name: $name
            mgm_uid: $uid
            dev_typ_id: $devTypeId
            import_credential_id: $credentialId
            ssh_hostname: "perf.local"
            ssh_port: 22
            do_not_import: true
            hide_in_gui: false
            force_initial_import: false
            mgm_comment: "Generated by scripts/performance/create_large_ruleset.py"
          }) {
            returning { mgm_id }
          }
        }
        """,
        {
            "name": management_name,
            "uid": f"{args.prefix}-mgm-{run_id}",
            "devTypeId": args.device_type_id,
            "credentialId": credential_id,
        },
    )
    management_id = int(management_data["insert_management"]["returning"][0]["mgm_id"])

    device_data = client.call(
        """
        mutation createPerfDevice($name: String!, $uid: String!, $devTypeId: Int!, $managementId: Int!) {
          insert_device(objects: {
            dev_name: $name
            dev_uid: $uid
            dev_typ_id: $devTypeId
            mgm_id: $managementId
            do_not_import: true
            hide_in_gui: false
            dev_comment: "Generated performance test gateway"
          }) {
            returning { dev_id }
          }
        }
        """,
        {
            "name": device_name,
            "uid": f"{args.prefix}-gw-{run_id}",
            "devTypeId": args.device_type_id,
            "managementId": management_id,
        },
    )
    return management_id, int(device_data["insert_device"]["returning"][0]["dev_id"])


def create_rulebase(client: GraphqlClient, prefix: str, run_id: str, management_id: int, import_id: int) -> int:
    data = client.call(
        """
        mutation createPerfRulebase($rulebase: firewall_rulebase_insert_input!) {
          insert_firewall_rulebase(objects: [$rulebase]) {
            returning { id }
          }
        }
        """,
        {
            "rulebase": {
                "name": f"{prefix}-rulebase-{run_id}",
                "uid": f"{prefix}-rb-{run_id}",
                "mgm_id": management_id,
                "is_global": False,
                "created": import_id,
            }
        },
    )
    return int(data["insert_firewall_rulebase"]["returning"][0]["id"])


def link_rulebase_to_device(client: GraphqlClient, device_id: int, rulebase_id: int, import_id: int) -> None:
    client.call(
        """
        mutation linkPerfRulebase($link: firewall_rulebase_link_insert_input!) {
          insert_firewall_rulebase_link(objects: [$link]) { affected_rows }
        }
        """,
        {
            "link": {
                "gw_id": device_id,
                "to_rulebase_id": rulebase_id,
                "link_type": LINK_TYPE_ORDERED,
                "is_initial": True,
                "is_global": False,
                "is_section": False,
                "created": import_id,
            }
        },
    )


def create_objects(
    client: GraphqlClient, prefix: str, run_id: str, management_id: int, import_id: int, count: int, batch_size: int
) -> list[int]:
    object_ids: list[int] = []
    objects = [
        {
            "mgm_id": management_id,
            "obj_name": f"{prefix}-host-{index:06d}",
            "obj_uid": f"{prefix}-{run_id}-host-{index:06d}",
            "obj_typ_id": OBJECT_TYPE_HOST,
            "obj_ip": f"10.{index // 65536 % 256}.{index // 256 % 256}.{index % 256}/32",
            "obj_ip_end": f"10.{index // 65536 % 256}.{index // 256 % 256}.{index % 256}/32",
            "obj_create": import_id,
            "active": True,
        }
        for index in range(count)
    ]
    for batch in chunks(objects, batch_size):
        data = client.call(
            """
            mutation createPerfObjects($objects: [firewall_nw_object_insert_input!]!) {
              insert_firewall_nw_object(objects: $objects) {
                returning { obj_id }
              }
            }
            """,
            {"objects": batch},
        )
        object_ids.extend(int(row["obj_id"]) for row in data["insert_firewall_nw_object"]["returning"])
    return object_ids


def create_services(
    client: GraphqlClient, prefix: str, run_id: str, management_id: int, import_id: int, count: int, batch_size: int
) -> list[int]:
    service_ids: list[int] = []
    services = [
        {
            "mgm_id": management_id,
            "svc_name": f"{prefix}-tcp-{10000 + index}",
            "svc_uid": f"{prefix}-{run_id}-svc-{index:06d}",
            "svc_typ_id": SERVICE_TYPE_SIMPLE,
            "ip_proto_id": IP_PROTO_TCP,
            "svc_port": 10000 + (index % 50000),
            "svc_create": import_id,
            "active": True,
        }
        for index in range(count)
    ]
    for batch in chunks(services, batch_size):
        data = client.call(
            """
            mutation createPerfServices($services: [firewall_nw_service_insert_input!]!) {
              insert_firewall_nw_service(objects: $services) {
                returning { svc_id }
              }
            }
            """,
            {"services": batch},
        )
        service_ids.extend(int(row["svc_id"]) for row in data["insert_firewall_nw_service"]["returning"])
    return service_ids


def create_rule_metadata(
    client: GraphqlClient,
    prefix: str,
    run_id: str,
    management_id: int,
    import_id: int,
    count: int,
    batch_size: int,
) -> None:
    metadata_rows = [
        {
            "mgm_id": management_id,
            "rule_uid": f"{prefix}-{run_id}-rule-{index:06d}",
            "rule_created": import_id,
        }
        for index in range(count)
    ]
    for batch in chunks(metadata_rows, batch_size):
        client.call(
            """
            mutation createPerfRuleMetadata($metadata: [firewall_rule_metadata_insert_input!]!) {
              insert_firewall_rule_metadata(
                objects: $metadata
                on_conflict: {
                  constraint: rule_metadata_mgm_id_rule_uid_unique
                  update_columns: []
                }
              ) { affected_rows }
            }
            """,
            {"metadata": batch},
        )


def create_rules(
    client: GraphqlClient,
    prefix: str,
    run_id: str,
    management_id: int,
    rulebase_id: int,
    import_id: int,
    count: int,
    batch_size: int,
) -> list[int]:
    rule_ids: list[int] = []
    rules = [
        {
            "mgm_id": management_id,
            "rulebase_id": rulebase_id,
            "rule_name": f"{prefix}-rule-{index:06d}",
            "rule_uid": f"{prefix}-{run_id}-rule-{index:06d}",
            "rule_num_numeric": (index + 1) * RULE_NUM_NUMERIC_STEP,
            "rule_ruleid": str(index + 1),
            "rule_disabled": False,
            "rule_src_neg": False,
            "rule_dst_neg": False,
            "rule_svc_neg": False,
            "action_id": ACTION_ACCEPT,
            "track_id": TRACK_LOG,
            "rule_src": "generated source",
            "rule_dst": "generated destination",
            "rule_svc": "generated service",
            "rule_action": "accept",
            "rule_track": "log",
            "rule_implied": False,
            "rule_create": import_id,
            "access_rule": True,
            "nat_rule": False,
            "is_global": False,
            "active": True,
        }
        for index in range(count)
    ]
    for batch in chunks(rules, batch_size):
        data = client.call(
            """
            mutation createPerfRules($rules: [firewall_rule_insert_input!]!) {
              insert_firewall_rule(objects: $rules) {
                returning { rule_id }
              }
            }
            """,
            {"rules": batch},
        )
        rule_ids.extend(int(row["rule_id"]) for row in data["insert_firewall_rule"]["returning"])
    return rule_ids


def create_rule_refs(
    client: GraphqlClient,
    rule_ids: list[int],
    object_ids: list[int],
    service_ids: list[int],
    management_id: int,
    import_id: int,
    batch_size: int,
) -> None:
    ref_rows: list[dict[str, Any]] = []
    for index, rule_id in enumerate(rule_ids):
        src_id = object_ids[index % len(object_ids)]
        dst_id = object_ids[(index + 1) % len(object_ids)]
        svc_id = service_ids[index % len(service_ids)]
        ref_rows.append({"rule_id": rule_id, "src_id": src_id, "dst_id": dst_id, "svc_id": svc_id})

    for batch in chunks(ref_rows, batch_size):
        client.call(
            """
            mutation createPerfRuleRefs(
              $ruleFroms: [firewall_rule_from_insert_input!]!
              $ruleTos: [firewall_rule_to_insert_input!]!
              $ruleServices: [firewall_rule_service_insert_input!]!
              $objectResolved: [firewall_rule_nw_object_resolved_insert_input!]!
              $serviceResolved: [firewall_rule_nw_service_resolved_insert_input!]!
            ) {
              insert_firewall_rule_from(objects: $ruleFroms) { affected_rows }
              insert_firewall_rule_to(objects: $ruleTos) { affected_rows }
              insert_firewall_rule_service(objects: $ruleServices) { affected_rows }
              insert_firewall_rule_nw_object_resolved(objects: $objectResolved) { affected_rows }
              insert_firewall_rule_nw_service_resolved(objects: $serviceResolved) { affected_rows }
            }
            """,
            {
                "ruleFroms": [
                    {"rule_id": row["rule_id"], "obj_id": row["src_id"], "rf_create": import_id} for row in batch
                ],
                "ruleTos": [
                    {"rule_id": row["rule_id"], "obj_id": row["dst_id"], "rt_create": import_id} for row in batch
                ],
                "ruleServices": [
                    {"rule_id": row["rule_id"], "svc_id": row["svc_id"], "rs_create": import_id} for row in batch
                ],
                "objectResolved": [
                    {"mgm_id": management_id, "rule_id": row["rule_id"], "obj_id": row["src_id"], "created": import_id}
                    for row in batch
                ]
                + [
                    {"mgm_id": management_id, "rule_id": row["rule_id"], "obj_id": row["dst_id"], "created": import_id}
                    for row in batch
                ],
                "serviceResolved": [
                    {"mgm_id": management_id, "rule_id": row["rule_id"], "svc_id": row["svc_id"], "created": import_id}
                    for row in batch
                ],
            },
        )


def create_rule_gateway_refs(
    client: GraphqlClient, rule_ids: list[int], device_id: int, import_id: int, batch_size: int
) -> None:
    rows = [{"rule_id": rule_id, "dev_id": device_id, "created": import_id} for rule_id in rule_ids]
    for batch in chunks(rows, batch_size):
        client.call(
            """
            mutation createPerfRuleGatewayRefs($rows: [firewall_rule_enforced_on_gateway_insert_input!]!) {
              insert_firewall_rule_enforced_on_gateway(objects: $rows) { affected_rows }
            }
            """,
            {"rows": batch},
        )


def update_import_counters(client: GraphqlClient, import_id: int, change_count: int) -> None:
    client.call(
        """
        mutation updatePerfImport($importId: bigint!, $changeCount: Int!, $stopTime: timestamp!) {
          update_import_control(
            where: { control_id: { _eq: $importId } }
            _set: {
              stop_time: $stopTime
              successful_import: true
              changes_found: true
              policy_changes_found: true
              security_relevant_changes_counter: $changeCount
            }
          ) { affected_rows }
        }
        """,
        {"importId": import_id, "changeCount": change_count, "stopTime": current_timestamp()},
    )


def main() -> int:
    args = parse_args()
    disable_tls_warnings_if_requested(args.insecure)
    if args.rules <= 0 or args.services <= 0 or args.batch_size <= 0:
        raise ValueError("--rules, --services, and --batch-size must be positive.")
    if args.objects < MIN_OBJECTS_FOR_DISTINCT_REFS:
        raise ValueError(
            f"--objects must be at least {MIN_OBJECTS_FOR_DISTINCT_REFS} "
            "so source and destination references stay distinct."
        )

    run_id = str(int(time.time()))
    client = GraphqlClient(
        api_url=args.api_url,
        headers=build_headers(args),
        timeout=args.timeout,
        verify=not args.insecure,
    )

    started = time.perf_counter()
    management_id, device_id = create_management_and_device(client, args, run_id)
    import_id = create_import(client, management_id)
    rulebase_id = create_rulebase(client, args.prefix, run_id, management_id, import_id)
    link_rulebase_to_device(client, device_id, rulebase_id, import_id)
    object_ids = create_objects(client, args.prefix, run_id, management_id, import_id, args.objects, args.batch_size)
    service_ids = create_services(client, args.prefix, run_id, management_id, import_id, args.services, args.batch_size)
    create_rule_metadata(client, args.prefix, run_id, management_id, import_id, args.rules, args.batch_size)
    rule_ids = create_rules(
        client, args.prefix, run_id, management_id, rulebase_id, import_id, args.rules, args.batch_size
    )
    create_rule_refs(client, rule_ids, object_ids, service_ids, management_id, import_id, args.batch_size)
    create_rule_gateway_refs(client, rule_ids, device_id, import_id, args.batch_size)
    update_import_counters(client, import_id, len(rule_ids))

    elapsed = time.perf_counter() - started
    sys.stdout.write(
        json.dumps(
            {
                "management_id": management_id,
                "device_id": device_id,
                "import_id": import_id,
                "rulebase_id": rulebase_id,
                "rules": len(rule_ids),
                "objects": len(object_ids),
                "services": len(service_ids),
                "elapsed_seconds": round(elapsed, 3),
            },
            indent=2,
        )
        + "\n"
    )
    return 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except Exception as exception:
        sys.stderr.write(f"ERROR: {exception}\n")
        sys.exit(1)
