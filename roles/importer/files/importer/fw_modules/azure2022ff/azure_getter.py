from __future__ import annotations

from typing import TYPE_CHECKING, Any, cast
from urllib.parse import quote

import requests
from fw_modules.azure2022ff.azure_const import (
    AZURE_API_VERSIONS,
    AZURE_LOGIN_URL,
    AZURE_MANAGEMENT_SCOPE,
    AZURE_MANAGEMENT_URL,
    AZURE_MAX_PAGES,
    AZURE_UNSUPPORTED_API_VERSION_ERROR_CODES,
)
from fw_modules.azure2022ff.azure_models import (
    AzureConfig,
    AzureDevice,
    AzureFirewallPolicy,
    AzureIpGroup,
    AzureRuleCollectionGroup,
)
from fwo_const import FWO_HTTP_TIMEOUT
from fwo_exceptions import FwApiCallFailedError, FwLoginFailedError, FwoImporterError
from fwo_log import FWOLogger

if TYPE_CHECKING:
    from model_controllers.management_controller import ManagementController

HTTP_OK = 200
HTTP_BAD_REQUEST = 400


class AzureApiVersionNotSupportedError(FwApiCallFailedError):
    """Azure Resource Manager rejected the api-version of a call."""


def get_policy_devices(mgm_details: ManagementController, force_import: bool = False) -> list[AzureDevice]:
    """
    Collect the devices to import together with the firewall policy enforced on each of them.

    The policy name is taken from the device's local rulebase name and defaults to the device name.

    Args:
        mgm_details: The management details object.
        force_import: Also include devices whose import is disabled.

    Returns:
        list[AzureDevice]: The devices to import.

    """
    devices: list[AzureDevice] = []
    for device in mgm_details.devices:
        if device.get("importDisabled") and not force_import:
            continue
        name = device["name"]
        devices.append(
            AzureDevice(
                uid=device.get("uid") or name,
                name=name,
                policy_name=device.get("local_rulebase_name") or name,
            )
        )
    if not devices:
        raise FwoImporterError(
            f"Azure management '{mgm_details.name}' has no device to import. "
            "Add a device named like the Azure firewall policy or set its local rulebase name."
        )
    return devices


def get_client_credentials(mgm_details: ManagementController) -> tuple[str, str]:
    """
    Get the service principal client id and secret.

    The additional credential holds the client id and secret if present (migrated cloud credentials),
    otherwise the import credential itself is used.

    Args:
        mgm_details: The management details object.

    Returns:
        tuple[str, str]: Client id and client secret.

    """
    if mgm_details.additional_credential is not None:
        return mgm_details.additional_credential.user, mgm_details.additional_credential.secret
    return mgm_details.import_user, mgm_details.secret


def get_access_token(tenant_id: str, client_id: str, client_secret: str) -> str:
    """
    Get an Azure Resource Manager access token with the OAuth 2.0 client credentials flow.

    Args:
        tenant_id: The Entra ID tenant id.
        client_id: The service principal application (client) id.
        client_secret: The service principal secret.

    Returns:
        str: The bearer access token.

    """
    token_url = f"{AZURE_LOGIN_URL}/{quote(tenant_id, safe='')}/oauth2/v2.0/token"
    payload = {
        "grant_type": "client_credentials",
        "client_id": client_id,
        "client_secret": client_secret,
        "scope": AZURE_MANAGEMENT_SCOPE,
    }
    try:
        response = requests.post(token_url, data=payload, timeout=FWO_HTTP_TIMEOUT)
    except requests.RequestException as e:
        raise FwLoginFailedError(f"Azure login failed for client id {client_id}: {type(e).__name__}") from None

    access_token = None
    if response.status_code == HTTP_OK:
        access_token = response.json().get("access_token")
    if not access_token:
        # never log the request payload as it contains the client secret
        raise FwLoginFailedError(
            f"Azure login failed for client id {client_id}, response code: {response.status_code}, "
            f"response text: {response.text}"
        )
    FWOLogger.debug(f"Azure login successful for client id {client_id}")
    return access_token


def azure_api_get(url: str, access_token: str) -> list[dict[str, Any]]:
    """
    Get all items of an Azure Resource Manager list call, following nextLink pages.

    Args:
        url: The full URL of the list call including the api-version.
        access_token: The bearer access token.

    Returns:
        list[dict[str, Any]]: The items of all pages.

    """
    items: list[dict[str, Any]] = []
    next_url: str | None = url
    pages = 0
    while next_url:
        if pages >= AZURE_MAX_PAGES:
            raise FwApiCallFailedError(f"Azure api call to '{url}' exceeded the maximum of {AZURE_MAX_PAGES} pages")
        # the bearer token must only ever be sent to Azure Resource Manager
        if not next_url.startswith(AZURE_MANAGEMENT_URL + "/"):
            raise FwApiCallFailedError(f"Azure api call refused for unexpected url '{next_url}'")
        result = _azure_api_get_page(next_url, access_token)
        items.extend(result.get("value", []))
        next_url = result.get("nextLink")
        pages += 1
    return items


