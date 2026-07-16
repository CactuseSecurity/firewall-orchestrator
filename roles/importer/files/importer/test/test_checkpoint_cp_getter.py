from types import SimpleNamespace
from typing import Any, cast
from unittest.mock import MagicMock, patch

import pytest
import requests
from fw_modules.checkpointR8x import cp_const, cp_getter
from fwo_exceptions import FwApiError, FwApiResponseDecodingError, FwLoginFailedError, FwoImporterError
from model_controllers.management_controller import ManagementController


def response_mock(
    json_result: dict[str, Any] | None = None, text: str = "", json_error: Exception | None = None
) -> MagicMock:
    response = MagicMock()
    if json_error is not None:
        response.json.side_effect = json_error
    else:
        response.json.return_value = json_result
    response.text = text
    return response


def manager_mock(
    devices: list[dict[str, str]] | None = None, domain: str = "", import_user: str = "importer"
) -> ManagementController:
    mock = SimpleNamespace(
        import_user=import_user,
        secret="mock-secret",  # noqa: S106
        devices=devices or [],
        get_domain_string=lambda: domain,
        build_fw_api_string=lambda: "https://mgm.invalid/web_api/",
    )
    return cast("ManagementController", mock)


def no_sleep(_seconds: float) -> None:
    return None


class TestCpApiCall:
    def test_returns_json_and_sets_sid_header(self):
        with patch.object(cp_getter.requests, "post", return_value=response_mock({"sid": "abc"})) as post:
            result = cp_getter.cp_api_call("https://mgm.invalid/", "show-changes", {"a": 1}, "sid-1")

        assert result == {"sid": "abc"}
        assert post.call_args.kwargs["headers"]["X-chkp-sid"] == "sid-1"
        assert post.call_args.args[0] == "https://mgm.invalid/show-changes"

    def test_login_call_omits_sid_header_and_shows_progress(self, capsys: pytest.CaptureFixture[str]):
        with patch.object(cp_getter.requests, "post", return_value=response_mock({"sid": "abc"})) as post:
            cp_getter.cp_api_call("https://mgm.invalid/", "login", {"user": "u"}, None, show_progress=True)

        assert "X-chkp-sid" not in post.call_args.kwargs["headers"]
        assert capsys.readouterr().out == "."

    def test_request_error_with_password_hides_payload(self):
        error = requests.exceptions.RequestException("boom")
        with (
            patch.object(cp_getter.requests, "post", side_effect=error),
            pytest.raises(FwApiError) as exception_info,
        ):
            cp_getter.cp_api_call("https://mgm.invalid/", "login", {"password": "secret"}, None)

        assert "secret" not in str(exception_info.value)
        assert "credential information" in str(exception_info.value)

    def test_request_error_without_password_includes_payload(self):
        error = requests.exceptions.RequestException("boom")
        with (
            patch.object(cp_getter.requests, "post", side_effect=error),
            pytest.raises(FwApiError, match="show-changes"),
        ):
            cp_getter.cp_api_call("https://mgm.invalid/", "show-changes", {"a": 1}, "sid-1")

    def test_invalid_json_response_raises_decoding_error(self):
        broken = response_mock(json_error=ValueError("no json"), text="<html>")
        with (
            patch.object(cp_getter.requests, "post", return_value=broken),
            pytest.raises(FwApiResponseDecodingError, match="<html>"),
        ):
            cp_getter.cp_api_call("https://mgm.invalid/", "show-changes", {}, "sid-1")


class TestLoginLogout:
    def test_login_returns_sid_and_sends_domain(self):
        with patch.object(cp_getter, "cp_api_call", return_value={"sid": "sid-1"}) as api_call:
            sid = cp_getter.login(manager_mock(domain="dom-1"))

        assert sid == "sid-1"
        assert api_call.call_args.args[2]["domain"] == "dom-1"

    def test_login_without_domain_and_missing_sid_fails(self):
        with (
            patch.object(cp_getter, "cp_api_call", return_value={"message": "denied"}) as api_call,
            pytest.raises(FwLoginFailedError, match="did not receive a sid"),
        ):
            cp_getter.login(manager_mock())

        assert "domain" not in api_call.call_args.args[2]

    def test_logout_sends_sid(self):
        with patch.object(cp_getter, "cp_api_call", return_value={}) as api_call:
            cp_getter.logout("https://mgm.invalid/", "sid-1")

        assert api_call.call_args.args == ("https://mgm.invalid/", "logout", {}, "sid-1")


