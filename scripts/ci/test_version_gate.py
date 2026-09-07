"""Unit tests for the version gate rules, see documentation/developer-docs/versioning.md."""

from __future__ import annotations

import difflib
import io
import json
from typing import TYPE_CHECKING

import pytest

from scripts.ci.version_gate import (
    MAX_DESCRIPTION_LENGTH,
    Verdict,
    build_parser,
    count_headings,
    evaluate_gate,
    evaluate_open_version,
    evaluate_revision_history,
    evaluate_tag,
    evaluate_upgrade_files,
    evaluate_version_lifecycle,
    last_revision_history_heading,
    main,
    opens_final_section,
    parse_product_version,
    parse_unified_diff,
    parse_version,
    read_tags,
    read_version,
    revision_history_has_final_section_addition,
    revision_history_heading_version,
    run_command,
    sealed_versions,
    sealing_version,
    tag_version,
    upgrade_file_version,
)

if TYPE_CHECKING:
    from pathlib import Path

REVISION_HISTORY = """# Revision history

## 9.4.4 - 27.08.2026
- something older

## 9.4.5 - 01.09.2026
- the current change
"""
REVISION_HISTORY_WITH_ADDITION = f"{REVISION_HISTORY}- another change\n"


def revision_history_for(version: str) -> str:
    """Build a revision history containing the baseline heading for a version."""
    return f"{REVISION_HISTORY}\n## {version}\n- a new change\n"


def diff_between(base_markdown: str, merged_markdown: str) -> str:
    """Build the revision-history diff the shell script hands to the gate."""
    return "".join(
        difflib.unified_diff(
            base_markdown.splitlines(keepends=True),
            merged_markdown.splitlines(keepends=True),
            fromfile="a/documentation/revision-history.md",
            tofile="b/documentation/revision-history.md",
            n=0,
        )
    )


def gate_inputs(merged_markdown: str, base_markdown: str = REVISION_HISTORY) -> tuple[str, str]:
    """Return the merged revision history and its diff, as the gate receives both."""
    return (merged_markdown, diff_between(base_markdown, merged_markdown))


def has_addition(base_markdown: str, merged_markdown: str) -> bool:
    """Run the final-section check on the diff between two revision-history snapshots."""
    return revision_history_has_final_section_addition(merged_markdown, diff_between(base_markdown, merged_markdown))


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

    @pytest.mark.parametrize("text", ["9.4", "9.4.5-dev", "v9.4.5", "09.4.5", "9.04.5", "9.4.05", "", "nine"])
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
        tags = ["v9.4.4", "v9.4.5-dev", "v09.4.6", "v9.4.6-rc1", "not-a-tag", "9.3.4"]
        assert sealed_versions(tags) == {"9.4.4", "9.4.5", "9.3.4"}

    @pytest.mark.parametrize("tag", ["v9.4.5", "v9.4.5-dev", "v9.4.5-rc1", "9.4.5-beta.2"])
    def test_tag_version_covers_snapshots_too(self, tag: str) -> None:
        assert tag_version(tag) == "9.4.5"

    def test_tag_version_ignores_unrelated_tags(self) -> None:
        assert tag_version("importer-rework") is None


