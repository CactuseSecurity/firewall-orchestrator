
from fw_modules.opnsense25ff.opnsense_model import (OPNsenseConfig,
                                                    OPNsenseHost,
                                                    OPNsenseNetwork,
                                                    OPNsensePort)
from netaddr import IPAddress, IPNetwork
from netaddr.core import AddrFormatError


def _helper_is_int(s):
    try:
        int(s)
        return True
    except ValueError:
        return False

def _helper_is_ip(s):
    try:
        IPAddress(s)
        return True
    except (ValueError, AddrFormatError):
        return False

def _helper_is_ip_subnet(s):
    if _helper_is_ip(s):
        return False
    try:
        IPNetwork(s)
        return True
    except (ValueError, AddrFormatError):
        return False

def _helper_is_ip_range(s):
    try:
        start, end = s.split("-", 1)
        IPAddress(start)
        IPAddress(end)
        return True
    except (ValueError, AddrFormatError):
        return False

def link_opnsense_ports_from_port_aliases(config: OPNsenseConfig) -> None:
    port_aliases = config.port_aliases
    for alias in port_aliases:
        for p in port_aliases[alias].value:
            if _helper_is_int(p.split(":", 1)[0]):
                p_name = "__p_" + p
                p_is_range = False
                p_port = 0
                p_port_end = 0
                if ":" in p:
                    start, end = p.split(":", 1)
                    p_port = int(start)
                    p_port_end = int(end)
                    p_is_range = True
                else:
                    p_port = int(p)
                    p_port_end = int(p)
                    p_is_range = False
                port_aliases[alias].childs.append(OPNsensePort(name=p_name, is_range=p_is_range, port=p_port, port_end=p_port_end))
            else:
                if p in port_aliases:
                    port_aliases[alias].childs.append(port_aliases[p])
        if len(port_aliases[alias].childs) != len(port_aliases[alias].value):
            print(f"[-] _link_opnsense_ports_from_port_aliases: port alias child count inconsistent:\n    {port_aliases[alias]}")