class TestChangeTasks:
    def test_process_single_task_without_status_fails(self):
        assert cp_getter.process_single_task({}) == (cp_getter.STATUS_FAILED, -1)

    def test_process_single_task_succeeded_with_changes(self):
        task = {"status": "succeeded", "task-details": [{"changes": True}]}
        assert cp_getter.process_single_task(task) == ("succeeded", 1)

    def test_process_single_task_succeeded_without_changes(self):
        task = {"status": "succeeded", "task-details": [{"changes": False}]}
        assert cp_getter.process_single_task(task) == ("succeeded", 0)

    def test_process_single_task_failed_in_progress_and_unknown(self):
        assert cp_getter.process_single_task({"status": "failed"}) == ("failed", 0)
        assert cp_getter.process_single_task({"status": "in progress"}) == ("in progress", 0)
        assert cp_getter.process_single_task({"status": "bogus"}) == (cp_getter.STATUS_FAILED, -1)

    def test_process_changes_task_returns_first_final_result(self, monkeypatch: pytest.MonkeyPatch):
        monkeypatch.setattr(cp_getter.time, "sleep", no_sleep)
        responses = [
            {"tasks": [{"status": "in progress"}]},
            {"tasks": [{"status": "succeeded", "task-details": [{"changes": True}]}]},
        ]
        with patch.object(cp_getter, "cp_api_call", side_effect=responses):
            assert cp_getter.process_changes_task("https://mgm.invalid/", {"task-id": "t1"}, "sid-1") == 1

    def test_process_changes_task_without_tasks_fails(self, monkeypatch: pytest.MonkeyPatch):
        monkeypatch.setattr(cp_getter.time, "sleep", no_sleep)
        with patch.object(cp_getter, "cp_api_call", return_value={}):
            assert cp_getter.process_changes_task("https://mgm.invalid/", {"task-id": "t1"}, "sid-1") == -1

    def test_process_changes_task_aborts_when_task_takes_too_long(self, monkeypatch: pytest.MonkeyPatch):
        monkeypatch.setattr(cp_getter.time, "sleep", no_sleep)
        with patch.object(cp_getter, "cp_api_call", return_value={"tasks": [{"status": "in progress"}]}):
            assert cp_getter.process_changes_task("https://mgm.invalid/", {"task-id": "t1"}, "sid-1") == -1

    def test_get_changes_truncates_microseconds(self):
        with (
            patch.object(cp_getter, "cp_api_call", return_value={"task-id": "t1"}) as api_call,
            patch.object(cp_getter, "process_changes_task", return_value=1) as process_task,
        ):
            result = cp_getter.get_changes("sid-1", "mgm.invalid", "443", "2026-07-01T10:00:00.123456")

        assert result == 1
        assert api_call.call_args.args[2]["from-date"] == "2026-07-01T10:00:00"
        assert process_task.call_args.args == ("https://mgm.invalid:443/web_api/", {"task-id": "t1"}, "sid-1")


