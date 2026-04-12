#!/usr/bin/python3
"""Delete inactive owners that are no longer referenced in FWO."""

from __future__ import annotations

import argparse
import getpass
import json
import sys
from dataclasses import dataclass
from typing import Any, cast

import urllib3

from scripts.customizing.customizing import CustomizingError, call, login
from scripts.customizing.fwo_custom_lib.basic_helpers import FWOLogger, get_logger

DEFAULT_FWORCH_CONFIG: str = "/etc/fworch/fworch.json"
DEFAULT_REQUEST_TIMEOUT_ROLE: str = "admin"
EXIT_CODE_REFERENCES_FOUND: int = 2


@dataclass(frozen=True)
class OwnerReferenceSummary:
    """Summarizes all relevant owner reference counts."""

    counts: dict[str, int]

    @property
    def total_references(self) -> int:
        """Return the total number of references across all tracked relations."""
        return sum(self.counts.values())

    @property
    def has_references(self) -> bool:
        """Return whether the owner is still referenced anywhere."""
        return self.total_references > 0

    def non_zero_counts(self) -> dict[str, int]:
        """Return only non-zero reference counters for reporting."""
        return {relation: count for relation, count in self.counts.items() if count > 0}


@dataclass(frozen=True)
class InactiveOwnerCandidate:
    """Inactive owner and its relation counts."""

    owner_id: int
    name: str
    app_id_external: str | None
    lifecycle_state_name: str | None
    lifecycle_state_active: bool | None
    references: OwnerReferenceSummary

    @property
    def can_be_deleted(self) -> bool:
        """Return whether this owner is safe to delete."""
        return not self.references.has_references


OWNER_REFERENCE_RELATIONS: tuple[str, ...] = (
    "change_histories",
    "changelogOwnersByOldOwnerId",
    "changelog_owners",
    "connections",
    "connectionsByProposedAppId",
    "ext_requests",
    "notifications",
    "nwgroups",
    "owner_networks",
    "owner_recertifications",
    "owner_responsibles",
    "owner_tickets",
    "permitted_owners",
    "recertifications",
    "reports",
    "reqtask_owners",
    "rule_owners",
    "selected_connections",
    "selected_objects",
    "service_groups",
    "services",
)

RELATION_LABELS: dict[str, str] = {
    "change_histories": "modelling change history",
    "changelogOwnersByOldOwnerId": "owner change log (old owner)",
    "changelog_owners": "owner change log (new owner)",
    "connections": "modelling connections",
    "connectionsByProposedAppId": "requested interfaces",
    "ext_requests": "external requests",
    "notifications": "notifications",
    "nwgroups": "network groups",
    "owner_networks": "owner networks",
    "owner_recertifications": "owner recertifications",
    "owner_responsibles": "owner responsibles",
    "owner_tickets": "owner tickets",
    "permitted_owners": "permitted owner mappings",
    "recertifications": "rule recertifications",
    "reports": "reports",
    "reqtask_owners": "workflow task owners",
    "rule_owners": "rule ownership mappings",
    "selected_connections": "selected connections",
    "selected_objects": "selected objects",
    "service_groups": "service groups",
    "services": "services",
}


def parse_args(argv: list[str] | None = None) -> argparse.Namespace:
    """Parse CLI arguments."""
    parser = argparse.ArgumentParser(description="Delete inactive owners that have no remaining references.")
    parser.add_argument("-u", "--user", help="Username for FWO login")
    parser.add_argument("-p", "--password", help="Password for FWO login")
    parser.add_argument(
        "-c",
        "--config-file",
        default=DEFAULT_FWORCH_CONFIG,
        help=f"Path to fworch config json file (default: {DEFAULT_FWORCH_CONFIG})",
    )
    parser.add_argument(
        "--owner-ids",
        nargs="+",
        type=int,
        help="Optional list of owner IDs to restrict cleanup to",
    )
    parser.add_argument(
        "--execute",
        action="store_true",
        help="Actually delete owners. Without this flag the script performs a dry run.",
    )
    parser.add_argument(
        "--fail-on-references",
        action="store_true",
        help="Exit with status code 2 if any inactive owner still has references.",
    )
    parser.add_argument(
        "--output-json",
        action="store_true",
        help="Print the evaluation result as JSON instead of log lines.",
    )
    parser.add_argument(
        "-d",
        "--debug-level",
        default=0,
        type=int,
        help="Increase log verbosity",
    )
    return parser.parse_args(argv)


def read_json_file(filename: str) -> dict[str, Any]:
    """Read a JSON file from disk."""
    with open(filename, encoding="utf-8") as file_handle:
        return json.load(file_handle)


def resolve_credentials(args: argparse.Namespace) -> tuple[str, str]:
    """Resolve username and password from args or interactive prompts."""
    username: str = args.user or input("Enter your username: ")
    password: str = args.password or getpass.getpass("Enter your password: ")
    return username, password