class TestRevisionHistory:
    @pytest.mark.parametrize(
        "heading",
        ["## 9.4.5", "## 9.4.5 - 01.09.2026", "## 9.4.5 - 01.09.2026 MAIN"],
    )
    def test_version_heading_allows_optional_trailing_text(self, heading: str) -> None:
        heading_text = last_revision_history_heading(f"{heading}\n- change\n")
        assert heading_text is not None
        assert revision_history_heading_version(heading_text) == "9.4.5"

    @pytest.mark.parametrize(
        "heading",
        ["## 9.3", "## 09.3.0", "## 9.03.0", "## 9.3.00", "## 9.3.0-dev"],
    )
    def test_noncanonical_version_headings_are_rejected(self, heading: str) -> None:
        heading_text = last_revision_history_heading(f"{heading}\n- change\n")
        assert heading_text is not None
        assert revision_history_heading_version(heading_text) is None

    @pytest.mark.parametrize("heading", ["# 9.3.0", "### 9.3.0"])
    def test_other_heading_levels_are_ignored(self, heading: str) -> None:
        assert last_revision_history_heading(f"{heading}\n- change\n") is None

    def test_final_level_two_heading_is_returned(self) -> None:
        markdown = "## 9.4.6\n- new\n\n## 9.3 - 31.07.2026\n- legacy\n"
        assert last_revision_history_heading(markdown) == "9.3 - 31.07.2026"

    def test_missing_heading_returns_none(self) -> None:
        assert last_revision_history_heading("# Revision history\n\nno sections yet\n") is None

    def test_text_added_to_final_section_is_detected(self) -> None:
        assert has_addition(REVISION_HISTORY, REVISION_HISTORY_WITH_ADDITION)

    def test_duplicate_text_added_to_final_section_is_detected(self) -> None:
        merged_revision_history = f"{REVISION_HISTORY}- the current change\n"
        assert has_addition(REVISION_HISTORY, merged_revision_history)

    def test_new_section_repeating_an_earlier_entry_is_detected(self) -> None:
        merged_revision_history = f"{REVISION_HISTORY}\n## 9.4.6 - 05.09.2026\n- the current change\n"
        assert has_addition(REVISION_HISTORY, merged_revision_history)

    def test_changing_only_the_final_heading_does_not_count_as_added_text(self) -> None:
        merged_revision_history = REVISION_HISTORY.replace("## 9.4.5", "## 9.4.6")
        assert not has_addition(REVISION_HISTORY, merged_revision_history)

    def test_reordering_final_section_does_not_count_as_added_text(self) -> None:
        base_revision_history = "# Revision history\n\n## 9.4.5\n- first\n- second\n"
        merged_revision_history = "# Revision history\n\n## 9.4.5\n- second\n- first\n"
        assert not has_addition(base_revision_history, merged_revision_history)

    def test_reindenting_an_entry_does_not_count_as_added_text(self) -> None:
        base_revision_history = "# Revision history\n\n## 9.4.5\n- the current change\n"
        merged_revision_history = "# Revision history\n\n## 9.4.5\n  - the current change \n"
        assert not has_addition(base_revision_history, merged_revision_history)

    def test_reindenting_does_not_mask_a_real_addition(self) -> None:
        base_revision_history = "# Revision history\n\n## 9.4.5\n- the current change\n"
        merged_revision_history = "# Revision history\n\n## 9.4.5\n  - the current change\n- another change\n"
        assert has_addition(base_revision_history, merged_revision_history)

    def test_relocating_an_entry_into_the_final_section_counts_as_added_text(self) -> None:
        # The cancellation covers the final section only, so text moved in from elsewhere in
        # the file is text this section did not have, see F21.
        base_revision_history = f"{REVISION_HISTORY}\n## 9.4.6\n- a new change\n"
        merged_revision_history = "# Revision history\n\n## 9.4.4 - 27.08.2026\n- something older\n\n"
        merged_revision_history += "## 9.4.5 - 01.09.2026\n\n## 9.4.6\n- a new change\n- the current change\n"
        assert has_addition(base_revision_history, merged_revision_history)

    def test_bump_relocating_the_previous_section_trailing_entry_counts_as_added_text(self) -> None:
        # Git renders the moved entry as a context line, so the new section is judged from the
        # merged file rather than from the diff's added lines, see F22.
        base_revision_history = f"{REVISION_HISTORY}- my change\n"
        merged_revision_history = f"{REVISION_HISTORY}\n## 9.4.6 - 07.09.2026\n- my change\n"
        assert has_addition(base_revision_history, merged_revision_history)

    def test_renaming_the_final_heading_and_adding_a_sub_heading_does_not_count_as_added_text(self) -> None:
        base_revision_history = "# Revision history\n\n## 9.4.5 - 01.09.2026\n- entry A\n- entry B\n"
        merged_revision_history = "# Revision history\n\n## 9.4.6 - 07.09.2026\n### details\n- entry A\n- entry B\n"
        assert not has_addition(base_revision_history, merged_revision_history)

    def test_renaming_the_final_heading_beside_another_edit_does_not_count_as_added_text(self) -> None:
        # A line inserted next to the heading puts both edits in one hunk, where the removed
        # heading is reported at the hunk's position rather than its own, see F23.
        base_revision_history = "# Revision history\n\n## 9.4.4\n- older\n\n## 9.4.5 - 01.09.2026\n- entry A\n"
        with_blank_line = "# Revision history\n\n## 9.4.4\n- older\n\n\n## 9.4.6 - 07.09.2026\n- entry A\n"
        with_entry_above = "# Revision history\n\n## 9.4.4\n- older\n- extra\n## 9.4.6 - 07.09.2026\n- entry A\n"

        assert not has_addition(base_revision_history, with_blank_line)
        assert not has_addition(base_revision_history, with_entry_above)

    def test_splitting_the_final_section_counts_as_added_text(self) -> None:
        # Accepted, not overlooked: splitting a section and moving an entry under a new heading
        # are the same edit to git, so requiring new text here would fail the reclassification
        # the versioning lifecycle asks for, see F24 and documentation/developer-docs/versioning.md.
        base_revision_history = "# Revision history\n\n## 9.4.5 - 01.09.2026\n- entry A\n- entry B\n"
        merged_revision_history = "# Revision history\n\n## 9.4.5 - 01.09.2026\n- entry A\n\n## 9.4.6\n- entry B\n"
        assert has_addition(base_revision_history, merged_revision_history)

    def test_opening_an_empty_section_does_not_count_as_added_text(self) -> None:
        assert not has_addition(REVISION_HISTORY, f"{REVISION_HISTORY}\n## 9.4.6 - 07.09.2026\n\n")

    def test_relocating_the_first_line_of_the_file_counts_as_added_text(self) -> None:
        base_revision_history = "- the current change\n\n## 9.4.6\n"
        merged_revision_history = "\n## 9.4.6\n- the current change\n"
        assert has_addition(base_revision_history, merged_revision_history)

    def test_text_added_to_an_earlier_section_does_not_count(self) -> None:
        merged_revision_history = REVISION_HISTORY.replace("- something older\n", "- something older\n- and more\n")
        assert not has_addition(REVISION_HISTORY, merged_revision_history)

    def test_large_diff_is_evaluated(self) -> None:
        added_lines = "".join(f"+- entry {index}\n" for index in range(20_000))
        revision_history_diff = f"@@ -5,0 +6,20000 @@\n{added_lines}"
        assert revision_history_has_final_section_addition(REVISION_HISTORY, revision_history_diff)

    @pytest.mark.parametrize(
        "merged_revision_history",
        [
            REVISION_HISTORY,
            f"{REVISION_HISTORY}\n",
            f"{REVISION_HISTORY}---\n",
            f"{REVISION_HISTORY}### More details\n",
            REVISION_HISTORY.replace("- the current change\n", ""),
            REVISION_HISTORY.replace("# Revision history\n", "# Revision history\n- added too early\n"),
        ],
    )
    def test_no_text_addition_to_final_section_is_rejected(self, merged_revision_history: str) -> None:
        assert not has_addition(REVISION_HISTORY, merged_revision_history)

    def test_revision_history_verdict_requires_text_below_matching_final_heading(self) -> None:
        assert evaluate_revision_history("9.4.5", *gate_inputs(REVISION_HISTORY_WITH_ADDITION)).ok

    def test_revision_history_verdict_rejects_heading_without_text(self) -> None:
        verdict = evaluate_revision_history("9.4.6", *gate_inputs(f"{REVISION_HISTORY}\n## 9.4.6\n"))
        assert not verdict.ok
        assert "add revision-history text" in verdict.reason