class TestPolicyStructure:
    def test_get_policy_structure_collects_matching_packages(self):
        package: dict[str, Any] = {
            "name": "pkg-1",
            "uid": "pkg-uid-1",
            "installation-targets": "all",
            "access-layers": [{"name": "layer-1", "uid": "layer-uid-1", "domain": {"uid": "dom-1"}}],
        }
        skipped_package: dict[str, Any] = {"name": "pkg-2", "uid": "pkg-uid-2"}
        policy_structure: list[dict[str, Any]] = []
        with patch.object(
            cp_getter,
            "get_show_packages_via_api",
            return_value=({"packages": [package, skipped_package]}, 1, 1),
        ):
            result = cp_getter.get_policy_structure(
                "https://mgm.invalid/", "sid-1", {}, manager_mock(), policy_structure
            )

        assert result == 0
        assert policy_structure == [
            {
                "name": "pkg-1",
                "uid": "pkg-uid-1",
                "targets": [{"name": "all", "uid": "all"}],
                "access-layers": [{"name": "layer-1", "uid": "layer-uid-1", "domain": "dom-1"}],
            }
        ]

    def test_get_policy_structure_defaults_policy_structure_argument(self):
        with patch.object(cp_getter, "get_show_packages_via_api", return_value=({"packages": []}, 1, 1)):
            assert cp_getter.get_policy_structure("https://mgm.invalid/", "sid-1", {}, manager_mock()) == 0

    def test_get_show_packages_via_api_returns_pagination_info(self):
        with patch.object(cp_getter, "cp_api_call", return_value={"packages": [], "total": 3, "to": 2}):
            packages, current, total = cp_getter.get_show_packages_via_api("https://mgm.invalid/", "sid-1", {})

        assert (current, total) == (2, 3)
        assert packages["total"] == 3

    def test_get_show_packages_via_api_with_zero_total(self):
        with patch.object(cp_getter, "cp_api_call", return_value={"packages": [], "total": 0}):
            assert cp_getter.get_show_packages_via_api("https://mgm.invalid/", "sid-1", {})[1:] == (0, 0)

    def test_get_show_packages_via_api_error_paths(self):
        with (
            patch.object(cp_getter, "cp_api_call", side_effect=Exception("down")),
            pytest.raises(FwApiError, match="could not return"),
        ):
            cp_getter.get_show_packages_via_api("https://mgm.invalid/", "sid-1", {})
        with (
            patch.object(cp_getter, "cp_api_call", return_value={"warning": "odd"}),
            pytest.raises(FwApiError, match="total"),
        ):
            cp_getter.get_show_packages_via_api("https://mgm.invalid/", "sid-1", {"limit": 500})
        with (
            patch.object(cp_getter, "cp_api_call", return_value={"total": 3}),
            pytest.raises(FwApiError, match="to field"),
        ):
            cp_getter.get_show_packages_via_api("https://mgm.invalid/", "sid-1", {})

    def test_parse_package_with_target_revisions_keeps_known_devices(self):
        manager = manager_mock(devices=[{"name": "gw-1", "uid": "gw-uid-1"}])
        package: dict[str, Any] = {
            "name": "pkg-1",
            "uid": "pkg-uid-1",
            "installation-targets-revision": [
                {"target-name": "gw-1", "target-uid": "gw-uid-1"},
                {"target-name": "gw-unknown", "target-uid": "gw-uid-unknown"},
                {"target-name": "gw-incomplete"},
            ],
        }

        current_package, already_fetched = cp_getter.parse_package(package, manager)

        assert already_fetched is True
        assert current_package["targets"] == [{"name": "gw-1", "uid": "gw-uid-1"}]

    def test_parse_package_without_matching_targets(self):
        package: dict[str, Any] = {"name": "pkg-1", "uid": "pkg-uid-1", "installation-targets-revision": []}
        assert cp_getter.parse_package(package, manager_mock()) == ({}, False)

    def test_add_access_layers_rejects_incomplete_layer(self):
        current_package: dict[str, Any] = {"access-layers": []}
        package: dict[str, Any] = {"uid": "pkg-uid-1", "access-layers": [{"name": "layer-without-uid"}]}
        with pytest.raises(FwApiError, match="missing name or uid"):
            cp_getter.add_access_layers_to_current_package(package, current_package)

    def test_add_access_layers_without_layers_keeps_package_unchanged(self):
        current_package: dict[str, Any] = {"access-layers": []}
        cp_getter.add_access_layers_to_current_package({"uid": "pkg-uid-1"}, current_package)
        assert current_package["access-layers"] == []


