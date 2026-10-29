# PR 5559 Rule Positioning Review

Review of [CactuseSecurity/firewall-orchestrator#5559](https://github.com/CactuseSecurity/firewall-orchestrator/pull/5559), current head `0a179b9c5959523669da753c6cbbf241e73d0f98` against `develop` at `8933a3dd142148d1e106c8a90d0442fbfe3d1688`.

Result: changes requested. No critical or high-severity defects remain, but three medium findings should be addressed before merge. Finding numbers and statuses are matched against the existing [Round 1 review](https://github.com/CactuseSecurity/firewall-orchestrator/pull/5559#issuecomment-6440973661).

## Open Findings

### F2 Medium Still Open Malformed JSON Can Still Break Ticket Loading

The write-permission problem is fixed: only `middleware-server` can update `rule_position`, and the general implementation-task mutations no longer include it. Known malformed objects are also retained as `UnknownRulePosition`.

However, `NewtonsoftRulePositionJsonConverter.ReadJson` calls `JObject.Load(reader)` before entering any exception handler. A valid JSONB scalar or array therefore still throws and aborts deserialization of the entire ticket response. Invalid metadata in an unknown object can similarly throw outside the guarded known-type path.

Location: `roles/lib/files/FWO.Data/RulePositioning/RulePositionJsonConverters.cs:155`

Recommended fix:

- Check the token shape or catch failures around the complete Newtonsoft read path.
- Treat every malformed stored value as null or `UnknownRulePosition`.
- Add a database constraint such as `rule_position IS NULL OR jsonb_typeof(rule_position) = 'object'`.
- Test scalar, array, invalid unknown metadata, and valid-position siblings in one ticket response.

Criticality is reduced from high to medium because user roles can no longer write the column.

### F5 Medium Still Open Positioning Holds the Ticket Lock

The PR adds meaningful safeguards: a 30-second timeout, request cancellation, graph limits, and operation-scoped caching. Nevertheless, positioning still runs synchronously inside `DoStateChangeActions`, while `WorkflowController` holds the per-ticket action lock.

Locations:

- `roles/lib/files/FWO.Services/Workflow/ActionHandler.cs:118`
- `roles/middleware/files/FWO.Middleware.Server/Controllers/WorkflowController.cs:146`

A slow calculation can therefore block other actions on that ticket for up to 30 seconds. The guard-object query also has unbounded nested relationship collections. This remains suitable for the follow-up issue recommended in Round 1.

### F11 Medium New Positioning Uses Startup Configuration After Settings Change

Workflow actions deliberately load a fresh `GlobalConfig` because the injected configuration was read only at middleware startup. Rule-positioning services, however, are singletons and retain that startup configuration.

Consequently, changing the designated zone matrix or automatic Internet-zone setting without restarting middleware causes the new layer-guard algorithm to query and validate against stale settings. It can clear the calculated position or store a position based on the wrong matrix.

Locations:

- `roles/middleware/files/FWO.Middleware.Server/Program.cs:69`
- `roles/middleware/files/FWO.Middleware.Server/Controllers/WorkflowController.cs:172`
- `roles/middleware/files/FWO.Middleware.Server/RulePositioningServiceCollectionExtensions.cs:18`
- `roles/lib/files/FWO.Compliance/RulePositioning/DesignatedZoneMatrixReader.cs:16`
- `roles/lib/files/FWO.Compliance/RulePositioning/CreationRuleZoneResolver.cs:139`

Recommended fix: load one current configuration snapshot per positioning operation and use it consistently for both matrix reading and zone resolution. Add a test that changes these settings after service construction.

### F8 Low Still Open Coding Guideline and Quality Gate Failures

The unbounded recursive inline-layer traversal remains in `CheckPointRulebaseGraphInterpreter.TryAddLayer`, and `RulebaseGraphSnapshotReader.TryMapGraph` still has eight parameters.

Locations:

- `roles/lib/files/FWO.Services/RulePositioning/CheckPoint/CheckPointRulebaseGraphInterpreter.cs:113`
- `roles/lib/files/FWO.Services/RulePositioning/RulebaseGraphSnapshotReader.cs:272`

Sonar reports 46 new issues and a failed quality gate. `git diff --check` also reports three added blank lines at end of file.

### F9 Low Still Open Document Placement Partially Fixed

The root requirements document and PDF and the agent session files were removed, which fixes most of F9. The PR still adds nine superseded documents under `documentation/work-packages/firewall-rule-placement/archive/previous-proposal/`.

### F10 Low Still Open Source Text Contract Tests

Source-text contract tests remain, and production source, SQL, and metadata files are still copied into the installed unit-test directory. These tests remain coupled to formatting, filenames, and source layout rather than runtime behavior.

## Prior Finding Status

| Finding | Criticality | Status |
| --- | --- | --- |
| F1 | high | Fixed. Version, upgrade SQL, revision history, and integration fixture now use 9.8.5. |
| F2 | medium now | Still open, narrowed. |
| F3 | medium | Fixed. General mutations cannot overwrite the calculated position. |
| F4 | medium | Fixed. Enum values are explicit. |
| F5 | medium | Still open, substantially mitigated. |
| F6 | medium | Fixed. The discriminator registry is cached. |
| F7 | low | Fixed. Strategies and supporting services are wired into production. |
| F8 | low | Still open. |
| F9 | low | Still open, partially fixed. |
| F10 | low | Still open. |
| F11 | medium | New. |

## Cross Layer Status

Impacted layers: shared libraries, middleware server, PostgreSQL, Hasura and GraphQL, installer and versioning, and UI settings.

Schema change: add nullable `request.impltask.rule_position` with SQL type `jsonb`.

- Fresh-install and upgrade schemas are aligned.
- Hasura read access is propagated to the existing roles.
- Hasura write access is limited to `middleware-server`.
- GraphQL and C# naming and nullability are aligned.
- The remaining schema risk is acceptance of non-object JSONB values.

Installer change: versioned feature affecting both fresh installation and upgrade.

- Product version, revision history, creation schema, upgrade SQL, and upgrade test fixture are aligned at 9.8.5.
- The upgrade SQL is idempotent.
- Both Ubuntu installer jobs pass.
- Debian and a locally managed disposable VM were not independently exercised.

RBAC change: all existing workflow roles may read `rule_position`; only the trusted middleware role may update it. No LDAP, JWT claim, editable-owner, or UI role semantics change is introduced.

## Validation

- `dotnet build --configuration Debug roles/FWO.sln --no-restore`: passed with zero warnings and zero errors.
- `dotnet test roles/tests-unit/files/FWO.Test/FWO.Test.csproj --no-build --configuration Debug`: 10,287 passed, 30 skipped, zero failed.
- GitHub Release build: passed.
- CodeQL: passed with no new alerts.
- GitGuardian: passed with no secrets detected.
- Both Ubuntu installer jobs, including Ansible Core 2.18: passed.
- Sonar quality gate: failed with 46 new issues and reliability rating C.
- Version gate: queued when last checked.
- `dotnet format roles/FWO.sln --verify-no-changes --no-restore`: found one pre-existing whitespace violation in unchanged `WorkflowConfigurationTransferService.cs:224`; it is not attributable to this PR.
- `git diff --check`: three blank-line-at-EOF warnings in changed files.
- Code coverage percentage was not measured.
- The disposable-VM installer test was not independently run because the FWO MCP VM tools were unavailable in the review session.

## Recommendations

Fix before merge:

- F2: make every stored JSONB shape safe to read and constrain the database value to an object or null.
- F11: use current global configuration for every positioning calculation.
- F8: resolve or explicitly justify the Sonar issues so the quality gate passes.

Track as follow-up:

- F5: move expensive positioning outside the ticket action lock or otherwise prevent it from blocking ticket operations.

Cleanup:

- F9: remove superseded archived planning documents unless they are intentionally retained project documentation.
- F10: replace source-text assertions with behavior or integration tests.

