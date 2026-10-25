from __future__ import annotations

from typing import TYPE_CHECKING, Any
from unittest.mock import MagicMock

import pytest
from fw_modules.azure2022ff import azure_getter
from fw_modules.azure2022ff.azure_models import AzureDevice
from fw_modules.azure2022ff.azure_normalizer import normalize_config
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
POLICY1_ID = "/subscriptions/sub-1/resourceGroups/rg-1/providers/Microsoft.Network/firewallPolicies/pol1"
POLICY2_ID = "/subscriptions/sub-1/resourceGroups/rg-1/providers/Microsoft.Network/firewallPolicies/pol2"
PARENT_POLICY_ID = "/subscriptions/sub-1/resourceGroups/hub-rg/providers/Microsoft.Network/firewallPolicies/parent"
ROOT_POLICY_ID = "/subscriptions/sub-2/resourceGroups/global-rg/providers/Microsoft.Network/firewallPolicies/root"
IP_GROUP: dict[str, Any] = {"id": "/subscriptions/sub-1/ipGroups/ig1", "name": "ig1", "properties": {"ipAddresses": []}}
HUB_IP_GROUP_ID = "/subscriptions/sub-2/resourceGroups/global-rg/providers/Microsoft.Network/ipGroups/hub-ig"
HUB_IP_GROUP: dict[str, Any] = {"id": HUB_IP_GROUP_ID, "name": "hub-ig", "properties": {"ipAddresses": ["10.1.0.0/16"]}}
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


class TestFirewallPolicyId:
    def test_policy_id_is_built_url_encoded(self) -> None:
        assert azure_getter.build_firewall_policy_id("sub-1", "rg 1", "pol/1") == (
            "/subscriptions/sub-1/resourceGroups/rg%201/providers/Microsoft.Network/firewallPolicies/pol%2F1"
        )

    @pytest.mark.parametrize(
        "policy_id",
        [
            POLICY1_ID,
            POLICY1_ID.lower(),
            "/subscriptions/s/resourceGroups/rg.1/providers/Microsoft.Network/firewallPolicies/pol.1",
        ],
    )
    def test_valid_policy_id(self, policy_id: str) -> None:
        assert azure_getter.check_firewall_policy_id(policy_id) == policy_id

    @pytest.mark.parametrize(
        "policy_id",
        [
            "/subscriptions/s/resourceGroups/../providers/Microsoft.Network/firewallPolicies/p",
            "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Network/firewallPolicies/..",
            f"{POLICY1_ID}/ruleCollectionGroups",
            f"{POLICY1_ID}?api-version=1",
            f"{POLICY1_ID}#x",
            "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Network/azureFirewalls/fw",
            "https://evil.example/subscriptions/s/resourceGroups/rg/providers/Microsoft.Network/firewallPolicies/p",
            "",
        ],
    )
    def test_invalid_policy_id_is_refused(self, policy_id: str) -> None:
        with pytest.raises(FwApiCallFailedError, match="Invalid Azure firewall policy id"):
            azure_getter.check_firewall_policy_id(policy_id)


class TestIpGroupId:
    @pytest.mark.parametrize("ip_group_id", [HUB_IP_GROUP_ID, HUB_IP_GROUP_ID.lower()])
    def test_valid_ip_group_id(self, ip_group_id: str) -> None:
        assert azure_getter.check_ip_group_id(ip_group_id) == ip_group_id

    @pytest.mark.parametrize(
        "ip_group_id",
        [
            "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Network/ipGroups/..",
            f"{HUB_IP_GROUP_ID}/x",
            f"{HUB_IP_GROUP_ID}?api-version=1",
            POLICY1_ID,
            "https://evil.example/subscriptions/s/resourceGroups/rg/providers/Microsoft.Network/ipGroups/g",
            "",
        ],
    )
    def test_invalid_ip_group_id_is_refused(self, ip_group_id: str) -> None:
        with pytest.raises(FwApiCallFailedError, match="Invalid Azure IP group id"):
            azure_getter.check_ip_group_id(ip_group_id)


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
        client.get_rule_collection_groups(POLICY1_ID)

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