class TestGlobalAssignments:
    def test_get_global_assignments_parses_objects(self):
        assignment = {
            "type": "global-assignment",
            "uid": "as-1",
            "global-domain": {"uid": "gd-1", "name": "Global"},
            "dependent-domain": {"uid": "dd-1", "name": "Local"},
            "global-access-policy": "policy-1",
        }
        with patch.object(cp_getter, "cp_api_call", return_value={"objects": [assignment], "total": 1, "to": 1}):
            result = cp_getter.get_global_assignments("https://mgm.invalid/", "sid-1", {})

        assert result == [
            {
                "uid": "as-1",
                "global-domain": {"uid": "gd-1", "name": "Global"},
                "dependent-domain": {"uid": "dd-1", "name": "Local"},
                "global-access-policy": "policy-1",
            }
        ]

    def test_fetch_global_assignments_chunk_with_zero_total(self):
        with patch.object(cp_getter, "cp_api_call", return_value={"objects": [], "total": 0}):
            assert cp_getter.fetch_global_assignments_chunk("https://mgm.invalid/", "sid-1", {})[1:] == (0, 0)

    def test_fetch_global_assignments_chunk_error_paths(self):
        with (
            patch.object(cp_getter, "cp_api_call", side_effect=Exception("down")),
            pytest.raises(FwoImporterError, match="show-global-assignments"),
        ):
            cp_getter.fetch_global_assignments_chunk("https://mgm.invalid/", "sid-1", {})
        with (
            patch.object(cp_getter, "cp_api_call", return_value={"warning": "odd"}),
            pytest.raises(FwoImporterError, match="total"),
        ):
            cp_getter.fetch_global_assignments_chunk("https://mgm.invalid/", "sid-1", {"limit": 500})
        with (
            patch.object(cp_getter, "cp_api_call", return_value={"total": 3}),
            pytest.raises(FwoImporterError, match="to"),
        ):
            cp_getter.fetch_global_assignments_chunk("https://mgm.invalid/", "sid-1", {})

    def test_parse_global_assignment_rejects_unexpected_type(self):
        with pytest.raises(FwoImporterError, match="unexpected type"):
            cp_getter.parse_global_assignment({"type": "bogus"})


class TestGetRulebases:
    def test_reuses_already_fetched_rulebase(self):
        fetched_rulebase: dict[str, Any] = {"uid": "rb-1", "chunks": []}
        native_config_domain: dict[str, Any] = {"rulebases": [fetched_rulebase], "nat_rulebases": []}
        policy_rulebases_uid_list: list[str] = []

        result = cp_getter.get_rulebases(
            "https://mgm.invalid/",
            "sid-1",
            {},
            native_config_domain,
            {"rulebase_links": []},
            policy_rulebases_uid_list,
            rulebase_uid="rb-1",
        )

        assert result == ["rb-1"]
        assert native_config_domain["rulebases"] == [fetched_rulebase]

    def test_fetches_nat_rulebase_with_default_configs(self):
        with patch.object(
            cp_getter, "get_rulebases_in_chunks", return_value={"uid": "rb-nat", "chunks": []}
        ) as chunk_getter:
            result = cp_getter.get_rulebases(
                "https://mgm.invalid/", "sid-1", {}, None, None, [], access_type="nat", rulebase_uid="rb-nat"
            )

        assert result == ["rb-nat"]
        assert chunk_getter.call_count == 1

    def test_resolves_rulebase_uid_from_name_with_unknown_access_type(self):
        with (
            patch.object(cp_getter, "get_uid_of_rulebase", return_value="rb-2") as uid_getter,
            patch.object(cp_getter, "get_rulebases_in_chunks", return_value={"uid": "rb-2", "chunks": []}),
        ):
            result = cp_getter.get_rulebases(
                "https://mgm.invalid/", "sid-1", {}, None, None, [], access_type="bogus", rulebase_name="Layer 1"
            )

        assert result == ["rb-2"]
        assert uid_getter.call_args.args[0] == "Layer 1"

    def test_without_uid_and_name_logs_error_and_continues(self):
        with patch.object(cp_getter, "get_rulebases_in_chunks", return_value={"uid": None, "chunks": []}):
            assert cp_getter.get_rulebases("https://mgm.invalid/", "sid-1", {}, None, None, []) == [None]

    def test_get_uid_of_rulebase_returns_uid(self):
        with patch.object(cp_getter, "cp_api_call", return_value={"uid": "rb-1"}):
            assert cp_getter.get_uid_of_rulebase("Layer 1", "https://mgm.invalid/", "access", "sid-1") == "rb-1"

    def test_get_uid_of_rulebase_returns_none_on_error(self):
        with patch.object(cp_getter, "cp_api_call", side_effect=Exception("down")):
            assert cp_getter.get_uid_of_rulebase("Layer 1", "https://mgm.invalid/", "access", "sid-1") is None

    def test_get_rulebases_in_chunks_creates_data_issue_on_api_error(self):
        service_provider = MagicMock()
        with (
            patch.object(cp_getter, "cp_api_call", side_effect=Exception("down")),
            patch.object(cp_getter, "ServiceProvider", return_value=service_provider),
            pytest.raises(FwApiError),
        ):
            cp_getter.get_rulebases_in_chunks("rb-1", {}, "https://mgm.invalid/", "access", "sid-1", {})

        create_data_issue = service_provider.get_global_state.return_value.import_state.api_call.create_data_issue
        assert create_data_issue.call_count == 1


