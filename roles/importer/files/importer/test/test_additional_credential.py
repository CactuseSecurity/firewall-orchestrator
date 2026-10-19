from __future__ import annotations

import base64
from typing import TYPE_CHECKING, Any
from unittest.mock import MagicMock

import pytest
from cryptography.hazmat.primitives import padding
from cryptography.hazmat.primitives.ciphers import Cipher, algorithms, modes

# Exercise the enable-mode helper directly without opening an SSH connection.
from fw_modules.ciscoasa9.fwcommon import _ensure_enable_mode  # pyright: ignore[reportPrivateUsage]
from fwo_exceptions import SecretDecryptionFailedError
from model_controllers.management_controller import ManagementController
from models.credential import AdditionalCredential

if TYPE_CHECKING:
    from pytest_mock import MockerFixture

k_enable_value = "decrypted-enable"
k_url_value = "enable"
k_main_key = "0123456789abcdef0123456789abcdef"


def encrypt_secret(secret: str) -> str:
    iv = b"0123456789abcdef"
    padder = padding.PKCS7(128).padder()
    plaintext = padder.update(secret.encode()) + padder.finalize()
    encryptor = Cipher(algorithms.AES(k_main_key.encode()), modes.CBC(iv)).encryptor()
    return base64.b64encode(iv + encryptor.update(plaintext) + encryptor.finalize()).decode()


@pytest.mark.parametrize("secret", ["enable-password", "", "päss:word"])
def test_decrypts_linked_credentials_of_parent_and_submanager(mocker: MockerFixture, secret: str) -> None:
    mocker.patch("model_controllers.management_controller.read_main_key", return_value=k_main_key)
    encrypted = encrypt_secret(secret)
    credential: dict[str, Any] = {"id": 2, "user": "client-id", "secret": encrypted}
    management: dict[str, Any] = {
        "import_credential": {"additional_credential": credential.copy()},
        "subManagers": [{"import_credential": {"additional_credential": credential.copy()}}],
    }

    ManagementController.decrypt_additional_credentials(management)

    assert management["import_credential"]["additional_credential"]["secret"] == secret
    assert management["subManagers"][0]["import_credential"]["additional_credential"]["secret"] == secret


@pytest.mark.parametrize("credential", [None, {}, {"additional_credential": None}])
def test_absent_additional_credential_needs_no_key(mocker: MockerFixture, credential: dict[str, Any] | None) -> None:
    read_key = mocker.patch("model_controllers.management_controller.read_main_key")
    ManagementController.decrypt_additional_credentials({"import_credential": credential})
    read_key.assert_not_called()


@pytest.mark.parametrize("secret", ["cleartext", "not-base64!", ""])
def test_invalid_additional_secret_fails_closed(mocker: MockerFixture, secret: str) -> None:
    mocker.patch("model_controllers.management_controller.read_main_key", return_value=k_main_key)
    management = {"import_credential": {"additional_credential": {"secret": secret}}}
    with pytest.raises(SecretDecryptionFailedError):
        ManagementController.decrypt_additional_credentials(management)


def test_missing_key_fails_closed(mocker: MockerFixture) -> None:
    mocker.patch("model_controllers.management_controller.read_main_key", side_effect=OSError)
    with pytest.raises(SecretDecryptionFailedError):
        ManagementController.decrypt_additional_credentials(
            {"import_credential": {"additional_credential": {"secret": encrypt_secret("password")}}}
        )


def test_url_management_still_decrypts_additional_secret(
    mocker: MockerFixture, management_controller: ManagementController
) -> None:
    mocker.patch("model_controllers.management_controller.read_main_key", return_value=k_main_key)
    mocker.patch("fwo_api.FwoApi.get_graphql_code", return_value="query")
    api = MagicMock()
    api.call.side_effect = [
        {
            "data": {
                "management": [
                    {
                        "hostname": "https://example.invalid/config",
                        "deviceType": {"id": 9},
                        "import_credential": {"additional_credential": {"secret": encrypt_secret(k_url_value)}},
                    }
                ]
            }
        },
        {"data": {"device_specific_config_node": []}},
    ]
    result = management_controller.get_mgm_details(api, 1)
    assert result["import_credential"]["additional_credential"]["secret"] == k_url_value
    assert result["importerHostname"] == ""


def test_cisco_enable_uses_additional_secret(
    mocker: MockerFixture, management_controller: ManagementController
) -> None:
    mocker.patch("fw_modules.ciscoasa9.fwcommon._get_current_prompt", side_effect=["asa>", "asa#"])
    management_controller.additional_credential = AdditionalCredential(id=2, secret=k_enable_value)
    connection = MagicMock()
    _ensure_enable_mode(connection, management_controller)
    assert connection.send_interactive.call_args.args[0][1][0] == k_enable_value


@pytest.mark.parametrize("additional", [None, {"id": 2, "user": "client-id", "secret": k_enable_value}])
def test_from_json_preserves_additional_credential(additional: dict[str, Any] | None) -> None:
    management: dict[str, Any] = {
        "id": 1,
        "uid": "manager",
        "name": "manager",
        "deviceType": {"name": "CiscoASA", "version": "9"},
        "hostname": "asa.example.invalid",
        "port": 22,
        "import_credential": {"user": "importer", "secret": "primary", "additional_credential": additional},
        "isSuperManager": False,
        "subManagers": [],
        "devices": [],
        "importerHostname": "localhost",
        "importDisabled": False,
    }
    result = ManagementController.from_json(management)
    if additional is None:
        assert result.additional_credential is None
    else:
        assert result.additional_credential is not None
        assert result.additional_credential.user == additional["user"]
        assert result.additional_credential.secret == additional["secret"]