class TestUnifiedDiffParsing:
    def test_added_lines_carry_their_line_number_in_the_new_file(self) -> None:
        revision_history_diff = diff_between(REVISION_HISTORY, REVISION_HISTORY_WITH_ADDITION)
        added_lines, removed_lines = parse_unified_diff(revision_history_diff)
        assert added_lines == [(8, "- another change")]
        assert removed_lines == []

    def test_file_headers_are_not_read_as_changed_lines(self) -> None:
        revision_history_diff = (
            "diff --git a/documentation/revision-history.md b/documentation/revision-history.md\n"
            "index 1234567..89abcde 100644\n"
            "--- a/documentation/revision-history.md\n"
            "+++ b/documentation/revision-history.md\n"
            "@@ -7 +7 @@\n"
            "-- the current change\n"
            "+- the rewritten change\n"
        )
        assert parse_unified_diff(revision_history_diff) == (
            [(7, "- the rewritten change")],
            [(7, "- the current change")],
        )

    def test_deletion_at_the_head_of_the_file_is_recorded(self) -> None:
        # Git writes '+0,0' for a deletion block at the head of a file, which must not be
        # mistaken for the diff header, see F20.
        revision_history_diff = "@@ -1 +0,0 @@\n-- the current change\n@@ -3,0 +3 @@\n+- the current change\n"
        assert parse_unified_diff(revision_history_diff) == (
            [(3, "- the current change")],
            [(0, "- the current change")],
        )

    def test_removed_lines_carry_the_position_their_hunk_is_anchored_at(self) -> None:
        revision_history_diff = "@@ -4 +3,0 @@\n-- misfiled entry\n@@ -8,0 +8 @@\n+- misfiled entry\n"
        assert parse_unified_diff(revision_history_diff) == (
            [(8, "- misfiled entry")],
            [(3, "- misfiled entry")],
        )

    def test_context_lines_advance_the_line_number(self) -> None:
        revision_history_diff = "@@ -6,2 +6,3 @@\n context line\n+- added below context\n"
        assert parse_unified_diff(revision_history_diff) == ([(7, "- added below context")], [])

    def test_missing_newline_marker_advances_no_line_number(self) -> None:
        revision_history_diff = "@@ -7,0 +8,2 @@\n+- added entry\n\\ No newline at end of file\n+- second entry\n"
        assert parse_unified_diff(revision_history_diff) == (
            [(8, "- added entry"), (9, "- second entry")],
            [],
        )

    def test_empty_diff_has_no_changed_lines(self) -> None:
        assert parse_unified_diff("") == ([], [])