class TestChunkMerging:
    def test_get_last_chunk_with_rulebase_skips_empty_chunks(self):
        assert cp_getter.get_last_chunk_with_rulebase({"chunks": [{"rulebase": []}, {}]}) is None

    def test_get_boundary_section_requires_sections(self):
        assert cp_getter.get_boundary_section({"rulebase": []}, first=True) is None
        assert cp_getter.get_boundary_section({"rulebase": [{"type": "access-rule"}]}, first=True) is None

    def test_merge_section_across_chunk_boundary_rejects_non_continuations(self):
        rule_chunk: dict[str, Any] = {"rulebase": [{"type": "access-rule", "uid": "rule-1"}]}
        section: dict[str, Any] = {"type": "access-section", "uid": "sec-1", "from": 1, "to": 2, "rulebase": []}
        other_section: dict[str, Any] = {"type": "access-section", "uid": "sec-2", "from": 3, "to": 4, "rulebase": []}
        gap_section: dict[str, Any] = {"type": "access-section", "uid": "sec-1", "from": 5, "to": 6, "rulebase": []}

        assert cp_getter.merge_section_across_chunk_boundary(rule_chunk, {"rulebase": [section]}) is False
        assert (
            cp_getter.merge_section_across_chunk_boundary({"rulebase": [section]}, {"rulebase": [other_section]})
            is False
        )
        assert (
            cp_getter.merge_section_across_chunk_boundary({"rulebase": [section]}, {"rulebase": [gap_section]}) is False
        )

    def test_merge_split_section_appends_chunk_when_no_merge_happened(self):
        current_rulebase: dict[str, Any] = {"chunks": []}
        chunk: dict[str, Any] = {"rulebase": [{"type": "access-rule", "uid": "rule-1"}]}

        cp_getter.merge_split_section_with_previous_chunk(current_rulebase, chunk)

        assert current_rulebase["chunks"] == [chunk]

    def test_control_while_loop_returns_pagination_markers(self):
        assert cp_getter.control_while_loop_in_get_rulebases_in_chunks(
            {}, {"total": 5, "to": 5}, "sid-1", "url", {}
        ) == (5, 5)
        assert cp_getter.control_while_loop_in_get_rulebases_in_chunks(
            {"uid": "rb-1"}, {"warning": "odd"}, "sid-1", "url", {"limit": 500}
        ) == (0, 0)

    def test_control_while_loop_rejects_missing_to_field(self):
        with pytest.raises(FwoImporterError, match="to field"):
            cp_getter.control_while_loop_in_get_rulebases_in_chunks({}, {"total": 5}, "sid-1", "url", {})


