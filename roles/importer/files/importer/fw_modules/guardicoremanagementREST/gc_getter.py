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


def is_last_page(page_length: int, read_count: int, total: int | None) -> bool:
    """Tell whether paging is done: the total is reached, a page is empty, or without a total a page is short."""
    if page_length == 0:
        return True
    if total is not None:
        return read_count >= total
    return page_length < gc_const.GC_PAGE_SIZE


def build_page_params(
    paging: gc_const.GcPaging, read_count: int, extra_params: dict[str, Any] | None
) -> dict[str, Any]:
    """Return the query parameters of the page that starts after the objects read so far."""
    params: dict[str, Any] = dict.fromkeys(paging.start_params, read_count)
    params.update(dict.fromkeys(paging.size_params, gc_const.GC_PAGE_SIZE))
    params.update(extra_params or {})
    return params


def has_new_objects(page_objects: list[dict[str, Any]], seen_ids: set[str]) -> bool:
    """
    Tell whether a page holds an object not read before, and remember the ids of the page.

    A page of objects without id cannot be checked and counts as new.
    """
    page_ids: set[str] = {str(obj["id"]) for obj in page_objects if "id" in obj}
    if len(page_ids) == 0:
        return True
    new_ids: set[str] = page_ids - seen_ids
    seen_ids.update(page_ids)
    return len(new_ids) > 0


def get_all_objects(
    session: requests.Session,
    url: str,
    paging: gc_const.GcPaging,
    extra_params: dict[str, Any] | None = None,
) -> list[dict[str, Any]]:
    """
    Read all objects of a paginated Guardicore list endpoint.

    The v4 endpoints differ in their paging parameters, see gc_const.GcPaging.
    When the response names the total, reading goes on until it is reached, as the API may cap the page size
    below the requested limit; only without a total a short page marks the end.

    Raises:
        FwApiCallFailedError: If a call fails, paging does not end, or a page repeats objects read before, i.e. the
            management ignores the paging parameters, which would import some objects several times and others not.

    """
    objects: list[dict[str, Any]] = []
    seen_ids: set[str] = set()
    for _ in range(gc_const.GC_MAX_PAGES):
        result = get_page(session, url, build_page_params(paging, len(objects), extra_params))
        page_objects: list[dict[str, Any]] = [obj for obj in result["objects"] if isinstance(obj, dict)]
        if not has_new_objects(page_objects, seen_ids):
            raise FwApiCallFailedError(
                f"Guardicore API call to {url} returned a page of objects read before: "
                f"the management ignores the paging parameters {', '.join(paging.start_params)}."
            )
        objects.extend(page_objects)
        if is_last_page(len(result["objects"]), len(objects), get_total(result)):
            return objects
    raise FwApiCallFailedError(f"Guardicore API call to {url} exceeded {gc_const.GC_MAX_PAGES} pages.")


def get_native_config(base_url: str, user: str, password: str) -> dict[str, Any]:
    """
    Fetch labels, label groups, assets and segmentation rules from the Guardicore management.

    Returns:
        dict[str, Any]: The native config with the raw API objects per type.

    """
    with requests.Session() as session:
        # keep a CA bundle path (the host trust store); an unset certificate check (None) means verify
        session.verify = fwo_globals.verify_certs if fwo_globals.verify_certs is not None else True
        token = login(session, base_url, user, password)
        session.headers.update({"Authorization": f"Bearer {token}", "Content-Type": HTTP_CONTENT_TYPE_JSON})

        native_config: dict[str, Any] = {
            "labels": get_all_objects(
                session, base_url + gc_const.GC_LABELS_ENDPOINT, gc_const.GC_PAGING_LABELS_AND_RULES
            ),
            "label_groups": get_all_objects(
                session, base_url + gc_const.GC_LABEL_GROUPS_ENDPOINT, gc_const.GC_PAGING_LABEL_GROUPS
            ),
            "assets": get_all_objects(
                session,
                base_url + gc_const.GC_ASSETS_ENDPOINT,
                gc_const.GC_PAGING_ASSETS,
                extra_params={"expand": "labels"},
            ),
            "rules": get_all_objects(
                session, base_url + gc_const.GC_RULES_ENDPOINT, gc_const.GC_PAGING_LABELS_AND_RULES
            ),
        }
    FWOLogger.debug(
        f"fetched {len(native_config['labels'])} labels, {len(native_config['label_groups'])} label groups, "
        f"{len(native_config['assets'])} assets and {len(native_config['rules'])} rules from Guardicore"
    )
    return native_config
