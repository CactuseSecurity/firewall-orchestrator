from __future__ import annotations

import hashlib
from dataclasses import dataclass
from typing import Any

from fwo_api import FwoApi
from fwo_base import replace_none_with_empty
from fwo_const import GRAPHQL_QUERY_PATH
from fwo_encrypt import decrypt, decrypt_aes_ciphertext, read_main_key
from fwo_exceptions import (
    FwLoginFailedError,
    FwoApiFailureError,
    SecretDecryptionFailedError,
)
from models.credential import AdditionalCredential
from models.gateway import Gateway
from models.management import Management

IMPORTER_HOST_NAME_SETTING_LEVELS: list[str] = ["management", "device_type", "global"]


@dataclass
class DeviceInfo:
    name: str = ""
    type_name: str = ""
    type_version: str = ""


@dataclass
class ConnectionInfo:
    hostname: str = ""
    port: int = 443


@dataclass
class CredentialInfo:
    secret: str = ""
    import_user: str = ""
    additional_credential: AdditionalCredential | None = None


@dataclass
class ManagerInfo:
    is_super_manager: bool = False
    sub_manager_ids: list[int] | None = None
    sub_managers: list[Management] | None = None


@dataclass
class DomainInfo:
    domain_name: str | None = None
    domain_uid: str | None = None


