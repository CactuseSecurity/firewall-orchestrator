from __future__ import annotations

from typing import TYPE_CHECKING, Any
from unittest.mock import MagicMock

import pytest
from fw_modules.azure2022ff import azure_getter
from fw_modules.azure2022ff.azure_models import AzureDevice
from fwo_exceptions import FwApiCallFailedError, FwLoginFailedError, FwoImporterError
from models.credential import AdditionalCredential

if TYPE_CHECKING:
    from model_controllers.management_controller import ManagementController
    from pytest_mock import MockerFixture

TENANT_ID = "tenant-1"
SUBSCRIPTION_ID = "sub-1"
RESOURCE_GROUP = "rg-1"
LIST_URL = "https://management.azure.com/subscriptions/sub-1/providers/Microsoft.Network/ipGroups?api-version=x"
RULE_COLLECTION_GROUP: dict[str, Any] = {
    "id": "/subscriptions/sub-1/resourceGroups/rg-1/providers/Microsoft.Network/firewallPolicies/pol1/ruleCollectionGroups/g1",
    "name": "g1",
    "properties": {"ruleCollections": []},
}
IP_GROUP: dict[str, Any] = {"id": "/subscriptions/sub-1/ipGroups/ig1", "name": "ig1", "properties": {"ipAddresses": []}}
NEWEST_API_VERSION = "2026-01-01"
FALLBACK_API_VERSION = "2024-05-01"
UNSUPPORTED_API_VERSION: dict[str, Any] = {"error": {"code": "NoRegisteredProviderFound", "message": "no provider"}}


def make_response(status_code: int, payload: dict[str, Any]) -> MagicMock:
    response = MagicMock()
    response.status_code = status_code
    response.json.return_value = payload
    response.text = str(payload)
    return response


@pytest.fixture
def azure_mgm(management_controller: ManagementController) -> ManagementController:
    management_controller.cloud_tenant_id = TENANT_ID
    management_controller.cloud_subscription_id = SUBSCRIPTION_ID
    management_controller.domain_name = RESOURCE_GROUP
    management_controller.devices = [
        {"name": "fw1", "uid": "fw1-uid", "importDisabled": False, "local_rulebase_name": "pol1"},
        {"name": "pol2", "uid": "pol2-uid", "importDisabled": False, "local_rulebase_name": None},
        {"name": "fw3", "uid": "fw3-uid", "importDisabled": True, "local_rulebase_name": None},
    ]
    return management_controller


class TestPolicyDevices:
    def test_policy_name_defaults_to_device_name_and_disabled_devices_are_skipped(
        self, azure_mgm: ManagementController
    ) -> None:
        devices = azure_getter.get_policy_devices(azure_mgm)

        assert devices == [
            AzureDevice(uid="fw1-uid", name="fw1", policy_name="pol1"),
            AzureDevice(uid="pol2-uid", name="pol2", policy_name="pol2"),
        ]

    def test_force_import_includes_disabled_devices(self, azure_mgm: ManagementController) -> None:
        assert len(azure_getter.get_policy_devices(azure_mgm, force_import=True)) == 3

    @pytest.mark.parametrize("uid", [None, ""])
    def test_device_without_uid_fails(self, azure_mgm: ManagementController, uid: str | None) -> None:
        # the gateway uid must match the FWO device uid, a fallback to the name would fail later with a vague error
        azure_mgm.devices[1]["uid"] = uid
        with pytest.raises(FwoImporterError, match="'pol2' has no uid"):
            azure_getter.get_policy_devices(azure_mgm)

    def test_disabled_device_without_uid_is_ignored(self, azure_mgm: ManagementController) -> None:
        azure_mgm.devices[2]["uid"] = None
        assert len(azure_getter.get_policy_devices(azure_mgm)) == 2

    def test_no_devices_fails(self, azure_mgm: ManagementController) -> None:
        azure_mgm.devices = []
        with pytest.raises(FwoImporterError, match="no device"):
            azure_getter.get_policy_devices(azure_mgm)


