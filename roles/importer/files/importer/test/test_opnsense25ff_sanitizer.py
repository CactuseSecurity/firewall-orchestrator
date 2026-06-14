from typing import Any

from fw_modules.opnsense25ff.opnsense_sanitizer import remove_opnsense_sensitive_data

# Non-credential placeholder used for fields the sanitizer is expected to strip.
# Routing the values through a variable avoids hard-coded credential literals in
# the test fixture (e.g. SonarQube S2068).
_PLACEHOLDER = "redacted-test-value"


def _native_config_with_secrets() -> dict[str, Any]:
    return {
        "opnsense": {
            "system": {
                "user": [
                    {
                        "name": "root",
                        "password": _PLACEHOLDER,
                        "apikeys": {"k": "v"},
                        "otp_seed": _PLACEHOLDER,
                        "authorizedkeys": _PLACEHOLDER,
                        "pwd_changed_at": _PLACEHOLDER,
                        "landing_page": "/ui",
                        "dashboard": "d",
                    }
                ]
            },
            "OPNsense": {
                "IPsec": {"preSharedKeys": {"preSharedKey": [{"ident": "a", "Key": _PLACEHOLDER}]}},
                "Firewall": {
                    "Alias": {
                        "geoip": {"url": "http://example.com/geoip"},
                        "aliases": {"alias": [{"name": "a1", "username": "u", "password": _PLACEHOLDER}]},
                    }
                },
                "Netflow": {"x": 1},
                "Syslog": {"y": 2},
            },
            "Deciso": {"UserPortal": {"group_options": {"otp_seed": _PLACEHOLDER}}},
            "hasync": {"x": 1},
            "openvpn": {"y": 2},
            "virtualip": {"vip": [{"password": _PLACEHOLDER}]},
            "ca": [{"prv": _PLACEHOLDER}],
            "cert": [{"prv": _PLACEHOLDER}],
            "rrd": {"x": 1},
            "snmpd": {"y": 2},
            "sysctl": {"z": 3},
            "widgets": {"w": 4},
            "dhcpdv6": {"d": 5},
        }
    }


def test_remove_opnsense_sensitive_data_strips_user_secrets() -> None:
    out = remove_opnsense_sensitive_data(_native_config_with_secrets())

    user = out["opnsense"]["system"]["user"][0]
    for removed_key in (
        "password",
        "apikeys",
        "otp_seed",
        "authorizedkeys",
        "pwd_changed_at",
        "landing_page",
        "dashboard",
    ):
        assert removed_key not in user
    # non-sensitive fields are preserved
    assert user["name"] == "root"


def test_remove_opnsense_sensitive_data_strips_keys_and_aliases() -> None:
    out = remove_opnsense_sensitive_data(_native_config_with_secrets())
    opnsense = out["opnsense"]["OPNsense"]

    assert "Key" not in opnsense["IPsec"]["preSharedKeys"]["preSharedKey"][0]
    assert "geoip" not in opnsense["Firewall"]["Alias"]
    alias = opnsense["Firewall"]["Alias"]["aliases"]["alias"][0]
    assert "username" not in alias
    assert "password" not in alias
    assert out["opnsense"]["Deciso"]["UserPortal"]["group_options"].get("otp_seed") is None


def test_remove_opnsense_sensitive_data_drops_excluded_sections_and_private_keys() -> None:
    out = remove_opnsense_sensitive_data(_native_config_with_secrets())
    opnsense = out["opnsense"]

    for excluded_service in ("Netflow", "Syslog"):
        assert excluded_service not in opnsense["OPNsense"]
    for excluded_section in ("hasync", "openvpn", "rrd", "snmpd", "sysctl", "widgets", "dhcpdv6"):
        assert excluded_section not in opnsense
    assert "password" not in opnsense["virtualip"]["vip"][0]
    assert "prv" not in opnsense["ca"][0]
    assert "prv" not in opnsense["cert"][0]
