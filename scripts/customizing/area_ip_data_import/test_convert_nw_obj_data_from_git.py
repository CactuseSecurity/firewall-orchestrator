# ruff: noqa: INP001
from scripts.customizing.area_ip_data_import.convertNwObjDataFromGit import (
    extract_socket_info,
    generate_public_ipv4_networks_as_internet_area,
    get_network_borders,
)

MIN_PUBLIC_IPV4_NETWORK_COUNT = 20


def test_get_network_borders_handles_host_and_network() -> None:
    assert get_network_borders("10.0.0.5") == ("10.0.0.5", "10.0.0.5", "host")
    assert get_network_borders("10.0.0.1/24") == ("10.0.0.0", "10.0.0.255", "network")


def test_extract_socket_info_reads_asset_and_object_values() -> None:
    asset = {
        "assets": {"values": ["10.0.0.1"]},
        "objects": [{"values": ["10.0.1.0/30"]}, {"ignored": True}],
    }

    assert extract_socket_info(asset, []) == [
        {"ip": "10.0.0.1", "ip-end": "10.0.0.1", "type": "host"},
        {"ip": "10.0.1.0", "ip-end": "10.0.1.3", "type": "network"},
    ]


def test_generate_public_ipv4_networks_as_internet_area() -> None:
    networks = generate_public_ipv4_networks_as_internet_area()

    assert networks[0] == {"ip": "0.0.0.0/5", "name": "inet"}
    assert networks[-1] == {"ip": "224.0.0.0/3", "name": "inet"}
    assert len(networks) > MIN_PUBLIC_IPV4_NETWORK_COUNT