class FakeAzureApi:
    """Serves firewall policies, their rule collection groups and IP groups like Azure Resource Manager."""

    def __init__(self, resources: list[dict[str, Any]]) -> None:
        # firewall policies and IP groups by resource id
        self.resources = {resource["id"].lower(): resource for resource in resources}
        # rule collections of a policy by policy id, other policies get a group without rule collections
        self.rule_collections: dict[str, list[dict[str, Any]]] = {}
        self.requested_paths: list[str] = []

    def get_page(self, url: str, access_token: str) -> dict[str, Any]:
        assert access_token == "tok"  # noqa: S105
        path = self.record(url)
        return self.resources[path.lower()]

    def list_items(self, url: str, access_token: str) -> list[dict[str, Any]]:
        assert access_token == "tok"  # noqa: S105
        path = self.record(url)
        if path.endswith("/ipGroups"):
            return [IP_GROUP]
        policy_id = path.removesuffix("/ruleCollectionGroups")
        policy_name = policy_id.rsplit("/", 1)[-1]
        rule_collections = self.rule_collections.get(policy_id.lower(), [])
        return [
            {
                **RULE_COLLECTION_GROUP,
                "id": f"{path}/{policy_name}-rcg",
                "name": f"{policy_name}-rcg",
                "properties": {"priority": 100, "ruleCollections": rule_collections},
            }
        ]

    def record(self, url: str) -> str:
        assert url.startswith("https://management.azure.com/subscriptions/")
        path = url.removeprefix("https://management.azure.com").split("?", 1)[0]
        self.requested_paths.append(path)
        return path


def make_policy(policy_id: str, base_policy_id: str | None = None) -> dict[str, Any]:
    properties: dict[str, Any] = {"provisioningState": "Succeeded"}
    if base_policy_id is not None:
        properties["basePolicy"] = {"id": base_policy_id}
    return {"id": policy_id, "name": policy_id.rsplit("/", 1)[-1], "properties": properties}


def make_network_rule(
    name: str, source_ip_groups: list[str], destination_ip_groups: list[str] | None = None
) -> dict[str, Any]:
    return {
        "name": name,
        "ruleType": "NetworkRule",
        "ipProtocols": ["TCP"],
        "sourceIpGroups": source_ip_groups,
        "destinationIpGroups": destination_ip_groups or [],
        "destinationAddresses": [] if destination_ip_groups else ["*"],
        "destinationPorts": ["443"],
    }


def make_filter_rule_collection(rules: list[dict[str, Any]]) -> dict[str, Any]:
    return {
        "name": "coll",
        "ruleCollectionType": "FirewallPolicyFilterRuleCollection",
        "priority": 100,
        "action": {"type": "Allow"},
        "rules": rules,
    }


def mock_azure_api(mocker: MockerFixture, resources: list[dict[str, Any]]) -> FakeAzureApi:
    fake_api = FakeAzureApi(resources)
    mocker.patch.object(azure_getter, "get_access_token", return_value="tok")
    mocker.patch.object(azure_getter, "_azure_api_get_page", side_effect=fake_api.get_page)
    mocker.patch.object(azure_getter, "azure_api_get", side_effect=fake_api.list_items)
    return fake_api


