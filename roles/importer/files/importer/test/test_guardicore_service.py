import fwo_const
from fw_modules.guardicoremanagementREST import gc_const
from fw_modules.guardicoremanagementREST.gc_models import GcRule
from fw_modules.guardicoremanagementREST.gc_service import (
    build_rule_services,
    get_protocol_number,
    get_rule_port_ranges,
    merge_port_ranges,
    subtract_port_ranges,
)


def build_rule(**fields: object) -> GcRule:
    return GcRule.model_validate({"id": "r1", "action": "ALLOW", "section_position": "ALLOW", **fields})


class TestPortRanges:
    def test_merge_port_ranges(self) -> None:
        assert merge_port_ranges([(80, 80), (79, 79), (100, 200), (150, 250)]) == [(79, 80), (100, 250)]

    def test_subtract_port_ranges(self) -> None:
        assert subtract_port_ranges([(0, 65535)], [(22, 22), (100, 199)]) == [(0, 21), (23, 99), (200, 65535)]
        assert subtract_port_ranges([(80, 80)], [(1, 10)]) == [(80, 80)]
        assert subtract_port_ranges([(80, 90)], [(80, 90)]) == []

    def test_rule_port_ranges_default_to_all_ports(self) -> None:
        assert get_rule_port_ranges(build_rule()) == [(gc_const.MIN_PORT, gc_const.MAX_PORT)]
        rule = build_rule(
            ports=[443, 80],
            port_ranges=[{"start": 8000, "end": 8100}],
            exclude_ports=[8080],
            exclude_port_ranges=[{"start": 8090, "end": 8200}],
        )
        assert get_rule_port_ranges(rule) == [(80, 80), (443, 443), (8000, 8079), (8081, 8089)]


class TestServices:
    def test_protocol_numbers(self) -> None:
        assert get_protocol_number("tcp") == gc_const.IP_PROTO_TCP
        assert get_protocol_number("47") == 47
        assert get_protocol_number("GRE") is None

    def test_rule_without_protocol_is_any(self) -> None:
        services = build_rule_services(build_rule())
        assert [svc.svc_uid for svc in services] == [gc_const.GC_ANY_SERVICE_UID]
        assert services[0].ip_proto == fwo_const.ANY_IP_PROTOCOL_ID
        assert services[0].svc_port is None

    def test_ported_services(self) -> None:
        services = build_rule_services(
            build_rule(ip_protocols=["TCP", "UDP"], ports=[53], port_ranges=[{"start": 1000, "end": 1010}])
        )
        assert [svc.svc_uid for svc in services] == ["tcp/53", "tcp/1000-1010", "udp/53", "udp/1000-1010"]
        assert (services[1].svc_port, services[1].svc_port_end, services[1].ip_proto) == (1000, 1010, 6)
        whole_protocol = build_rule_services(build_rule(ip_protocols=["TCP"]))
        assert [svc.svc_uid for svc in whole_protocol] == ["tcp"]

    def test_icmp_and_other_protocols(self) -> None:
        services = build_rule_services(
            build_rule(
                ip_protocols=["ICMP", "47", "GRE"],
                icmp_matches=[{"icmp_type": 8, "icmp_codes": [1, 0]}, {"icmp_type": None}],
            )
        )
        assert [svc.svc_uid for svc in services] == ["icmp type 8 code 0,1", "icmp", "47"]
        assert services[0].svc_port is None
        assert services[2].ip_proto == 47

    def test_rule_without_remaining_service_is_empty(self) -> None:
        assert build_rule_services(build_rule(ip_protocols=["TCP"], ports=[22], exclude_ports=[22])) == []
        assert build_rule_services(build_rule(ip_protocols=["GRE"])) == []