class TestOpensFinalSection:
    def test_inserted_heading_opens_a_section(self) -> None:
        added_lines = [(8, ""), (9, "## 9.4.6 - 07.09.2026")]

        assert opens_final_section(added_lines, [], "## 9.4.6 - 07.09.2026")

    def test_renamed_heading_does_not_open_a_section(self) -> None:
        added_lines = [(3, "## 9.4.6 - 07.09.2026")]
        removed_lines = [(3, "## 9.4.5 - 01.09.2026")]

        assert not opens_final_section(added_lines, removed_lines, "## 9.4.6 - 07.09.2026")

    def test_added_sub_heading_does_not_turn_a_rename_into_an_opened_section(self) -> None:
        # '###' is not a section heading where the final section is located, so it must not be
        # one where headings are counted either, see F26.
        added_lines = [(3, "## 9.4.6 - 07.09.2026"), (4, "### details")]
        removed_lines = [(3, "## 9.4.5 - 01.09.2026")]

        assert not opens_final_section(added_lines, removed_lines, "## 9.4.6 - 07.09.2026")

    def test_heading_added_elsewhere_does_not_open_the_final_section(self) -> None:
        added_lines = [(4, "## 9.4.5 - 01.09.2026")]

        assert not opens_final_section(added_lines, [], "## 9.4.6 - 07.09.2026")

    def test_only_level_two_headings_are_counted(self) -> None:
        diff_lines = [
            (1, "## 9.4.6 - 07.09.2026"),
            (2, "### details"),
            (3, "#### more"),
            (4, "##missing space"),
            (5, "- an entry"),
        ]

        assert count_headings(diff_lines) == 1


