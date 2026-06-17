# Ambient Role UI Smoke Tests

These tests exercise the Blazor UI with a multi-role user and fail when a routed
page issues GraphQL calls without an explicit or ambient role.

Run them against an installed FWO instance:

```bash
python3 -m venv /tmp/fwo-ui-smoke
/tmp/fwo-ui-smoke/bin/pip install -r roles/tests-integration/files/ui/ambient_role_smoke/requirements.txt
/tmp/fwo-ui-smoke/bin/playwright install chromium
FWO_UI_BASE_URL=https://fworch.example \
FWO_UI_USERNAME=user1_test \
FWO_UI_PASSWORD=secret \
/tmp/fwo-ui-smoke/bin/pytest -q roles/tests-integration/files/ui/ambient_role_smoke
```

Optional environment variables:

- `FWO_UI_ROUTES`: comma-separated routes to visit instead of the default route set.
- `FWO_UI_HEADLESS`: set to `false` to show the browser.
- `FWO_UI_IGNORE_HTTPS_ERRORS`: defaults to `true` for test installations.
- `FWO_UI_TIMEOUT_MS`: Playwright timeout in milliseconds.

The default route set expects a user that can access reporting, modelling,
recertification, workflow, monitoring, compliance, network analysis, and personal
settings pages.
