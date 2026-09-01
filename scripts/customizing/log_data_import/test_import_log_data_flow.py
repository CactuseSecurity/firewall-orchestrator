import json
import logging
from pathlib import Path
from typing import Any

import pytest

from scripts.customizing.log_data_import import import_log_data_from_git as importer

LOGGER: logging.Logger = logging.getLogger("test_import_log_data_flow")
CSV_CONTENT: str = "App ID,Log count,Src IP,Dst IP,Port,Protocol\nAPP-1,42,192.0.2.1,198.51.100.1,443,6\n"


def return_true(*_args: object, **_kwargs: object) -> bool:
    return True


def return_false(*_args: object, **_kwargs: object) -> bool:
    return False


def write_config(tmp_path: Path, repository_directory: Path, start_path: str | None = None) -> str:
    config_file: Path = tmp_path / "customizingConfigLogData.json"
    config: dict[str, str] = {
        "logDataGitRepo": "local.logdata/log_repo",
        "logDataGitUser": "local",
        "logDataGitPassword": "local",
        "logDataGitRepoTargetDir": str(repository_directory),
        "logDataGitBranch": "main",
    }
    if start_path is not None:
        config[importer.START_PATH_CONFIG_KEY] = start_path
    config_file.write_text(
        json.dumps(config),
        encoding="utf-8",
    )
    return str(config_file)


def prepare_repository(tmp_path: Path, monkeypatch: pytest.MonkeyPatch) -> tuple[str, Path, Path]:
    repository_directory: Path = tmp_path / "repo"
    (repository_directory / "2026-08-12").mkdir(parents=True)
    (repository_directory / "2026-08-12" / "fw.csv").write_text(CSV_CONTENT, encoding="utf-8")
    output_file: Path = tmp_path / "import_log_data_from_git.json"
    monkeypatch.setattr(importer, "OUTPUT_FILE", output_file)
    monkeypatch.setattr(importer, "MANIFEST_FILE", tmp_path / ".fwo-log-import-manifest.json")
    return write_config(tmp_path, repository_directory), repository_directory, output_file


def test_import_data_writes_entries_and_manifest(tmp_path: Path, monkeypatch: pytest.MonkeyPatch) -> None:
    config_file, _repository_directory, output_file = prepare_repository(tmp_path, monkeypatch)
    monkeypatch.setattr(importer, "update_git_repo", return_true)

    result: int = importer.import_data(config_file, None, LOGGER)

    manifest: dict[str, Any] = json.loads(importer.MANIFEST_FILE.read_text(encoding="utf-8"))
    entries: dict[str, Any] = json.loads(output_file.read_text(encoding="utf-8"))
    assert result == 0
    assert entries["import_time"].endswith("+00:00")
    assert entries["logs"][0]["app_id"] == "APP-1"
    assert entries["logs"][0]["log_count"] == 42
    assert manifest["csv_files"] == ["2026-08-12/fw.csv"]