class TestVersionLifecycle:
    def test_open_version_passes_with_its_reason(self) -> None:
        verdict = evaluate_version_lifecycle("9.4.5", "9.4.5", {"9.4.4"})

        assert verdict.ok
        assert verdict.reason == "version 9.4.5 is still open"

    def test_bump_onto_a_sealed_base_passes_with_its_reason(self) -> None:
        verdict = evaluate_version_lifecycle("9.4.6", "9.4.5", {"9.4.5"})

        assert verdict.ok
        assert verdict.reason == "version 9.4.5 is sealed, opening version 9.4.6"

    def test_sealed_merged_version_is_rejected(self) -> None:
        verdict = evaluate_version_lifecycle("9.4.5", "9.4.5", {"9.4.5"})

        assert not verdict.ok
        assert "already sealed" in verdict.reason

    def test_malformed_version_raises(self) -> None:
        with pytest.raises(ValueError, match="valid product version"):
            evaluate_version_lifecycle("9.4", "9.4.5", set())


class TestGateWithoutVersionBump:
    def test_open_version_passes(self) -> None:
        verdict = evaluate_gate(
            "9.4.5",
            "9.4.5",
            {"9.4.4"},
            *gate_inputs(REVISION_HISTORY_WITH_ADDITION),
        )
        assert verdict.ok
        assert "still open" in verdict.reason

    def test_sealed_version_is_blocked(self) -> None:
        verdict = evaluate_gate("9.4.5", "9.4.5", {"9.4.5"}, *gate_inputs(REVISION_HISTORY))
        assert not verdict.ok
        assert "already sealed" in verdict.reason

    def test_zero_padded_version_is_rejected(self) -> None:
        verdict = evaluate_gate("9.04.5", "9.04.5", {"9.4.5"}, *gate_inputs(REVISION_HISTORY))
        assert not verdict.ok
        assert "valid product version" in verdict.reason

    def test_snapshot_tag_keeps_the_version_open(self) -> None:
        verdict = evaluate_gate(
            "9.4.5",
            "9.4.5",
            sealed_versions(["v9.4.5-rc1"]),
            *gate_inputs(REVISION_HISTORY_WITH_ADDITION),
        )
        assert verdict.ok

    def test_revision_history_addition_is_required_without_a_bump(self) -> None:
        verdict = evaluate_gate("9.4.5", "9.4.5", set(), *gate_inputs(REVISION_HISTORY))
        assert not verdict.ok
        assert "add revision-history text" in verdict.reason

    def test_verified_automation_can_skip_revision_history(self) -> None:
        verdict = evaluate_gate(
            "9.4.5",
            "9.4.5",
            set(),
            *gate_inputs(REVISION_HISTORY),
            revision_history_required=False,
        )
        assert verdict.ok
        assert "revision history is exempt" in verdict.reason

    def test_verified_automation_still_obeys_version_rules(self) -> None:
        verdict = evaluate_gate(
            "9.4.5",
            "9.4.5",
            {"9.4.5"},
            *gate_inputs(REVISION_HISTORY),
            revision_history_required=False,
        )
        assert not verdict.ok
        assert "already sealed" in verdict.reason


