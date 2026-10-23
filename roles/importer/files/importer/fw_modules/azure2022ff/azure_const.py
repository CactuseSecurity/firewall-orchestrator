# api versions in order of preference; older ones are used if a cloud does not support the newer ones yet
AZURE_API_VERSIONS: tuple[str, ...] = ("2026-01-01", "2024-05-01")
# error codes of Azure Resource Manager for an api-version unknown to the resource provider
AZURE_UNSUPPORTED_API_VERSION_ERROR_CODES = frozenset({"InvalidApiVersionParameter", "NoRegisteredProviderFound"})
AZURE_MANAGEMENT_URL = "https://management.azure.com"
AZURE_LOGIN_URL = "https://login.microsoftonline.com"
AZURE_MANAGEMENT_SCOPE = "https://management.azure.com/.default"
# upper bound for following nextLink pages of a single list call
AZURE_MAX_PAGES = 100

AZURE_ANY = "*"
AZURE_FILTER_RULE_COLLECTION = "FirewallPolicyFilterRuleCollection"
AZURE_NETWORK_RULE = "NetworkRule"
AZURE_ACTION_DENY = "Deny"
AZURE_PROTOCOL_ANY = "Any"
AZURE_PROTOCOL_ICMP = "ICMP"
AZURE_IP_PROTOCOL_NUMBERS: dict[str, int] = {"TCP": 6, "UDP": 17, "ICMP": 1}

AZURE_PORT_MIN = 0
AZURE_PORT_MAX = 65535

ANY_OBJECT_NAME = "any"