class TestInlineLayers:
    def test_get_inline_layers_recursively_links_and_fetches_inline_layer(self):
        section: dict[str, Any] = {
            "type": "access-section",
            "uid": "sec-1",
            "rulebase": [{"uid": "rule-1", "type": "access-rule", "inline-layer": "il-1"}],
        }
        current_rulebase: dict[str, Any] = {
            "uid": "rb-1",
            "chunks": [{"rulebase": [section]}, {"objects-dictionary": []}],
        }
        device_config: dict[str, Any] = {"rulebase_links": []}

        with patch.object(cp_getter, "get_rulebases", return_value=["rb-1", "il-1"]) as rulebase_getter:
            result = cp_getter.get_inline_layers_recursively(
                current_rulebase,
                device_config,
                {},
                "https://mgm.invalid/",
                "sid-1",
                {},
                is_global=False,
                policy_rulebases_uid_list=["rb-1"],
            )

        assert result == ["rb-1", "il-1"]
        assert rulebase_getter.call_args.kwargs["rulebase_uid"] == "il-1"
        link_types = [link["type"] for link in device_config["rulebase_links"]]
        assert link_types == ["concatenated", "inline"]

    def test_section_traversal_wraps_plain_rule_into_dummy_section(self):
        device_config: dict[str, Any] = {"rulebase_links": []}
        rule: dict[str, Any] = {"type": "access-rule", "uid": "rule-1"}

        section, current_uid = cp_getter.section_traversal_and_links(rule, "rb-1", device_config, is_global=False)

        assert current_uid == "rb-1"
        assert section["rulebase"] == [rule]
        assert device_config["rulebase_links"] == []

    def test_section_traversal_links_placeholder_as_concatenated_rulebase(self):
        device_config: dict[str, Any] = {"rulebase_links": []}
        placeholder: dict[str, Any] = {"type": "place-holder", "uid": "ph-1"}

        _, current_uid = cp_getter.section_traversal_and_links(placeholder, "rb-1", device_config, is_global=True)

        assert current_uid == "ph-1"
        assert device_config["rulebase_links"][0]["is_section"] is False
        assert device_config["rulebase_links"][0]["is_global"] is True

    def test_section_traversal_links_real_section(self):
        device_config: dict[str, Any] = {"rulebase_links": []}
        section: dict[str, Any] = {"type": "access-section", "uid": "sec-1", "rulebase": []}

        _, current_uid = cp_getter.section_traversal_and_links(section, "rb-1", device_config, is_global=False)

        assert current_uid == "sec-1"
        assert device_config["rulebase_links"][0]["is_section"] is True


class TestPlaceholders:
    def test_get_placeholder_in_rulebase_finds_placeholder_in_section(self):
        rulebase: dict[str, Any] = {
            "uid": "rb-1",
            "chunks": [
                {"objects-dictionary": []},
                {
                    "rulebase": [
                        {"type": "access-rule", "uid": "rule-1"},
                        {
                            "type": "access-section",
                            "uid": "sec-1",
                            "rulebase": [{"type": "place-holder", "uid": "ph-1"}],
                        },
                    ]
                },
            ],
        }

        assert cp_getter.get_placeholder_in_rulebase(rulebase) == ("ph-1", "sec-1")

    def test_get_placeholder_in_rulebase_without_placeholder(self):
        rulebase: dict[str, Any] = {"uid": "rb-1", "chunks": [{"rulebase": [{"type": "access-rule", "uid": "rule-1"}]}]}
        assert cp_getter.get_placeholder_in_rulebase(rulebase) == (None, None)

    def test_assign_placeholder_uids_falls_back_to_rulebase_uid(self):
        rule: dict[str, Any] = {"type": "place-holder", "uid": "ph-1"}
        section_without_uid: dict[str, Any] = {"type": "access-section", "rulebase": [rule]}

        result = cp_getter.assign_placeholder_uids({"uid": "rb-1"}, section_without_uid, rule, None, None)

        assert result == ("ph-1", "rb-1")


