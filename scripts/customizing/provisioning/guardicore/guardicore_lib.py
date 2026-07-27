"""Shared helpers for Guardicore provisioning scripts."""

from __future__ import annotations

import functools
import json
from dataclasses import dataclass
from typing import Any, TypeAlias, cast

import requests
import urllib3

HTTP_CONTENT_TYPE_JSON: str = "application/json"
HTTP_OK: int = 200
DEFAULT_GUARDICORE_AUTH_ENDPOINT: str = "/api/v3.0/authenticate"
FWO_CONFIG_FILE: str = "/etc/fworch/fworch.json"
JsonDict: TypeAlias = dict[str, Any]
JsonList: TypeAlias = list[Any]


@dataclass(frozen=True)
class GuardicoreConfig:
    base_url: str
    token: str
    verify_ssl: bool | str
    timeout_seconds: int


@dataclass(frozen=True)
class FwoConfig:
    graphql_url: str
    jwt: str
    verify_ssl: bool | str
    timeout_seconds: int
    role: str


def apply_ssl_settings(session: requests.Session, verify_setting: bool | str) -> None:
    session.verify = verify_setting
    if verify_setting is False:
        urllib3_module = cast("Any", urllib3)
        urllib3_exceptions = urllib3_module.exceptions
        disable_warnings = urllib3_module.disable_warnings
        insecure_warning = getattr(urllib3_exceptions, "InsecureRequestWarning", Warning)
        disable_warnings(insecure_warning)


@functools.lru_cache(maxsize=1)
def read_fwo_tls_config(fwo_config_filename: str) -> JsonDict:
    """
    Read the local FWO TLS paths, or return an empty mapping when there is no config file.

    These scripts also run against remote FWO installations, where there is no local
    fworch.json to read, so a missing file is not an error here. A file that exists but
    cannot be parsed is a real misconfiguration and is raised rather than silently
    degrading to "no client identity", which would surface as an opaque TLS failure.
    """
    try:
        with open(fwo_config_filename, encoding="utf-8") as config_file:
            return cast("JsonDict", json.load(config_file))
    except OSError:
        return {}


def load_fwo_client_identity(fwo_config_filename: str | None = None) -> tuple[str, str] | None:
    """Return the local FWO client certificate pair, which the API vhost requires."""
    config = read_fwo_tls_config(fwo_config_filename or FWO_CONFIG_FILE)
    certificate = config.get("tls_client_certificate")
    private_key = config.get("tls_client_private_key")
    if isinstance(certificate, str) and isinstance(private_key, str):
        return (certificate, private_key)
    return None


def load_fwo_ca_certificate(fwo_config_filename: str | None = None) -> str | None:
    """Return the internal CA bundle, which is absent from the system and certifi stores."""
    ca_certificate = read_fwo_tls_config(fwo_config_filename or FWO_CONFIG_FILE).get("tls_ca_certificate")
    return ca_certificate if isinstance(ca_certificate, str) else None


def apply_fwo_ssl_settings(session: requests.Session, verify_setting: bool | str) -> None:
    """Apply verification and present the FWO client identity when one is installed."""
    apply_ssl_settings(session, verify_setting)
    client_identity = load_fwo_client_identity()
    if client_identity is not None:
        session.cert = client_identity


def resolve_ssl_verification_settings(args: Any) -> tuple[bool | str, bool | str]:
    verify_ssl = not args.insecure
    fwo_verify: bool | str = verify_ssl
    guardicore_verify: bool | str = verify_ssl

    if args.fwo_insecure:
        fwo_verify = False
    elif args.fwo_ca_cert:
        fwo_verify = args.fwo_ca_cert
    elif verify_ssl:
        # FWO serves an internal CA certificate that no default trust store knows.
        fwo_verify = load_fwo_ca_certificate() or verify_ssl

    if args.guardicore_insecure:
        guardicore_verify = False
    elif args.guardicore_ca_cert:
        guardicore_verify = args.guardicore_ca_cert

    return fwo_verify, guardicore_verify


