"""Tests for the internal TLS identity used on FWO API and middleware calls."""

import json
from pathlib import Path
from typing import Any

import pytest
import requests
from fwo_api import FwoApi
from fwo_config import read_config, read_tls_identity
from fwo_exceptions import FwoImporterError
from services.enums import Lifetime, Services
from services.service_provider import ServiceProvider

CLIENT_CERTIFICATE = "/etc/fworch/secrets/client/client.crt"
CLIENT_PRIVATE_KEY = "/etc/fworch/secrets/client/client.key"
CA_CERTIFICATE = "/etc/fworch/fworch-internal-ca.crt"


def write_config_file(tmp_path: Path, overrides: dict[str, Any] | None = None) -> str:
    """Write a fworch.json fixture and return its path."""
    config: dict[str, Any] = {
        "middleware_uri": "https://127.0.0.1:8443/",
        "api_uri": "https://127.0.0.1:9443/api/v1/graphql",
        "product_version": "9.2.4",
        "tls_client_certificate": CLIENT_CERTIFICATE,
        "tls_client_private_key": CLIENT_PRIVATE_KEY,
        "tls_ca_certificate": CA_CERTIFICATE,
    }
    if overrides is not None:
        config.update(overrides)
    config_file = tmp_path / "fworch.json"
    config_file.write_text(json.dumps(config), encoding="utf-8")
    return str(config_file)


def use_config_file(config_file: str, monkeypatch: pytest.MonkeyPatch) -> None:
    """
    Point the config location at a fixture instead of /etc/fworch/fworch.json.

    Only the location is redirected: replacing the reader would leave the reader itself
    untested, which is how the ServiceProvider coupling went unnoticed.
    """
    read_tls_identity.cache_clear()
    monkeypatch.setattr("fwo_config.FWO_CONFIG_FILE", config_file)


class TestReadConfig:
    def test_read_config_exposes_tls_identity(self, tmp_path: Path, monkeypatch: pytest.MonkeyPatch) -> None:
        importer_password = tmp_path / "importer_pwd"
        importer_password.write_text("secret\n", encoding="utf-8")
        monkeypatch.setattr("fwo_config.IMPORTER_PWD_FILE", str(importer_password))

        config = read_config(write_config_file(tmp_path))

        assert config["tls_client_certificate"] == CLIENT_CERTIFICATE
        assert config["tls_client_private_key"] == CLIENT_PRIVATE_KEY
        assert config["tls_ca_certificate"] == CA_CERTIFICATE

    def test_read_config_exits_when_ca_certificate_missing(
        self, tmp_path: Path, monkeypatch: pytest.MonkeyPatch, caplog: pytest.LogCaptureFixture
    ) -> None:
        importer_password = tmp_path / "importer_pwd"
        importer_password.write_text("secret\n", encoding="utf-8")
        monkeypatch.setattr("fwo_config.IMPORTER_PWD_FILE", str(importer_password))
        config_file = tmp_path / "fworch.json"
        config_file.write_text(
            json.dumps(
                {
                    "middleware_uri": "https://127.0.0.1:8443/",
                    "api_uri": "https://127.0.0.1:9443/api/v1/graphql",
                    "product_version": "9.2.4",
                    "tls_client_certificate": CLIENT_CERTIFICATE,
                    "tls_client_private_key": CLIENT_PRIVATE_KEY,
                }
            ),
            encoding="utf-8",
        )

        read_tls_identity.cache_clear()

        with pytest.raises(SystemExit):
            read_config(str(config_file))

        # asserting only SystemExit let a regression through where the operator was told
        # "unspecified error" instead of which key is missing
        assert any("tls_ca_certificate" in message for message in caplog.messages), (
            f"the missing key must be named, got: {caplog.messages}"
        )


class TestConfigureInternalApiSession:
    def test_session_presents_client_identity_and_internal_ca(
        self, tmp_path: Path, monkeypatch: pytest.MonkeyPatch
    ) -> None:
        use_config_file(write_config_file(tmp_path), monkeypatch)

        with requests.Session() as session:
            FwoApi._configure_internal_api_session(session)  # pyright: ignore[reportPrivateUsage]

            assert session.cert == (CLIENT_CERTIFICATE, CLIENT_PRIVATE_KEY)
            # verify must name the CA file: requests resolves True to certifi,
            # which does not contain the internal CA.
            assert session.verify == CA_CERTIFICATE
            assert session.verify is not True
            assert session.verify is not False

    def test_identity_survives_service_provider_reset(self, tmp_path: Path, monkeypatch: pytest.MonkeyPatch) -> None:
        """
        main_loop resets the ServiceProvider after every management and then logs in
        again on the next cycle. Resolving the identity through the container made the
        importer fail every cycle after the first, so it must not depend on it.
        """
        config_file = write_config_file(tmp_path)
        use_config_file(config_file, monkeypatch)
        ServiceProvider().register(Services.FWO_CONFIG, lambda: read_config(config_file), Lifetime.SINGLETON)

        with requests.Session() as first_cycle:
            FwoApi._configure_internal_api_session(first_cycle)  # pyright: ignore[reportPrivateUsage]
        assert first_cycle.verify == CA_CERTIFICATE

        ServiceProvider().reset()

        with requests.Session() as second_cycle:
            FwoApi._configure_internal_api_session(second_cycle)  # pyright: ignore[reportPrivateUsage]

        assert second_cycle.verify == CA_CERTIFICATE
        assert second_cycle.cert == (CLIENT_CERTIFICATE, CLIENT_PRIVATE_KEY)

    def test_missing_identity_reports_the_offending_key(self, tmp_path: Path) -> None:
        read_tls_identity.cache_clear()
        config_file = write_config_file(tmp_path)
        Path(config_file).write_text(json.dumps({"api_uri": "https://api/"}), encoding="utf-8")

        with pytest.raises(FwoImporterError, match="tls_client_certificate"):
            read_tls_identity(config_file)

    def test_missing_config_file_is_reported(self, tmp_path: Path) -> None:
        read_tls_identity.cache_clear()

        with pytest.raises(FwoImporterError, match="unable to access"):
            read_tls_identity(str(tmp_path / "absent.json"))
