from __future__ import annotations

import logging
from typing import TYPE_CHECKING, Any

import fwo_file_import
import fwo_globals
import pytest
from model_controllers import import_state_controller
from model_controllers.management_controller import (
    ConnectionInfo,
    CredentialInfo,
    DeviceInfo,
    DomainInfo,
    ManagementController,
    ManagerInfo,
)
from models.import_state import ImportState

if TYPE_CHECKING:
    from pathlib import Path
    from types import TracebackType


class FakeResponse:
    is_redirect: bool = False
    status_code: int = 200
    headers: dict[str, str] = {}  # noqa: RUF012

    def __enter__(self) -> FakeResponse:  # noqa: PYI034
        return self

    def __exit__(self, *args: object) -> None:
        return None

    def raise_for_status(self) -> None:
        return None

    @property
    def raw(self) -> FakeRaw:
        return FakeRaw()


class FakeRaw:
    """Serves the config like urllib3's read1: the content, then b'' at the end."""

    def __init__(self) -> None:
        self.chunks: list[bytes] = [b'{"ManagerSet": []}']

    def read1(self, amt: int, decode_content: bool) -> bytes:  # noqa: ARG002
        return self.chunks.pop(0) if self.chunks else b""


class FakeSession:
    instances: list[FakeSession] = []  # noqa: RUF012

    def __init__(self) -> None:
        self.verify: bool | str = "unset"
        self.headers: dict[str, str] = {}
        FakeSession.instances.append(self)

    def __enter__(self) -> FakeSession:  # noqa: PYI034
        return self

    def __exit__(
        self,
        exc_type: type[BaseException] | None,
        exc: BaseException | None,
        tb: TracebackType | None,
    ) -> None:
        return None

    def get(self, url: str, **kwargs: Any) -> FakeResponse:  # noqa: ARG002
        return FakeResponse()


def create_management(mgm_id: int) -> ManagementController:
    return ManagementController(
        mgm_id,
        "uid",
        [],
        DeviceInfo(name=f"mgm-{mgm_id}", type_name="Check Point", type_version="R8x"),
        ConnectionInfo(hostname="fw.example", port=443),
        "importer",
        CredentialInfo(),
        ManagerInfo(),
        DomainInfo(),
    )


@pytest.fixture(autouse=True)
def reset_globals() -> Any:
    saved_verify = fwo_globals.verify_certs
    saved_cli = fwo_globals.cli_verify_certs
    FakeSession.instances = []
    yield
    fwo_globals.verify_certs = saved_verify
    fwo_globals.cli_verify_certs = saved_cli


class TestResolveRequestsVerify:
    def test_switched_off_returns_false(self, tmp_path: Path) -> None:
        bundle = tmp_path / "ca.crt"
        bundle.write_text("ca")

        assert fwo_globals.resolve_requests_verify(check_certificates=False, candidates=[str(bundle)]) is False

    def test_switched_on_names_the_host_trust_store(self, tmp_path: Path) -> None:
        bundle = tmp_path / "ca.crt"
        bundle.write_text("ca")

        verify = fwo_globals.resolve_requests_verify(
            check_certificates=True, candidates=[str(tmp_path / "missing.crt"), str(bundle)]
        )

        assert verify == str(bundle)

    def test_switched_on_without_host_trust_store_falls_back_to_certifi(
        self, tmp_path: Path, monkeypatch: pytest.MonkeyPatch
    ) -> None:
        class NoDefaultPaths:
            openssl_cafile: str = str(tmp_path / "missing-openssl.crt")

        monkeypatch.setattr(fwo_globals.ssl, "get_default_verify_paths", NoDefaultPaths)

        verify = fwo_globals.resolve_requests_verify(
            check_certificates=True, candidates=[str(tmp_path / "missing.crt")]
        )

        assert verify is True

    def test_set_cli_verify_certs_normalises_none(self) -> None:
        fwo_globals.set_cli_verify_certs(None)
        assert fwo_globals.cli_verify_certs is False

        fwo_globals.set_cli_verify_certs(cli_verify_certs_in=True)
        assert fwo_globals.cli_verify_certs is True


class TestResolveManagementVerify:
    def test_unchecked_management_is_warned_once(self, caplog: pytest.LogCaptureFixture) -> None:
        management = create_management(987654)
        caplog.set_level(logging.WARNING)

        first = import_state_controller.resolve_management_verify(management, check_certificates=False)
        second = import_state_controller.resolve_management_verify(management, check_certificates=False)

        warnings = [
            record for record in caplog.records if "certificate checking is switched off" in record.getMessage()
        ]
        assert first is False
        assert second is False
        assert len(warnings) == 1
        assert "fw.example" in warnings[0].getMessage()

    def test_checked_management_is_not_warned(self, caplog: pytest.LogCaptureFixture) -> None:
        caplog.set_level(logging.WARNING)

        verify = import_state_controller.resolve_management_verify(create_management(987655), check_certificates=True)

        assert verify is not False
        assert not [
            record for record in caplog.records if "certificate checking is switched off" in record.getMessage()
        ]


class TestFileImportVerify:
    @staticmethod
    def read_remote_file(monkeypatch: pytest.MonkeyPatch) -> FakeSession:
        monkeypatch.setattr(fwo_file_import.requests, "Session", FakeSession)
        import_state = ImportState()
        import_state.import_file_name = "https://config.example/config.json"

        fwo_file_import.read_file(None, import_state)  # type: ignore[arg-type]

        return FakeSession.instances[-1]

    def test_uses_value_set_for_the_management(self, monkeypatch: pytest.MonkeyPatch) -> None:
        fwo_globals.verify_certs = "/etc/ssl/certs/ca-certificates.crt"

        session = self.read_remote_file(monkeypatch)

        assert session.verify == "/etc/ssl/certs/ca-certificates.crt"

    def test_keeps_switched_off_value(self, monkeypatch: pytest.MonkeyPatch) -> None:
        fwo_globals.verify_certs = False

        session = self.read_remote_file(monkeypatch)

        assert session.verify is False

    def test_fails_closed_when_never_set(self, monkeypatch: pytest.MonkeyPatch) -> None:
        fwo_globals.verify_certs = None

        session = self.read_remote_file(monkeypatch)

        assert session.verify is not False
