from __future__ import annotations

from dataclasses import dataclass, field
from datetime import datetime
from typing import TYPE_CHECKING, Any

import fwo_const
from fwo_api import FwoApi
from fwo_exceptions import FwoApiWriteError
from fwo_log import FWOLogger
from models.time_object import validate_iso_timestamp_value

if TYPE_CHECKING:
    from model_controllers.import_state_controller import ImportStateController
    from models.rule_hit import RuleHit


@dataclass
class StoredRuleHits:
    """Last hits of one rule as stored in the database."""

    rule_metadata_id: int
    rule_last_hit: str | None
    hits_per_device: dict[int, str] = field(default_factory=dict[int, str])


@dataclass
class RuleHitWriteResult:
    """Number of rows written by a rule hit import run."""

    gateway_hits_written: int = 0
    rule_last_hits_written: int = 0


class RuleHitImport:
    """
    Writes the rule hits read by a rule hit import run.

    rule_hit stores the last hit per rule and gateway, a changed value is overwritten.
    rule_metadata.rule_last_hit holds the latest hit across all gateways and only ever moves forward,
    so a hit stored before (e.g. read for a gateway whose import is disabled now) is never replaced by an older one.
    Neither value is ever cleared.
    """

    def __init__(self, import_state: ImportStateController):
        self.import_state = import_state

    def write_rule_hits(self, rule_hits: list[RuleHit]) -> RuleHitWriteResult:
        """
        Store the changed rule hits of a rule hit import run.

        Args:
            rule_hits: Rule hits read from the firewall management.

        Returns:
            The number of written gateway hits and rule last hits.

        """
        result = RuleHitWriteResult()
        if len(rule_hits) == 0:
            FWOLogger.info("rule hit import - no rule hits found")
            return result

        stored_hits = self.get_stored_rule_hits()
        changed_gateway_hits = self.collect_changed_gateway_hits(rule_hits, stored_hits)
        changed_rule_last_hits = self.collect_changed_rule_last_hits(stored_hits)

        result.gateway_hits_written = self.upsert_gateway_hits(changed_gateway_hits)
        result.rule_last_hits_written = self.update_rule_last_hits(changed_rule_last_hits)
        FWOLogger.info(
            f"rule hit import - {len(rule_hits)} rule hits read, {result.gateway_hits_written} gateway hits and "
            f"{result.rule_last_hits_written} rule last hits written"
        )
        return result

    def get_stored_rule_hits(self) -> dict[tuple[int, str], StoredRuleHits]:
        """Return the stored hits of all rules of the management and its sub-managers by (mgm_id, rule_uid)."""
        mgm_ids = sorted(set(self.import_state.state.management_map.values()))
        query = FwoApi.get_graphql_code(
            [fwo_const.GRAPHQL_QUERY_PATH + "rule_metadata/getRuleHitsOfManagements.graphql"]
        )
        result = self.import_state.api_call.call(query, query_variables={"mgmIds": mgm_ids})
        if "errors" in result:
            raise FwoApiWriteError(f"failed to read stored rule hits: {result['errors']!s}")

        stored_hits: dict[tuple[int, str], StoredRuleHits] = {}
        for rule_metadatum in result["data"]["firewall_rule_metadata"]:
            hits_per_device: dict[int, str] = {}
            for rule_hit in rule_metadatum["rule_hits"]:
                hits_per_device[rule_hit["dev_id"]] = normalize_hit(rule_hit["rule_last_hit"])
            stored_hits[(rule_metadatum["mgm_id"], rule_metadatum["rule_uid"])] = StoredRuleHits(
                rule_metadata_id=rule_metadatum["rule_metadata_id"],
                rule_last_hit=normalize_optional_hit(rule_metadatum["rule_last_hit"]),
                hits_per_device=hits_per_device,
            )
        return stored_hits

    def collect_changed_gateway_hits(
        self, rule_hits: list[RuleHit], stored_hits: dict[tuple[int, str], StoredRuleHits]
    ) -> dict[tuple[int, int], str]:
        """
        Return the gateway hits differing from the stored ones by (rule_metadata_id, dev_id).

        The stored hits are updated in place, so they reflect the state after writing.
        A rule reported more than once for the same gateway counts with its latest hit.
        """
        changed_hits: dict[tuple[int, int], str] = {}
        for rule_hit in rule_hits:
            dev_id = self.import_state.state.gateway_map.get(rule_hit.gateway_mgm_id, {}).get(rule_hit.gateway_uid)
            if dev_id is None:
                FWOLogger.debug(f"rule hit import - gateway {rule_hit.gateway_uid} is not imported, skipping hit")
                continue
            stored_rule = stored_hits.get((rule_hit.rule_mgm_id, rule_hit.rule_uid))
            if stored_rule is None:
                FWOLogger.debug(f"rule hit import - rule {rule_hit.rule_uid} is not imported yet, skipping hit")
                continue

            key = (stored_rule.rule_metadata_id, dev_id)
            if key in changed_hits and not is_later(rule_hit.last_hit, changed_hits[key]):
                continue
            if key not in changed_hits and stored_rule.hits_per_device.get(dev_id) == rule_hit.last_hit:
                continue
            changed_hits[key] = rule_hit.last_hit
            stored_rule.hits_per_device[dev_id] = rule_hit.last_hit
        return changed_hits

    @staticmethod
    def collect_changed_rule_last_hits(stored_hits: dict[tuple[int, str], StoredRuleHits]) -> dict[int, str]:
        """Return the rules whose latest gateway hit is later than their stored last hit, by rule_metadata_id."""
        changed_last_hits: dict[int, str] = {}
        for stored_rule in stored_hits.values():
            if len(stored_rule.hits_per_device) == 0:
                continue
            latest_hit = max(stored_rule.hits_per_device.values(), key=datetime.fromisoformat)
            if stored_rule.rule_last_hit is None or is_later(latest_hit, stored_rule.rule_last_hit):
                changed_last_hits[stored_rule.rule_metadata_id] = latest_hit
        return changed_last_hits

    def upsert_gateway_hits(self, changed_hits: dict[tuple[int, int], str]) -> int:
        """Write the changed gateway hits in chunks and return the number of written rows."""
        rows: list[dict[str, Any]] = [
            {"rule_metadata_id": rule_metadata_id, "dev_id": dev_id, "rule_last_hit": last_hit}
            for (rule_metadata_id, dev_id), last_hit in changed_hits.items()
        ]
        mutation = FwoApi.get_graphql_code([fwo_const.GRAPHQL_QUERY_PATH + "rule_metadata/upsertRuleHits.graphql"])
        written = 0
        for chunk in chunked(rows, fwo_const.API_CALL_CHUNK_SIZE):
            result = self.import_state.api_call.call(mutation, query_variables={"ruleHits": chunk})
            if "errors" in result:
                raise FwoApiWriteError(f"failed to write rule hits: {result['errors']!s}")
            written += result["data"]["insert_firewall_rule_hit"]["affected_rows"]
        return written

    def update_rule_last_hits(self, changed_last_hits: dict[int, str]) -> int:
        """Write the changed rule last hits in chunks and return the number of written rows."""
        updates: list[dict[str, Any]] = [
            {"where": {"rule_metadata_id": {"_eq": rule_metadata_id}}, "_set": {"rule_last_hit": last_hit}}
            for rule_metadata_id, last_hit in changed_last_hits.items()
        ]
        mutation = FwoApi.get_graphql_code([fwo_const.GRAPHQL_QUERY_PATH + "rule_metadata/updateLastHits.graphql"])
        written = 0
        for chunk in chunked(updates, fwo_const.API_CALL_CHUNK_SIZE):
            result = self.import_state.api_call.call(mutation, query_variables={"hit_info": chunk})
            if "errors" in result:
                raise FwoApiWriteError(f"failed to write rule last hits: {result['errors']!s}")
            written += sum(update["affected_rows"] for update in result["data"]["update_firewall_rule_metadata_many"])
        return written


def normalize_hit(value: str) -> str:
    """Normalize a stored hit timestamp to the UTC ISO format of RuleHit.last_hit (no timezone counts as UTC)."""
    normalized_value = validate_iso_timestamp_value("Stored rule hit", value)
    if normalized_value is None:
        raise ValueError("stored rule hit must not be empty")
    return normalized_value


def normalize_optional_hit(value: str | None) -> str | None:
    """Normalize a stored, possibly empty hit timestamp."""
    if value is None:
        return None
    return normalize_hit(value)


def is_later(first_hit: str, second_hit: str) -> bool:
    """Return whether the first normalized hit timestamp is later than the second."""
    return datetime.fromisoformat(first_hit) > datetime.fromisoformat(second_hit)


def chunked(items: list[dict[str, Any]], chunk_size: int) -> list[list[dict[str, Any]]]:
    """Split a list into chunks of at most chunk_size items."""
    return [items[start : start + chunk_size] for start in range(0, len(items), chunk_size)]