class TestNatRules:
    def test_get_nat_rules_collects_chunks(self):
        with patch.object(cp_getter, "cp_api_call", return_value={"total": 2, "to": 2}):
            nat_rules = cp_getter.get_nat_rules_from_api_as_dict("https://mgm.invalid/", "sid-1", {})

        assert nat_rules == {"nat_rule_chunks": [{"total": 2, "to": 2}]}

    def test_get_nat_rules_logs_missing_total_and_stops(self):
        with patch.object(cp_getter, "cp_api_call", return_value={"to": 1}):
            nat_rules = cp_getter.get_nat_rules_from_api_as_dict("https://mgm.invalid/", "sid-1", {}, {})

        assert len(nat_rules["nat_rule_chunks"]) == 1

    def test_get_nat_rules_rejects_missing_to_field(self):
        with (
            patch.object(cp_getter, "cp_api_call", return_value={"total": 5}),
            pytest.raises(FwApiError, match="to field"),
        ):
            cp_getter.get_nat_rules_from_api_as_dict("https://mgm.invalid/", "sid-1", {})


class TestObjectDictionaryResolution:
    def test_find_element_by_uid(self):
        elements: list[dict[str, Any]] = [{"name": "no-uid"}, {"uid": "u-1", "name": "found"}]
        assert cp_getter.find_element_by_uid(elements, "u-1") == {"uid": "u-1", "name": "found"}
        assert cp_getter.find_element_by_uid(elements, "u-2") is None

    def test_resolve_ref_returns_matched_object(self):
        obj_dict: list[dict[str, Any]] = [{"uid": "u-1", "type": "host", "name": "h-1"}]
        assert cp_getter.resolve_ref_from_object_dictionary("u-1", obj_dict) == obj_dict[0]

    def test_resolve_ref_warns_for_unknown_object(self):
        assert cp_getter.resolve_ref_from_object_dictionary(None, []) is None
        assert cp_getter.resolve_ref_from_object_dictionary("u-unknown", [], field_name="source") is None

    def test_resolve_ref_skips_warning_for_track_and_none_track_uid(self):
        assert cp_getter.resolve_ref_from_object_dictionary("u-unknown", [], field_name="track") is None
        assert cp_getter.resolve_ref_from_object_dictionary("29e53e3d-23bf-48fe-b6b1-d59bd88036f9", []) is None

    def test_resolve_ref_adds_voip_object_to_native_config(self):
        native_config_domain: dict[str, Any] = {"objects": []}
        matched: dict[str, Any] = {"uid": "u-1", "type": "CpmiVoipSipDomain", "name": "voip-1", "domain": "dom-1"}

        result = cp_getter.resolve_ref_from_object_dictionary("u-1", [matched], native_config_domain)

        assert result == matched
        assert native_config_domain["objects"][0]["type"] == "CpmiVoipSipDomain"
        assert native_config_domain["objects"][0]["chunks"][0]["objects"][0]["color"] == "black"

    def test_resolve_ref_list_resolves_all_value_shapes(self):
        obj_dict: list[dict[str, Any]] = [
            {"uid": "u-1", "type": "host", "name": "h-1"},
            {"uid": "Log", "type": "Track", "name": "Log"},
        ]
        nested_rule: dict[str, Any] = {"uid": "rule-2", "source": ["u-1"]}
        rulebase: dict[str, Any] = {
            "objects-dictionary": obj_dict,
            "rulebase": [
                {"uid": "rule-1", "source": "u-1", "rulebase": [nested_rule]},
                {"uid": "rule-3"},
            ],
        }

        cp_getter.resolve_ref_list_from_object_dictionary(rulebase, "source")
        cp_getter.resolve_ref_list_from_object_dictionary(
            {"objects-dictionary": obj_dict, "rulebase": [{"uid": "rule-4", "track": {"type": "Log"}}]}, "track"
        )

        assert rulebase["rulebase"][0]["source"]["name"] == "h-1"
        assert nested_rule["source"][0]["name"] == "h-1"