class TestClientCredentials:
    def test_import_credential_is_used_without_additional_credential(self, azure_mgm: ManagementController) -> None:
        assert azure_getter.get_client_credentials(azure_mgm) == ("mock-user", "mock-secret")

    def test_additional_credential_is_preferred(self, azure_mgm: ManagementController) -> None:
        azure_mgm.additional_credential = AdditionalCredential(id=1, user="client-id", secret="client-secret")  # noqa: S106
        assert azure_getter.get_client_credentials(azure_mgm) == ("client-id", "client-secret")


class TestAccessToken:
    def test_token_request(self, mocker: MockerFixture) -> None:
        post = mocker.patch("requests.post", return_value=make_response(200, {"access_token": "tok"}))

        assert azure_getter.get_access_token(TENANT_ID, "cid", "csecret") == "tok"
        url = post.call_args.args[0]
        assert url == "https://login.microsoftonline.com/tenant-1/oauth2/v2.0/token"
        assert post.call_args.kwargs["data"]["scope"] == "https://management.azure.com/.default"
        assert post.call_args.kwargs["data"]["grant_type"] == "client_credentials"

    def test_failed_login_does_not_leak_secret(self, mocker: MockerFixture) -> None:
        mocker.patch("requests.post", return_value=make_response(401, {"error": "invalid_client"}))

        with pytest.raises(FwLoginFailedError) as exc_info:
            azure_getter.get_access_token(TENANT_ID, "cid", "csecret")
        assert "csecret" not in str(exc_info.value)

    def test_tenant_id_is_url_encoded(self, mocker: MockerFixture) -> None:
        post = mocker.patch("requests.post", return_value=make_response(200, {"access_token": "tok"}))

        azure_getter.get_access_token("../evil", "cid", "csecret")
        assert post.call_args.args[0] == "https://login.microsoftonline.com/..%2Fevil/oauth2/v2.0/token"


class TestApiGet:
    def test_follows_next_link(self, mocker: MockerFixture) -> None:
        pages = [
            make_response(200, {"value": [1], "nextLink": LIST_URL + "&page=2"}),
            make_response(200, {"value": [2]}),
        ]
        get = mocker.patch("requests.get", side_effect=pages)

        assert azure_getter.azure_api_get(LIST_URL, "tok") == [1, 2]
        assert get.call_args.kwargs["headers"]["Authorization"] == "Bearer tok"

    def test_refuses_next_link_to_foreign_host(self, mocker: MockerFixture) -> None:
        mocker.patch(
            "requests.get", return_value=make_response(200, {"value": [], "nextLink": "https://evil.example/x"})
        )

        with pytest.raises(FwApiCallFailedError, match="unexpected url"):
            azure_getter.azure_api_get(LIST_URL, "tok")

    def test_page_limit(self, mocker: MockerFixture) -> None:
        mocker.patch("requests.get", return_value=make_response(200, {"value": [], "nextLink": LIST_URL}))

        with pytest.raises(FwApiCallFailedError, match="maximum"):
            azure_getter.azure_api_get(LIST_URL, "tok")

    def test_error_response(self, mocker: MockerFixture) -> None:
        mocker.patch("requests.get", return_value=make_response(403, {"error": "forbidden"}))

        with pytest.raises(FwApiCallFailedError, match="403") as exc_info:
            azure_getter.azure_api_get(LIST_URL, "tok")
        assert not isinstance(exc_info.value, azure_getter.AzureApiVersionNotSupportedError)

    def test_unsupported_api_version_response(self, mocker: MockerFixture) -> None:
        mocker.patch("requests.get", return_value=make_response(400, UNSUPPORTED_API_VERSION))

        with pytest.raises(azure_getter.AzureApiVersionNotSupportedError):
            azure_getter.azure_api_get(LIST_URL, "tok")

    def test_other_bad_request_is_no_version_error(self, mocker: MockerFixture) -> None:
        response = make_response(400, {})
        response.json.side_effect = ValueError("no json")
        mocker.patch("requests.get", return_value=response)

        with pytest.raises(FwApiCallFailedError, match="400") as exc_info:
            azure_getter.azure_api_get(LIST_URL, "tok")
        assert not isinstance(exc_info.value, azure_getter.AzureApiVersionNotSupportedError)


