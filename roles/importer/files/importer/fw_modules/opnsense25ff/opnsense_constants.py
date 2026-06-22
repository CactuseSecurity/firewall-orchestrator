OPNSENSE_UUID_ALIAS = "@uuid"
PREDEFINED_RULE_UID_PREFIX = "opnsense-default-rule-"
MAX_DEPTH: int = 10
BUILTIN_SERVICE_PORTS: dict[str, int] = {
    "http": 80,
    "https": 443,
    "ssh": 22,
    "domain": 53,
}
