from __future__ import annotations

import re
from typing import TYPE_CHECKING, Any, TypeVar, cast
from urllib.parse import quote

import requests
from fw_modules.azure2022ff.azure_const import (
    AZURE_API_VERSIONS,
    AZURE_FILTER_RULE_COLLECTION,
    AZURE_LOGIN_URL,
    AZURE_MANAGEMENT_SCOPE,
    AZURE_MANAGEMENT_URL,
    AZURE_MAX_PAGES,
    AZURE_MAX_POLICY_DEPTH,
    AZURE_NETWORK_RULE,
    AZURE_UNSUPPORTED_API_VERSION_ERROR_CODES,
)
from fw_modules.azure2022ff.azure_models import (
    AzureConfig,
    AzureDevice,
    AzureFirewallPolicy,
    AzureFirewallPolicyResource,
    AzureIpGroup,
    AzureRule,
    AzureRuleCollectionGroup,
)
from fwo_const import FWO_HTTP_TIMEOUT
from fwo_exceptions import FwApiCallFailedError, FwLoginFailedError, FwoImporterError
from fwo_log import FWOLogger

if TYPE_CHECKING:
    from collections.abc import Callable, Iterator

    from model_controllers.management_controller import ManagementController

HTTP_OK = 200
HTTP_BAD_REQUEST = 400
# resource ids of firewall policies and IP groups, the only kinds of resource ids read from Azure that are used to
# build urls; each part is a single path segment, '.' and '..' excluded
_SEGMENT = r"(?!\.\.?(?:/|$))[^/?#]+"
_NETWORK_PROVIDER_PATH = rf"/subscriptions/{_SEGMENT}/resourceGroups/{_SEGMENT}/providers/Microsoft\.Network"
AZURE_FIREWALL_POLICY_ID_PATTERN = re.compile(rf"{_NETWORK_PROVIDER_PATH}/firewallPolicies/{_SEGMENT}", re.IGNORECASE)
AZURE_IP_GROUP_ID_PATTERN = re.compile(rf"{_NETWORK_PROVIDER_PATH}/ipGroups/{_SEGMENT}", re.IGNORECASE)

T = TypeVar("T")


class AzureApiVersionNotSupportedError(FwApiCallFailedError):
    """Azure Resource Manager rejected the api-version of a call."""


