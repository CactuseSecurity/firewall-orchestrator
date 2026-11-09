from pathlib import Path

from test.conftest import find_repo_graphql_query_path


def test_find_repo_graphql_query_path_returns_repo_query_dir(tmp_path: Path) -> None:
    query_path = tmp_path / "roles" / "common" / "files" / "fwo-api-calls"
    test_file = tmp_path / "roles" / "importer" / "files" / "importer" / "test" / "conftest.py"
    query_path.mkdir(parents=True)
    test_file.parent.mkdir(parents=True)
    test_file.touch()

    assert find_repo_graphql_query_path(test_file) == query_path


def test_find_repo_graphql_query_path_returns_none_without_repo_layout(tmp_path: Path) -> None:
    test_file = tmp_path / "usr" / "local" / "fworch" / "importer" / "test" / "conftest.py"
    test_file.parent.mkdir(parents=True)
    test_file.touch()

    assert find_repo_graphql_query_path(test_file) is None
