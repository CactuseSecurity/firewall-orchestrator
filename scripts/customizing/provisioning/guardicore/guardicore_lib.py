"""Shared helpers for Guardicore provisioning scripts."""

from __future__ import annotations

from dataclasses import dataclass
from typing import Any, cast

import requests
import urllib3

HTTP_CONTENT_TYPE_JSON: str = "application/json"
HTTP_OK: int = 200
DEFAULT_GUARDICORE_AUTH_ENDPOINT: str = "/api/v3.0/authenticate"


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
        urllib3.disable_warnings(urllib3.exceptions.InsecureRequestWarning)


def resolve_ssl_verification_settings(args: Any) -> tuple[bool | str, bool | str]:
    verify_ssl = not args.insecure
    fwo_verify: bool | str = verify_ssl
    guardicore_verify: bool | str = verify_ssl

    if args.fwo_insecure:
        fwo_verify = False
    elif args.fwo_ca_cert:
        fwo_verify = args.fwo_ca_cert

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
    payload: dict[str, str] = {"Username": user, "Password": password}
    headers: dict[str, str] = {"Content-Type": HTTP_CONTENT_TYPE_JSON}
    endpoint = middleware_url.rstrip("/") + "/api/AuthenticationToken/Get"

    with requests.Session() as session:
        apply_ssl_settings(session, verify_ssl)
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
    payload: dict[str, str] = {"username": user, "password": password}
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
    variables: dict[str, Any],
    error_cls: type[Exception],
) -> dict[str, Any]:
    headers: dict[str, str] = {
        "Content-Type": HTTP_CONTENT_TYPE_JSON,
        "Authorization": f"Bearer {config.jwt}",
        "x-hasura-role": config.role,
    }
    payload_query = " ".join(query.splitlines())
    payload: dict[str, Any] = {"query": payload_query, "variables": variables}

    with requests.Session() as session:
        apply_ssl_settings(session, config.verify_ssl)
        session.headers.update(headers)
        try:
            response = session.post(config.graphql_url, json=payload, timeout=config.timeout_seconds)
            response.raise_for_status()
        except requests.exceptions.RequestException as exc:
            raise error_cls(f"GraphQL query failed: {exc}") from exc

    result = response.json()
    if not isinstance(result, dict):
        raise error_cls("GraphQL response was not a JSON object.")
    result = cast("dict[str, Any]", result)
    if "errors" in result:
        raise error_cls(f"GraphQL returned errors: {result['errors']}")
    return result


def extract_label_items(payload: Any) -> list[dict[str, Any]]:
    if isinstance(payload, list):
        payload_list = cast("list[Any]", payload)
        return [cast("dict[str, Any]", item) for item in payload_list if isinstance(item, dict)]
    if isinstance(payload, dict):
        payload_dict = cast("dict[str, Any]", payload)
        for key in ("objects", "items", "labels", "results", "data"):
            candidate = payload_dict.get(key)
            if isinstance(candidate, list):
                candidate_list = cast("list[Any]", candidate)
                return [cast("dict[str, Any]", item) for item in candidate_list if isinstance(item, dict)]
        return [payload_dict]
    return []