def get_policy_devices(mgm_details: ManagementController, force_import: bool = False) -> list[AzureDevice]:
    """
    Collect the devices to import together with the firewall policy enforced on each of them.

    The policy name is taken from the device's local rulebase name and defaults to the device name.
    The device uid becomes the gateway uid, which the import maps to the FWO device, so it is required.

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
        uid = device.get("uid")
        if not uid:
            raise FwoImporterError(
                f"Azure device '{name}' has no uid. Set its uid to the resource id of the Azure firewall "
                "or add the device with the autodiscovery."
            )
        devices.append(
            AzureDevice(
                uid=uid,
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


def build_subscription_path(subscription_id: str, path: str) -> str:
    return f"/subscriptions/{quote(subscription_id, safe='')}/{path}"


def build_firewall_policy_id(subscription_id: str, resource_group: str, policy_name: str) -> str:
    return build_subscription_path(
        subscription_id,
        f"resourceGroups/{quote(resource_group, safe='')}/providers/Microsoft.Network/"
        f"firewallPolicies/{quote(policy_name, safe='')}",
    )


def check_firewall_policy_id(policy_id: str) -> str:
    """
    Check that a resource id read from Azure (a parent policy reference) denotes a firewall policy.

    Only such ids are appended to the management url, so a reference can never select another kind of resource.

    Args:
        policy_id: The resource id of the firewall policy.

    Returns:
        str: The unchanged resource id.

    """
    if not AZURE_FIREWALL_POLICY_ID_PATTERN.fullmatch(policy_id):
        raise FwApiCallFailedError(f"Invalid Azure firewall policy id '{policy_id}'")
    return policy_id


def check_ip_group_id(ip_group_id: str) -> str:
    """
    Check that a resource id read from Azure (an IP group reference of a rule) denotes an IP group.

    Only such ids are appended to the management url, so a reference can never select another kind of resource.

    Args:
        ip_group_id: The resource id of the IP group.

    Returns:
        str: The unchanged resource id.

    """
    if not AZURE_IP_GROUP_ID_PATTERN.fullmatch(ip_group_id):
        raise FwApiCallFailedError(f"Invalid Azure IP group id '{ip_group_id}'")
    return ip_group_id


class AzureApiClient:
    """Reads Azure Resource Manager resources with the newest api version the cloud supports."""

    def __init__(self, subscription_id: str, access_token: str) -> None:
        self.subscription_id = subscription_id
        self.access_token = access_token
        # determined by the first successful call and used for all further calls
        self.api_version: str | None = None

    def call_with_api_version(self, resource_path: str, api_call: Callable[[str], T]) -> T:
        """
        Run an api call for a resource path, falling back to older api versions if the cloud rejects the newer ones.

        Args:
            resource_path: The path below the management url, starting with '/subscriptions/'.
            api_call: Gets the full url including the api-version and runs the call.

        Returns:
            T: The result of the api call.

        """
        api_versions = [self.api_version] if self.api_version else list(AZURE_API_VERSIONS)
        for api_version in api_versions:
            url = f"{AZURE_MANAGEMENT_URL}{resource_path}?api-version={api_version}"
            try:
                result = api_call(url)
            except AzureApiVersionNotSupportedError:
                FWOLogger.info(
                    f"Azure api version {api_version} not supported for '{resource_path}', trying an older one"
                )
                continue
            self.api_version = api_version
            return result
        raise FwApiCallFailedError(
            f"Azure api versions {', '.join(api_versions)} are not supported for '{resource_path}'"
        )

    def list_items(self, path: str) -> list[dict[str, Any]]:
        """
        Get all items of a list call relative to the subscription.

        Args:
            path: The resource path below the subscription.

        Returns:
            list[dict[str, Any]]: The items of all pages.

        """
        resource_path = build_subscription_path(self.subscription_id, path)
        return self.call_with_api_version(resource_path, lambda url: azure_api_get(url, self.access_token))

    def get_firewall_policy(self, policy_id: str) -> AzureFirewallPolicyResource:
        resource = self.call_with_api_version(policy_id, lambda url: _azure_api_get_page(url, self.access_token))
        return AzureFirewallPolicyResource.model_validate(resource)

    def get_ip_group(self, ip_group_id: str) -> AzureIpGroup:
        resource = self.call_with_api_version(ip_group_id, lambda url: _azure_api_get_page(url, self.access_token))
        return AzureIpGroup.model_validate(resource)

    def get_rule_collection_groups(self, policy_id: str) -> list[AzureRuleCollectionGroup]:
        items = self.call_with_api_version(
            f"{policy_id}/ruleCollectionGroups", lambda url: azure_api_get(url, self.access_token)
        )
        return [AzureRuleCollectionGroup.model_validate(item) for item in items]


class AzureFirewallPolicyCollector:
    """Fetches firewall policies together with all their parent policies, each policy once."""

    def __init__(self, api_client: AzureApiClient) -> None:
        self.api_client = api_client
        self.policies: list[AzureFirewallPolicy] = []
        self.fetched_policy_ids: set[str] = set()

    def add_policy_hierarchy(self, policy_id: str) -> None:
        """
        Fetch a firewall policy and the chain of its parent policies.

        Parent policies may lie in other resource groups, they are fetched by the resource id the child references.

        Args:
            policy_id: The resource id of the firewall policy.

        """
        next_policy_id: str | None = policy_id
        depth = 0
        while next_policy_id is not None and next_policy_id.lower() not in self.fetched_policy_ids:
            if depth >= AZURE_MAX_POLICY_DEPTH:
                raise FwApiCallFailedError(
                    f"Azure firewall policy '{policy_id}' has more than {AZURE_MAX_POLICY_DEPTH} levels of parent policies"
                )
            policy = self.fetch_policy(check_firewall_policy_id(next_policy_id))
            next_policy_id = policy.base_policy.id if policy.base_policy is not None else None
            depth += 1

    def fetch_policy(self, policy_id: str) -> AzureFirewallPolicy:
        resource = self.api_client.get_firewall_policy(policy_id)
        policy = AzureFirewallPolicy(
            id=resource.id,
            name=resource.name,
            basePolicy=resource.properties.base_policy,
            ruleCollectionGroups=self.api_client.get_rule_collection_groups(policy_id),
        )
        self.fetched_policy_ids.add(policy_id.lower())
        self.fetched_policy_ids.add(resource.id.lower())
        self.policies.append(policy)
        return policy


def get_native_config(mgm_details: ManagementController, devices: list[AzureDevice]) -> AzureConfig:
    """
    Get the native configuration from the Azure Resource Manager REST API.

    Args:
        mgm_details: The management details object.
        devices: The devices whose firewall policies (including their parent policies) are fetched.

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
    ip_groups = [AzureIpGroup.model_validate(item) for item in ip_group_items]

    policy_collector = AzureFirewallPolicyCollector(api_client)
    for policy_name in sorted({device.policy_name for device in devices}):
        policy_collector.add_policy_hierarchy(build_firewall_policy_id(subscription_id, resource_group, policy_name))
    native_config.firewall_policies = policy_collector.policies
    native_config.ip_groups = ip_groups + get_missing_ip_groups(api_client, policy_collector.policies, ip_groups)
    native_config.api_version = api_client.api_version
    return native_config


