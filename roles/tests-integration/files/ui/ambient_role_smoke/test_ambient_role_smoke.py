from __future__ import annotations

import os
from collections.abc import Callable, Generator, Sequence
from dataclasses import dataclass
from typing import Final, TypeAlias
from urllib.parse import urljoin

import pytest

pytest.importorskip("playwright.sync_api")
from playwright.sync_api import (
    Browser,
    BrowserContext,
    ConsoleMessage,
    Locator,
    Page,
    Playwright,
    Response,
    expect,
    sync_playwright,
)


RouteAction: TypeAlias = Callable[[Page], None]
Credentials: TypeAlias = tuple[str, str]

DEFAULT_ROUTES: Final[Sequence[str]] = [
    "/settings/user",
    "/settings/language",
    "/settings/report",
    "/settings/modellingpersonal",
    "/settings/recertificationpersonal",
    "/report/generation",
    "/report/schedule",
    "/report/archive",
    "/networkmodelling",
    "/certification",
    "/request/tickets",
    "/request/ticketsoverview",
    "/request/approvals",
    "/request/plannings",
    "/request/implementations",
    "/request/reviews",
    "/monitoring",
    "/monitoring/import_status",
    "/monitoring/alerts",
    "/monitoring/modelling_requests",
    "/monitoring/requested_interfaces",
    "/compliance/checks",
    "/compliance/matrix",
    "/compliance/policies",
    "/network_analysis",
]

ROLE_ERROR_PATTERNS: Final[Sequence[str]] = [
    "requires an explicit role",
    "User has none of the required roles",
    "x-hasura-role",
    "GraphQL API call requires",
]

IGNORED_CONSOLE_FRAGMENTS: Final[Sequence[str]] = [
    "favicon",
    "DevTools failed to load source map",
]


@dataclass
class BrowserErrors:
    console_errors: list[str]
    page_errors: list[str]
    failed_responses: list[str]

    @classmethod
    def empty(cls) -> BrowserErrors:
        console_errors: list[str] = []
        page_errors: list[str] = []
        failed_responses: list[str] = []
        return cls(console_errors=console_errors, page_errors=page_errors, failed_responses=failed_responses)

    def clear(self) -> None:
        self.console_errors.clear()
        self.page_errors.clear()
        self.failed_responses.clear()

    def assert_clean(self, route: str, page: Page) -> None:
        body_locator: Locator = page.locator("body")
        body_text: str = body_locator.inner_text(timeout=5_000)
        role_errors: list[str] = []
        for role_error_pattern in ROLE_ERROR_PATTERNS:
            pattern: str = role_error_pattern
            if pattern in body_text:
                role_errors.append(pattern)
        failures: list[str] = [
            *self.console_errors,
            *self.page_errors,
            *self.failed_responses,
            *[f"body contains '{pattern}'" for pattern in role_errors],
        ]
        failure_message: str = f"{route} had browser/API failures:\n" + "\n".join(failures)
        assert not failures, failure_message


@dataclass
class UiSession:
    page: Page
    errors: BrowserErrors


@pytest.fixture(scope="session")
def base_url() -> str:
    value: str = os.environ.get("FWO_UI_BASE_URL", "").strip()
    if not value:
        pytest.skip("FWO_UI_BASE_URL is required for UI ambient role smoke tests.")
    normalized_base_url: str = value if value.endswith("/") else f"{value}/"
    return normalized_base_url


@pytest.fixture(scope="session")
def credentials() -> Credentials:
    username: str = os.environ.get("FWO_UI_USERNAME", "").strip()
    password: str = os.environ.get("FWO_UI_PASSWORD", "")
    if not username or not password:
        pytest.skip("FWO_UI_USERNAME and FWO_UI_PASSWORD are required.")
    configured_credentials: Credentials = (username, password)
    return configured_credentials


@pytest.fixture(scope="session")
def routes() -> list[str]:
    configured_routes: str = os.environ.get("FWO_UI_ROUTES", "").strip()
    if configured_routes:
        configured_route_items: list[str] = configured_routes.split(",")
        parsed_routes: list[str] = []
        for configured_route_item in configured_route_items:
            parsed_route: str = configured_route_item.strip()
            if parsed_route:
                parsed_routes.append(parsed_route)
        return parsed_routes
    default_routes: list[str] = list(DEFAULT_ROUTES)
    return default_routes


@pytest.fixture(scope="session")
def browser() -> Generator[Browser, None, None]:
    headless_value: str = os.environ.get("FWO_UI_HEADLESS", "true")
    headless: bool = headless_value.lower() != "false"
    executable_path: str = os.environ.get("FWO_UI_BROWSER_EXECUTABLE", "").strip()
    with sync_playwright() as playwright_context:
        active_playwright: Playwright = playwright_context
        browser_instance: Browser = active_playwright.chromium.launch(
            executable_path=executable_path or None,
            headless=headless,
        )
        try:
            yield browser_instance
        finally:
            browser_instance.close()