def _azure_api_get_page(url: str, access_token: str) -> dict[str, Any]:
    response = requests.get(
        url,
        headers={"Authorization": f"Bearer {access_token}", "Content-Type": "application/json"},
        timeout=FWO_HTTP_TIMEOUT,
    )
    if (
        response.status_code == HTTP_BAD_REQUEST
        and _get_error_code(response) in AZURE_UNSUPPORTED_API_VERSION_ERROR_CODES
    ):
        raise AzureApiVersionNotSupportedError(f"api-version not supported for url '{url}': {response.text}")
    if response.status_code != HTTP_OK:
        raise FwApiCallFailedError(
            f"error while sending api_call to url '{url}', response code: {response.status_code}, "
            f"response text: {response.text}"
        )
    FWOLogger.debug(f"api_call to url '{url}'", 3)
    return response.json()


def _get_error_code(response: requests.Response) -> str | None:
    try:
        error: Any = response.json().get("error")
    except ValueError:
        return None
    if not isinstance(error, dict):
        return None
    error_details = cast("dict[str, Any]", error)
    error_code = error_details.get("code")
    return error_code if isinstance(error_code, str) else None


def build_subscription_url(subscription_id: str, path: str, api_version: str) -> str:
    return f"{AZURE_MANAGEMENT_URL}/subscriptions/{quote(subscription_id, safe='')}/{path}?api-version={api_version}"


class AzureApiClient:
    """Lists Azure Resource Manager resources of a subscription with the newest api version the cloud supports."""

    def __init__(self, subscription_id: str, access_token: str) -> None:
        self.subscription_id = subscription_id
        self.access_token = access_token
        # determined by the first successful call and used for all further calls
        self.api_version: str | None = None

    def list_items(self, path: str) -> list[dict[str, Any]]:
        """
        Get all items of a list call relative to the subscription.

        Args:
            path: The resource path below the subscription.

        Returns:
            list[dict[str, Any]]: The items of all pages.

        """
        api_versions = [self.api_version] if self.api_version else list(AZURE_API_VERSIONS)
        for api_version in api_versions:
            url = build_subscription_url(self.subscription_id, path, api_version)
            try:
                items = azure_api_get(url, self.access_token)
            except AzureApiVersionNotSupportedError:
                FWOLogger.info(f"Azure api version {api_version} not supported for '{path}', trying an older one")
                continue
            self.api_version = api_version
            return items
        raise FwApiCallFailedError(f"Azure api versions {', '.join(api_versions)} are not supported for '{path}'")

    def get_rule_collection_groups(self, resource_group: str, policy_name: str) -> list[AzureRuleCollectionGroup]:
        path = (
            f"resourceGroups/{quote(resource_group, safe='')}/providers/Microsoft.Network/"
            f"firewallPolicies/{quote(policy_name, safe='')}/ruleCollectionGroups"
        )
        return [AzureRuleCollectionGroup.model_validate(item) for item in self.list_items(path)]


def get_native_config(mgm_details: ManagementController, devices: list[AzureDevice]) -> AzureConfig:
    """
    Get the native configuration from the Azure Resource Manager REST API.

    Args:
        mgm_details: The management details object.
        devices: The devices whose firewall policies are fetched.

    Returns:
        AzureConfig: The native configuration.

    """
    tenant_id = mgm_details.cloud_tenant_id
    subscription_id = mgm_details.cloud_subscription_id
    resource_group = mgm_details.domain_name
    if not tenant_id or not subscription_id or not resource_group:
        raise FwoImporterError(
            f"Azure management '{mgm_details.name}' needs cloud tenant id, cloud subscription id "
            "and the resource group as config path."
        )

    client_id, client_secret = get_client_credentials(mgm_details)
    access_token = get_access_token(tenant_id, client_id, client_secret)

    api_client = AzureApiClient(subscription_id, access_token)
    native_config = AzureConfig()
    # IP groups are listed for the whole subscription as policies may reference groups of other resource groups
    ip_group_items = api_client.list_items("providers/Microsoft.Network/ipGroups")
    native_config.ip_groups = [AzureIpGroup.model_validate(item) for item in ip_group_items]

    policy_names = sorted({device.policy_name for device in devices})
    for policy_name in policy_names:
        rule_collection_groups = api_client.get_rule_collection_groups(resource_group, policy_name)
        native_config.firewall_policies.append(
            AzureFirewallPolicy(name=policy_name, ruleCollectionGroups=rule_collection_groups)
        )
    native_config.api_version = api_client.api_version
    return native_config
