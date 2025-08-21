#!/usr/bin/env python3
"""Parser for Cisco ASA configuration files.

The script converts an ASA configuration into a simplified
normalized JSON structure which is inspired by the public
sample configuration provided at
https://fwodemodata.cactus.de/demo12_cpr8x_v9.json.

The parser is not a complete ASA parser.  It focuses on
constructs used in the example configuration: interfaces,
network objects and groups, service groups, time ranges and
ACLs.  Unknown lines are ignored so the script can be applied
to larger configurations without failing.

Usage:
    python asa_config_parser.py <asa_config_file>
"""

from __future__ import annotations
from typing import Any

def has_config_changed(full_config, mgm_details, force=False):
    # dummy - may be filled with real check later on
    return True


def get_config(config2import, full_config, current_import_id, mgm_details, limit=100, force=False, jwt=''):
    logger = getFwoLogger()


def _parse_address(tokens: list[str], idx: int) -> tuple[dict[str, Any], int]:
    """Parse an address specification starting at ``tokens[idx]``.

    Returns a tuple of (address_dict, next_index).
    """

    t = tokens[idx]
    if t == "any":
        return {"type": "any"}, idx + 1
    if t == "object":
        return {"type": "object", "value": tokens[idx + 1]}, idx + 2
    if t == "object-group":
        return {"type": "object-group", "value": tokens[idx + 1]}, idx + 2
    if t == "host":
        return {"type": "host", "ip": tokens[idx + 1]}, idx + 2

    # assume subnet form: ip mask
    return {
        "type": "subnet",
        "ip": tokens[idx],
        "netmask": tokens[idx + 1] if idx + 1 < len(tokens) else "",
    }, idx + 2


