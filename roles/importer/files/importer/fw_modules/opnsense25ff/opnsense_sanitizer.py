# sanitizing irrelevant parts of the config.xml
from typing import Any


def remove_opnsense_sensitive_data(native_config: dict[str, Any]) -> dict[str, Any]:

    # remove sensitive user data such as:
    # - authorizedkeys
    # - otp_seed
    # - password
    # - pwd_changed_at
    # - landing_page
    # - apikeys
    # - dashboard
    for user in native_config.get("opnsense", {}).get("system", {}).get("user", {}):
        user.pop("authorizedkeys", None)
        user.pop("otp_seed", None)
        user.pop("password", None)
        user.pop("pwd_changed_at", None)
        user.pop("landing_page", None)
        user.pop("apikeys", None)
        user.pop("dashboard", None)

    # remove psk's from ipsec conf
    for psk_entry in native_config.get("opnsense", {}).get("OPNsense", {}).get("IPsec", {}).get("preSharedKeys", {}).get("preSharedKey", {}):
        psk_entry.pop("Key", None)

    # remove geoip url
    native_config.get("opnsense", {}).get("OPNsense", {}).get("Firewall", {}).get("Alias", {}).pop("geoip", None)

    # remove username and password fields from aliases:
    for alias in native_config.get("opnsense", {}).get("OPNsense", {}).get("Firewall", {}).get("Alias", {}).get("aliases", {}).get("alias", {}):
        alias.pop("password")
        alias.pop("username")

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
        native_config.get("opnsense", {}).get("OPNsense", {}).pop(service, None)

    # remove not necessary hasync settings
    native_config.get("opnsense", {}).pop("hasync", None)
    # remove not necessary openvpn settings
    native_config.get("opnsense", {}).pop("openvpn", None)

    # remove password from CARP VIP configuration
    for vip in native_config.get("opnsense", {}).get("virtualip", {}).get("vip", {}):
        vip.pop("password", None)

    # remove private keys from certs
    if isinstance(native_config.get("opnsense", {}).get("ca", {}), list):
        for ca in native_config.get("opnsense", {}).get("ca", {}):
            ca.pop("prv")
    else:
        native_config.get("opnsense", {}).get("ca", {}).pop("prv")
    if isinstance(native_config.get("opnsense", {}).get("cert", {}), list):
        for cert in native_config.get("opnsense", {}).get("cert", {}):
            cert.pop("prv")
    else:
        native_config.get("opnsense", {}).get("cert", {}).pop("prv")


    native_config.get("opnsense", {}).get("Deciso", {}).get("UserPortal", {}).get("group_options", {}).pop("otp_seed", None)

    # remove not necessary system level settings:
    system_excludes = [
        "dhcpdv6",
        "openvpn",
        "rrd",
        "snmpd",
        "sysctl",
        "widgets"
    ]
    for sys_ex in system_excludes:
        native_config.get("opnsense", {}).pop(sys_ex, None)


    return native_config