def xlinking_rules_to_aliases(config: OPNsenseConfig) -> None:
    host_aliases, net_aliases, port_aliases = config.host_aliases, config.net_aliases, config.port_aliases
    access_rules, nat_rules = config.access_rules, config.nat_rules

    for ar in access_rules:
        # linking source and destination ports in access rules
        if ar.source_port:
            for p in ar.source_port:
                if p in port_aliases:
                    ar.source_port.append(port_aliases[p])
                    ar.source_port.remove(p)
                    port_aliases[p].is_used_by.append(ar)
        if ar.dest_port:
            for p in ar.dest_port:
                if p in port_aliases:
                    ar.dest_port.append(port_aliases[p])
                    ar.dest_port.remove(p)
                    port_aliases[p].is_used_by.append(ar)
        # linking source and destination addresses in access rules
        if ar.source:
            for s in ar.source:
                if not isinstance(s, str):
                    continue
                if s in host_aliases:
                    ar.source.append(host_aliases[s])
                    ar.source.remove(s)
                    if ar not in host_aliases[s].is_used_by:
                        host_aliases[s].is_used_by.append(ar)
                if s in net_aliases:
                    ar.source.append(net_aliases[s])
                    ar.source.remove(s)
                    if ar not in net_aliases[s].is_used_by:
                        net_aliases[s].is_used_by.append(ar)
        if ar.dest:
            for d in ar.dest:
                if not isinstance(d, str):
                    continue
                if d in host_aliases:
                    ar.dest.append(host_aliases[d])
                    ar.dest.remove(d)
                    if ar not in host_aliases[d].is_used_by:
                        host_aliases[d].is_used_by.append(ar)
                if d in net_aliases:
                    ar.dest.append(net_aliases[d])
                    ar.dest.remove(d)
                    if ar not in net_aliases[d].is_used_by:
                        net_aliases[d].is_used_by.append(ar)

    for nr in nat_rules:
        # linking source, destination and xlat ports in nat rules
        if nr.source_port:
            for p in nr.source_port:
                if p in port_aliases:
                    nr.source_port.append(port_aliases[p])
                    nr.source_port.remove(p)
                    port_aliases[p].is_used_by.append(nr)
        if nr.dest_port:
            for p in nr.dest_port:
                if p in port_aliases:
                    nr.dest_port.append(port_aliases[p])
                    nr.dest_port.remove(p)
                    port_aliases[p].is_used_by.append(nr)
        if nr.xlat_port:
            for p in nr.xlat_port:
                if p in port_aliases:
                    nr.xlat_port.append(port_aliases[p])
                    nr.xlat_port.remove(p)
                    port_aliases[p].is_used_by.append(nr)
        # linking source and destination addresses in nat rules
        if nr.source_net:
            for s in nr.source_net:
                if not isinstance(s, str):
                    continue
                if s in host_aliases:
                    nr.source_net.append(host_aliases[s])
                    nr.source_net.remove(s)
                    if nr not in host_aliases[s].is_used_by:
                        host_aliases[s].is_used_by.append(nr)
                if s in net_aliases:
                    nr.source_net.append(net_aliases[s])
                    nr.source_net.remove(s)
                    if nr not in net_aliases[s].is_used_by:
                        net_aliases[s].is_used_by.append(nr)
        if nr.source_addr:
            for s in nr.source_addr:
                if not isinstance(s, str):
                    continue
                if s in host_aliases:
                    nr.source_addr.append(host_aliases[s])
                    nr.source_addr.remove(s)
                    if nr not in host_aliases[s].is_used_by:
                        host_aliases[s].is_used_by.append(nr)
                if s in net_aliases:
                    nr.source_addr.append(net_aliases[s])
                    nr.source_addr.remove(s)
                    if nr not in net_aliases[s].is_used_by:
                        net_aliases[s].is_used_by.append(nr)
        if nr.dest_net:
            for d in nr.dest_net:
                if not isinstance(d, str):
                    continue
                if d in host_aliases:
                    nr.dest_net.append(host_aliases[d])
                    nr.dest_net.remove(d)
                    if nr not in host_aliases[d].is_used_by:
                        host_aliases[d].is_used_by.append(nr)
                if d in net_aliases:
                    nr.dest_net.append(net_aliases[d])
                    nr.dest_net.remove(d)
                    if nr not in net_aliases[d].is_used_by:
                        net_aliases[d].is_used_by.append(nr)
        if nr.dest_addr:
            for d in nr.dest_addr:
                if not isinstance(d, str):
                    continue
                if d in host_aliases:
                    nr.dest_addr.append(host_aliases[d])
                    nr.dest_addr.remove(d)
                    if nr not in host_aliases[d].is_used_by:
                        host_aliases[d].is_used_by.append(nr)
                if d in net_aliases:
                    nr.dest_addr.append(net_aliases[d])
                    nr.dest_addr.remove(d)
                    if nr not in net_aliases[d].is_used_by:
                        net_aliases[d].is_used_by.append(nr)
        if nr.xlat_addr:
            for d in nr.xlat_addr:
                if not isinstance(d, str):
                    continue
                if d in host_aliases:
                    nr.xlat_addr.append(host_aliases[d])
                    nr.xlat_addr.remove(d)
                    if nr not in host_aliases[d].is_used_by:
                        host_aliases[d].is_used_by.append(nr)
                if d in net_aliases:
                    nr.xlat_addr.append(net_aliases[d])
                    nr.xlat_addr.remove(d)
                    if nr not in net_aliases[d].is_used_by:
                        net_aliases[d].is_used_by.append(nr)

def enrich_opnsense_net_and_hosts(config: OPNsenseConfig) -> None:

    for alias_list in [config.host_aliases, config.net_aliases]:
        for alias in alias_list:
            for value in alias_list[alias].value:
                if value in config.host_aliases:
                    alias_list[alias].childs.append(config.host_aliases[value])
                elif value in config.net_aliases:
                    alias_list[alias].childs.append(config.net_aliases[value])
                else:
                    if _helper_is_ip(value) or _helper_is_ip_range(value):
                        # single ips or ip ranges
                        host = OPNsenseHost(
                            name = "__h_" + value,
                            is_range = _helper_is_ip_range(value),
                            host = IPAddress(value.split("-", 1)[0]),
                            host_end = IPAddress(value.split("-", 1)[1]) if _helper_is_ip_range(value) else IPAddress(value.split("-", 1)[0])
                        )
                        alias_list[alias].childs.append(host)
                    elif _helper_is_ip_subnet(value):
                        # ip subnet aliases
                        net = OPNsenseNetwork(
                            name = "__n_" + value,
                            net = IPNetwork(value)
                        )
                        alias_list[alias].childs.append(net)
                    else:
                        # arbitrary values
                        alias_list[alias].childs.append(value)
