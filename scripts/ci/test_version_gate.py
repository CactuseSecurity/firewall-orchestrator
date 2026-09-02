"""Unit tests for the version gate rules, see documentation/developer-docs/versioning.md."""

from __future__ import annotations

import io
import json
from typing import TYPE_CHECKING

import pytest

from scripts.ci.version_gate import (
    MAX_DESCRIPTION_LENGTH,
    Verdict,
    build_parser,
    evaluate_gate,
    evaluate_open_version,
    evaluate_tag,
    last_revision_history_version,
    main,
    parse_product_version,
    parse_version,
    read_tags,
    read_version,
    run_command,
    sealed_versions,
    sealing_version,
    tag_version,
)

if TYPE_CHECKING:
    from pathlib import Path

REVISION_HISTORY = """# Revision history

## 9.4.4 - 27.08.2026
- something older

## 9.4.5 - 01.09.2026
- the current change
"""


def revision_history_for(version: str) -> str:
    """Build a revision history whose last section documents the given version."""
    return f"{REVISION_HISTORY}\n## {version} - 02.09.2026\n- a new change\n"


class TestParsing:
    def test_parse_product_version_reads_quoted_value(self) -> None:
        assert (
            parse_product_version('### general settings\nproduct_version: "9.4.5"\nproduct_name: fworch\n') == "9.4.5"
        )

    def test_parse_product_version_reads_unquoted_value(self) -> None:
        assert parse_product_version("product_version: 9.4.5\n") == "9.4.5"

    def test_parse_product_version_ignores_similar_keys(self) -> None:
        assert parse_product_version("debian_testing_version: 12\nproduct_version: 10.0.1\n") == "10.0.1"

    def test_parse_product_version_without_key_fails(self) -> None:
        with pytest.raises(ValueError, match="product_version"):
            parse_product_version("product_name: fworch\n")

    def test_parse_version_returns_comparable_tuple(self) -> None:
        assert parse_version("9.4.5") == (9, 4, 5)
        assert parse_version("10.0.1") > parse_version("9.4.5")

    @pytest.mark.parametrize("text", ["9.4", "9.4.5-dev", "v9.4.5", "", "nine"])
    def test_parse_version_rejects_malformed_versions(self, text: str) -> None:
        with pytest.raises(ValueError, match="valid product version"):
            parse_version(text)


class TestTagClassification:
    @pytest.mark.parametrize("tag", ["v9.4.5", "9.4.5", "v9.4.5-dev", "9.4.5-dev"])
    def test_stable_and_dev_tags_seal(self, tag: str) -> None:
        assert sealing_version(tag) == "9.4.5"

    @pytest.mark.parametrize("tag", ["v9.4.5-rc1", "v9.4.5-beta", "v9.4.5-alpha.1", "release-9.4.5", "v9.4"])
    def test_snapshot_and_unrelated_tags_do_not_seal(self, tag: str) -> None:
        assert sealing_version(tag) is None

    def test_sealed_versions_collects_only_sealing_tags(self) -> None:
        tags = ["v9.4.4", "v9.4.5-dev", "v9.4.6-rc1", "not-a-tag", "9.3.4"]
        assert sealed_versions(tags) == {"9.4.4", "9.4.5", "9.3.4"}

    @pytest.mark.parametrize("tag", ["v9.4.5", "v9.4.5-dev", "v9.4.5-rc1", "9.4.5-beta.2"])
    def test_tag_version_covers_snapshots_too(self, tag: str) -> None:
        assert tag_version(tag) == "9.4.5"

    def test_tag_version_ignores_unrelated_tags(self) -> None:
        assert tag_version("importer-rework") is None


class TestRevisionHistory:
    def test_last_section_is_returned(self) -> None:
        assert last_revision_history_version(REVISION_HISTORY) == "9.4.5"

    def test_main_marker_is_tolerated(self) -> None:
        assert last_revision_history_version("## 9.4.5 - 01.09.2026 MAIN\n- change\n") == "9.4.5"

    def test_missing_section_returns_none(self) -> None:
        assert last_revision_history_version("# Revision history\n\nno sections yet\n") is None


