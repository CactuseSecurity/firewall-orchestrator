# library for FWORCH API calls
import argparse
import getpass
import json
import sys
from typing import Any

import requests
import urllib3

HTTP_OK: int = 200


class CustomizingError(Exception):
    """Raised for errors when calling the FWO API from customizing scripts."""


def call(
    url: str,
    jwt: str,
    query: str,
    query_variables: dict[str, Any] | str = "",
    role: str = "reporter",
) -> dict[str, Any] | None:
    request_headers: dict[str, str] = {
        "Content-Type": "application/json",
        "Authorization": "Bearer " + jwt,
        "x-hasura-role": role,
    }
    full_query: dict[str, Any] = {"query": query, "variables": query_variables}

    with requests.Session() as session:
        session.verify = False
        session.headers.update(request_headers)

        try:
            response = session.post(url, data=json.dumps(full_query), timeout=600)
            response.raise_for_status()
        except requests.exceptions.RequestException as exc:
            if exc.response is not None and exc.response.status_code != HTTP_OK:
                raise CustomizingError("fwo_api call ERROR: got error code: " + str(exc.response.status_code)) from exc
            raise CustomizingError("fwo_api call ERROR: got no result from FWO API call") from exc
        return response.json()


def login(
    user: str, password: str, user_management_api_base_url: str, method: str = "api/AuthenticationToken/Get"
) -> str:
    payload: dict[str, str] = {"Username": user, "Password": password}

    with requests.Session() as session:
        session.verify = False
        session.headers = {"Content-Type": "application/json"}

        try:
            response = session.post(user_management_api_base_url + method, data=json.dumps(payload))
        except requests.exceptions.RequestException:
            raise CustomizingError("fwo_api login ERROR: no valid response from: " + str(user_management_api_base_url))

        if response.status_code == HTTP_OK:
            return response.text  # the JWT
        raise CustomizingError(
            "fwo_api login ERROR: did not receive a JWT during login to api_url: " + str(user_management_api_base_url)
        )


def get_config_value(fwo_api_base_url: str, jwt: str, key: str = "limit") -> str | None:
    query_variables: dict[str, str] = {"key": key}
    config_query: str = "query getConf($key: String) {  config(where: {config_key: {_eq: $key}}) { config_value } }"
    result: dict[str, Any] | None = call(
        fwo_api_base_url, jwt, config_query, query_variables=query_variables, role="importer"
    )
    if result is None:
        return None
    if "data" in result and "config" in result["data"]:
        first_result: dict[str, Any] = result["data"]["config"][0]
        if "config_value" in first_result:
            return first_result["config_value"]
        return None
    return None


def get_config_values(fwo_api_base_url: str, jwt: str, key_filter: str = "limit") -> dict[str, Any] | None:
    query_variables: dict[str, str] = {"keyFilter": key_filter + "%"}
    config_query: str = "query getConf($keyFilter: String) { config(where: {config_key: {_ilike: $keyFilter}}) { config_key config_value } }"
    result: dict[str, Any] | None = call(
        fwo_api_base_url, jwt, config_query, query_variables=query_variables, role="importer"
    )
    if result is None:
        return None
    if "data" in result and "config" in result["data"]:
        result_array: list[dict[str, Any]] = result["data"]["config"]
        config_values: dict[str, Any] = {v["config_key"]: v["config_value"] for v in result_array}
        return config_values
    return None


def read_json_file(filename: str) -> dict[str, Any]:
    try:
        with open(filename, encoding="utf-8") as json_fh:
            json_dict: dict[str, Any] = json.loads(json_fh.read())
    except Exception:
        raise CustomizingError("read_json_file ERROR: while reading file: " + filename)
    return json_dict


def set_custom_txt_values(
    fwo_api_base_url: str,
    jwt: str,
    query_variables: dict[str, Any] | None = None,
) -> int | str:
    if query_variables is None:
        query_variables = {}
    custom_txt_mutation = """
        mutation upsertCustomText($id: String!, $language: String!, $txt: String!) {
            insert_customtxt(
                objects: {
                    id: $id
                    language: $language
                    txt: $txt
                },
                on_conflict: {
                    constraint: customtxt_pkey ,
                    update_columns: [txt]
                }
            ) {
                returning {
                    id: id
                }
            }
        }
    """
    result: dict[str, Any] | None = call(
        fwo_api_base_url, jwt, custom_txt_mutation, query_variables=query_variables, role="admin"
    )
    if result is None:
        return -1
    if result["data"]["insert_customtxt"]["returning"][0]["id"]:
        return result["data"]["insert_customtxt"]["returning"][0]["id"]
    return -1