class TestApiClient:
    def test_uses_newest_api_version(self, mocker: MockerFixture) -> None:
        api_get = mocker.patch.object(azure_getter, "azure_api_get", return_value=[IP_GROUP])
        client = azure_getter.AzureApiClient(SUBSCRIPTION_ID, "tok")

        assert client.list_items("providers/Microsoft.Network/ipGroups") == [IP_GROUP]
        assert client.api_version == NEWEST_API_VERSION
        assert api_get.call_args.args[0].endswith(f"ipGroups?api-version={NEWEST_API_VERSION}")

    def test_falls_back_to_older_api_version_and_keeps_it(self, mocker: MockerFixture) -> None:
        api_get = mocker.patch.object(
            azure_getter,
            "azure_api_get",
            side_effect=[azure_getter.AzureApiVersionNotSupportedError("unsupported"), [IP_GROUP], []],
        )
        client = azure_getter.AzureApiClient(SUBSCRIPTION_ID, "tok")

        client.list_items("providers/Microsoft.Network/ipGroups")
        client.get_rule_collection_groups(RESOURCE_GROUP, "pol1")

        urls = [call.args[0] for call in api_get.call_args_list]
        assert urls[0].endswith(f"api-version={NEWEST_API_VERSION}")
        assert urls[1].endswith(f"api-version={FALLBACK_API_VERSION}")
        assert urls[2].endswith(f"ruleCollectionGroups?api-version={FALLBACK_API_VERSION}")
        assert client.api_version == FALLBACK_API_VERSION

    def test_no_supported_api_version_fails(self, mocker: MockerFixture) -> None:
        mocker.patch.object(
            azure_getter, "azure_api_get", side_effect=azure_getter.AzureApiVersionNotSupportedError("unsupported")
        )
        client = azure_getter.AzureApiClient(SUBSCRIPTION_ID, "tok")

        with pytest.raises(FwApiCallFailedError, match="not supported"):
            client.list_items("providers/Microsoft.Network/ipGroups")


class TestGetNativeConfig:
    def test_fetches_ip_groups_and_each_policy_once(
        self, mocker: MockerFixture, azure_mgm: ManagementController
    ) -> None:
        mocker.patch.object(azure_getter, "get_access_token", return_value="tok")

        def fake_get(url: str, access_token: str) -> list[dict[str, Any]]:
            assert access_token == "tok"  # noqa: S105
            if url.endswith(f"ipGroups?api-version={NEWEST_API_VERSION}"):
                return [IP_GROUP]
            return [RULE_COLLECTION_GROUP]

        api_get = mocker.patch.object(azure_getter, "azure_api_get", side_effect=fake_get)
        devices = [
            AzureDevice(uid="a", name="a", policy_name="pol1"),
            AzureDevice(uid="b", name="b", policy_name="pol1"),
        ]

        native_config = azure_getter.get_native_config(azure_mgm, devices)

        assert [ip_group.name for ip_group in native_config.ip_groups] == ["ig1"]
        assert [policy.name for policy in native_config.firewall_policies] == ["pol1"]
        assert native_config.firewall_policies[0].rule_collection_groups[0].name == "g1"
        urls = [call.args[0] for call in api_get.call_args_list]
        assert urls[1] == (
            "https://management.azure.com/subscriptions/sub-1/resourceGroups/rg-1/providers/Microsoft.Network/"
            f"firewallPolicies/pol1/ruleCollectionGroups?api-version={NEWEST_API_VERSION}"
        )
        assert native_config.api_version == NEWEST_API_VERSION

    def test_missing_cloud_settings_fail(self, azure_mgm: ManagementController) -> None:
        azure_mgm.cloud_subscription_id = None
        with pytest.raises(FwoImporterError, match="subscription id"):
            azure_getter.get_native_config(azure_mgm, [])