class TestGateWithoutVersionBump:
    def test_open_version_passes(self) -> None:
        verdict = evaluate_gate("9.4.5", "9.4.5", {"9.4.4"}, REVISION_HISTORY)
        assert verdict.ok
        assert "still open" in verdict.reason

    def test_sealed_version_is_blocked(self) -> None:
        verdict = evaluate_gate("9.4.5", "9.4.5", {"9.4.5"}, REVISION_HISTORY)
        assert not verdict.ok
        assert "already sealed" in verdict.reason

    def test_snapshot_tag_keeps_the_version_open(self) -> None:
        verdict = evaluate_gate("9.4.5", "9.4.5", sealed_versions(["v9.4.5-rc1"]), REVISION_HISTORY)
        assert verdict.ok

    def test_no_revision_history_is_required_without_a_bump(self) -> None:
        assert evaluate_gate("9.4.5", "9.4.5", set(), "").ok


class TestGateWithVersionBump:
    def test_bump_after_sealing_passes(self) -> None:
        verdict = evaluate_gate("9.4.6", "9.4.5", {"9.4.5"}, revision_history_for("9.4.6"))
        assert verdict.ok
        assert "opening version 9.4.6" in verdict.reason

    def test_bump_before_sealing_is_blocked(self) -> None:
        verdict = evaluate_gate("9.4.6", "9.4.5", {"9.4.4"}, revision_history_for("9.4.6"))
        assert not verdict.ok
        assert "v9.4.5-dev or v9.4.5" in verdict.reason

    def test_dev_tag_alone_unblocks_the_bump(self) -> None:
        verdict = evaluate_gate("9.4.6", "9.4.5", sealed_versions(["v9.4.5-dev"]), revision_history_for("9.4.6"))
        assert verdict.ok

    def test_bump_onto_an_already_sealed_version_is_blocked(self) -> None:
        verdict = evaluate_gate("9.4.6", "9.4.5", {"9.4.5", "9.4.6"}, revision_history_for("9.4.6"))
        assert not verdict.ok
        assert "choose a higher version" in verdict.reason

    def test_backwards_bump_is_blocked(self) -> None:
        verdict = evaluate_gate("9.4.4", "9.4.5", {"9.4.5"}, revision_history_for("9.4.4"))
        assert not verdict.ok
        assert "must not go backwards" in verdict.reason

    def test_minor_and_major_jumps_are_allowed(self) -> None:
        assert evaluate_gate("9.5.0", "9.4.5", {"9.4.5"}, revision_history_for("9.5.0")).ok
        assert evaluate_gate("10.0.0", "9.4.5", {"9.4.5"}, revision_history_for("10.0.0")).ok

    def test_missing_revision_history_section_is_blocked(self) -> None:
        verdict = evaluate_gate("9.4.6", "9.4.5", {"9.4.5"}, REVISION_HISTORY)
        assert not verdict.ok
        assert "last section is 9.4.5" in verdict.reason

    def test_empty_revision_history_is_reported_as_missing(self) -> None:
        verdict = evaluate_gate("9.4.6", "9.4.5", {"9.4.5"}, "")
        assert not verdict.ok
        assert "last section is missing" in verdict.reason

    def test_malformed_merged_version_is_blocked(self) -> None:
        verdict = evaluate_gate("9.4", "9.4.5", {"9.4.5"}, REVISION_HISTORY)
        assert not verdict.ok
        assert "valid product version" in verdict.reason


class TestOpenVersionAudit:
    def test_unsealed_branch_version_passes(self) -> None:
        assert evaluate_open_version("9.4.6", {"9.4.5"}).ok

    def test_sealed_branch_version_fails(self) -> None:
        verdict = evaluate_open_version("9.4.5", {"9.4.5"})
        assert not verdict.ok
        assert "Raise product_version" in verdict.reason

    def test_malformed_branch_version_fails(self) -> None:
        assert not evaluate_open_version("nine", set()).ok


class TestTagValidation:
    def test_matching_tag_passes(self) -> None:
        assert evaluate_tag("v9.4.5", "9.4.5").ok

    def test_snapshot_tag_must_match_too(self) -> None:
        assert evaluate_tag("v9.4.5-rc1", "9.4.5").ok

    def test_mismatched_tag_fails(self) -> None:
        verdict = evaluate_tag("v9.4.5", "9.4.6")
        assert not verdict.ok
        assert "must point at a commit carrying version 9.4.5" in verdict.reason

    def test_unrelated_tag_is_ignored(self) -> None:
        verdict = evaluate_tag("importer-rework", "9.4.5")
        assert verdict.ok
        assert "not a version tag" in verdict.reason