def test_import_data_searches_the_whole_repository_without_a_start_path(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    config_file, repository_directory, output_file = prepare_repository(tmp_path, monkeypatch)
    (repository_directory / "other.csv").write_text(CSV_CONTENT.replace("APP-1", "APP-2"), encoding="utf-8")
    monkeypatch.setattr(importer, "update_git_repo", return_true)

    result: int = importer.import_data(config_file, None, LOGGER)

    entries: dict[str, Any] = json.loads(output_file.read_text(encoding="utf-8"))
    assert result == 0
    assert [entry["app_id"] for entry in entries["logs"]] == ["APP-1", "APP-2"]


def test_import_data_limits_csv_search_to_the_configured_start_path(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    _config_file, repository_directory, output_file = prepare_repository(tmp_path, monkeypatch)
    config_file: str = write_config(tmp_path, repository_directory, "2026-08-12")
    (repository_directory / "other.csv").write_text(CSV_CONTENT.replace("APP-1", "APP-2"), encoding="utf-8")
    monkeypatch.setattr(importer, "update_git_repo", return_true)

    result: int = importer.import_data(config_file, None, LOGGER)

    manifest: dict[str, Any] = json.loads(importer.MANIFEST_FILE.read_text(encoding="utf-8"))
    entries: dict[str, Any] = json.loads(output_file.read_text(encoding="utf-8"))
    assert result == 0
    assert [entry["app_id"] for entry in entries["logs"]] == ["APP-1"]
    assert manifest["csv_files"] == ["2026-08-12/fw.csv"]


@pytest.mark.parametrize("target_outside_repository", [False, True])
def test_import_data_rejects_a_csv_symlink_escaping_the_configured_start_path(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
    caplog: pytest.LogCaptureFixture,
    target_outside_repository: bool,
) -> None:
    _config_file, repository_directory, output_file = prepare_repository(tmp_path, monkeypatch)
    config_file: str = write_config(tmp_path, repository_directory, "2026-08-12")
    target_directory: Path = tmp_path if target_outside_repository else repository_directory
    target_file: Path = target_directory / "outside.csv"
    target_file.write_text(CSV_CONTENT.replace("APP-1", "APP-2"), encoding="utf-8")
    link_target: Path = Path("../../outside.csv") if target_outside_repository else Path("../outside.csv")
    (repository_directory / "2026-08-12" / "linked.csv").symlink_to(link_target)
    monkeypatch.setattr(importer, "update_git_repo", return_true)

    with caplog.at_level(logging.ERROR, logger=LOGGER.name):
        result: int = importer.import_data(config_file, None, LOGGER)

    assert result == 1
    assert "linked.csv resolves outside the selected log data search directory" in caplog.text
    assert not output_file.exists()


def test_import_data_finds_csv_files_below_a_symlinked_repository_directory(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    """A start path must not make the found files unreachable from the configured repository."""
    _config_file, repository_directory, output_file = prepare_repository(tmp_path, monkeypatch)
    linked_repository_directory: Path = tmp_path / "linked_repo"
    linked_repository_directory.symlink_to(repository_directory)
    config_file: str = write_config(tmp_path, linked_repository_directory, "2026-08-12")
    monkeypatch.setattr(importer, "update_git_repo", return_true)

    result: int = importer.import_data(config_file, None, LOGGER)

    manifest: dict[str, Any] = json.loads(importer.MANIFEST_FILE.read_text(encoding="utf-8"))
    entries: dict[str, Any] = json.loads(output_file.read_text(encoding="utf-8"))
    assert result == 0
    assert [entry["app_id"] for entry in entries["logs"]] == ["APP-1"]
    assert manifest["csv_files"] == ["2026-08-12/fw.csv"]


def test_import_data_reports_a_rejected_file_below_an_abbreviated_repository_directory(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    """A repository directory which is not written out in its shortest form is a valid one as well."""
    _config_file, repository_directory, output_file = prepare_repository(tmp_path, monkeypatch)
    (repository_directory / "2026-08-12" / "fw.csv").write_text(
        CSV_CONTENT.replace(",443,6", ",443,1"), encoding="utf-8"
    )
    abbreviated_repository_directory: Path = repository_directory / "2026-08-12" / ".."
    config_file: str = write_config(tmp_path, abbreviated_repository_directory, "2026-08-12")
    monkeypatch.setattr(importer, "update_git_repo", return_true)

    result: int = importer.import_data(config_file, None, LOGGER)

    manifest: dict[str, Any] = json.loads(importer.MANIFEST_FILE.read_text(encoding="utf-8"))
    entries: dict[str, Any] = json.loads(output_file.read_text(encoding="utf-8"))
    assert result == 0
    assert entries["logs"] == []
    assert manifest["csv_files"] == []


def test_import_data_rejects_a_start_path_outside_the_repository(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch, caplog: pytest.LogCaptureFixture
) -> None:
    _config_file, repository_directory, output_file = prepare_repository(tmp_path, monkeypatch)
    config_file: str = write_config(tmp_path, repository_directory, "../outside")
    monkeypatch.setattr(importer, "update_git_repo", return_true)

    with caplog.at_level(logging.ERROR, logger=LOGGER.name):
        result: int = importer.import_data(config_file, None, LOGGER)

    assert result == 1
    assert "must name a directory within the log data repository" in caplog.text
    assert not output_file.exists()


def test_import_data_builds_the_repository_url_from_the_credentials(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    config_file, _, _ = prepare_repository(tmp_path, monkeypatch)
    used_urls: list[str] = []

    def record_url(repo_url: str, *_args: object, **_kwargs: object) -> bool:
        used_urls.append(repo_url)
        return True

    monkeypatch.setattr(importer, "update_git_repo", record_url)

    importer.import_data(config_file, 1, LOGGER)

    assert used_urls == ["https://local:local@local.logdata/log_repo"]


def test_import_data_reports_a_failed_clone(tmp_path: Path, monkeypatch: pytest.MonkeyPatch) -> None:
    config_file, _, output_file = prepare_repository(tmp_path, monkeypatch)
    monkeypatch.setattr(importer, "update_git_repo", return_false)

    result: int = importer.import_data(config_file, None, LOGGER)

    assert result == 1
    assert not output_file.exists()


def test_import_data_reuses_a_pending_import(tmp_path: Path, monkeypatch: pytest.MonkeyPatch) -> None:
    config_file, _repository_directory, output_file = prepare_repository(tmp_path, monkeypatch)
    monkeypatch.setattr(importer, "update_git_repo", return_true)
    importer.import_data(config_file, None, LOGGER)
    original_output: str = output_file.read_text(encoding="utf-8")

    def fail_if_called(*_args: object, **_kwargs: object) -> bool:
        raise AssertionError("pending imports must not refresh the repository")

    monkeypatch.setattr(importer, "update_git_repo", fail_if_called)

    result: int = importer.import_data(config_file, None, LOGGER)

    assert result == 0
    assert importer.MANIFEST_FILE.exists()
    assert output_file.read_text(encoding="utf-8") == original_output


def exhaust_pending_reuses(config_file: str, monkeypatch: pytest.MonkeyPatch) -> list[int]:
    """Reuse the pending import until it was kept back more often than MAX_PENDING_REUSES."""
    monkeypatch.setattr(importer, "update_git_repo", return_true)
    importer.import_data(config_file, None, LOGGER)
    return [importer.import_data(config_file, None, LOGGER) for _ in range(importer.MAX_PENDING_REUSES + 1)]


def test_import_data_keeps_reusing_after_the_reuse_limit(tmp_path: Path, monkeypatch: pytest.MonkeyPatch) -> None:
    config_file, _repository_directory, output_file = prepare_repository(tmp_path, monkeypatch)

    results: list[int] = exhaust_pending_reuses(config_file, monkeypatch)

    manifest: dict[str, Any] = json.loads(importer.MANIFEST_FILE.read_text(encoding="utf-8"))
    assert results == [0] * (importer.MAX_PENDING_REUSES + 1)
    assert manifest["reuses"] == importer.MAX_PENDING_REUSES + 1
    assert output_file.exists()


def test_acknowledge_import_recovers_after_the_reuse_limit(tmp_path: Path, monkeypatch: pytest.MonkeyPatch) -> None:
    config_file, _repository_directory, output_file = prepare_repository(tmp_path, monkeypatch)
    exhaust_pending_reuses(config_file, monkeypatch)
    monkeypatch.setattr(importer, "commit_and_push_deletions", return_false)
    assert importer.acknowledge_import(config_file, LOGGER) == 1
    monkeypatch.setattr(importer, "commit_and_push_deletions", return_true)

    result: int = importer.acknowledge_import(config_file, LOGGER)

    assert result == 0
    assert not importer.MANIFEST_FILE.exists()
    assert not output_file.exists()


def test_acknowledge_import_reports_a_persistent_failure(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch, caplog: pytest.LogCaptureFixture
) -> None:
    config_file, _repository_directory, _ = prepare_repository(tmp_path, monkeypatch)
    exhaust_pending_reuses(config_file, monkeypatch)
    monkeypatch.setattr(importer, "commit_and_push_deletions", return_false)

    with caplog.at_level(logging.ERROR, logger=LOGGER.name):
        assert importer.acknowledge_import(config_file, LOGGER) == 1

    assert f"could not be pushed in {importer.MAX_PENDING_REUSES + 2} runs" in caplog.text


def test_acknowledge_import_reports_a_single_failure(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch, caplog: pytest.LogCaptureFixture
) -> None:
    config_file, _repository_directory, _ = prepare_repository(tmp_path, monkeypatch)
    monkeypatch.setattr(importer, "update_git_repo", return_true)
    importer.import_data(config_file, None, LOGGER)
    monkeypatch.setattr(importer, "commit_and_push_deletions", return_false)

    with caplog.at_level(logging.ERROR, logger=LOGGER.name):
        assert importer.acknowledge_import(config_file, LOGGER) == 1

    assert "the data is kept and imported again" in caplog.text


def test_reuse_reports_a_source_the_middleware_kept_back(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch, caplog: pytest.LogCaptureFixture
) -> None:
    config_file, _repository_directory, _ = prepare_repository(tmp_path, monkeypatch)

    with caplog.at_level(logging.ERROR, logger=LOGGER.name):
        exhaust_pending_reuses(config_file, monkeypatch)

    assert "check the log data settings" in caplog.text
    assert "pushed to the log data repository" not in caplog.text, "no deletion push was attempted"


def test_reuse_reports_a_deletion_which_cannot_be_pushed(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch, caplog: pytest.LogCaptureFixture
) -> None:
    config_file, _repository_directory, _ = prepare_repository(tmp_path, monkeypatch)
    monkeypatch.setattr(importer, "update_git_repo", return_true)
    importer.import_data(config_file, None, LOGGER)
    monkeypatch.setattr(importer, "commit_and_push_deletions", return_false)
    assert importer.acknowledge_import(config_file, LOGGER) == 1

    with caplog.at_level(logging.ERROR, logger=LOGGER.name):
        for _ in range(importer.MAX_PENDING_REUSES + 1):
            importer.import_data(config_file, None, LOGGER)

    assert "can be pushed to the log data repository" in caplog.text
    assert "check the log data settings" not in caplog.text


def test_read_reuses_ignores_an_unusable_value() -> None:
    assert importer.read_reuses({"reuses": "many"}) == 0
    assert importer.read_reuses({"reuses": -1}) == 0
    assert importer.read_reuses({"reuses": 2}) == 2


def test_acknowledge_import_deletes_manifest_and_output(tmp_path: Path, monkeypatch: pytest.MonkeyPatch) -> None:
    config_file, repository_directory, output_file = prepare_repository(tmp_path, monkeypatch)
    monkeypatch.setattr(importer, "update_git_repo", return_true)
    importer.import_data(config_file, None, LOGGER)
    deleted_files: list[list[Path]] = []

    def record_deletions(_directory: str, files: list[Path], *_args: object, **_kwargs: object) -> bool:
        deleted_files.append(files)
        return True

    monkeypatch.setattr(importer, "commit_and_push_deletions", record_deletions)

    result: int = importer.acknowledge_import(config_file, LOGGER)

    assert result == 0
    assert deleted_files == [[repository_directory / "2026-08-12/fw.csv"]]
    assert not importer.MANIFEST_FILE.exists()
    assert not output_file.exists()


def test_acknowledge_import_keeps_the_manifest_when_the_push_fails(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    config_file, _repository_directory, _ = prepare_repository(tmp_path, monkeypatch)
    monkeypatch.setattr(importer, "update_git_repo", return_true)
    importer.import_data(config_file, None, LOGGER)
    monkeypatch.setattr(importer, "commit_and_push_deletions", return_false)

    result: int = importer.acknowledge_import(config_file, LOGGER)

    assert result == 1
    assert importer.MANIFEST_FILE.exists()


def test_acknowledge_import_without_manifest_does_nothing(tmp_path: Path, monkeypatch: pytest.MonkeyPatch) -> None:
    config_file, _, _ = prepare_repository(tmp_path, monkeypatch)

    assert importer.acknowledge_import(config_file, LOGGER) == 0


def test_acknowledge_import_drops_a_broken_manifest(tmp_path: Path, monkeypatch: pytest.MonkeyPatch) -> None:
    config_file, _repository_directory, output_file = prepare_repository(tmp_path, monkeypatch)
    importer.MANIFEST_FILE.write_text(json.dumps({"csv_files": "fw.csv"}), encoding="utf-8")
    output_file.write_text("{}", encoding="utf-8")

    result: int = importer.acknowledge_import(config_file, LOGGER)

    assert result == 1, "the middleware is told that the acknowledgement failed"
    assert not importer.MANIFEST_FILE.exists()
    assert not output_file.exists(), "the next run starts a fresh import instead of failing again"


def test_acknowledge_import_drops_a_corrupt_manifest(tmp_path: Path, monkeypatch: pytest.MonkeyPatch) -> None:
    config_file, _repository_directory, output_file = prepare_repository(tmp_path, monkeypatch)
    # a manifest truncated by a stopped script, the exact file a crashing run would leave behind
    importer.MANIFEST_FILE.write_text('{"csv_files": ["2026-08-12/fw.csv"', encoding="utf-8")
    output_file.write_text("{}", encoding="utf-8")

    result: int = importer.acknowledge_import(config_file, LOGGER)

    assert result == 1
    assert not importer.MANIFEST_FILE.exists()


def test_import_data_recovers_from_a_corrupt_manifest(tmp_path: Path, monkeypatch: pytest.MonkeyPatch) -> None:
    config_file, _repository_directory, output_file = prepare_repository(tmp_path, monkeypatch)
    monkeypatch.setattr(importer, "update_git_repo", return_true)
    importer.import_data(config_file, None, LOGGER)
    importer.MANIFEST_FILE.write_text('{"csv_files": ["2026-08-12/fw.csv"', encoding="utf-8")

    result: int = importer.import_data(config_file, None, LOGGER)

    manifest: dict[str, Any] = json.loads(importer.MANIFEST_FILE.read_text(encoding="utf-8"))
    assert result == 0, "an unreadable manifest must not stall the import in every following run"
    assert manifest["csv_files"] == ["2026-08-12/fw.csv"], "the repository was read again"
    assert output_file.exists()


def test_write_json_file_keeps_the_previous_content_on_a_failed_write(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    target_file: Path = tmp_path / "manifest.json"
    target_file.write_text(json.dumps({"csv_files": ["fw.csv"]}), encoding="utf-8")

    def fail_to_serialize(*_args: object, **_kwargs: object) -> str:
        raise ValueError("no space left on device")

    monkeypatch.setattr(importer.json, "dumps", fail_to_serialize)

    with pytest.raises(ValueError, match="no space left on device"):
        importer.write_json_file(target_file, {"csv_files": []})

    assert json.loads(target_file.read_text(encoding="utf-8"))["csv_files"] == ["fw.csv"]


def test_main_imports_by_default(tmp_path: Path, monkeypatch: pytest.MonkeyPatch) -> None:
    config_file, _, _ = prepare_repository(tmp_path, monkeypatch)
    monkeypatch.setattr(importer, "update_git_repo", return_true)
    monkeypatch.setattr("sys.argv", ["import_log_data_from_git.py", "--config", config_file])

    assert importer.main() == 0


def test_main_acknowledges_when_requested(tmp_path: Path, monkeypatch: pytest.MonkeyPatch) -> None:
    config_file, _, _ = prepare_repository(tmp_path, monkeypatch)
    monkeypatch.setattr(importer, "update_git_repo", return_true)
    monkeypatch.setattr(importer, "commit_and_push_deletions", return_true)
    monkeypatch.setattr("sys.argv", ["import_log_data_from_git.py", "--config", config_file, "--acknowledge-import"])
    importer.import_data(config_file, None, LOGGER)

    assert importer.main() == 0


def test_import_data_skips_a_file_with_missing_columns(tmp_path: Path, monkeypatch: pytest.MonkeyPatch) -> None:
    config_file, repository_directory, output_file = prepare_repository(tmp_path, monkeypatch)
    (repository_directory / "broken.csv").write_text("App ID,Log count\nAPP-1,42\n", encoding="utf-8")
    monkeypatch.setattr(importer, "update_git_repo", return_true)

    result: int = importer.import_data(config_file, None, LOGGER)

    manifest: dict[str, Any] = json.loads(importer.MANIFEST_FILE.read_text(encoding="utf-8"))
    entries: dict[str, Any] = json.loads(output_file.read_text(encoding="utf-8"))
    assert result == 0
    assert len(entries["logs"]) == 1
    assert manifest["csv_files"] == ["2026-08-12/fw.csv"]


def test_import_data_keeps_a_file_with_rejected_rows_for_retry(tmp_path: Path, monkeypatch: pytest.MonkeyPatch) -> None:
    config_file, repository_directory, output_file = prepare_repository(tmp_path, monkeypatch)
    rejected_file: Path = repository_directory / "2026-08-12" / "fw.csv"
    rejected_file.write_text(
        CSV_CONTENT + "APP-2,1,192.0.2.2,198.51.100.2,443,1\n",
        encoding="utf-8",
    )
    accepted_file: Path = repository_directory / "accepted.csv"
    accepted_file.write_text(
        "App ID,Log count,Src IP,Dst IP,Port,Protocol\nAPP-3,7,192.0.2.3,198.51.100.3,53,17\n",
        encoding="utf-8",
    )
    monkeypatch.setattr(importer, "update_git_repo", return_true)

    result: int = importer.import_data(config_file, None, LOGGER)

    manifest: dict[str, Any] = json.loads(importer.MANIFEST_FILE.read_text(encoding="utf-8"))
    entries: dict[str, Any] = json.loads(output_file.read_text(encoding="utf-8"))
    assert result == 0
    assert [entry["app_id"] for entry in entries["logs"]] == ["APP-3"]
    assert manifest["csv_files"] == ["accepted.csv"]


def test_import_data_writes_an_empty_file_when_every_file_is_broken(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    config_file, repository_directory, output_file = prepare_repository(tmp_path, monkeypatch)
    (repository_directory / "2026-08-12" / "fw.csv").write_text(
        "App ID,Log count,Src IP,Dst IP,Port,Protocol\nAPP-1,42,192.0.2.1,198.51.100.1,443,1\n",
        encoding="utf-8",
    )
    monkeypatch.setattr(importer, "update_git_repo", return_true)

    result: int = importer.import_data(config_file, None, LOGGER)

    manifest: dict[str, Any] = json.loads(importer.MANIFEST_FILE.read_text(encoding="utf-8"))
    entries: dict[str, Any] = json.loads(output_file.read_text(encoding="utf-8"))
    assert result == 0
    assert entries["logs"] == []
    assert manifest["csv_files"] == []


def test_convert_row_normalizes_the_log_timestamp() -> None:
    row: dict[str, str] = {
        "App ID": "APP-1",
        "Log count": "42",
        "Src IP": "192.0.2.1",
        "Dst IP": "198.51.100.1",
        "Port": "443",
        "Protocol": "6",
        "Log timestamp": "2026-08-12 08:15:00",
    }

    assert importer.convert_row(row)["log_time"] == "2026-08-12T08:15:00+00:00"


def test_convert_row_rejects_an_unparsable_log_timestamp() -> None:
    row: dict[str, str] = {
        "App ID": "APP-1",
        "Log count": "42",
        "Src IP": "192.0.2.1",
        "Dst IP": "198.51.100.1",
        "Port": "443",
        "Protocol": "6",
        "Log timestamp": "12.08.2026 08:15",
    }

    with pytest.raises(ValueError, match="not a valid log timestamp"):
        importer.convert_row(row)


def test_import_data_keeps_a_file_with_a_broken_timestamp_for_retry(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    config_file, repository_directory, output_file = prepare_repository(tmp_path, monkeypatch)
    (repository_directory / "2026-08-12" / "fw.csv").write_text(
        "App ID,Log count,Src IP,Dst IP,Port,Protocol,Log timestamp\n"
        "APP-1,42,192.0.2.1,198.51.100.1,443,6,12.08.2026 08:15\n"
        "APP-1,7,192.0.2.2,198.51.100.2,443,6,2026-08-12T08:20:00Z\n",
        encoding="utf-8",
    )
    monkeypatch.setattr(importer, "update_git_repo", return_true)

    result: int = importer.import_data(config_file, None, LOGGER)

    manifest: dict[str, Any] = json.loads(importer.MANIFEST_FILE.read_text(encoding="utf-8"))
    entries: dict[str, Any] = json.loads(output_file.read_text(encoding="utf-8"))
    assert result == 0
    assert entries["logs"] == []
    assert manifest["csv_files"] == []


def test_get_optional_value_falls_back_when_the_setting_is_not_a_string(
    tmp_path: Path, caplog: pytest.LogCaptureFixture
) -> None:
    config_file: Path = tmp_path / "customizingConfigLogData.json"
    # a start path entered as a JSON number, the config file is edited by hand on the appliance
    config: dict[str, object] = {importer.START_PATH_CONFIG_KEY: 20260812}
    config_file.write_text(json.dumps(config), encoding="utf-8")

    with caplog.at_level(logging.WARNING, logger=LOGGER.name):
        value: str = importer.get_optional_value(str(config_file), importer.START_PATH_CONFIG_KEY, "fallback", LOGGER)

    assert value == "fallback"
    assert "must be a string in config file" in caplog.text


def test_import_data_rejects_an_absolute_start_path(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch, caplog: pytest.LogCaptureFixture
) -> None:
    _config_file, repository_directory, output_file = prepare_repository(tmp_path, monkeypatch)
    # an absolute path is refused even when it points into the repository: the setting names a
    # repository-relative directory, so an absolute one would silently survive a moved target dir
    config_file: str = write_config(tmp_path, repository_directory, str(repository_directory / "2026-08-12"))
    monkeypatch.setattr(importer, "update_git_repo", return_true)

    with caplog.at_level(logging.ERROR, logger=LOGGER.name):
        result: int = importer.import_data(config_file, None, LOGGER)

    assert result == 1
    assert "must be a repository-relative directory" in caplog.text
    assert not output_file.exists()


def test_import_data_rejects_a_start_path_which_does_not_exist(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch, caplog: pytest.LogCaptureFixture
) -> None:
    _config_file, repository_directory, output_file = prepare_repository(tmp_path, monkeypatch)
    config_file: str = write_config(tmp_path, repository_directory, "2026-08-13")
    monkeypatch.setattr(importer, "update_git_repo", return_true)

    with caplog.at_level(logging.ERROR, logger=LOGGER.name):
        result: int = importer.import_data(config_file, None, LOGGER)

    assert result == 1, "a start path typo must not be read as an empty repository"
    assert "does not name an existing directory" in caplog.text
    assert not output_file.exists()


def test_get_csv_search_directory_rejects_a_start_path_naming_a_file(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch, caplog: pytest.LogCaptureFixture
) -> None:
    _config_file, repository_directory, _ = prepare_repository(tmp_path, monkeypatch)
    config_file: str = write_config(tmp_path, repository_directory, "2026-08-12/fw.csv")

    with caplog.at_level(logging.ERROR, logger=LOGGER.name):
        search_directory: Path | None = importer.get_csv_search_directory(config_file, repository_directory, LOGGER)

    assert search_directory is None
    assert "does not name an existing directory" in caplog.text


def test_import_data_recovers_from_a_manifest_which_is_not_an_object(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    config_file, _repository_directory, output_file = prepare_repository(tmp_path, monkeypatch)
    monkeypatch.setattr(importer, "update_git_repo", return_true)
    importer.import_data(config_file, None, LOGGER)
    # a manifest replaced by the bare file list, the shape an older script version wrote
    importer.MANIFEST_FILE.write_text(json.dumps(["2026-08-12/fw.csv"]), encoding="utf-8")

    result: int = importer.import_data(config_file, None, LOGGER)

    manifest: dict[str, Any] = json.loads(importer.MANIFEST_FILE.read_text(encoding="utf-8"))
    assert result == 0, "a manifest which is not an object must not stall every following run"
    assert manifest["csv_files"] == ["2026-08-12/fw.csv"], "the repository was read again"
    assert output_file.exists()


def test_import_data_summarizes_a_run_without_rejections(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch, caplog: pytest.LogCaptureFixture
) -> None:
    config_file, _repository_directory, _ = prepare_repository(tmp_path, monkeypatch)
    monkeypatch.setattr(importer, "update_git_repo", return_true)

    with caplog.at_level(logging.INFO, logger=LOGGER.name):
        assert importer.import_data(config_file, None, LOGGER) == 0

    assert "converted 1 CSV files into 1 log entries" in caplog.text
    assert "not importable line(s)" not in caplog.text, "a clean run must not report a rejection"


def test_import_data_summarizes_the_not_importable_lines(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch, caplog: pytest.LogCaptureFixture
) -> None:
    config_file, repository_directory, _ = prepare_repository(tmp_path, monkeypatch)
    (repository_directory / "2026-08-12" / "fw.csv").write_text(
        CSV_CONTENT + "APP-2,1,192.0.2.2,198.51.100.2,443,1\n" + "APP-3,0,192.0.2.3,198.51.100.3,443,6\n",
        encoding="utf-8",
    )
    monkeypatch.setattr(importer, "update_git_repo", return_true)

    with caplog.at_level(logging.INFO, logger=LOGGER.name):
        assert importer.import_data(config_file, None, LOGGER) == 0

    assert "1 CSV file(s) with 2 not importable line(s) were kept in the log data repository" in caplog.text
    assert "not imported from 2026-08-12/fw.csv: 2 not importable line(s):" in caplog.text
    assert "line 3: Port is only valid with Protocol 6 or 17" in caplog.text
    assert "line 4: App ID, Log count, Src IP and Dst IP must be present" in caplog.text


def test_import_data_counts_rejections_across_files(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch, caplog: pytest.LogCaptureFixture
) -> None:
    config_file, repository_directory, _ = prepare_repository(tmp_path, monkeypatch)
    (repository_directory / "2026-08-12" / "fw.csv").write_text(
        CSV_CONTENT + "APP-2,1,192.0.2.2,198.51.100.2,443,1\n", encoding="utf-8"
    )
    (repository_directory / "broken.csv").write_text("App ID,Log count\nAPP-1,42\n", encoding="utf-8")
    monkeypatch.setattr(importer, "update_git_repo", return_true)

    with caplog.at_level(logging.INFO, logger=LOGGER.name):
        assert importer.import_data(config_file, None, LOGGER) == 0

    assert "converted 0 CSV files into 0 log entries" in caplog.text
    assert "2 CSV file(s) with 1 not importable line(s) were kept" in caplog.text
    # a file rejected before its rows were read has no example lines, only the reason
    assert "not imported from broken.csv: " in caplog.text
    assert "is missing one or more mandatory columns" in caplog.text


def test_import_data_reports_at_most_five_example_lines_per_file(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch, caplog: pytest.LogCaptureFixture
) -> None:
    config_file, repository_directory, _ = prepare_repository(tmp_path, monkeypatch)
    rejected_row_count: int = importer.MAX_REPORTED_EXAMPLE_ROWS + 2
    rejected_rows: str = "".join(
        f"APP-{row},1,192.0.2.{row},198.51.100.{row},443,1\n" for row in range(1, rejected_row_count + 1)
    )
    (repository_directory / "2026-08-12" / "fw.csv").write_text(
        "App ID,Log count,Src IP,Dst IP,Port,Protocol\n" + rejected_rows, encoding="utf-8"
    )
    monkeypatch.setattr(importer, "update_git_repo", return_true)

    with caplog.at_level(logging.INFO, logger=LOGGER.name):
        assert importer.import_data(config_file, None, LOGGER) == 0

    summary: str = next(line for line in caplog.text.splitlines() if "not imported from" in line)
    assert summary.count("line ") == importer.MAX_REPORTED_EXAMPLE_ROWS
    assert f"{rejected_row_count} not importable line(s)" in summary
    assert f"{rejected_row_count - importer.MAX_REPORTED_EXAMPLE_ROWS} more" in summary


def test_describe_rejected_file_reports_a_file_without_example_lines() -> None:
    rejected: importer.RejectedFile = importer.RejectedFile(reason="fw.csv is missing one or more mandatory columns")

    assert importer.describe_rejected_file(rejected) == "fw.csv is missing one or more mandatory columns"
