import logging
from pathlib import Path
from types import TracebackType
from typing import Any, ClassVar, Self

import pytest
import requests

import scripts.customizing.app_data_import.get_owner_data1_from_multiple_sources as source1
import scripts.customizing.app_data_import.get_owner_data2_from_csvs as source2


class FakeResponse:
    def __init__(self, status_code: int = 200, text: str = "{}") -> None:
        self.status_code = status_code
        self.text = text


class FakeSession:
    response: FakeResponse = FakeResponse()
    raise_on_request: requests.exceptions.RequestException | None = None
    calls: ClassVar[list[dict[str, Any]]] = []

    def __init__(self) -> None:
        self.verify: bool = True

    def __enter__(self) -> Self:
        return self

    def __exit__(
        self,
        exc_type: type[BaseException] | None,
        exc: BaseException | None,
        tb: TracebackType | None,
    ) -> None:
        return None

    def post(self, url: str, data: dict[str, str], timeout: tuple[int, int]) -> FakeResponse:
        if self.raise_on_request is not None:
            raise self.raise_on_request
        self.calls.append({"method": "post", "url": url, "data": data, "timeout": timeout})
        return self.response

    def get(
        self,
        url: str,
        headers: dict[str, str],
        params: dict[str, str],
        timeout: tuple[int, int],
    ) -> FakeResponse:
        if self.raise_on_request is not None:
            raise self.raise_on_request
        self.calls.append({"method": "get", "url": url, "headers": headers, "params": params, "timeout": timeout})
        return self.response


@pytest.fixture(autouse=True)
def reset_fake_session(monkeypatch: pytest.MonkeyPatch) -> None:
    FakeSession.response = FakeResponse()
    FakeSession.raise_on_request = None
    FakeSession.calls = []
    monkeypatch.setattr(source1.requests, "Session", FakeSession)


def test_source1_get_existing_owner_ids_deduplicates_values() -> None:
    assert source1.get_existing_owner_ids(
        [
            {"app_id_external": "APP-001"},
            {"app_id_external": "APP-001"},
            {"name": "missing"},
            {"app_id_external": "APP-002"},
        ]
    ) == ["APP-001", "APP-002"]


def test_source1_build_dn_and_network_helpers(monkeypatch: pytest.MonkeyPatch) -> None:
    def fake_gethostbyaddr(ip_address: str) -> tuple[str, list[str], list[str]]:
        return "host.example.org", [], [ip_address]

    monkeypatch.setattr(source1.socket, "gethostbyaddr", fake_gethostbyaddr)

    assert source1.build_dn("alice", "CN={USERID},OU=Users") == "CN=alice,OU=Users"
    assert source1.get_network_borders("10.0.0.1/30") == ("10.0.0.0", "10.0.0.3", "network")
    assert source1.get_network_borders("10.0.0.5") == ("10.0.0.5", "10.0.0.5", "host")
    assert source1.reverse_dns_lookup("10.0.0.5") == "host.example.org"


def test_source1_extract_socket_info_resolves_hosts_and_objects(monkeypatch: pytest.MonkeyPatch) -> None:
    def reverse_dns_lookup(_ip: str) -> str:
        return "srv.example.org"

    monkeypatch.setattr(source1, "reverse_dns_lookup", reverse_dns_lookup)
    asset = {
        "assets": {"values": ["10.0.0.5", "10.0.1.0/30"]},
        "objects": [{"name": "object-net", "values": ["10.0.2.0/30"]}],
    }

    sockets = source1.extract_socket_info(asset, [])

    assert sockets == [
        {"ip": "10.0.0.5", "ip_end": "10.0.0.5", "type": "host", "name": "srv.example.org"},
        {"ip": "10.0.1.0", "ip_end": "10.0.1.3", "type": "network", "name": "NET-10.0.1.0"},
        {"name": "object-net", "ip": "10.0.2.0", "ip_end": "10.0.2.3", "type": "network"},
    ]