def build_inactive_owners_query(filter_by_owner_ids: bool) -> str:
    """Build a GraphQL query for inactive owners and their aggregate references."""
    owner_ids_declaration: str = "($ownerIds: [Int!])" if filter_by_owner_ids else ""
    owner_ids_condition: str = ", id: { _in: $ownerIds }" if filter_by_owner_ids else ""
    aggregate_fields: str = "\n".join(
        f"    {relation}_aggregate {{ aggregate {{ count }} }}" for relation in OWNER_REFERENCE_RELATIONS
    )

    return f"""
query getInactiveOwners{owner_ids_declaration} {{
  owner(where: {{ active: {{ _eq: false }}{owner_ids_condition} }}) {{
    id
    name
    app_id_external
    owner_lifecycle_state {{
      name
      active_state
    }}
{aggregate_fields}
  }}
}}
""".strip()


def extract_aggregate_count(owner_node: dict[str, Any], relation_name: str) -> int:
    """Extract a Hasura aggregate count for an owner relationship."""
    aggregate_node: dict[str, Any] | None = owner_node.get(f"{relation_name}_aggregate")
    if not isinstance(aggregate_node, dict):
        return 0
    aggregate_data: dict[str, Any] | None = aggregate_node.get("aggregate")
    if not isinstance(aggregate_data, dict):
        return 0
    count_value: Any = aggregate_data.get("count", 0)
    return count_value if isinstance(count_value, int) else 0


def parse_owner_candidate(owner_node: dict[str, Any]) -> InactiveOwnerCandidate:
    """Convert GraphQL owner payload into an inactive owner candidate."""
    owner_id: int = int(owner_node["id"])
    owner_name: str = str(owner_node["name"])
    app_id_external_raw: object = owner_node.get("app_id_external")
    lifecycle_state_node: object = owner_node.get("owner_lifecycle_state")
    lifecycle_state_name: str | None = None
    lifecycle_state_active: bool | None = None

    if isinstance(app_id_external_raw, str) and app_id_external_raw.strip():
        app_id_external: str | None = app_id_external_raw
    else:
        app_id_external = None
    if isinstance(lifecycle_state_node, dict):
        lifecycle_state_dict: dict[str, object] = cast("dict[str, object]", lifecycle_state_node)
        lifecycle_state_name_raw: object = lifecycle_state_dict.get("name")
        if isinstance(lifecycle_state_name_raw, str) and lifecycle_state_name_raw.strip():
            lifecycle_state_name = lifecycle_state_name_raw
        lifecycle_state_active_raw: object = lifecycle_state_dict.get("active_state")
        if isinstance(lifecycle_state_active_raw, bool):
            lifecycle_state_active = lifecycle_state_active_raw

    relation_counts: dict[str, int] = {
        relation_name: extract_aggregate_count(owner_node, relation_name) for relation_name in OWNER_REFERENCE_RELATIONS
    }

    return InactiveOwnerCandidate(
        owner_id=owner_id,
        name=owner_name,
        app_id_external=app_id_external,
        lifecycle_state_name=lifecycle_state_name,
        lifecycle_state_active=lifecycle_state_active,
        references=OwnerReferenceSummary(relation_counts),
    )


def fetch_inactive_owner_candidates(
    graphql_url: str,
    jwt: str,
    owner_ids: list[int] | None,
) -> list[InactiveOwnerCandidate]:
    """Fetch inactive owners and all tracked references."""
    variables: dict[str, Any] = {"ownerIds": owner_ids} if owner_ids else {}
    query: str = build_inactive_owners_query(filter_by_owner_ids=owner_ids is not None)
    response: dict[str, Any] | None = call(
        graphql_url,
        jwt,
        query,
        query_variables=variables,
        role=DEFAULT_REQUEST_TIMEOUT_ROLE,
    )
    if response is None:
        return []
    owner_rows: list[dict[str, Any]] = response.get("data", {}).get("owner", [])
    return [parse_owner_candidate(owner_row) for owner_row in owner_rows]


def build_delete_owner_mutation() -> str:
    """Build the GraphQL delete mutation."""
    return """
mutation deleteOwner($ownerId: Int!) {
  delete_owner_by_pk(id: $ownerId) {
    id
    name
    app_id_external
  }
}
""".strip()


def delete_owner(graphql_url: str, jwt: str, owner_id: int) -> dict[str, Any]:
    """Delete an owner by primary key."""
    response: dict[str, Any] | None = call(
        graphql_url,
        jwt,
        build_delete_owner_mutation(),
        query_variables={"ownerId": owner_id},
        role=DEFAULT_REQUEST_TIMEOUT_ROLE,
    )
    if response is None:
        raise CustomizingError(f"Deleting owner {owner_id} returned no response")
    deleted_owner: dict[str, Any] | None = response.get("data", {}).get("delete_owner_by_pk")
    if not isinstance(deleted_owner, dict):
        raise CustomizingError(f"Deleting owner {owner_id} returned no deleted owner payload")
    return deleted_owner