def set_modelling_service_values(
    fwo_api_base_url: str,
    jwt: str,
    query_variables: dict[str, Any] | None = None,
) -> int | str:
    if query_variables is None:
        query_variables = {}
    modelling_service_mutation = """
        mutation upsertService(
            $name: String
            $app_id: Int
            $is_global: Boolean
            $port: Int
            $port_end: Int
            $proto_id: Int
            ) {
                insert_modelling_service(
                    objects: {
                        name: $name
                        app_id: $app_id
                        is_global: $is_global
                        port: $port
                        port_end: $port_end
                        proto_id: $proto_id
                    }
                    on_conflict: {
                        constraint: modelling_service_unique_name,
                        update_columns: [name, app_id, is_global, port, port_end, proto_id]
                    }
                ) {
                    returning {
                        id
                    }
                }
        }
    """

    result: dict[str, Any] | None = call(
        fwo_api_base_url, jwt, modelling_service_mutation, query_variables=query_variables, role="admin"
    )
    if result is None:
        return -1
    if result["data"]["insert_modelling_service"]["returning"][0]["id"]:
        return result["data"]["insert_modelling_service"]["returning"][0]["id"]
    return -1


def set_config_values(
    fwo_api_base_url: str,
    jwt: str,
    query_variables: dict[str, Any] | None = None,
) -> int | str:
    if query_variables is None:
        query_variables = {}
    config_mutation = """
        mutation upsertConfigItem($config_key: String!, $config_value: String!, $config_user: Int!) {
            insert_config(
                objects: {
                config_key: $config_key,
                config_value: $config_value,
                config_user: $config_user
                },
                on_conflict: {
                constraint: config_pkey,
                update_columns: [config_value]
                }
            ) {
                returning {
                id: config_key
                }
            }
        }
    """
    result: dict[str, Any] | None = call(
        fwo_api_base_url, jwt, config_mutation, query_variables=query_variables, role="admin"
    )
    if result is None:
        return -1
    if result["data"]["insert_config"]["returning"][0]["id"]:
        return result["data"]["insert_config"]["returning"][0]["id"]
    return -1


def get_credentials() -> tuple[str, str]:
    username: str = input("Enter your username: ")
    password: str = getpass.getpass("Enter your password: ")
    return username, password


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="Writing custom settings via API to firewall orchestrator")

    parser.add_argument(
        "-c",
        "--customSettingsFile",
        dest="custom_settings_file",
        required=True,
        help="Filename of custom settings file for firewall orchstrator (mandatory parameter)",
    )

    args: argparse.Namespace = parser.parse_args()

    if len(sys.argv) == 1:
        parser.print_help(sys.stderr)
        sys.exit(1)

    settings_file: str = args.custom_settings_file
    fwo_config_filename: str = "/etc/fworch/fworch.json"
    urllib3.disable_warnings()

    fwo_config: dict[str, Any] = read_json_file(fwo_config_filename)
    user_management_api_base_url: str = fwo_config["middleware_uri"]
    fwo_api_base_url: str = fwo_config["api_uri"]

    # read credentials interactively
    username: str
    password: str
    username, password = get_credentials()

    # login with the credentials to get JWT
    jwt: str = login(username, password, user_management_api_base_url, method="api/AuthenticationToken/Get")

    # read settings to write to API from file
    settings: dict[str, Any] = read_json_file(settings_file)

    # write settings to FWO API using the JWT
    # overwrites existing values making this script idempotent

    for key, values in settings.items():
        if key == "config":
            obj: dict[str, Any]
            for obj in values:
                set_config_values(fwo_api_base_url, jwt, query_variables=obj)
        elif key == "customtxt":
            for obj in values:
                set_custom_txt_values(fwo_api_base_url, jwt, query_variables=obj)
        elif key == "modelling_service":
            for obj in values:
                set_modelling_service_values(fwo_api_base_url, jwt, query_variables=obj)