def parse_asa_config(config: str) -> dict[str, Any]:
    """Convert raw ASA configuration text to a normalized Python dict."""

    lines = config.splitlines()
    result: dict[str, Any] = {
        "hostname": None,
        "interfaces": [],
        "time_ranges": {},
        "network_objects": {},
        "network_groups": {},
        "service_groups": {},
        "protocol_groups": {},
        "icmp_type_groups": {},
        "acls": {},
        "access_groups": [],
    }

    i = 0
    while i < len(lines):
        line = lines[i]
        stripped = line.strip()
        if not stripped or stripped.startswith("!"):
            i += 1
            continue

        if stripped.startswith("hostname"):
            result["hostname"] = stripped.split()[1]

        elif stripped.startswith("interface "):
            iface = {"name": stripped.split()[1]}
            i += 1
            while i < len(lines) and lines[i].startswith(" "):
                sub = lines[i].strip()
                if sub.startswith("nameif "):
                    iface["nameif"] = sub.split()[1]
                elif sub.startswith("security-level "):
                    iface["security_level"] = int(sub.split()[1])
                elif sub.startswith("ip address "):
                    parts = sub.split()
                    iface["ip_address"] = parts[2]
                    iface["netmask"] = parts[3]
                i += 1
            result["interfaces"].append(iface)
            continue

        elif stripped.startswith("time-range "):
            name = stripped.split()[1]
            tr: dict[str, Any] = {}
            i += 1
            while i < len(lines) and lines[i].startswith(" "):
                sub = lines[i].strip()
                if sub.startswith("periodic "):
                    tr["type"] = "periodic"
                    tr["value"] = sub[len("periodic "):]
                elif sub.startswith("absolute "):
                    tr["type"] = "absolute"
                    tr["value"] = sub[len("absolute "):]
                i += 1
            result["time_ranges"][name] = tr
            continue

        elif stripped.startswith("object network"):
            name = stripped.split()[2]
            obj: dict[str, Any] = {}
            i += 1
            while i < len(lines) and lines[i].startswith(" "):
                sub = lines[i].strip()
                if sub.startswith("subnet "):
                    _, ip, mask = sub.split()
                    obj = {"type": "subnet", "ip": ip, "netmask": mask}
                elif sub.startswith("host "):
                    _, ip = sub.split()
                    obj = {"type": "host", "ip": ip}
                elif sub.startswith("fqdn v4 "):
                    obj = {"type": "fqdn", "fqdn": sub.split()[2]}
                i += 1
            result["network_objects"][name] = obj
            continue

        elif stripped.startswith("object-group network"):
            name = stripped.split()[2]
            members: list[dict[str, Any]] = []
            i += 1
            while i < len(lines) and lines[i].startswith(" "):
                sub = lines[i].strip()
                if sub.startswith("network-object object "):
                    members.append({"type": "object", "value": sub.split()[2]})
                elif sub.startswith("network-object "):
                    parts = sub.split()
                    members.append(
                        {
                            "type": "subnet",
                            "ip": parts[1],
                            "netmask": parts[2],
                        }
                    )
                i += 1
            result["network_groups"][name] = members
            continue

        elif stripped.startswith("object-group service"):
            tokens = stripped.split()
            name = tokens[2]
            proto = tokens[3] if len(tokens) > 3 else ""
            members: list[dict[str, Any]] = []
            i += 1
            while i < len(lines) and lines[i].startswith(" "):
                sub = lines[i].strip()
                if sub.startswith("service-object "):
                    parts = sub.split()
                    if len(parts) >= 4 and parts[2] == "eq":
                        members.append({"proto": parts[1], "port": parts[3]})
                    elif len(parts) >= 5 and parts[2] == "range":
                        members.append(
                            {
                                "proto": parts[1],
                                "port_range": (parts[3], parts[4]),
                            }
                        )
                elif sub.startswith("port-object "):
                    parts = sub.split()
                    if parts[1] == "eq":
                        members.append({"proto": proto, "port": parts[2]})
                    elif parts[1] == "range":
                        members.append(
                            {
                                "proto": proto,
                                "port_range": (parts[2], parts[3]),
                            }
                        )
                i += 1
            result["service_groups"][name] = {"protocol": proto, "members": members}
            continue

        elif stripped.startswith("object-group protocol"):
            name = stripped.split()[2]
            members: list[str] = []
            i += 1
            while i < len(lines) and lines[i].startswith(" "):
                sub = lines[i].strip()
                if sub.startswith("protocol-object "):
                    members.append(sub.split()[1])
                i += 1
            result["protocol_groups"][name] = members
            continue

        elif stripped.startswith("object-group icmp-type"):
            name = stripped.split()[2]
            members: list[str] = []
            i += 1
            while i < len(lines) and lines[i].startswith(" "):
                sub = lines[i].strip()
                if sub.startswith("icmp-object "):
                    members.append(sub.split()[1])
                i += 1
            result["icmp_type_groups"][name] = members
            continue

        elif stripped.startswith("access-list "):
            tokens = stripped.split()
            acl_name = tokens[1]
            rest = tokens[2:]
            if rest and rest[0] == "remark":
                remark = " ".join(rest[1:])
                result["acls"].setdefault(acl_name, []).append({"remark": remark})
            else:
                rule: dict[str, Any] = {}
                if rest and rest[0] == "extended":
                    action = rest[1]
                    proto = rest[2]
                    idx = 3
                    src, idx = _parse_address(rest, idx)
                    dst, idx = _parse_address(rest, idx)
                    rule.update(
                        {
                            "action": action,
                            "protocol": proto,
                            "source": src,
                            "destination": dst,
                        }
                    )
                    if idx < len(rest):
                        rule["service"] = " ".join(rest[idx:])
                else:
                    rule["raw"] = " ".join(rest)
                result["acls"].setdefault(acl_name, []).append(rule)
            i += 1
            continue

        elif stripped.startswith("access-group "):
            parts = stripped.split()
            entry = {"acl": parts[1], "direction": parts[2]}
            if len(parts) >= 5 and parts[3] == "interface":
                entry["interface"] = parts[4]
            result["access_groups"].append(entry)

        i += 1

    return result


# def main() -> None:
#     parser = argparse.ArgumentParser(description="Parse ASA config into normalized JSON")
#     parser.add_argument("config_file", help="path to ASA configuration file")
#     args = parser.parse_args()

#     with open(args.config_file, "r", encoding="utf-8") as fh:
#         raw_config = fh.read()

#     parsed = parse_asa_config(raw_config)
#     print(json.dumps(parsed, indent=2))


# if __name__ == "__main__":
#     main()