def build_report_payload(candidates: list[InactiveOwnerCandidate]) -> dict[str, Any]:
    """Build structured output for dry-runs and automation."""
    deletable_owners: list[InactiveOwnerCandidate] = [candidate for candidate in candidates if candidate.can_be_deleted]
    blocked_owners: list[InactiveOwnerCandidate] = [
        candidate for candidate in candidates if not candidate.can_be_deleted
    ]

    def serialize_candidate(candidate: InactiveOwnerCandidate) -> dict[str, Any]:
        return {
            "id": candidate.owner_id,
            "name": candidate.name,
            "app_id_external": candidate.app_id_external,
            "owner_lifecycle_state": candidate.lifecycle_state_name,
            "owner_lifecycle_state_active": candidate.lifecycle_state_active,
            "total_references": candidate.references.total_references,
            "references": candidate.references.non_zero_counts(),
        }

    return {
        "inactive_owner_count": len(candidates),
        "deletable_owner_count": len(deletable_owners),
        "blocked_owner_count": len(blocked_owners),
        "deletable_owners": [serialize_candidate(candidate) for candidate in deletable_owners],
        "blocked_owners": [serialize_candidate(candidate) for candidate in blocked_owners],
    }


def log_candidates(logger: FWOLogger, candidates: list[InactiveOwnerCandidate], execute: bool) -> None:
    """Log the cleanup plan in a readable format."""
    if not candidates:
        logger.info("No inactive owners found")
        return

    dry_run_prefix: str = "" if execute else "[dry-run] "
    logger.info("%sFound %s inactive owner(s)", dry_run_prefix, len(candidates))

    candidate: InactiveOwnerCandidate
    for candidate in candidates:
        owner_identity: str = f"{candidate.owner_id} ({candidate.name})"
        if candidate.app_id_external:
            owner_identity += f" [{candidate.app_id_external}]"
        if candidate.can_be_deleted:
            logger.info("%sOwner %s can be deleted", dry_run_prefix, owner_identity)
            continue

        formatted_references: str = ", ".join(
            f"{RELATION_LABELS.get(relation_name, relation_name)}={count}"
            for relation_name, count in sorted(candidate.references.non_zero_counts().items())
        )
        logger.info("%sOwner %s is still referenced: %s", dry_run_prefix, owner_identity, formatted_references)


def run_cleanup(args: argparse.Namespace, logger: FWOLogger) -> int:
    """Run the owner cleanup workflow."""
    fwo_config: dict[str, Any] = read_json_file(args.config_file)
    middleware_url: str = str(fwo_config["middleware_uri"])
    graphql_url: str = str(fwo_config["api_uri"])
    username: str
    password: str
    username, password = resolve_credentials(args)

    jwt: str = login(username, password, middleware_url, method="api/AuthenticationToken/Get")
    candidates: list[InactiveOwnerCandidate] = fetch_inactive_owner_candidates(graphql_url, jwt, args.owner_ids)

    if args.output_json:
        sys.stdout.write(f"{json.dumps(build_report_payload(candidates), indent=2, sort_keys=True)}\n")
    else:
        log_candidates(logger, candidates, args.execute)

    blocked_candidates: list[InactiveOwnerCandidate] = [
        candidate for candidate in candidates if not candidate.can_be_deleted
    ]
    deletable_candidates: list[InactiveOwnerCandidate] = [
        candidate for candidate in candidates if candidate.can_be_deleted
    ]

    if not args.execute:
        return EXIT_CODE_REFERENCES_FOUND if args.fail_on_references and blocked_candidates else 0

    deleted_owner_ids: list[int] = []
    candidate_to_delete: InactiveOwnerCandidate
    for candidate_to_delete in deletable_candidates:
        deleted_owner: dict[str, Any] = delete_owner(graphql_url, jwt, candidate_to_delete.owner_id)
        deleted_owner_ids.append(int(deleted_owner["id"]))
        logger.info("Deleted inactive owner %s (%s)", deleted_owner["id"], deleted_owner["name"])

    logger.info("Deleted %s owner(s)", len(deleted_owner_ids))
    return EXIT_CODE_REFERENCES_FOUND if args.fail_on_references and blocked_candidates else 0


def main(argv: list[str] | None = None) -> int:
    """CLI entry point."""
    args: argparse.Namespace = parse_args(argv)
    logger: FWOLogger = get_logger(args.debug_level)
    urllib3.disable_warnings(urllib3.exceptions.InsecureRequestWarning)
    try:
        return run_cleanup(args, logger)
    except (CustomizingError, KeyError, OSError, ValueError):
        logger.exception("Cleanup failed")
        return 1


if __name__ == "__main__":
    sys.exit(main())
