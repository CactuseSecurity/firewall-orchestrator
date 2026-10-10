from __future__ import annotations

from typing import Any, cast

import fwo_globals
import requests
from fw_modules.guardicoremanagementREST import gc_const
from fwo_const import FWO_HTTP_TIMEOUT
from fwo_exceptions import FwApiCallFailedError, FwLoginFailedError
from fwo_log import FWOLogger

HTTP_CONTENT_TYPE_JSON = "application/json"


def build_api_base_url(hostname: str, port: int | str | None) -> str:
    port_suffix = f":{port}" if port else ""
    return f"https://{hostname}{port_suffix}"


def login(session: requests.Session, base_url: str, user: str, password: str) -> str:
    """
    Log in to the Guardicore management and return the access token.

    Raises:
        FwLoginFailedError: If the login is rejected or the response holds no token.

    """
    endpoint = base_url + gc_const.GC_AUTH_ENDPOINT
    try:
        response = session.post(
            endpoint,
            json={"username": user, "password": password},
            headers={"Content-Type": HTTP_CONTENT_TYPE_JSON},
            timeout=FWO_HTTP_TIMEOUT,
        )
        response.raise_for_status()
        result = response.json()
    except (requests.exceptions.RequestException, ValueError) as exc:
        raise FwLoginFailedError(f"Guardicore login failed for {endpoint}: {exc}") from exc

    if isinstance(result, dict):
        result_dict = cast("dict[str, Any]", result)
        for token_key in gc_const.GC_TOKEN_KEYS:
            token = result_dict.get(token_key)
            if isinstance(token, str) and token:
                return token
    raise FwLoginFailedError(f"Guardicore login response from {endpoint} did not contain an access token.")


def get_page(session: requests.Session, url: str, params: dict[str, Any]) -> dict[str, Any]:
    try:
        response = session.get(url, params=params, timeout=FWO_HTTP_TIMEOUT)
        response.raise_for_status()
        result = response.json()
    except (requests.exceptions.RequestException, ValueError) as exc:
        raise FwApiCallFailedError(f"Guardicore API call to {url} failed: {exc}") from exc
    if not isinstance(result, dict):
        raise FwApiCallFailedError(f"Guardicore API call to {url} returned no object list.")
    result_dict = cast("dict[str, Any]", result)
    if not isinstance(result_dict.get("objects"), list):
        raise FwApiCallFailedError(f"Guardicore API call to {url} returned no object list.")
    return result_dict


def get_total(result: dict[str, Any]) -> int | None:
    for total_key in ("total_count", "total"):
        total = result.get(total_key)
        if isinstance(total, int):
            return total
    return None


def get_all_objects(
    session: requests.Session,
    url: str,
    offset_param: str = "offset",
    limit_param: str = "limit",
    extra_params: dict[str, Any] | None = None,
) -> list[dict[str, Any]]:
    """
    Read all objects of a paginated Guardicore list endpoint.

    The v4 endpoints differ in their paging parameters: assets use start_at/max_results, the others offset/limit.
    """
    objects: list[dict[str, Any]] = []
    for _ in range(gc_const.GC_MAX_PAGES):
        params: dict[str, Any] = {offset_param: len(objects), limit_param: gc_const.GC_PAGE_SIZE}
        params.update(extra_params or {})
        result = get_page(session, url, params)
        page_objects: list[dict[str, Any]] = [obj for obj in result["objects"] if isinstance(obj, dict)]
        objects.extend(page_objects)
        total = get_total(result)
        if len(page_objects) < gc_const.GC_PAGE_SIZE or (total is not None and len(objects) >= total):
            return objects
    raise FwApiCallFailedError(f"Guardicore API call to {url} exceeded {gc_const.GC_MAX_PAGES} pages.")


def get_native_config(base_url: str, user: str, password: str) -> dict[str, Any]:
    """
    Fetch labels, label groups, assets and segmentation rules from the Guardicore management.

    Returns:
        dict[str, Any]: The native config with the raw API objects per type.

    """
    with requests.Session() as session:
        # an unset certificate check (None) means the requests default, which verifies
        session.verify = fwo_globals.verify_certs is not False
        token = login(session, base_url, user, password)
        session.headers.update({"Authorization": f"Bearer {token}", "Content-Type": HTTP_CONTENT_TYPE_JSON})

        native_config: dict[str, Any] = {
            "labels": get_all_objects(session, base_url + gc_const.GC_LABELS_ENDPOINT),
            "label_groups": get_all_objects(session, base_url + gc_const.GC_LABEL_GROUPS_ENDPOINT),
            "assets": get_all_objects(
                session,
                base_url + gc_const.GC_ASSETS_ENDPOINT,
                offset_param="start_at",
                limit_param="max_results",
                extra_params={"expand": "labels"},
            ),
            "rules": get_all_objects(session, base_url + gc_const.GC_RULES_ENDPOINT),
        }
    FWOLogger.debug(
        f"fetched {len(native_config['labels'])} labels, {len(native_config['label_groups'])} label groups, "
        f"{len(native_config['assets'])} assets and {len(native_config['rules'])} rules from Guardicore"
    )
    return native_config