class TestObjectTypeHandlers:
    def test_handle_cpmi_any_object_variants(self):
        base: dict[str, Any] = {"uid": "u-1", "domain": "dom-1"}
        any_obj = cp_getter.handle_cpmi_any_object({**base, "name": "Any", "color": "red"})
        none_obj = cp_getter.handle_cpmi_any_object({**base, "name": "None"})

        assert any_obj["chunks"][0]["objects"][0]["type"] == "network"
        assert any_obj["chunks"][0]["objects"][0]["color"] == "red"
        assert none_obj["chunks"][0]["objects"][0]["type"] == "group"
        assert cp_getter.handle_cpmi_any_object({**base, "name": "Other"}) == {}

    def test_handle_gateway_global_updatable_and_zone_objects(self):
        obj: dict[str, Any] = {
            "uid": "u-1",
            "name": "gw-1",
            "comments": "c",
            "domain": "dom-1",
            "ipv4-address": "10.0.0.1",
        }

        gateway = cp_getter.handle_gateway_objects(obj)
        global_obj = cp_getter.handle_global_object(obj)
        updatable = cp_getter.handle_updatable_objects(obj)
        zone = cp_getter.handle_network_zone_objects(obj)

        assert gateway["chunks"][0]["objects"][0]["ipv4-address"] == "10.0.0.1"
        assert global_obj["chunks"][0]["objects"][0]["ipv4-address"] == cp_getter.fwo_const.ANY_IP_IPV4
        assert "ipv4-address" not in updatable["chunks"][0]["objects"][0]
        assert zone["chunks"][0]["objects"][0]["type"] == "network"


class TestGetObjectDetailsFromApi:
    def object_response(self, obj_type: str, name: str = "obj-1") -> dict[str, Any]:
        return {
            "object": {
                "uid": "u-1",
                "name": name,
                "type": obj_type,
                "comments": "c",
                "domain": "dom-1",
                "ipv4-address": "10.0.0.1",
            }
        }

    def test_api_error_and_none_response_raise(self):
        with (
            patch.object(cp_getter, "cp_api_call", side_effect=Exception("down")),
            pytest.raises(FwoImporterError, match="error while trying"),
        ):
            cp_getter.get_object_details_from_api("u-1")
        with (
            patch.object(cp_getter, "cp_api_call", return_value=None),
            pytest.raises(FwoImporterError, match="None received"),
        ):
            cp_getter.get_object_details_from_api("u-1")

    def test_broken_reference_returns_empty_dict(self):
        with patch.object(cp_getter, "cp_api_call", return_value={"code": "generic_err"}):
            assert cp_getter.get_object_details_from_api("u-1") == {}
        with patch.object(cp_getter, "cp_api_call", return_value={}):
            assert cp_getter.get_object_details_from_api("u-1") == {}

    def test_special_object_types_are_converted(self):
        cases = {
            "CpmiAnyObject": "network",
            "simple-gateway": "host",
            "Global": "host",
            "updatable-object": "host",
            "Internet": "network",
        }
        for obj_type, expected_type in cases.items():
            name = "Any" if obj_type == "CpmiAnyObject" else "obj-1"
            with patch.object(cp_getter, "cp_api_call", return_value=self.object_response(obj_type, name)):
                result = cp_getter.get_object_details_from_api("u-1", "sid-1", "https://mgm.invalid/")
            assert result["chunks"][0]["objects"][0]["type"] == expected_type, obj_type

    def test_plain_api_object_types_are_returned_as_is(self):
        for obj_type in ["access-role", cp_const.api_obj_types[0]]:
            with patch.object(cp_getter, "cp_api_call", return_value=self.object_response(obj_type)):
                assert cp_getter.get_object_details_from_api("u-1")["type"] == obj_type

    def test_unexpected_object_type_returns_empty_dict(self):
        with patch.object(cp_getter, "cp_api_call", return_value=self.object_response("weird-type")):
            assert cp_getter.get_object_details_from_api("u-1") == {}
