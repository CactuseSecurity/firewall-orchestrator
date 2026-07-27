import json
from typing import Any

from scripts.customizing.fwo_custom_lib.basic_helpers import get_logger
from scripts.customizing.iiq_request_missing_roles.iiq_client import IIQClient
from scripts.customizing.iiq_request_missing_roles.iiq_request_missing_fwmgt_roles import init_statistics

EXPECTED_ROLE_CHECK_CALL_COUNT = 2


class FakeResponse:
    def __init__(self, ok: bool = True, status_code: int = 200, text: str = "") -> None:
        self.ok = ok
        self.status_code = status_code
        self.text = text


class RecordingIIQClient(IIQClient):
    def __init__(self) -> None:
        super().__init__(
            "iiq.example.org",
            "requester",
            "secret",
            "AD EXAMPLE",
            "USR",
            logger=get_logger(0),
        )
        self.responses: list[FakeResponse] = []
        self.calls: list[dict[str, Any]] = []

    def send(
        self,
        body: dict[str, Any] | None = None,
        method: str = "POST",
        url_path: str = "",
        url_parameter: str = "",
    ) -> Any:
        self.calls.append(
            {
                "body": body if body is not None else {},
                "method": method,
                "url_path": url_path,
                "url_parameter": url_parameter,
            }
        )
        return self.responses.pop(0)


def test_get_org_id_returns_parent_org_id() -> None:
    client = RecordingIIQClient()
    client.responses.append(
        FakeResponse(
            text=json.dumps(
                {
                    "Resources": [
                        {
                            "urn:ietf:params:scim:schemas:sailpoint:1.0:User": {
                                "parent_org_id": "ORG-1",
                            }
                        }
                    ]
                }
            )
        )
    )

    assert client.get_org_id("tiso-user") == "ORG-1"
    assert client.calls[0]["method"] == "GET"
    assert "filter=userName%20eq%20%22tiso-user%22" in client.calls[0]["url_parameter"]


def test_get_org_id_returns_none_for_missing_response_data() -> None:
    client = RecordingIIQClient()
    client.responses.append(FakeResponse(text=json.dumps({"Resources": [{}]})))

    assert client.get_org_id("tiso-user") is None


def test_request_group_creation_replaces_template_values_and_counts_simulation() -> None:
    client = RecordingIIQClient()
    client.responses.append(
        FakeResponse(text="Validierung der Auftragsdaten erfolgreich. Workflow wurde nicht gestartet.")
    )
    stats = init_statistics()

    client.request_group_creation("APP", "001", "ORG-1", "bo-it", "Payments", stats, run_workflow=False)

    body = client.calls[0]["body"]
    assert body["requesterName"] == "requester"
    assert body["startWorkflow"] is False
    assert body["objectModelList"][0]["afOrgId"] == "ORG-1"
    assert body["objectModelList"][0]["afAnsprechpartnerName"] == "bo-it"
    assert "Payments" in body["objectModelList"][0]["afDesc"]
    assert stats["apps_request_simulated"] == ["APP_001"]
    assert stats["apps_request_simulated_count"] == 1


def test_request_group_creation_counts_error_response() -> None:
    client = RecordingIIQClient()
    client.responses.append(FakeResponse(ok=False, status_code=500, text="failed"))
    stats = init_statistics()

    client.request_group_creation("APP", "001", "ORG-1", "bo-it", "Payments", stats, run_workflow=True)

    assert stats["apps_with_request_errors"] == ["APP_001"]
    assert client.calls[0]["body"]["startWorkflow"] is True


def test_app_functions_exist_in_iiq_updates_existing_role_stats() -> None:
    client = RecordingIIQClient()
    client.responses.append(FakeResponse(text=json.dumps({"totalResults": 1, "Resources": [{"id": "role"}]})))
    stats = init_statistics()

    assert client.app_functions_exist_in_iiq("APP", "001", stats) is True
    assert stats["existing_technical_functions"] == ["A_APP_001_FW_RULEMGT"]
    assert stats["existing_technical_functions_count"] == 1


def test_app_functions_exist_in_iiq_checks_second_pattern_when_first_misses() -> None:
    client = RecordingIIQClient()
    client.responses.extend(
        [
            FakeResponse(text=json.dumps({"totalResults": 0, "Resources": []})),
            FakeResponse(text=json.dumps({"totalResults": 0, "Resources": []})),
        ]
    )
    stats = init_statistics()

    assert client.app_functions_exist_in_iiq("APP", "001", stats) is False
    assert len(client.calls) == EXPECTED_ROLE_CHECK_CALL_COUNT


def test_write_group_creation_stats_classifies_known_response_texts() -> None:
    client = RecordingIIQClient()
    stats = init_statistics()
    client.responses.extend(
        [
            FakeResponse(text="die Alfabet-ID ist ungültig"),
            FakeResponse(text="Es existiert bereits der offene Auftrag"),
            FakeResponse(text="unexpected"),
        ]
    )

    client.request_group_creation("APP", "001", "ORG-1", "bo-it", "Payments", stats)
    client.request_group_creation("APP", "002", "ORG-1", "bo-it", "Payments", stats)
    client.request_group_creation("APP", "003", "ORG-1", "bo-it", "Payments", stats)

    assert stats["apps_with_invalid_alfabet_id"] == ["APP_001"]
    assert stats["apps_with_running_requests"] == ["APP_002"]
    assert stats["apps_with_unexpected_request_errors"] == ["APP_003"]
