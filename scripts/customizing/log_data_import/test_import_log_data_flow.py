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


def write_config(tmp_path: Path, repository_directory: Path) -> str:
    config_file: Path = tmp_path / "customizingConfigLogData.json"
    config_file.write_text(
        json.dumps(
            {
                "logDataGitRepo": "local.logdata/log_repo",
                "logDataGitUser": "local",
                "logDataGitPassword": "local",
                "logDataGitRepoTargetDir": str(repository_directory),
                "logDataGitBranch": "main",
            }
        ),
        encoding="utf-8",
    )
    return str(config_file)


def prepare_repository(tmp_path: Path, monkeypatch: pytest.MonkeyPatch) -> tuple[str, Path, Path]:
    repository_directory: Path = tmp_path / "repo"
    (repository_directory / "2026-08-12").mkdir(parents=True)
    (repository_directory / "2026-08-12" / "fw.csv").write_text(CSV_CONTENT, encoding="utf-8")
    output_file: Path = tmp_path / "import_log_data_from_git.json"
    monkeypatch.setattr(importer, "OUTPUT_FILE", output_file)
    return write_config(tmp_path, repository_directory), repository_directory, output_file


def test_import_data_writes_entries_and_manifest(tmp_path: Path, monkeypatch: pytest.MonkeyPatch) -> None:
    config_file, repository_directory, output_file = prepare_repository(tmp_path, monkeypatch)
    monkeypatch.setattr(importer, "update_git_repo", return_true)

    result: int = importer.import_data(config_file, None, LOGGER)

    manifest: dict[str, Any] = json.loads(
        (repository_directory / importer.MANIFEST_FILE_NAME).read_text(encoding="utf-8")
    )
    entries: dict[str, Any] = json.loads(output_file.read_text(encoding="utf-8"))
    assert result == 0
    assert entries["logs"][0]["app_id"] == "APP-1"
    assert entries["logs"][0]["log_count"] == 42
    assert manifest["csv_files"] == ["2026-08-12/fw.csv"]


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
    assert not (repository_directory / importer.MANIFEST_FILE_NAME).exists()
    assert not output_file.exists()


def test_acknowledge_import_keeps_the_manifest_when_the_push_fails(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    config_file, repository_directory, _ = prepare_repository(tmp_path, monkeypatch)
    monkeypatch.setattr(importer, "update_git_repo", return_true)
    importer.import_data(config_file, None, LOGGER)
    monkeypatch.setattr(importer, "commit_and_push_deletions", return_false)

    result: int = importer.acknowledge_import(config_file, LOGGER)

    assert result == 1
    assert (repository_directory / importer.MANIFEST_FILE_NAME).exists()


def test_acknowledge_import_without_manifest_does_nothing(tmp_path: Path, monkeypatch: pytest.MonkeyPatch) -> None:
    config_file, _, _ = prepare_repository(tmp_path, monkeypatch)

    assert importer.acknowledge_import(config_file, LOGGER) == 0


def test_acknowledge_import_rejects_a_broken_manifest(tmp_path: Path, monkeypatch: pytest.MonkeyPatch) -> None:
    config_file, repository_directory, _ = prepare_repository(tmp_path, monkeypatch)
    (repository_directory / importer.MANIFEST_FILE_NAME).write_text(
        json.dumps({"csv_files": "fw.csv"}), encoding="utf-8"
    )

    with pytest.raises(TypeError):
        importer.acknowledge_import(config_file, LOGGER)


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

    manifest: dict[str, Any] = json.loads(
        (repository_directory / importer.MANIFEST_FILE_NAME).read_text(encoding="utf-8")
    )
    entries: dict[str, Any] = json.loads(output_file.read_text(encoding="utf-8"))
    assert result == 0
    assert len(entries["logs"]) == 1
    assert manifest["csv_files"] == ["2026-08-12/fw.csv"]


def test_import_data_writes_an_empty_file_when_every_file_is_broken(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    config_file, repository_directory, output_file = prepare_repository(tmp_path, monkeypatch)
    (repository_directory / "2026-08-12" / "fw.csv").write_text("App ID\nAPP-1\n", encoding="utf-8")
    monkeypatch.setattr(importer, "update_git_repo", return_true)

    result: int = importer.import_data(config_file, None, LOGGER)

    entries: dict[str, Any] = json.loads(output_file.read_text(encoding="utf-8"))
    assert result == 0
    assert entries["logs"] == []


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

    assert importer.convert_row(row)["log_time"] == "2026-08-12T08:15:00"


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


def test_import_data_skips_rows_with_a_broken_timestamp(tmp_path: Path, monkeypatch: pytest.MonkeyPatch) -> None:
    config_file, repository_directory, output_file = prepare_repository(tmp_path, monkeypatch)
    (repository_directory / "2026-08-12" / "fw.csv").write_text(
        "App ID,Log count,Src IP,Dst IP,Port,Protocol,Log timestamp\n"
        "APP-1,42,192.0.2.1,198.51.100.1,443,6,12.08.2026 08:15\n"
        "APP-1,7,192.0.2.2,198.51.100.2,443,6,2026-08-12T08:20:00Z\n",
        encoding="utf-8",
    )
    monkeypatch.setattr(importer, "update_git_repo", return_true)

    result: int = importer.import_data(config_file, None, LOGGER)

    entries: dict[str, Any] = json.loads(output_file.read_text(encoding="utf-8"))
    assert result == 0
    assert len(entries["logs"]) == 1
    assert entries["logs"][0]["log_time"] == "2026-08-12T08:20:00+00:00"
