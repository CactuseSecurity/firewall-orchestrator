from unittest import mock

from fwo_api import REDACTED_VALUE, FwoApi


def test_show_import_api_call_info_redacts_variables_and_authorization_header() -> None:
    FwoApi.login = mock.MagicMock()  # Mock login to avoid side effects
    api = FwoApi("https://fworch.example/api", "importer", "jwt", "uri", "api-uri")

    payload = {
        "query": "mutation Test($password: String!) { test(password: $password) }",
        "variables": {"password": "secret-password"},
    }
    headers = {
        "Content-Type": "application/json",
        "Authorization": "Bearer jwt-secret",
        "x-hasura-role": "importer",
    }

    message = api.show_import_api_call_info("https://fworch.example/api", payload, headers)
    assert REDACTED_VALUE in message
    assert "secret-password" not in message
    assert "Bearer jwt-secret" not in message
    assert '"Authorization": "<redacted>"' in message
    assert '"x-hasura-role": "importer"' in message


def test_show_api_call_info_redacts_variables_and_hasura_admin_secret_header() -> None:
    FwoApi.login = mock.MagicMock()  # Mock login to avoid side effects
    api = FwoApi("https://fworch.example/api", "importer", "jwt", "uri", "api-uri")
    payload = {
        "query": "query Test { config { config_value } }",
        "variables": {"adminSecret": "hasura-secret"},
    }
    headers = {
        "x-hasura-admin-secret": "hasura-secret",
    }

    message = api.show_api_call_info("https://fworch.example/api", payload, headers)
    assert REDACTED_VALUE in message
    assert "hasura-secret" not in message
    assert '"x-hasura-admin-secret": "<redacted>"' in message


def test_summarize_query_variables_does_not_include_values() -> None:
    summary = FwoApi.summarize_query_variables({"password": "secret-password", "ruleChanges": [{"uid": "1"}]})

    assert "password" in summary
    assert "ruleChanges" in summary
    assert "secret-password" not in summary
    assert "uid" not in summary
    assert REDACTED_VALUE in summary