class TestVerdict:
    def test_long_reasons_are_truncated_for_the_commit_status(self) -> None:
        payload = Verdict(ok=False, reason="x" * 300).to_dict()
        assert payload["reason"] == "x" * 300
        assert isinstance(payload["description"], str)
        assert len(payload["description"]) == MAX_DESCRIPTION_LENGTH

    def test_short_reasons_are_kept_verbatim(self) -> None:
        payload = Verdict(ok=True, reason="all good").to_dict()
        assert payload["description"] == "all good"


class TestInputHandling:
    def test_read_version_prefers_the_literal(self) -> None:
        assert read_version(" 9.4.6 ", None) == "9.4.6"

    def test_read_version_falls_back_to_the_file(self, tmp_path: Path) -> None:
        all_yml = tmp_path / "all.yml"
        all_yml.write_text('product_version: "9.4.5"\n', encoding="utf-8")
        assert read_version(None, str(all_yml)) == "9.4.5"

    def test_read_version_without_any_source_fails(self) -> None:
        with pytest.raises(ValueError, match="version or the path"):
            read_version(None, None)

    def test_read_tags_from_file_skips_blank_lines(self, tmp_path: Path) -> None:
        tags_file = tmp_path / "tags.txt"
        tags_file.write_text("v9.4.4\n\n v9.4.5-dev \n", encoding="utf-8")
        assert read_tags(str(tags_file)) == ["v9.4.4", "v9.4.5-dev"]

    def test_read_tags_from_stdin(self, monkeypatch: pytest.MonkeyPatch) -> None:
        monkeypatch.setattr("sys.stdin", io.StringIO("v9.4.4\nv9.4.5\n"))
        assert read_tags("-") == ["v9.4.4", "v9.4.5"]


class TestCommandLine:
    def test_gate_command_reads_files(self, tmp_path: Path) -> None:
        merged = tmp_path / "merged.yml"
        merged.write_text('product_version: "9.4.6"\n', encoding="utf-8")
        base = tmp_path / "base.yml"
        base.write_text('product_version: "9.4.5"\n', encoding="utf-8")
        history = tmp_path / "revision-history.md"
        history.write_text(revision_history_for("9.4.6"), encoding="utf-8")
        tags = tmp_path / "tags.txt"
        tags.write_text("v9.4.5\n", encoding="utf-8")

        arguments = build_parser().parse_args(
            [
                "gate",
                "--merged-file",
                str(merged),
                "--base-file",
                str(base),
                "--revision-history",
                str(history),
                "--tags-file",
                str(tags),
            ]
        )
        assert run_command(arguments).ok

    def test_check_open_command(self, tmp_path: Path) -> None:
        tags = tmp_path / "tags.txt"
        tags.write_text("v9.4.5\n", encoding="utf-8")
        arguments = build_parser().parse_args(["check-open", "--version", "9.4.5", "--tags-file", str(tags)])
        assert not run_command(arguments).ok

    def test_check_tag_command(self) -> None:
        arguments = build_parser().parse_args(["check-tag", "--tag", "v9.4.5", "--version", "9.4.5"])
        assert run_command(arguments).ok

    def test_main_prints_json_and_returns_exit_code(
        self,
        monkeypatch: pytest.MonkeyPatch,
        capsys: pytest.CaptureFixture[str],
        tmp_path: Path,
    ) -> None:
        tags = tmp_path / "tags.txt"
        tags.write_text("v9.4.5\n", encoding="utf-8")
        monkeypatch.setattr(
            "sys.argv",
            ["version_gate.py", "check-open", "--version", "9.4.5", "--tags-file", str(tags)],
        )

        exit_code = main()

        assert exit_code == 1
        payload = json.loads(capsys.readouterr().out)
        assert payload["ok"] is False
        assert "sealed" in payload["reason"]

    def test_main_reports_unreadable_input_as_a_failed_verdict(
        self,
        monkeypatch: pytest.MonkeyPatch,
        capsys: pytest.CaptureFixture[str],
    ) -> None:
        monkeypatch.setattr(
            "sys.argv",
            ["version_gate.py", "check-tag", "--tag", "v9.4.5", "--file", "/does/not/exist.yml"],
        )

        exit_code = main()

        assert exit_code == 1
        assert "could not be evaluated" in json.loads(capsys.readouterr().out)["reason"]