class ManagementController(Management):
    def __init__(
        self,
        mgm_id: int,
        uid: str,
        devices: list[dict[str, Any]],
        device_info: DeviceInfo,
        connection_info: ConnectionInfo,
        importer_hostname: str,
        credential_info: CredentialInfo,
        manager_info: ManagerInfo,
        domain_info: DomainInfo,
        import_disabled: bool = False,
    ):
        super().__init__(
            mgm_id=mgm_id,
            uid=uid,
            devices=devices,
            import_disabled=import_disabled,
            name=device_info.name,
            device_type_name=device_info.type_name,
            device_type_version=device_info.type_version,
            hostname=connection_info.hostname,
            port=connection_info.port,
            importer_hostname=importer_hostname,
            import_user=credential_info.import_user,
            secret=credential_info.secret,
            is_super_manager=manager_info.is_super_manager,
            sub_manager_ids=manager_info.sub_manager_ids or [],
            sub_managers=manager_info.sub_managers or [],
            current_mgm_id=mgm_id,
            current_mgm_is_super_manager=manager_info.is_super_manager,
            domain_name=domain_info.domain_name,
            domain_uid=domain_info.domain_uid,
            additional_credential=credential_info.additional_credential,
        )

    @classmethod
    def from_json(cls, json_dict: dict[str, Any]) -> ManagementController:
        device_info = DeviceInfo(
            name=json_dict["name"],
            type_name=json_dict["deviceType"]["name"],
            type_version=json_dict["deviceType"]["version"],
        )

        connection_info = ConnectionInfo(
            hostname=json_dict["hostname"],
            port=json_dict["port"],
        )

        credential_info = CredentialInfo(
            import_user=json_dict["import_credential"]["user"],
            secret=json_dict["import_credential"]["secret"],
            additional_credential=(
                AdditionalCredential(**json_dict["import_credential"]["additional_credential"])
                if json_dict["import_credential"].get("additional_credential") is not None
                else None
            ),
        )

        manager_info = ManagerInfo(
            is_super_manager=json_dict["isSuperManager"],
            sub_manager_ids=[subManager["id"] for subManager in json_dict["subManagers"]],
            sub_managers=[cls.from_json(subManager) for subManager in json_dict["subManagers"]],
        )
        domain_info = DomainInfo(domain_name=json_dict.get("configPath"), domain_uid=json_dict.get("domainUid"))

        return cls(
            mgm_id=json_dict["id"],
            uid=json_dict["uid"],
            devices=json_dict["devices"],
            device_info=device_info,
            connection_info=connection_info,
            importer_hostname=json_dict.get("importerHostname", ""),
            credential_info=credential_info,
            manager_info=manager_info,
            domain_info=domain_info,
            import_disabled=json_dict["importDisabled"],
        )

    def __str__(self):
        return f"{self.hostname}({self.mgm_id})"

    # TODO: fix device type URIs
    def build_fw_api_string(self):
        if self.device_type_name == "Check Point":
            return f"https://{self.hostname}:{self.port!s}/web_api/"
        if self.device_type_name == "CiscoFMC":
            return f"https://{self.hostname}:{self.port!s}/api/fmc_platform/v1/"
        if self.device_type_name == "Fortinet":
            return f"https://{self.hostname}:{self.port!s}/api/v2/"
        if self.device_type_name in {"FortiAdom", "FortiManager"}:
            return f"https://{self.hostname}:{self.port!s}/jsonrpc"
        if self.device_type_name == "PaloAlto":
            return f"https://{self.hostname}:{self.port!s}/restapi/v10.0/"
        raise FwLoginFailedError(f"Unsupported device type: {self.device_type_name}")

    def get_domain_string(self) -> str:
        return self.domain_uid if self.domain_uid is not None else self.domain_name  # type: ignore #TODO: check if None check is needed if yes, change type  # noqa: PGH003

    @classmethod
    def build_gateway_list(cls, mgm_details: ManagementController) -> list[Gateway]:
        devs: list[Gateway] = []
        for dev in mgm_details.devices:
            # check if gateway import is enabled
            if dev.get("do_not_import"):
                continue
            devs.append(
                Gateway(
                    Name=dev["name"],
                    Uid=f"{dev['name']}/{mgm_details.calc_manager_uid_hash()}",
                )
            )
        return devs

    def calc_manager_uid_hash(self):
        combination = f"""
            {replace_none_with_empty(self.hostname)}
            {replace_none_with_empty(str(self.port))}
            {replace_none_with_empty(self.domain_uid)}
            {replace_none_with_empty(self.domain_name)}
        """
        return hashlib.sha256(combination.encode()).hexdigest()

    def get_mgm_details(self, api_conn: FwoApi, mgm_id: int) -> dict[str, Any]:
        get_mgm_details_query = FwoApi.get_graphql_code(
            [
                GRAPHQL_QUERY_PATH + "device/getSingleManagementDetails.graphql",
                GRAPHQL_QUERY_PATH + "device/fragments/managementDetails.graphql",
                GRAPHQL_QUERY_PATH + "device/fragments/subManagements.graphql",
                GRAPHQL_QUERY_PATH + "device/fragments/deviceTypeDetails.graphql",
                GRAPHQL_QUERY_PATH + "device/fragments/importCredentials.graphql",
            ]
        )

        api_call_result = api_conn.call(get_mgm_details_query, query_variables={"mgmId": mgm_id})
        if (
            "data" not in api_call_result
            or "management" not in api_call_result["data"]
            or len(api_call_result["data"]["management"]) < 1
        ):
            raise FwoApiFailureError("did not succeed in getting management details from FWO API")

        if "://" not in api_call_result["data"]["management"][0]["hostname"]:
            # only decrypt if we have a real management and are not fetching the config from an URL
            # decrypt secret read from API
            try:
                secret = api_call_result["data"]["management"][0]["import_credential"]["secret"]
                decrypted_secret = decrypt(secret, read_main_key())
            except Exception:
                raise SecretDecryptionFailedError
            api_call_result["data"]["management"][0]["import_credential"]["secret"] = decrypted_secret
            if "subManagers" in api_call_result["data"]["management"][0]:
                for sub_mgm in api_call_result["data"]["management"][0]["subManagers"]:
                    try:
                        secret = sub_mgm["import_credential"]["secret"]
                        decrypted_secret = decrypt(secret, read_main_key())
                    except Exception:
                        raise SecretDecryptionFailedError
                    sub_mgm["import_credential"]["secret"] = decrypted_secret
        management = api_call_result["data"]["management"][0]
        self.decrypt_additional_credentials(management)
        management["importerHostname"] = self.get_importer_host_name(api_conn, mgm_id, management["deviceType"]["id"])
        return management

    @staticmethod
    def get_importer_host_name(api_conn: FwoApi, mgm_id: int, device_type_id: int) -> str:
        """
        Resolve the device specific setting importerHostName of a management.

        The setting is inherited along Global > device type > management; the most specific level that sets it wins.
        An empty result means that every importer is responsible for the management.
        """
        query = FwoApi.get_graphql_code([GRAPHQL_QUERY_PATH + "deviceSpecificSettings/getImporterHostName.graphql"])
        query_variables: dict[str, Any] = {"deviceTypeKey": str(device_type_id), "managementKey": str(mgm_id)}
        api_call_result = api_conn.call(query, query_variables=query_variables)
        nodes: list[dict[str, Any]] | None = api_call_result.get("data", {}).get("device_specific_config_node")
        if nodes is None:
            raise FwoApiFailureError("did not succeed in getting the importer host name from FWO API")

        host_names_by_level: dict[str, Any] = {
            node["node_type"]: node["values"][0]["config_value"] for node in nodes if node.get("values")
        }
        for level in IMPORTER_HOST_NAME_SETTING_LEVELS:
            host_name = host_names_by_level.get(level)
            if isinstance(host_name, str):
                return host_name.strip()
        return ""

    @staticmethod
    def decrypt_additional_credentials(management: dict[str, Any]) -> None:
        """Decrypt linked secrets for the management and its queried sub-managers, failing closed."""
        for manager in [management, *management.get("subManagers", [])]:
            credential: dict[str, Any] = manager.get("import_credential") or {}
            additional_credential: dict[str, Any] | None = credential.get("additional_credential")
            if additional_credential is None:
                continue
            try:
                additional_credential["secret"] = decrypt_aes_ciphertext(
                    additional_credential["secret"], read_main_key()
                )
            except Exception:
                raise SecretDecryptionFailedError from None