def get_missing_ip_groups(
    api_client: AzureApiClient, policies: list[AzureFirewallPolicy], known_ip_groups: list[AzureIpGroup]
) -> list[AzureIpGroup]:
    """
    Fetch the referenced IP groups that are not in the subscription, typically those of parent policies.

    They are fetched one by one by their resource id, so only read access on the groups themselves is needed,
    not on their whole subscription.

    Args:
        api_client: The client of the management's subscription.
        policies: The firewall policies whose rules reference the IP groups.
        known_ip_groups: The IP groups already listed for the subscription.

    Returns:
        list[AzureIpGroup]: The fetched IP groups.

    """
    known_ip_group_ids = {ip_group.id.lower() for ip_group in known_ip_groups}
    missing_ip_groups: list[AzureIpGroup] = []
    for ip_group_id in get_referenced_ip_group_ids(policies):
        if ip_group_id.lower() in known_ip_group_ids:
            continue
        FWOLogger.debug(f"Fetching Azure IP group '{ip_group_id}' from outside the subscription")
        missing_ip_groups.append(api_client.get_ip_group(check_ip_group_id(ip_group_id)))
        known_ip_group_ids.add(ip_group_id.lower())
    return missing_ip_groups


def get_referenced_ip_group_ids(policies: list[AzureFirewallPolicy]) -> list[str]:
    """
    Collect the IP groups referenced by the imported rules (network rules of filter rule collections).

    Args:
        policies: The firewall policies.

    Returns:
        list[str]: The resource ids, each once regardless of its casing, in order of first reference.

    """
    ip_group_ids: dict[str, str] = {}
    for rule in get_imported_rules(policies):
        for ip_group_id in rule.source_ip_groups + rule.destination_ip_groups:
            ip_group_ids.setdefault(ip_group_id.lower(), ip_group_id)
    return list(ip_group_ids.values())


def get_imported_rules(policies: list[AzureFirewallPolicy]) -> Iterator[AzureRule]:
    """Yield the rules the normalizer imports: the network rules of filter rule collections."""
    for policy in policies:
        for rule_collection_group in policy.rule_collection_groups:
            filter_rule_collections = [
                rule_collection
                for rule_collection in rule_collection_group.properties.rule_collections
                if rule_collection.rule_collection_type == AZURE_FILTER_RULE_COLLECTION
            ]
            for rule_collection in filter_rule_collections:
                yield from (rule for rule in rule_collection.rules if rule.rule_type == AZURE_NETWORK_RULE)
