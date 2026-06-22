# sanitizing irrelevant parts of the config.xml
from typing import Any, TypeGuard, cast


def _is_dict(value: object) -> TypeGuard[dict[str, Any]]:
    return isinstance(value, dict)


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
        "ftpproxies",
        "IDS",
        "monit",
        "Netflow",
        "ntopng",
        "postfix",
        "redis",
        "Syslog",
        "TrafficShaper",
        "unboundplus",
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
    system_excludes = ["dhcpdv6", "openvpn", "rrd", "snmpd", "sysctl", "widgets"]
    for sys_ex in system_excludes:
        opnsense.pop(sys_ex, None)

    return native_config
