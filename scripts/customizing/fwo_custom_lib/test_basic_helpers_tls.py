from __future__ import annotations

from typing import TYPE_CHECKING

from scripts.customizing.fwo_custom_lib import basic_helpers

if TYPE_CHECKING:
    from pathlib import Path

    import pytest


def test_resolve_requests_verify_switched_off_returns_false(tmp_path: Path) -> None:
    bundle: Path = tmp_path / "ca.crt"
    bundle.write_text("ca")

    assert basic_helpers.resolve_requests_verify(check_certificates=False, candidates=[str(bundle)]) is False


def test_resolve_requests_verify_names_host_trust_store(tmp_path: Path) -> None:
    bundle: Path = tmp_path / "ca.crt"
    bundle.write_text("ca")

    verify: bool | str = basic_helpers.resolve_requests_verify(
        check_certificates=True, candidates=[str(tmp_path / "missing.crt"), str(bundle)]
    )

    assert verify == str(bundle)


def test_resolve_requests_verify_falls_back_to_certifi(tmp_path: Path, monkeypatch: pytest.MonkeyPatch) -> None:
    class NoDefaultPaths:
        openssl_cafile: str = str(tmp_path / "missing-openssl.crt")

    monkeypatch.setattr(basic_helpers.ssl, "get_default_verify_paths", NoDefaultPaths)

    assert basic_helpers.resolve_requests_verify(check_certificates=True, candidates=[]) is True