def login_fwo(
    user: str,
    password: str,
    middleware_url: str,
    verify_ssl: bool | str,
    timeout: int,
    error_cls: type[Exception],
) -> str:
    payload: dict[str, Any] = {"Username": user, "Password": password}
    headers: dict[str, str] = {"Content-Type": HTTP_CONTENT_TYPE_JSON}
    endpoint = middleware_url.rstrip("/") + "/api/AuthenticationToken/Get"

    with requests.Session() as session:
        apply_fwo_ssl_settings(session, verify_ssl)
        try:
            response = session.post(endpoint, json=payload, headers=headers, timeout=timeout)
        except requests.exceptions.RequestException as exc:
            raise error_cls(f"FWO login failed for {endpoint}: {exc}") from exc

    if response.status_code != HTTP_OK:
        raise error_cls(f"FWO login failed with status {response.status_code}: {response.text}")
    return response.text


def login_guardicore(
    user: str,
    password: str,
    base_url: str,
    verify_ssl: bool | str,
    timeout: int,
    error_cls: type[Exception],
) -> str:
    payload: dict[str, Any] = {"username": user, "password": password}
    headers: dict[str, str] = {"Content-Type": HTTP_CONTENT_TYPE_JSON}
    endpoint = base_url.rstrip("/") + DEFAULT_GUARDICORE_AUTH_ENDPOINT

    with requests.Session() as session:
        apply_ssl_settings(session, verify_ssl)
        try:
            response = session.post(endpoint, json=payload, headers=headers, timeout=timeout)
            response.raise_for_status()
        except requests.exceptions.RequestException as exc:
            raise error_cls(f"Guardicore login failed for {endpoint}: {exc}") from exc

    try:
        result = response.json()
    except ValueError as exc:
        raise error_cls("Guardicore login response was not valid JSON.") from exc

    for token_key in ("access_token", "token", "jwt", "accessToken"):
        token = result.get(token_key)
        if isinstance(token, str) and token:
            return token

    raise error_cls(f"Guardicore login response did not include a token: {result}")


def run_graphql_query(
    config: FwoConfig,
    query: str,
    variables: JsonDict,
    error_cls: type[Exception],
) -> JsonDict:
    headers: dict[str, str] = {
        "Content-Type": HTTP_CONTENT_TYPE_JSON,
        "Authorization": f"Bearer {config.jwt}",
        "x-hasura-role": config.role,
    }
    payload_query = " ".join(query.splitlines())
    payload: JsonDict = {"query": payload_query, "variables": variables}

    with requests.Session() as session:
        apply_fwo_ssl_settings(session, config.verify_ssl)
        session.headers.update(headers)
        try:
            response = session.post(config.graphql_url, json=payload, timeout=config.timeout_seconds)
            response.raise_for_status()
        except requests.exceptions.RequestException as exc:
            raise error_cls(f"GraphQL query failed: {exc}") from exc

    result = response.json()
    if not isinstance(result, dict):
        raise error_cls("GraphQL response was not a JSON object.")
    result = cast("JsonDict", result)
    if "errors" in result:
        raise error_cls(f"GraphQL returned errors: {result['errors']}")
    return result


def extract_label_items(payload: Any) -> list[JsonDict]:
    if isinstance(payload, list):
        payload_list = cast("JsonList", payload)
        return [cast("JsonDict", item) for item in payload_list if isinstance(item, dict)]
    if isinstance(payload, dict):
        payload_dict = cast("JsonDict", payload)
        for key in ("objects", "items", "labels", "results", "data"):
            candidate = payload_dict.get(key)
            if isinstance(candidate, list):
                candidate_list = cast("JsonList", candidate)
                return [cast("JsonDict", item) for item in candidate_list if isinstance(item, dict)]
        return [payload_dict]
    return []
