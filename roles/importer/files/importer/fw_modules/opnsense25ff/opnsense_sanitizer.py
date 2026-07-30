# sanitizing irrelevant parts of the config.xml
from typing import Any, TypeGuard, cast

from fw_modules.opnsense25ff.opnsense_constants import MAX_SANITIZER_DEPTH

# Element names that carry credentials anywhere in config.xml. The deny-list below only covers
# the sections known today, so every remaining section is additionally swept for these keys.
# None of them is read by the parser, so removing them cannot break normalization.
SENSITIVE_KEY_NAMES: frozenset[str] = frozenset(
    {
        "api_key",
        "apikey",
        "apikeys",
        "apisecret",
        "auth_token",
        "authorizedkeys",
        "authtoken",
        "bindpw",
        "ddnsdomainkey",
        "key",
        "ldap_bindpw",
        "md5-hash",
        "nt-hash",
        "otp_seed",
        "pass",
        "passwd",
        "password",
        "pre-shared-key",
        "private-key",
        "privatekey",
        "privkey",
        "prv",
        "psk",
        "pwd",
        "radius_secret",
        "secret",
        "shared_secret",
        "sharedkey",
        "token",
        "tsigkey",
    }
)


def _is_dict(value: object) -> TypeGuard[dict[str, Any]]:
    return isinstance(value, dict)


def _redact_sensitive_keys(value: object, depth: int = 0) -> None:
    # defense in depth: drop every credential-bearing element regardless of the section it lives in,
    # so plugin sections not covered by the explicit deny-list below cannot leak into debug dumps
    if depth >= MAX_SANITIZER_DEPTH:
        return
    if isinstance(value, list):
        for item in cast("list[object]", value):
            _redact_sensitive_keys(item, depth + 1)
        return
    if not _is_dict(value):
        return
    for sensitive_key in [key for key in value if key.lower() in SENSITIVE_KEY_NAMES]:
        value.pop(sensitive_key, None)
    for child in list(value.values()):
        _redact_sensitive_keys(child, depth + 1)


def _get_dict(data: dict[str, Any], *keys: str) -> dict[str, Any]:
    current: object = data
    for key in keys:
        if not _is_dict(current):
            return {}
        current = current.get(key, {})
    return current if _is_dict(current) else {}


def _as_dict_list(value: object) -> list[dict[str, Any]]:
    if _is_dict(value):
        return [value]
    if isinstance(value, list):
        return [item for item in cast("list[object]", value) if _is_dict(item)]
    return []


def remove_opnsense_sensitive_data(native_config: dict[str, Any]) -> dict[str, Any]:
    opnsense = _get_dict(native_config, "opnsense")
    opnsense_settings = _get_dict(opnsense, "OPNsense")

    # remove sensitive user data such as:
    # - authorizedkeys
    # - otp_seed
    # - password
    # - pwd_changed_at
    # - landing_page
    # - apikeys
    # - dashboard
    for user in _as_dict_list(_get_dict(opnsense, "system").get("user")):
        user.pop("authorizedkeys", None)
        user.pop("otp_seed", None)
        user.pop("password", None)
        user.pop("pwd_changed_at", None)
        user.pop("landing_page", None)
        user.pop("apikeys", None)
        user.pop("dashboard", None)

    # remove psk's from ipsec conf
    for psk_entry in _as_dict_list(_get_dict(opnsense_settings, "IPsec", "preSharedKeys").get("preSharedKey")):
        psk_entry.pop("Key", None)

    # remove psk's and private keys from legacy ipsec phase1 entries
    for phase1_entry in _as_dict_list(_get_dict(opnsense, "ipsec").get("phase1")):
        phase1_entry.pop("pre-shared-key", None)
        phase1_entry.pop("private-key", None)

    # remove PPP (PPPoE/PPTP/L2TP) dial-in credentials
    for ppp_entry in _as_dict_list(_get_dict(opnsense, "ppps").get("ppp")):
        ppp_entry.pop("password", None)
        ppp_entry.pop("username", None)

    # remove geoip url
    alias_config = _get_dict(opnsense_settings, "Firewall", "Alias")
    alias_config.pop("geoip", None)

    # remove username and password fields from aliases:
    for alias in _as_dict_list(_get_dict(alias_config, "aliases").get("alias")):
        alias.pop("password", None)
        alias.pop("username", None)

    # remove not necessary service settings:
    service_exclude = [
        "cron",
        "crowdsec",
        "DHCRelay",
        "DynDNS",  # contains dynamic dns account passwords and api tokens
        "ftpproxies",
        "IDS",
        "monit",
        "Netflow",
        "ntopng",
        "OpenVPNExport",  # contains client certificate/key export credentials
        "postfix",
        "redis",
        "Syslog",
        "TrafficShaper",
        "unboundplus",
        "wireguard",  # contains interface/peer private keys
    ]

    for service in service_exclude:
        opnsense_settings.pop(service, None)

    # remove not necessary hasync settings
    opnsense.pop("hasync", None)
    # remove not necessary openvpn settings
    opnsense.pop("openvpn", None)

    # remove password from CARP VIP configuration
    for vip in _as_dict_list(_get_dict(opnsense, "virtualip").get("vip")):
        vip.pop("password", None)

    # remove private keys from certs
    for ca in _as_dict_list(opnsense.get("ca")):
        ca.pop("prv", None)
    for cert in _as_dict_list(opnsense.get("cert")):
        cert.pop("prv", None)

    _get_dict(opnsense, "Deciso", "UserPortal", "group_options").pop("otp_seed", None)

    # remove not necessary system level settings:
    # - dyndnses: legacy dynamic dns accounts (passwords/api tokens)
    # - authserver is nested below system and holds ldap bind and radius secrets
    system_excludes = ["dhcpdv6", "dyndnses", "openvpn", "rrd", "snmpd", "sysctl", "widgets"]
    for sys_ex in system_excludes:
        opnsense.pop(sys_ex, None)
    _get_dict(opnsense, "system").pop("authserver", None)

    # sweep all remaining sections for credential-bearing elements
    _redact_sensitive_keys(native_config)

    return native_config