@pytest.fixture()
def ui_session(browser: Browser, base_url: str, credentials: Credentials) -> Generator[UiSession, None, None]:
    timeout_value: str = os.environ.get("FWO_UI_TIMEOUT_MS", "30000")
    timeout: int = int(timeout_value)
    ignore_https_errors_value: str = os.environ.get("FWO_UI_IGNORE_HTTPS_ERRORS", "true")
    ignore_https_errors: bool = ignore_https_errors_value.lower() != "false"
    context: BrowserContext = browser.new_context(ignore_https_errors=ignore_https_errors)
    context.set_default_timeout(timeout)
    test_page: Page = context.new_page()
    errors: BrowserErrors = BrowserErrors.empty()
    attach_error_handlers(test_page, errors)

    login(test_page, base_url, credentials)
    session: UiSession = UiSession(test_page, errors)
    yield session
    context.close()


def test_multi_role_user_can_visit_main_pages(ui_session: UiSession, base_url: str, routes: list[str]) -> None:
    for route in routes:
        active_route: str = route
        ui_session.errors.clear()
        go_to_route(ui_session.page, base_url, active_route)
        run_safe_route_action(ui_session.page, active_route)
        wait_for_ui_to_settle(ui_session.page)
        ui_session.errors.assert_clean(active_route, ui_session.page)


def login(page: Page, base_url: str, credentials: Credentials) -> None:
    username: str
    password: str
    username, password = credentials
    login_url: str = urljoin(base_url, "login")
    login_response: Response | None = page.goto(login_url, wait_until="domcontentloaded")
    username_input: Locator = page.locator("#UsernameInput")
    password_input: Locator = page.locator("#PasswordInput")
    submit_button: Locator = page.locator("button[type='submit']")
    username_input.fill(username)
    password_input.fill(password)
    submit_button.click()
    expect(username_input).to_have_count(0, timeout=30_000)
    if login_response is not None:
        login_status: int = login_response.status
        if login_status >= 400:
            pytest.fail(f"Login page returned HTTP {login_status}: {login_url}")
    wait_for_ui_to_settle(page)


def go_to_route(page: Page, base_url: str, route: str) -> None:
    target_url: str = urljoin(base_url, route.lstrip("/"))
    response: Response | None = page.goto(target_url, wait_until="domcontentloaded")
    if response is not None:
        response_status: int = response.status
        if response_status >= 400:
            pytest.fail(f"Route returned HTTP {response_status}: {target_url}")
    wait_for_ui_to_settle(page)


def wait_for_ui_to_settle(page: Page) -> None:
    page.wait_for_load_state("networkidle")
    spinner: Locator = page.locator(".spinner-border")
    loading_text: Locator = page.locator("text=Loading")
    spinner.wait_for(state="detached", timeout=10_000)
    loading_text.wait_for(state="detached", timeout=10_000)


def run_safe_route_action(page: Page, route: str) -> None:
    actions: dict[str, RouteAction] = {
        "/settings/language": click_first_primary_button,
        "/settings/report": click_first_primary_button,
        "/settings/modellingpersonal": click_first_primary_button,
        "/settings/recertificationpersonal": click_first_primary_button,
    }
    normalized_route: str = normalize_route(route)
    action: RouteAction | None = actions.get(normalized_route)
    if action is not None:
        action(page)


def click_first_primary_button(page: Page) -> None:
    button: Locator = page.locator("button.btn-primary:not([disabled])").first
    button_count: int = button.count()
    if button_count == 0:
        pytest.fail("Expected an enabled primary action button.")
    button.click()
    wait_for_ui_to_settle(page)


def normalize_route(route: str) -> str:
    route_without_query: str = route.split("?", 1)[0]
    route_without_fragment: str = route_without_query.split("#", 1)[0]
    normalized_route: str = "/" + route_without_fragment.strip("/")
    return normalized_route


def attach_error_handlers(page: Page, errors: BrowserErrors) -> None:
    def on_console(message: ConsoleMessage) -> None:
        message_type: str = message.type
        text: str = message.text
        record_console_error(errors, message_type, text)

    def on_page_error(error: object) -> None:
        error_text: str = str(error)
        errors.page_errors.append(error_text)

    def on_response(response: Response) -> None:
        record_failed_response(errors, response)

    page.on("console", on_console)
    page.on("pageerror", on_page_error)
    page.on("response", on_response)


def record_console_error(errors: BrowserErrors, message_type: str, text: str) -> None:
    ignored: bool = False
    for ignored_fragment in IGNORED_CONSOLE_FRAGMENTS:
        fragment: str = ignored_fragment
        if fragment in text:
            ignored = True
            break
    if message_type != "error":
        return
    if ignored:
        return
    errors.console_errors.append(f"console error: {text}")


def record_failed_response(errors: BrowserErrors, response: Response) -> None:
    status: int = response.status
    url: str = response.url
    if status not in {401, 403, 500}:
        return
    if "/api/" not in url and "/v1/graphql" not in url:
        return
    errors.failed_responses.append(f"{status} {url}")