class TestGateWithVersionBump:
    def test_bump_after_sealing_passes(self) -> None:
        verdict = evaluate_gate("9.4.6", "9.4.5", {"9.4.5"}, *gate_inputs(revision_history_for("9.4.6")))
        assert verdict.ok
        assert "opening version 9.4.6" in verdict.reason

    def test_bump_from_zero_padded_base_version_is_rejected(self) -> None:
        verdict = evaluate_gate("9.4.6", "9.04.5", {"9.4.5"}, *gate_inputs(revision_history_for("9.4.6")))
        assert not verdict.ok
        assert "valid product version" in verdict.reason

    def test_zero_padded_bump_is_rejected(self) -> None:
        verdict = evaluate_gate("9.04.6", "9.4.5", {"9.4.5"}, *gate_inputs(revision_history_for("9.4.6")))
        assert not verdict.ok
        assert "valid product version" in verdict.reason

    def test_bump_before_sealing_is_blocked(self) -> None:
        verdict = evaluate_gate("9.4.6", "9.4.5", {"9.4.4"}, *gate_inputs(revision_history_for("9.4.6")))
        assert not verdict.ok
        assert "v9.4.5-dev or v9.4.5" in verdict.reason

    def test_dev_tag_alone_unblocks_the_bump(self) -> None:
        verdict = evaluate_gate(
            "9.4.6",
            "9.4.5",
            sealed_versions(["v9.4.5-dev"]),
            *gate_inputs(revision_history_for("9.4.6")),
        )
        assert verdict.ok

    def test_bump_onto_an_already_sealed_version_is_blocked(self) -> None:
        verdict = evaluate_gate("9.4.6", "9.4.5", {"9.4.5", "9.4.6"}, *gate_inputs(revision_history_for("9.4.6")))
        assert not verdict.ok
        assert "choose a higher version" in verdict.reason

    def test_backwards_bump_is_blocked(self) -> None:
        verdict = evaluate_gate("9.4.4", "9.4.5", {"9.4.5"}, *gate_inputs(revision_history_for("9.4.4")))
        assert not verdict.ok
        assert "must not go backwards" in verdict.reason

    def test_minor_and_major_jumps_are_allowed(self) -> None:
        assert evaluate_gate("9.5.0", "9.4.5", {"9.4.5"}, *gate_inputs(revision_history_for("9.5.0"))).ok
        assert evaluate_gate("10.0.0", "9.4.5", {"9.4.5"}, *gate_inputs(revision_history_for("10.0.0"))).ok

    def test_missing_revision_history_section_is_blocked(self) -> None:
        verdict = evaluate_gate("9.4.6", "9.4.5", {"9.4.5"}, *gate_inputs(REVISION_HISTORY))
        assert not verdict.ok
        assert "must end with a '## 9.4.6' heading" in verdict.reason

    def test_matching_revision_history_heading_must_be_last(self) -> None:
        revision_history = f"{revision_history_for('9.4.6')}\n## 9.3 - 31.07.2026\n- legacy heading\n"
        verdict = evaluate_gate("9.4.6", "9.4.5", {"9.4.5"}, *gate_inputs(revision_history))
        assert not verdict.ok
        assert "last level-two heading is '## 9.3 - 31.07.2026'" in verdict.reason

    def test_empty_revision_history_is_reported_as_missing(self) -> None:
        verdict = evaluate_gate("9.4.6", "9.4.5", {"9.4.5"}, *gate_inputs(""))
        assert not verdict.ok
        assert "last level-two heading is missing" in verdict.reason

    def test_malformed_merged_version_is_blocked(self) -> None:
        verdict = evaluate_gate("9.4", "9.4.5", {"9.4.5"}, *gate_inputs(REVISION_HISTORY))
        assert not verdict.ok
        assert "valid product version" in verdict.reason


class TestUpgradeFileNames:
    def test_patchless_name_is_read_as_patch_zero(self) -> None:
        assert upgrade_file_version("9.0.sql") == (9, 0, 0)

    def test_name_without_a_version_is_ignored(self) -> None:
        assert upgrade_file_version("cleanup.sql") is None
        assert upgrade_file_version("9.4.7.sql.bak") is None


class TestUpgradeFileSelection:
    def test_file_for_the_opened_version_passes(self) -> None:
        verdict = evaluate_upgrade_files("9.4.7", "9.4.6", ["9.4.6.sql", "9.4.7.sql"], ["9.4.6.sql"])
        assert verdict.ok

    def test_file_for_the_still_open_version_passes(self) -> None:
        verdict = evaluate_upgrade_files("9.4.7", "9.4.7", ["9.4.7.sql"], [])
        assert verdict.ok

    def test_file_left_behind_by_a_higher_version_merging_first_fails(self) -> None:
        verdict = evaluate_upgrade_files("9.5.1", "9.5.0", ["9.5.0.sql", "9.4.7.sql"], ["9.5.0.sql"])
        assert not verdict.ok
        assert "9.4.7.sql is below version 9.5.0" in verdict.reason
        assert "Rename it to 9.5.1.sql" in verdict.reason

    def test_file_above_the_product_version_fails(self) -> None:
        verdict = evaluate_upgrade_files("9.4.7", "9.4.6", ["9.5.0.sql"], [])
        assert not verdict.ok
        assert "above product_version 9.4.7" in verdict.reason

    def test_file_the_base_branch_already_carries_is_not_judged_again(self) -> None:
        verdict = evaluate_upgrade_files("9.5.1", "9.5.0", ["9.4.7.sql"], ["9.4.7.sql"])
        assert verdict.ok

    def test_names_without_a_version_are_left_alone(self) -> None:
        assert evaluate_upgrade_files("9.4.7", "9.4.6", ["readme.sql"], []).ok

    def test_malformed_version_fails(self) -> None:
        assert not evaluate_upgrade_files("nine", "9.4.6", ["9.4.7.sql"], []).ok