class TestGetNativeConfig:
    def test_fetches_ip_groups_and_each_policy_once(
        self, mocker: MockerFixture, azure_mgm: ManagementController
    ) -> None:
        fake_api = mock_azure_api(mocker, [make_policy(POLICY1_ID)])
        devices = [
            AzureDevice(uid="a", name="a", policy_name="pol1"),
            AzureDevice(uid="b", name="b", policy_name="pol1"),
        ]

        native_config = azure_getter.get_native_config(azure_mgm, devices)

        assert [ip_group.name for ip_group in native_config.ip_groups] == ["ig1"]
        assert [policy.name for policy in native_config.firewall_policies] == ["pol1"]
        assert native_config.firewall_policies[0].id == POLICY1_ID
        assert native_config.firewall_policies[0].base_policy is None
        assert native_config.firewall_policies[0].rule_collection_groups[0].name == "pol1-rcg"
        assert fake_api.requested_paths == [
            "/subscriptions/sub-1/providers/Microsoft.Network/ipGroups",
            POLICY1_ID,
            f"{POLICY1_ID}/ruleCollectionGroups",
        ]
        assert native_config.api_version == NEWEST_API_VERSION

    def test_fetches_parent_policies_of_other_resource_groups_once(
        self, mocker: MockerFixture, azure_mgm: ManagementController
    ) -> None:
        fake_api = mock_azure_api(
            mocker,
            [
                make_policy(POLICY1_ID, base_policy_id=PARENT_POLICY_ID),
                make_policy(POLICY2_ID, base_policy_id=PARENT_POLICY_ID.upper()),
                make_policy(PARENT_POLICY_ID, base_policy_id=ROOT_POLICY_ID),
                make_policy(ROOT_POLICY_ID),
            ],
        )
        devices = [
            AzureDevice(uid="a", name="a", policy_name="pol1"),
            AzureDevice(uid="b", name="b", policy_name="pol2"),
        ]

        native_config = azure_getter.get_native_config(azure_mgm, devices)

        assert [policy.name for policy in native_config.firewall_policies] == ["pol1", "parent", "root", "pol2"]
        parent_policy = native_config.firewall_policies[1]
        assert parent_policy.base_policy is not None
        assert parent_policy.base_policy.id == ROOT_POLICY_ID
        policy_requests = [
            path for path in fake_api.requested_paths if path.endswith(("pol1", "pol2", "parent", "root"))
        ]
        assert policy_requests == [POLICY1_ID, PARENT_POLICY_ID, ROOT_POLICY_ID, POLICY2_ID]
        dumped = native_config.model_dump(by_alias=True)
        assert dumped["firewallPolicies"][0]["basePolicy"] == {"id": PARENT_POLICY_ID}

    def test_parent_policy_id_of_other_resource_type_is_refused(
        self, mocker: MockerFixture, azure_mgm: ManagementController
    ) -> None:
        foreign_id = "/subscriptions/sub-1/resourceGroups/rg-1/providers/Microsoft.KeyVault/vaults/secrets"
        fake_api = mock_azure_api(mocker, [make_policy(POLICY1_ID, base_policy_id=foreign_id)])

        with pytest.raises(FwApiCallFailedError, match="Invalid Azure firewall policy id"):
            azure_getter.get_native_config(azure_mgm, [AzureDevice(uid="a", name="a", policy_name="pol1")])
        assert all("KeyVault" not in path for path in fake_api.requested_paths)

    def test_parent_policy_chain_is_limited(self, mocker: MockerFixture, azure_mgm: ManagementController) -> None:
        mocker.patch.object(azure_getter, "AZURE_MAX_POLICY_DEPTH", 2)
        mock_azure_api(
            mocker,
            [
                make_policy(POLICY1_ID, base_policy_id=PARENT_POLICY_ID),
                make_policy(PARENT_POLICY_ID, base_policy_id=ROOT_POLICY_ID),
                make_policy(ROOT_POLICY_ID),
            ],
        )

        with pytest.raises(FwApiCallFailedError, match="more than 2 levels"):
            azure_getter.get_native_config(azure_mgm, [AzureDevice(uid="a", name="a", policy_name="pol1")])

    def test_cyclic_parent_policies_are_fetched_once(
        self, mocker: MockerFixture, azure_mgm: ManagementController
    ) -> None:
        mock_azure_api(
            mocker,
            [
                make_policy(POLICY1_ID, base_policy_id=PARENT_POLICY_ID),
                make_policy(PARENT_POLICY_ID, base_policy_id=POLICY1_ID),
            ],
        )

        native_config = azure_getter.get_native_config(azure_mgm, [AzureDevice(uid="a", name="a", policy_name="pol1")])

        assert [policy.name for policy in native_config.firewall_policies] == ["pol1", "parent"]

    def test_ip_groups_of_parent_policy_in_other_subscription_are_fetched_by_id(
        self, mocker: MockerFixture, azure_mgm: ManagementController
    ) -> None:
        fake_api = mock_azure_api(
            mocker,
            [make_policy(POLICY1_ID, base_policy_id=ROOT_POLICY_ID), make_policy(ROOT_POLICY_ID), HUB_IP_GROUP],
        )
        fake_api.rule_collections[ROOT_POLICY_ID.lower()] = [
            make_filter_rule_collection(
                [
                    make_network_rule("r1", source_ip_groups=[HUB_IP_GROUP_ID]),
                    # the same group with other casing and a group of the management's subscription
                    make_network_rule(
                        "r2", source_ip_groups=[IP_GROUP["id"]], destination_ip_groups=[HUB_IP_GROUP_ID.upper()]
                    ),
                ]
            )
        ]
        devices = [AzureDevice(uid="fw-uid", name="fw", policy_name="pol1")]

        native_config = azure_getter.get_native_config(azure_mgm, devices)

        assert [ip_group.name for ip_group in native_config.ip_groups] == ["ig1", "hub-ig"]
        assert fake_api.requested_paths.count(HUB_IP_GROUP_ID) == 1
        assert fake_api.requested_paths[-1] == HUB_IP_GROUP_ID
        normalized = normalize_config(native_config, devices, "mgm-uid", skip_unsupported_rules=True)
        root_rules = next(rulebase for rulebase in normalized.rulebases if rulebase.uid == "root").rules
        assert [rule.rule_src for rule in root_rules.values()] == ["hub-ig", "ig1"]

    def test_ip_groups_of_rules_that_are_not_imported_are_not_fetched(
        self, mocker: MockerFixture, azure_mgm: ManagementController
    ) -> None:
        fake_api = mock_azure_api(mocker, [make_policy(POLICY1_ID)])
        fake_api.rule_collections[POLICY1_ID.lower()] = [
            make_filter_rule_collection(
                [{"name": "app", "ruleType": "ApplicationRule", "sourceIpGroups": [HUB_IP_GROUP_ID]}]
            ),
            {
                "name": "nat",
                "ruleCollectionType": "FirewallPolicyNatRuleCollection",
                "rules": [make_network_rule("dnat", source_ip_groups=[HUB_IP_GROUP_ID])],
            },
        ]

        azure_getter.get_native_config(azure_mgm, [AzureDevice(uid="a", name="a", policy_name="pol1")])

        assert HUB_IP_GROUP_ID not in fake_api.requested_paths

    def test_ip_group_id_of_other_resource_type_is_refused(
        self, mocker: MockerFixture, azure_mgm: ManagementController
    ) -> None:
        foreign_id = "/subscriptions/sub-2/resourceGroups/hub-rg/providers/Microsoft.KeyVault/vaults/secrets"
        fake_api = mock_azure_api(mocker, [make_policy(POLICY1_ID)])
        fake_api.rule_collections[POLICY1_ID.lower()] = [
            make_filter_rule_collection([make_network_rule("r1", source_ip_groups=[foreign_id])])
        ]

        with pytest.raises(FwApiCallFailedError, match="Invalid Azure IP group id"):
            azure_getter.get_native_config(azure_mgm, [AzureDevice(uid="a", name="a", policy_name="pol1")])
        assert all("KeyVault" not in path for path in fake_api.requested_paths)

    def test_missing_cloud_settings_fail(self, azure_mgm: ManagementController) -> None:
        azure_mgm.cloud_subscription_id = None
        with pytest.raises(FwoImporterError, match="subscription id"):
            azure_getter.get_native_config(azure_mgm, [])