def test_source1_rlm_login_and_get_owners_use_expected_auth_shapes() -> None:
    FakeSession.response = FakeResponse(text='{"access_token": "token"}')

    assert source1.rlm_login("user", "secret", "https://rlm/login") == "token"
    assert FakeSession.calls[0]["data"]["client_id"] == "securechange"

    FakeSession.response = FakeResponse(text='{"owners": []}')
    assert source1.rlm_get_owners("token", "https://rlm/owners", rlm_version=2.5) == {"owners": []}
    assert FakeSession.calls[1]["headers"]["Authorization"] == "Bearer token"

    FakeSession.response = FakeResponse(text='{"owners": []}')
    source1.rlm_get_owners("token", "https://rlm/owners", rlm_version=2.6)
    assert FakeSession.calls[2]["params"] == {"access_token": "token"}


def test_source1_api_errors_are_wrapped() -> None:
    FakeSession.response = FakeResponse(status_code=401, text="denied")
    with pytest.raises(source1.ApiLoginFailedError):
        source1.rlm_login("user", "secret", "https://rlm/login")

    FakeSession.response = FakeResponse(status_code=500, text="failed")
    with pytest.raises(source1.ApiFailureError):
        source1.rlm_get_owners("token", "https://rlm/owners")

    FakeSession.raise_on_request = requests.exceptions.RequestException("down")
    with pytest.raises(source1.ApiServiceUnavailableError):
        source1.rlm_get_owners("token", "https://rlm/owners")


def test_source2_build_dn_and_extract_app_data_from_csv_file(tmp_path: Path, monkeypatch: pytest.MonkeyPatch) -> None:
    monkeypatch.setattr(source2, "repo_target_dir", str(tmp_path))
    monkeypatch.setattr(source2, "ldap_path", "CN={USERID},OU=Users", raising=False)
    monkeypatch.setattr(source2, "logger", logging.getLogger("legacy-source2-test"))
    owner_csv = tmp_path / "owners.csv"
    owner_csv.write_text(
        "name,id,c2,biso,tiso\n"
        "Payments,APP-001,x,biso-user,tiso-user\n"
        "Duplicate,APP-001,x,biso-user,tiso-user\n"
        "Ignored,XYZ-001,x,biso-user,tiso-user\n",
        encoding="utf-8",
    )
    ip_csv = tmp_path / "ips.csv"
    ip_csv.write_text(
        "c0,c1,id,c3,c4,c5,c6,c7,c8,c9,c10,c11,ip\n"
        "x,x,APP-001,x,x,x,x,x,x,x,x,x,10.0.0.5\n"
        "x,x,APP-001,x,x,x,x,x,x,x,x,x,10.0.0.5\n"
        "x,x,APP-002,x,x,x,x,x,x,x,x,x,10.0.0.6\n",
        encoding="utf-8",
    )
    app_data: dict[str, dict[str, Any]] = {}

    assert source2.build_dn("alice", "CN={USERID},OU=Users") == "CN=alice,OU=Users"
    source2.extract_app_data_from_csv_file("owners.csv", app_data, contains_ip=False)
    source2.extract_app_data_from_csv_file("ips.csv", app_data, contains_ip=True)

    assert app_data == {
        "APP-001": {
            "app_id_external": "APP-001",
            "name": "Payments",
            "BISO": "CN=biso-user,OU=Users",
            "modellers": [],
            "import_source": source2.import_source_string,
            "app_servers": [{"ip": "10.0.0.5", "ip_end": "10.0.0.5", "type": "host", "name": "host_10.0.0.5"}],
        }
    }


def test_source2_extract_app_data_exits_when_csv_cannot_be_read(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    monkeypatch.setattr(source2, "repo_target_dir", str(tmp_path))
    monkeypatch.setattr(source2, "logger", logging.getLogger("legacy-source2-test"))

    with pytest.raises(SystemExit):
        source2.extract_app_data_from_csv_file("missing.csv", {}, contains_ip=False)
