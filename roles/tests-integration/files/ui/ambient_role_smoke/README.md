# Ambient Role UI Smoke Tests

These tests exercise the Blazor UI with a multi-role user and fail when a routed
page issues GraphQL calls without an explicit or ambient role.

Run them against an installed FWO instance through Ansible:

```bash
scripts/run-ui-ambient-role-smoke.sh
```

The Ansible task copies this C# test project to
`/tmp/fworch-ambient-role-smoke` and runs `dotnet test` with the Chrome
executable installed by FWO for PDF export.

For direct local runs without Ansible:

```bash
FWO_UI_BASE_URL=https://fworch.example \
FWO_UI_USERNAME=user1_test \
FWO_UI_PASSWORD=secret \
dotnet test roles/tests-integration/files/ui/ambient_role_smoke/FWO.Ui.AmbientRoleSmoke.csproj
```

Optional environment variables:

- `FWO_UI_ROUTES`: comma-separated routes to visit instead of the default route set.
- `FWO_UI_HEADLESS`: set to `false` to show the browser.
- `FWO_UI_IGNORE_HTTPS_ERRORS`: defaults to `true` for test installations.
- `FWO_UI_TIMEOUT_MS`: browser interaction timeout in milliseconds.
- `FWO_UI_BROWSER_EXECUTABLE`: browser path for Playwright; Ansible sets this
  to `/usr/local/fworch/bin/chrome`.

The default route set expects a user that can access reporting, modelling,
recertification, workflow, monitoring, compliance, network analysis, and personal
settings pages.