class TestGateWithUpgradeFiles:
    def test_gate_rejects_an_upgrade_file_below_the_base_version(self) -> None:
        verdict = evaluate_gate(
            "9.5.1",
            "9.5.0",
            {"9.5.0"},
            *gate_inputs(revision_history_for("9.5.1")),
            merged_upgrade_files=["9.5.0.sql", "9.4.7.sql"],
            base_upgrade_files=["9.5.0.sql"],
        )
        assert not verdict.ok
        assert "would skip it" in verdict.reason

    def test_gate_accepts_an_upgrade_file_for_the_opened_version(self) -> None:
        verdict = evaluate_gate(
            "9.5.1",
            "9.5.0",
            {"9.5.0"},
            *gate_inputs(revision_history_for("9.5.1")),
            merged_upgrade_files=["9.5.0.sql", "9.5.1.sql"],
            base_upgrade_files=["9.5.0.sql"],
        )
        assert verdict.ok

    def test_gate_without_upgrade_file_names_is_unchanged(self) -> None:
        verdict = evaluate_gate("9.4.6", "9.4.5", {"9.4.5"}, *gate_inputs(revision_history_for("9.4.6")))
        assert verdict.ok


class TestOpenVersionAudit:
    def test_unsealed_branch_version_passes(self) -> None:
        assert evaluate_open_version("9.4.6", {"9.4.5"}).ok

    def test_sealed_branch_version_fails(self) -> None:
        verdict = evaluate_open_version("9.4.5", {"9.4.5"})
        assert not verdict.ok
        assert "Raise product_version" in verdict.reason

    def test_zero_padded_branch_version_is_rejected(self) -> None:
        verdict = evaluate_open_version("9.04.5", {"9.4.5"})
        assert not verdict.ok
        assert "valid product version" in verdict.reason

    def test_malformed_branch_version_fails(self) -> None:
        assert not evaluate_open_version("nine", set()).ok


class TestTagValidation:
    def test_matching_tag_passes(self) -> None:
        assert evaluate_tag("v9.4.5", "9.4.5").ok

    @pytest.mark.parametrize(
        ("tag", "product_version"),
        [("v9.04.5", "9.4.5"), ("v9.4.5", "9.04.5"), ("v9.04.5-rc1", "9.4.5")],
    )
    def test_zero_padded_tag_or_product_version_is_rejected(self, tag: str, product_version: str) -> None:
        verdict = evaluate_tag(tag, product_version)
        assert not verdict.ok
        assert "valid product version" in verdict.reason

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
        merged_history = revision_history_for("9.4.6")
        history = tmp_path / "revision-history.md"
        history.write_text(merged_history, encoding="utf-8")
        history_diff = tmp_path / "revision-history.diff"
        history_diff.write_text(diff_between(REVISION_HISTORY, merged_history), encoding="utf-8")
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
                "--revision-history-diff",
                str(history_diff),
                "--tags-file",
                str(tags),
            ]
        )
        assert run_command(arguments).ok

    def test_gate_command_can_skip_revision_history(self, tmp_path: Path) -> None:
        history = tmp_path / "revision-history.md"
        history.write_text(REVISION_HISTORY, encoding="utf-8")
        history_diff = tmp_path / "revision-history.diff"
        history_diff.write_text("", encoding="utf-8")
        tags = tmp_path / "tags.txt"
        tags.write_text("", encoding="utf-8")

        arguments = build_parser().parse_args(
            [
                "gate",
                "--merged-version",
                "9.4.5",
                "--base-version",
                "9.4.5",
                "--revision-history",
                str(history),
                "--revision-history-diff",
                str(history_diff),
                "--tags-file",
                str(tags),
                "--skip-revision-history",
            ]
        )
        verdict = run_command(arguments)
        assert verdict.ok
        assert "revision history is exempt" in verdict.reason

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
