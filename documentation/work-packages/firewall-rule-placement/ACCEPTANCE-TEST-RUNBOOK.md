# Firewall rule positioning acceptance test runbook

## 1. Purpose

This is the authoritative agent-first acceptance runbook for the implemented firewall rule-positioning feature. It is deliberately more extensive than a normal smoke test because its immediate purpose is to prevent a predictable failure during a live management demonstration.

The runbook answers four questions:

1. Does the standard algorithm calculate and persist the end of the imported initial Check Point layer?
2. Does the layer-guard/zone-pair algorithm select the one functional inline layer and the intended Section?
3. Do realistic failures stop safely without a fallback, a broken workflow, or a firewall-side change?
4. Can the successful and failed behaviors be repeated from a known state and explained simply?

It is not an exhaustive permutation of every unit-level edge case. The automated suites already cover those. This document selects the integration paths, realistic data variations, lifecycle behavior, and failure modes most likely to matter on an actual customer installation.

## 2. Authority and feature boundary

Read these before executing the runbook:

- [roadmap and current status](README.md);
- [durable decisions](DECISIONS.md);
- [generic production completion](phase-3/WP-05-generic-production-completion-implementation.md);
- [workflow activation](phase-3/WP-06-workflow-activation-implementation.md);
- [layer-guard/zone-pair calculation](phase-4/WP-01-customer-specific-recipes-implementation.md); and
- [customer-algorithm activation](phase-4/WP-02-customer-positioning-activation-implementation.md).

The feature's terminal product output is a semantic JSON value in `request.impltask.rule_position`. The feature currently does **not**:

- render a Check Point API request;
- add or move a rule on a firewall;
- publish a Check Point session;
- install policy;
- display the calculated position in a dedicated UI control;
- retry a completed failure automatically; or
- recalculate when an existing implementation task is edited.

An acceptance test is invalid if it claims any of those behaviors. The product calculation is successful when the normal workflow creates an applicable implementation task and the expected semantic position is persisted.

### 2.1 Supported production capabilities

| Behavior | Device type | Algorithm | Numeric value | Required data | Expected terminal result |
| --- | --- | --- | ---: | --- | --- |
| Standard | Check Point gateway (`9`) | `DefaultEndOfRulebase` | `5` | Historically consistent imported rulebase graph | End of initial layer |
| Layer guard and zone pair | Check Point gateway (`9`) | `CheckPointLayerGuardEndOfZonePairSection` | `7` | Graph, guard objects at the same import, effective request ranges, designated zones, matching Section | End of selected zone-pair Section |

There is no algorithm fallback. The two strategies must remain non-overlapping.

### 2.2 Semantic-position values

Known positions use:

```json
{
  "position_type": "check-point.rule-position.v1",
  "schema_version": 1,
  "positioning_algorithm": 7,
  "layer_uid": "<imported-layer-uid>",
  "anchor_kind": "BELOW",
  "anchor_rule_uid": "<imported-rule-uid>"
}
```

`positioning_algorithm` is `5` for the standard algorithm and `7` for the layer-guard/zone-pair algorithm. The persisted GraphQL enum representation of `anchor_kind` is:

| Persisted value | Meaning | Rule UID requirement |
| --- | --- | --- |
| `"TOP"` | `Top` | Must be null |
| `"BOTTOM"` | `Bottom` | Must be null |
| `"ABOVE"` | `Above` | Required |
| `"BELOW"` | `Below` | Required |

JSON property order is irrelevant. Compare parsed values, not text formatting.

## 3. Scope and safety rules

### 3.1 Included

- Source, build, and unit-test baseline.
- Installed UI, API, middleware, database, and workflow integration.
- Effective device-specific settings at global, device-type, management, and gateway scopes.
- Real imported Check Point data when it is structurally suitable.
- Importer-faithful normalized fixtures in the isolated FWO VM when live data is unsuitable.
- Standard and layer-guard/zone-pair success paths.
- Representative missing, ambiguous, and malformed data failures.
- Creation-only activation, replay suppression, persistence, clearing contracts, safe logging, and cleanup.
- Evidence collection and Monday demonstration rehearsal.

### 3.2 Excluded

- Fresh installation, upgrade, rollback, or installer testing.
- Whether a clean installation supplies automatic implementation-task workflow defaults.
- Proving that the importer interprets Check Point correctly.
- Changing the company Check Point testing server to manufacture a topology.
- Testing every possible address object, graph corruption, enum representation, or cancellation timing.
- Firewall rendering, mutation, publish, policy installation, or post-install verification.
- Fortinet positioning.

### 3.3 Hard safety rules

1. Treat the external Check Point testing system as read-only.
2. Snapshot or back up the local FWO VM database before loading a fixture or creating a disposable acceptance matrix.
3. Never load fixture data into a shared production or company test database.
4. Never seed the expected `request.impltask.rule_position`; only production code may create or clear it.
5. Never replace a production reader, resolver, strategy, recipe, or persistence service with a mock in deployed acceptance tests.
6. Do not hand-edit normalized fixture rows until they “look right.” Regenerate them from importer input instead.
7. Stop if a command target, management ID, gateway ID, or database identity is uncertain.
8. Never expose credentials, complete rule contents, customer object names, or raw proprietary payloads in committed evidence.

## 4. Execution vocabulary

### 4.1 Status

| Status | Meaning |
| --- | --- |
| `PASS` | Observed result matches every assertion and required evidence exists. |
| `FAIL` | Product behavior or persisted state contradicts an assertion. |
| `BLOCKED` | A named prerequisite is unavailable; this is not a product failure. |
| `NOT-RUN` | The test was deliberately outside the current batch. |

Do not turn `BLOCKED` into `PASS` by weakening the test or inventing friendly data.

### 4.2 Priority

| Priority | Meaning |
| --- | --- |
| `P0` | Blocks the Monday demonstration and the overall GO decision. |
| `P1` | Important integration or regression confidence; failure requires explicit review. |
| `P2` | Useful additional confidence; may be deferred with a reason. |

### 4.3 Test-data classes

| Class | Meaning |
| --- | --- |
| `LIVE` | Data produced by a real import from the read-only Check Point test system. |
| `FIXTURE` | Normalized database state previously produced by the real importer from sanitized Check Point API-shaped input. |
| `AUTOMATED` | Existing repository unit/contract tests; no deployed VM data required. |

## 5. Environment inventory

Create an uncommitted execution directory named `evidence/rule-positioning/<run-id>/`. Use a run ID such as `2026-11-01T0900-agent-a`. Store this completed inventory as `00-environment.md`.

| Item | Required value |
| --- | --- |
| Repository commit | `git rev-parse HEAD` |
| Working-tree diff | `git status --short` and `git diff --stat` |
| VM identity | Hostname, snapshot name, OS, time zone |
| FWO version | Product version reported by the installation |
| UI URL | Usually `https://<host>/` or forwarded guest port `443` |
| GraphQL URL | Usually `https://<host>:9443/` |
| Middleware URL | Usually `https://<host>:8888/` |
| Database | Local `fworchdb` on the VM |
| Middleware service/log | `fworch-middleware.service`, `/var/log/fworch/middleware.log` |
| Test role | Admin for setup and workflow creation |
| Management | ID, name alias used only in evidence, device type |
| Gateway | ID, name alias, device type ID (must be `9`) |
| Latest successful rule import | `control_id`, `stop_time`, management ID |
| Designated zone matrix | Criterion ID and safe aliases for relevant zones |
| Fixture bundle | Version/checksum, or `none` when live data is sufficient |
| Database restore point | Snapshot or backup identifier and restore owner |

Do not put passwords, tokens, private IP inventories, or unredacted customer names in this file.

### 5.1 Connectivity and service gate

Run from the laptop or VM as appropriate:

```powershell
git rev-parse HEAD
git status --short
curl.exe -k -sS -o NUL -w "%{http_code}`n" "https://<fwo-host>/"
curl.exe -k -sS -o NUL -w "%{http_code}`n" "https://<fwo-host>:9443/api/healthz"
curl.exe -k -sS -o NUL -w "%{http_code}`n" "https://<fwo-host>:8888/"
```

On the VM:

```bash
systemctl is-active fworch-middleware.service
sudo -u postgres psql -d fworchdb -v ON_ERROR_STOP=1 -c 'select current_database(), current_user, now();'
test -r /var/log/fworch/middleware.log
```

Record status codes and command output. A redirect or authenticated response is acceptable for UI/middleware; connection refusal, TLS routing failure, or an inactive service is not.

### 5.2 Database helper

Use read-only SQL unless a fixture or cleanup step explicitly authorizes a write:

```bash
sudo -u postgres psql -d fworchdb -v ON_ERROR_STOP=1
```

Inside `psql`, set run-specific identifiers:

```sql
\set mgm_id <management-id>
\set gateway_id <gateway-id>
\set impltask_id <implementation-task-id>
\set import_id <latest-successful-rule-import-id>
```

### 5.3 Workflow preparation is test setup

Rule positioning activates when an applicable implementation task is durably created. Whether a clean installation already assigns the `Create implementation tasks` workflow action or enables rule auto-planning is outside this feature's acceptance scope.

Before workflow tests, prove that the disposable VM can create implementation tasks through supported product behavior. If required, an agent may use the UI to:

1. assign `Create implementation tasks` to the chosen workflow state; and
2. enable rule auto-planning at the narrowest suitable test scope.

Record the original values, the configured state/action, the effective override scope, and the exact workflow path in `00-environment.md`. Restore them during cleanup. Do not insert an implementation task with SQL or call an internal persistence mutation to simulate creation. A missing default is an environment prerequisite, not a rule-positioning failure; inability to create a task after valid setup is a workflow blocker.

## 6. Evidence standard

Each test gets a directory named `<test-id>/` containing, where applicable:

- `result.md`: status, start/end time, agent, commit, environment, and concise conclusion;
- `before.sql.txt` and `after.sql.txt`: relevant database observations;
- `middleware.log.txt`: only the bounded time/ID-related extract;
- `ui-before.png`, `ui-action.png`, and `ui-after.png`;
- `graphql.json`: redacted response when GraphQL is the assertion surface;
- `test-matrix.json` and `test-matrix.sha256`: exact disposable matrix input when DATA-05A is used;
- `fixture-manifest.yml`: copied reference, not a modified manifest; and
- `cleanup.txt`: cleanup command and result.

Every `result.md` must answer:

1. Which exact request and implementation task IDs were exercised?
2. Which algorithm was effective at the gateway?
3. Which import ID and data class were used?
4. What `rule_position` existed before and after?
5. Did the workflow operation itself succeed?
6. Was any fallback observed?
7. Was any firewall-side mutation attempted?
8. If failed or blocked, which component owns the next investigation?

## 7. Source and automated baseline

### BASE-01 — Build and full unit suite (`P0`, `AUTOMATED`)

From the repository root:

```powershell
dotnet format roles/FWO.sln --no-restore --verify-no-changes
dotnet build --configuration Debug roles/FWO.sln --no-restore
dotnet test roles/tests-unit/files/FWO.Test/FWO.Test.csproj --no-build --configuration Debug
git diff --check
```

Expected:

- formatter exit code `0`;
- build exit code `0`, zero errors;
- unit suite exit code `0`, zero failed tests; and
- `git diff --check` produces no output.

Do not dismiss a failure as “unrelated” without naming it in the run result.

### BASE-02 — Focused positioning contracts (`P0`, `AUTOMATED`)

```powershell
dotnet test roles/tests-unit/files/FWO.Test/FWO.Test.csproj --no-build --configuration Debug --filter "FullyQualifiedName~RulePositioningWorkflowCallerTest|FullyQualifiedName~RulebaseGraphSnapshotReaderTest|FullyQualifiedName~CheckPointLayerGuardSnapshotReaderTest|FullyQualifiedName~CheckPointGraphPositioningTest|FullyQualifiedName~CheckPointRulePositioningStrategyTest|FullyQualifiedName~CheckPointLayerGuardZonePairRecipeTest|FullyQualifiedName~RulePositionPersistenceTest|FullyQualifiedName~DeviceSpecificSettings"
```

Expected: zero failures. Preserve the `.trx` or console output.

This baseline is the executable evidence for contracts that cannot safely be forced through a deployed VM, including cancellation timing, persistence-write failure, and stale-value clearing for every typed calculation failure.

### BASE-03 — Production-boundary source check (`P1`, `AUTOMATED`)

Confirm that production still registers both exact Check Point strategies and contains no renderer or vendor write call in the positioning path:

```powershell
rg -n "CheckPointGraphRulePositioningStrategy|CheckPointRulePositioningStrategy" roles/middleware roles/lib
rg -n "CheckPointClient|add-access-rule|publish|install-policy" roles/lib/files/FWO.Services/RulePositioning roles/lib/files/FWO.Compliance/RulePositioning
```

Expected:

- both strategy registrations are present;
- the second search has no matches in the positioning namespaces.

## 8. Live normalized-data discovery

Discovery determines whether `LIVE` data is suitable. It does not change the Check Point server or the FWO database.

### DATA-01 — Latest successful fixed import (`P0`)

```sql
SELECT control_id, mgm_id, import_type_id, successful_import, start_time, stop_time
FROM import_control
WHERE mgm_id = :mgm_id
  AND import_type_id = 1
  AND successful_import
  AND stop_time IS NOT NULL
ORDER BY stop_time DESC, control_id DESC
LIMIT 1;
```

Expected: exactly one selected row with positive `control_id`. Record it as `:import_id`. Positioning intentionally uses the latest successful import regardless of age.

### DATA-02 — Gateway and structural links (`P0`)

```sql
SELECT d.dev_id,
       d.mgm_id,
       l.id AS link_id,
       l.link_type,
       lt.name AS link_type_name,
       l.is_initial,
       l.is_global,
       l.is_section,
       source_rb.uid AS source_rulebase_uid,
       source_rule.rule_uid AS source_rule_uid,
       target_rb.uid AS target_rulebase_uid,
       target_rb.name AS target_rulebase_name,
       l.created,
       l.removed
FROM device d
JOIN firewall.rulebase_link l ON l.gw_id = d.dev_id
LEFT JOIN stm_link_type lt ON lt.id = l.link_type
LEFT JOIN firewall.rulebase source_rb ON source_rb.id = l.from_rulebase_id
LEFT JOIN firewall.rule source_rule ON source_rule.rule_id = l.from_rule_id
JOIN firewall.rulebase target_rb ON target_rb.id = l.to_rulebase_id
WHERE d.dev_id = :gateway_id
  AND d.mgm_id = :mgm_id
  AND l.created <= :import_id
  AND (l.removed IS NULL OR l.removed > :import_id)
ORDER BY l.id;
```

Classify the result:

- standard-ready: one valid initial path and at least one access layer;
- customer-ready: at least one `inline` link (`link_type = 3`) from a source guard rule, plus Section links beneath the relevant inline layer;
- unsuitable: missing/ambiguous initial structure, no guarded inline layer, or broken historical endpoints.

“Unsuitable” means use a `FIXTURE`; it does not mean change the external Check Point policy.

### DATA-03 — Rules and Section candidates (`P0`)

```sql
SELECT rb.id AS rulebase_id,
       rb.uid AS rulebase_uid,
       rb.name AS rulebase_name,
       r.rule_id,
       r.rule_uid,
       r.rule_num_numeric,
       r.rule_create,
       r.removed
FROM firewall.rulebase rb
LEFT JOIN firewall.rule r
  ON r.rulebase_id = rb.id
 AND r.mgm_id = rb.mgm_id
 AND r.rule_create <= :import_id
 AND (r.removed IS NULL OR r.removed > :import_id)
WHERE rb.mgm_id = :mgm_id
  AND rb.created <= :import_id
  AND (rb.removed IS NULL OR rb.removed > :import_id)
ORDER BY rb.id, r.rule_num_numeric, r.rule_id;
```

From DATA-02, identify Section target rulebases (`is_section = true`). Record their exact imported `name` and `uid`. Customer matching trims outer whitespace and compares invariant uppercase, but it does not repair wrong brackets or interior spacing.

The canonical name is destination first:

```text
[DESTINATION-ZONE] < [SOURCE-ZONE]
```

### DATA-04 — Inline guard semantics (`P0`)

```sql
WITH guard_rules AS (
    SELECT DISTINCT l.from_rule_id AS rule_id, l.to_rulebase_id
    FROM firewall.rulebase_link l
    WHERE l.gw_id = :gateway_id
      AND l.link_type = 3
      AND l.created <= :import_id
      AND (l.removed IS NULL OR l.removed > :import_id)
)
SELECT g.rule_id,
       target.uid AS target_layer_uid,
       r.rule_src_neg,
       r.rule_dst_neg,
       'source' AS side,
       rf.negated AS association_negated,
       o.obj_id,
       o.obj_uid,
       o.obj_name,
       o.obj_ip,
       o.obj_ip_end,
       ot.obj_typ_name
FROM guard_rules g
JOIN firewall.rule r ON r.rule_id = g.rule_id
JOIN firewall.rulebase target ON target.id = g.to_rulebase_id
JOIN firewall.rule_from rf ON rf.rule_id = r.rule_id
JOIN firewall.nw_object o ON o.obj_id = rf.obj_id
JOIN stm_obj_typ ot ON ot.obj_typ_id = o.obj_typ_id
WHERE rf.rf_create <= :import_id
  AND (rf.removed IS NULL OR rf.removed > :import_id)
UNION ALL
SELECT g.rule_id,
       target.uid,
       r.rule_src_neg,
       r.rule_dst_neg,
       'destination',
       rt.negated,
       o.obj_id,
       o.obj_uid,
       o.obj_name,
       o.obj_ip,
       o.obj_ip_end,
       ot.obj_typ_name
FROM guard_rules g
JOIN firewall.rule r ON r.rule_id = g.rule_id
JOIN firewall.rulebase target ON target.id = g.to_rulebase_id
JOIN firewall.rule_to rt ON rt.rule_id = r.rule_id
JOIN firewall.nw_object o ON o.obj_id = rt.obj_id
JOIN stm_obj_typ ot ON ot.obj_typ_id = o.obj_typ_id
WHERE rt.rt_create <= :import_id
  AND (rt.removed IS NULL OR rt.removed > :import_id)
ORDER BY rule_id, side, obj_id;
```

Also export flattened group members when a guard references a group:

```sql
SELECT f.objgrp_flat_id,
       parent.obj_uid AS group_uid,
       f.objgrp_flat_member_id,
       member.obj_uid AS member_uid,
       member.obj_ip,
       member.obj_ip_end,
       f.negated,
       f.import_created,
       f.removed
FROM objgrp_flat f
JOIN firewall.nw_object parent ON parent.obj_id = f.objgrp_flat_id
JOIN firewall.nw_object member ON member.obj_id = f.objgrp_flat_member_id
WHERE parent.mgm_id = :mgm_id
  AND f.import_created <= :import_id
  AND (f.removed IS NULL OR f.removed > :import_id)
ORDER BY f.objgrp_flat_id, f.objgrp_flat_member_id, f.import_created;
```

### DATA-05 — Zone-matrix suitability (`P0`)

Use the UI compliance/network-zone pages or read-only GraphQL/SQL to record:

- designated matrix criterion ID;
- source and destination zone aliases;
- every normalized IP range in those zones;
- whether coverage is complete for the planned request ranges; and
- whether each side resolves to exactly one persisted zone.

For a pre-existing matrix, do not choose request IPs first and then silently reshape zones to make them pass. Record the intended matrix, then select explicit passing and failing request ranges. If the disposable VM has no suitable matrix, follow DATA-05A instead of declaring the customer suite blocked.

### DATA-05A — Create a disposable acceptance matrix when needed (`P0`)

This matrix is local FWO test configuration. Creating it does not modify Check Point and does not mock imported graph or guard data.

Derive the smallest useful matrix from DATA-02 through DATA-04:

1. Find an imported inline guard that definitely contains one source and destination test range.
2. Find a populated Section inside that guard-selected layer whose exact imported name has the canonical `[DESTINATION] < [SOURCE]` form.
3. Use those two exact zone names and ranges for the happy path.
4. When the same live data permits it, add only the extra zones/ranges needed for:
   - a uniquely guarded pair whose canonical Section is absent; and
   - a valid zone pair whose ranges are definitely outside every guard.
5. Keep the matrix small—normally two to six zones—and use one run-specific name such as `RP acceptance <run-id>`.

The ranges come from the real imported guard analysis; the zone names come from the imported Section selected for success or from the deliberately absent canonical name selected for a failure. Do not change rulebases, links, guards, rules, objects, or the external Check Point system to accommodate the matrix.

Create the matrix through the supported JSON import at **Settings > Network Topology > Matrix** (`/settings/matrix`). A minimal two-zone starting document is:

```json
{
  "name": "RP acceptance <run-id>",
  "comment": "Disposable rule-positioning acceptance matrix",
  "areas": [
    {
      "name": "<SOURCE-ZONE>",
      "id_string": "rp_source",
      "subnets": [
        { "name": "Guard-contained source", "ip": "<source-test-ip-or-range>" }
      ],
      "communication_to": []
    },
    {
      "name": "<DESTINATION-ZONE>",
      "id_string": "rp_destination",
      "subnets": [
        { "name": "Guard-contained destination", "ip": "<destination-test-ip-or-range>" }
      ],
      "communication_to": []
    }
  ]
}
```

Follow the repository's [network-matrix import contract](../../interface-descriptions-json/network-matrix-import-format.md). Then select the imported matrix as the designated zone matrix on the same settings page. Save the exact uploaded JSON, its SHA-256 checksum, the resulting criterion ID, zone IDs, effective designated-matrix value, and a screenshot. Resolve the planned request ranges through the production zone-resolution surface and require complete coverage with exactly one persisted zone per side before running positioning.

If no combination of real imported guards and Sections can support a required scenario, record the proof and use the importer-faithful fixture protocol in section 9. The absence of a prebuilt fixture bundle is not itself a blocker: the executing agent must generate one according to that protocol. Restore the original designated matrix and remove the disposable matrix through supported behavior, or restore the VM snapshot, during cleanup.

### DATA-06 — Live-data decision (`P0`)

Write `data-decision.md` with one of:

- `LIVE-STANDARD-AND-CUSTOMER`;
- `LIVE-STANDARD_FIXTURE-CUSTOMER`;
- `FIXTURE-STANDARD-AND-CUSTOMER`; or
- `BLOCKED` with the unavailable prerequisite.

Include the queries and facts supporting the decision.

## 9. Importer-faithful fixture protocol

### 9.1 The boundary

The fixture is allowed to mock what a Check Point import **produces**, not what positioning wishes it received.

The acceptance boundary begins after a successful importer run has written normalized data. Fixture preparation may use sanitized or synthetic Check Point API-shaped input, but that input must pass through the repository's real Check Point importer. The exported database result—warts and all—is the fixture.

The following shortcuts are forbidden:

- constructing `RulebaseGraphSnapshot` in a test harness;
- constructing `CheckPointLayerGuardSnapshot` directly;
- returning a prepared `CreationRuleZoneInput`;
- invoking `CheckPointLayerGuardZonePairRecipeFactory` as the acceptance action;
- inserting the expected `rule_position`;
- writing only the rows read by the happy path while omitting importer-created companions;
- inventing `created`/`removed` values independent of `import_control`; or
- changing exported rows after seeing a positioning failure.

If the fixture fails because the importer produced inconvenient normalized data, preserve the evidence. Fix the API-shaped fixture input and rerun the importer; never polish the normalized output.

### 9.2 Required fixture topology

The primary fixture should be realistic rather than minimal. Its importer input should produce:

- one Check Point management and one type-`9` gateway;
- one policy/initial path and at least one populated initial access layer;
- at least two guarded inline layers, only one of which matches the happy-path traffic;
- guard objects that include at least one group or network range and a normal Check Point `Any` on one side where realistic;
- at least one unrelated Section with the same name in the wrong inline layer;
- one correctly named populated zone-pair Section in the matching layer;
- one empty Section or empty boundary for the empty-Section test;
- at least one ordinary non-Section rule before or after the Section structure;
- group flattening rows produced by the importer;
- a completed successful rule import and proper historical intervals; and
- no feature-specific position data.

These extra structures prevent an unrealistically clean fixture from proving only that the test author understands the recipe.

### 9.3 Fixture generation

Perform generation in an isolated builder VM/database, not the acceptance database:

1. Record the repository/importer commit.
2. Prepare sanitized Check Point API-shaped responses containing the topology above.
3. Run the real `checkpointR8x` importer normally against that input.
4. Require a completed successful rule import.
5. Run DATA-01 through DATA-04 against the produced rows.
6. Export the exact normalized rows and relationships for the dedicated management/gateway.
7. Export a row-count and identity manifest.
8. Hash every bundle file.
9. Make the bundle immutable for the acceptance run.

The bundle must include:

```text
fixture/
  manifest.yml
  source-shape.md
  importer-command.txt
  importer-log.redacted.txt
  normalized-data.sql
  validation.sql
  cleanup.sql
  SHA256SUMS
```

`source-shape.md` describes vendor response types and topology without proprietary names or addresses. `normalized-data.sql` is generated from actual importer output; it is not authored from the reader contracts.

### 9.4 Manifest

At minimum:

```yaml
fixture_version: 1
generated_utc: "<timestamp>"
repository_commit: "<sha>"
importer_package: checkpointR8x
source_kind: sanitized-check-point-api-shaped-responses
external_server_modified: false
management_alias: RP_FIXTURE_MGMT
gateway_alias: RP_FIXTURE_GW
device_type_id: 9
successful_rule_import_id: <id>
tables:
  import_control: <count>
  management: <count>
  device: <count>
  firewall.rulebase: <count>
  firewall.rulebase_link: <count>
  firewall.rule: <count>
  firewall.rule_from: <count>
  firewall.rule_to: <count>
  firewall.nw_object: <count>
  firewall.nw_object_group: <count>
  objgrp_flat: <count>
contains_expected_rule_position: false
```

### 9.5 Replay gate

Before replay:

- take a VM/database snapshot;
- stop workflow activity from other users;
- verify the target is `fworchdb` on the isolated VM;
- verify fixture aliases and primary keys do not collide;
- verify checksums; and
- record the restore owner.

Replay must be one transaction with `ON_ERROR_STOP`. If a foreign key or unique constraint fails, roll back and mark the fixture `BLOCKED`; do not patch rows interactively.

```bash
sha256sum -c SHA256SUMS
sudo -u postgres psql -d fworchdb -v ON_ERROR_STOP=1 -f normalized-data.sql
sudo -u postgres psql -d fworchdb -v ON_ERROR_STOP=1 -f validation.sql
```

The validation script must prove:

- exactly one fixture management/gateway with device type `9`;
- exactly one latest successful rule import for that management;
- every active link endpoint exists at that import;
- every inline link has one active source rule in its declared source rulebase;
- every guard rule association points to an active normalized object;
- group objects retain importer-produced `objgrp_flat` rows;
- Section links and target rulebases exist exactly as exported;
- historical intervals contain the selected import;
- the fixture contains no request/implementation task and no `rule_position`; and
- DATA-01 through DATA-04 return the manifest's expected identities/counts.

### 9.6 Fixture trace through production readers

Before any workflow test, trace one happy-path candidate on paper:

```text
import_control.control_id
  -> getLatestSuccessfulRuleImport
  -> firewall.rulebase_link at the same half-open history interval
  -> getGatewayRulebaseLinksAtImport
  -> complete firewall.rulebase + firewall.rule endpoints
  -> getRulebasesAtImport
  -> inline link from_rule_id
  -> firewall.rule_from / firewall.rule_to + normalized objects/groups
  -> getCheckPointLayerGuardsAtImport
  -> production graph and guard readers
```

If any expected row exists only in the manifest but is invisible to those real queries, the fixture is invalid.

## 10. Shared workflow action

Use this action whenever a test says “create an applicable task”:

1. Sign in with the test admin/planner account.
2. Confirm the intended effective positioning setting at `/settings/devicespecificsettings`.
3. Create a new ticket with a new access-rule request task.
4. Use `create` for both request and implementation action.
5. Select the recorded management and a concrete type-`9` gateway.
6. Enter the test's exact source and destination ranges/groups.
7. Advance the installation's configured workflow until a gateway-specific implementation task is durably created.
8. Record ticket, request-task, and implementation-task IDs immediately.
9. Wait for middleware processing to finish before querying the position.

Workflow state names and buttons vary by configuration. Record the actual clicked path once in `00-environment.md` and reuse it. Do not create the implementation task with SQL; that bypasses the activation seam.

Query the result:

```sql
SELECT id,
       reqtask_id,
       device_id,
       mgm_id,
       implementation_action,
       rule_position
FROM request.impltask
WHERE id = :impltask_id;
```

Bound the log evidence by task ID and time:

```bash
grep -iF "implementation task <id>" /var/log/fworch/middleware.log | tail -n 20
```

Expected log vocabulary includes stable statuses such as `Stored`, `ClearedAfterFailure`, or `PersistenceFailed` and a fixed failure category. It intentionally does not disclose detailed object, rule, or customer data.

## 11. Acceptance test catalog

### 11.1 Configuration and readiness

| ID | Pri. | Data | Test | Expected |
| --- | --- | --- | --- | --- |
| CFG-01 | P0 | LIVE/FIXTURE | Open `/settings/devicespecificsettings`; inspect global, Check Point device type, management, and gateway levels. | “Check Point: end of layer-guard zone-pair section” and standard end-of-rulebase option are localized and selectable at every supported level. |
| CFG-02 | P0 | LIVE/FIXTURE | Set global standard, management customer, gateway standard; reload each level. | Effective value and inherited source follow gateway → management → device type → global precedence. |
| CFG-03 | P1 | AUTOMATED | Run enum/settings serialization tests from BASE-02. | Existing ordinals `0..6` remain stable; customer algorithm is numeric `7` and wire value `cpLayerGuardEndOfZonePairSection`. |
| CFG-04 | P0 | LIVE/FIXTURE | Set designated zone matrix and verify source/destination test ranges. | Matrix exists, both happy-path sides have complete coverage, and each resolves to one persisted zone. |
| CFG-05 | P1 | LIVE/FIXTURE | Switch only the selected gateway between algorithms and create independent tasks. | Effective gateway setting selects exactly one strategy; there is no fallback to the other. |

### 11.2 Standard algorithm

| ID | Pri. | Data | Test | Expected |
| --- | --- | --- | --- | --- |
| STD-01 | P0 | LIVE/FIXTURE | Populated initial layer. | Algorithm `5`, initial layer UID, `"BELOW"`, final direct rule UID. |
| STD-02 | P1 | FIXTURE | Existing but empty initial layer. | Algorithm `5`, initial layer UID, `"BOTTOM"`, null anchor rule UID. |
| STD-03 | P1 | LIVE/FIXTURE | Parent expands to two concrete gateways. | One implementation task and one independently correct stored position per gateway. |
| STD-04 | P0 | LIVE/FIXTURE | Remove/disable designated matrix only in an isolated run, then use standard algorithm. | Standard calculation still succeeds; it has no zone, guard, group, or ticket prerequisite. Restore config immediately. |
| STD-05 | P1 | FIXTURE | Imported graph has no valid initial layer. | Workflow task creation succeeds; position is null; no alternate layer is chosen. |
| STD-06 | P1 | LIVE/FIXTURE | Unsupported/undefined positioning option for Check Point. | No customer or standard fallback position is stored. |
| STD-07 | P1 | AUTOMATED | Run stale-clear and graph-failure caller tests. | Every completed typed failure performs one null persistence mutation; cancellation-before-write preserves existing value. |

#### STD-01 detailed procedure

1. Record the initial layer UID and final direct rule UID from DATA-02/DATA-03.
2. Set the gateway algorithm to standard end of rulebase.
3. Create an applicable task using any valid access-rule addresses; do not configure customer-only zone data for the sake of this test.
4. Query `request.impltask.rule_position`.

Expected parsed JSON:

```json
{
  "position_type": "check-point.rule-position.v1",
  "schema_version": 1,
  "positioning_algorithm": 5,
  "layer_uid": "<initial-layer-uid>",
  "anchor_kind": "BELOW",
  "anchor_rule_uid": "<final-direct-rule-uid>"
}
```

Evidence: settings screenshot, DATA-02/DATA-03 rows, task IDs, before/after query, bounded middleware log.

### 11.3 Layer-guard/zone-pair success

| ID | Pri. | Data | Test | Expected |
| --- | --- | --- | --- | --- |
| CUST-01 | P0 | LIVE/FIXTURE | Unique matching guard, complete single zones, populated canonical Section. | Algorithm `7`, selected inline-layer UID, `Below` final rule of the matching Section. |
| CUST-02 | P1 | FIXTURE | Matching Section is empty but structurally valid. | Correct canonical boundary for that empty Section; no neighboring Section is mistaken for its content. |
| CUST-03 | P1 | FIXTURE | Source and destination resolve to the same persisted zone. | Canonical `[ZONE] < [ZONE]` Section is selected. |
| CUST-04 | P1 | LIVE/FIXTURE | Request references an existing catalogued network group. | Group expands to leaf ranges; the same ranges drive guard and zone decisions. |
| CUST-05 | P0 | FIXTURE | Object/group is created or modified earlier in the same ticket and absent/stale in Flow. | Final ticket-effective membership overrides Flow and produces the expected position. |
| CUST-06 | P1 | FIXTURE | Multiple request objects/ranges on a side all belong to one zone and guard. | Calculation succeeds; ranges are combined without selecting multiple zones. |
| CUST-07 | P1 | FIXTURE | Same Section name also exists in a nonmatching inline layer. | Only the guard-selected layer's Section is used. |
| CUST-08 | P1 | FIXTURE | Guard uses importer-produced group flattening and `Any` on the other side. | Containment follows imported normalized semantics and succeeds only for the intended layer. |

#### CUST-01 detailed procedure

1. From DATA-04, choose source/destination ranges definitely contained by exactly one guard.
2. From DATA-05 or the DATA-05A acceptance matrix, prove those ranges resolve completely to exactly one persisted source zone and one persisted destination zone.
3. Derive the canonical Section name: `[DESTINATION] < [SOURCE]`.
4. From DATA-02/DATA-03, prove exactly one matching normalized Section exists inside the selected inline layer and record its final rule UID.
5. Set the gateway algorithm to “Check Point: end of layer-guard zone-pair section.”
6. Create an applicable task using the recorded ranges.
7. Query and parse `rule_position`.

Expected:

```json
{
  "position_type": "check-point.rule-position.v1",
  "schema_version": 1,
  "positioning_algorithm": 7,
  "layer_uid": "<guard-selected-inline-layer-uid>",
  "anchor_kind": "BELOW",
  "anchor_rule_uid": "<last-rule-uid-in-zone-pair-section>"
}
```

Also assert:

- workflow creation succeeded;
- no position points to the same-named Section in another layer;
- the import ID used for guards equals the graph import ID;
- middleware reports `Stored`; and
- no Check Point write/publish/install operation appears in logs.

### 11.4 Representative customer failures

| ID | Pri. | Data | Test | Expected |
| --- | --- | --- | --- | --- |
| FAIL-01 | P0 | FIXTURE/LIVE | Unique layer guard and zones, but canonical zone-pair Section is absent in that layer. | Workflow succeeds; position is null/cleared; no other Section or layer fallback. |
| FAIL-02 | P0 | FIXTURE/LIVE | Request ranges are definitely outside every inline-layer guard. | Workflow succeeds; position is null/cleared; no main/initial/first-inline fallback. |
| FAIL-03 | P1 | FIXTURE | Two guards definitely contain the request. | Position null; no first-match selection. |
| FAIL-04 | P1 | FIXTURE | One definite matching guard plus another indeterminate candidate. | Position null; calculation fails closed. |
| FAIL-05 | P1 | LIVE/FIXTURE | One side has partial/no zone coverage. | Position null; no synthetic or first-zone fallback. |
| FAIL-06 | P1 | FIXTURE | One side resolves to multiple distinct persisted zones. | Position null; no prioritization or merge. |
| FAIL-07 | P1 | FIXTURE | Same-ticket group has missing member, ambiguity, or cycle. | Position null; no older Flow-catalog fallback when ticket-local state is authoritative. |
| FAIL-08 | P2 | FIXTURE | Guard/topology rows do not share the fixed import interval. | Position null; invalid snapshot/guard data fails safely. |
| FAIL-09 | P1 | FIXTURE | Canonical-looking Section has wrong interior spacing/brackets. | Position null; normalization does not silently repair it. |
| FAIL-10 | P1 | FIXTURE | Matching Section exists only in another layer. | Position null; Section search remains inside the selected functional layer. |

#### FAIL-01 detailed procedure: missing zone-pair Section

Prefer using the same immutable fixture as CUST-01 with a second valid zone pair whose canonical Section is deliberately absent. This avoids mutating topology between success and failure demonstrations.

1. Prove from DATA-04 that the request still has exactly one definite matching guard.
2. Prove from DATA-05 that source and destination each resolve completely to exactly one persisted zone.
3. Compute the canonical Section name.
4. Query DATA-02/DATA-03 and prove that Section name is absent **inside the selected layer**.
5. Record whether it exists elsewhere; either result is acceptable, and existence elsewhere makes the no-fallback assertion stronger.
6. Create the applicable task.
7. Confirm workflow creation succeeds.
8. Confirm `rule_position IS NULL`.
9. Confirm middleware reports a contained positioning failure and does not expose rule/object payloads.

Important: the public outcome is intentionally coarse (`CalculationFailed`/`ClearedAfterFailure`). It may not literally say “Section not found.” The deterministic data proof establishes the missing-Section cause.

#### FAIL-02 detailed procedure: no matching layer guard

1. Choose source or destination ranges that resolve to valid persisted zones but are definitely outside all imported inline guard ranges.
2. Prove every candidate is `NotContained` using DATA-04; do not use an IP-less/dynamic object that makes the result indeterminate.
3. Create the applicable task.
4. Confirm workflow creation succeeds.
5. Confirm `rule_position IS NULL`.
6. Confirm the main/initial layer and first inline layer were not selected as fallbacks.
7. Confirm no firewall-side action occurred.

The public outcome is again deliberately coarse. The precondition evidence distinguishes this scenario from FAIL-01.

## 12. Workflow, persistence, and containment

| ID | Pri. | Data | Test | Expected |
| --- | --- | --- | --- | --- |
| LIFE-01 | P0 | LIVE/FIXTURE | Normal applicable implementation-task creation. | Exactly one positioning attempt after durable creation; task has positive ID before persistence. |
| LIFE-02 | P1 | LIVE/FIXTURE | Repeat/replay the already claimed workflow transition. | No second calculation or position rewrite. |
| LIFE-03 | P0 | LIVE/FIXTURE | Edit gateway, management, metadata, or rule content after creation. | No automatic recalculation or clearing; document this current product boundary. |
| LIFE-04 | P1 | LIVE/FIXTURE | Delete and recreate as a new task ID. | New creation gets one new attempt. |
| LIFE-05 | P1 | AUTOMATED | Caller receives each typed failure with a pre-existing position. | One null write clears stale position. |
| LIFE-06 | P1 | AUTOMATED | Position persistence mutation fails. | `PersistenceFailed`, no retry, surrounding workflow continues. |
| LIFE-07 | P1 | AUTOMATED | Positioning exceeds the 30-second activation budget or throws. | Failure is contained; later workflow actions still run. |
| LIFE-08 | P1 | LIVE/FIXTURE | Create an inapplicable object/update/delete/combined task shape. | No positioning attempt and no position mutation. |
| LIFE-09 | P1 | LIVE/FIXTURE | Customer task includes cross-management sibling tasks. | Only the effective management's positive ordered ticket slice participates. |
| LIFE-10 | P1 | LIVE/FIXTURE | Inspect logs after success and controlled failures. | Only IDs, stable status, and fixed category; no complete rule, object/group name, UID, credentials, or payload. |

### 12.1 Detecting duplicate writes

Before creation, note the middleware log offset/time. After the task settles:

```sql
SELECT id, rule_position
FROM request.impltask
WHERE id = :impltask_id;

SELECT object_scope,
       object_id,
       from_state_id,
       to_state_id,
       executed_at,
       executed_by
FROM request.state_change_execution
WHERE object_scope = 'ImplementationTask'
  AND object_id = :impltask_id;
```

Capture both query results and the bounded log after the original creation. Submit the same already-claimed transition again, then rerun them. The guard row's `to_state_id` and `executed_at`, the stored position, and the positioning-log count must remain unchanged. A later legitimate transition to another state is a different event and is not a replay failure.

### 12.2 Proving no firewall mutation

For all acceptance tests:

- do not advance into an automatic implementation phase that writes firewalls;
- bound the run by time and task ID;
- search middleware/importer/vendor-operation logs for Check Point add-rule, publish, and install-policy activity;
- when permitted, verify the external Check Point session/audit view has no test-run change; and
- record “not executed by feature boundary” rather than claiming an unavailable external proof.

## 13. Failure triage

Use the first failing boundary, not the final symptom:

| Observation | Likely boundary | First evidence |
| --- | --- | --- |
| No implementation task | Workflow setup/configuration | UI/network trace and workflow logs |
| Task exists, no positioning log | Creation marker/applicability/service resolution | Task shape, state transition, middleware log |
| `CalculationFailed`, graph query absent/bad | Import history/normalized graph | DATA-01 to DATA-03 and GraphQL/API log |
| `CalculationFailed`, customer only | Effective ranges, guards, matrix, zones, or Section | DATA-04/DATA-05 and deterministic scenario preconditions |
| `PersistenceFailed` | GraphQL mutation/API permission/database | Hasura/API log and row state |
| Position stored with wrong layer/anchor | Strategy/recipe/graph interpretation | Parsed position plus fixed-import topology evidence |
| Workflow itself fails | Workflow regression, not an acceptable positioning failure | UI error and middleware workflow log |
| External firewall changed | Scope violation | Stop immediately and preserve evidence |

Do not “fix” a failing acceptance test by changing its expected result. Open a defect with the test ID, run ID, commit, fixture checksum/import ID, task IDs, parsed before/after state, and minimal logs.

## 14. Weekend agent execution plan

Run sequentially where state is shared.

### Batch A — Non-mutating baseline

1. BASE-01 through BASE-03.
2. Environment inventory and connectivity gate.
3. DATA-01 through DATA-05.
4. When necessary, snapshot the VM and create the small disposable matrix from DATA-05A.
5. Run DATA-06 and decide live versus importer-fixture mode.

### Batch B — Configuration and standard path

1. Snapshot effective settings.
2. CFG-01 through CFG-05.
3. STD-01 through STD-07.
4. Restore the original settings and prove restoration.

### Batch C — Fixture preparation

Run this batch only when DATA-05A proves the live imported topology cannot support a required scenario.

1. Generate the importer-faithful bundle if one does not already exist.
2. Validate manifest and checksums.
3. Snapshot the acceptance VM/database.
4. Replay the immutable bundle.
5. Run validation and production-reader trace.
6. Stop if any row/identity/count differs from the manifest.

### Batch D — Customer success

1. CUST-01 first.
2. CUST-02 through CUST-08.
3. Rerun CUST-01 from reset state.

### Batch E — Controlled failures

1. FAIL-01 and FAIL-02 first because they are demonstration candidates.
2. FAIL-03 through FAIL-10 in risk order.
3. Rerun FAIL-01 and FAIL-02 from reset state.

### Batch F — Lifecycle and cleanup

1. LIFE-01 through LIFE-10.
2. Restore settings.
3. Restore the original designated matrix and remove the DATA-05A matrix through supported behavior, or restore the VM snapshot.
4. Remove test tickets/tasks through supported UI/API behavior when safe.
5. Run fixture `cleanup.sql` or restore the VM snapshot.
6. Rerun DATA-01/DATA-02 to prove only intended baseline data remains.
7. Close browser sessions and stop only processes started for testing.

### 14.1 Agent handoff

Each agent ends with:

```markdown
## Agent handoff

- Run ID:
- Commit:
- VM/snapshot:
- Tests attempted:
- Passed:
- Failed:
- Blocked:
- Highest-severity finding:
- Evidence root:
- Active test data/settings left behind:
- Cleanup/restore completed:
- Exact next test or investigation:
```

Never say “everything works” without linking the result matrix and evidence.

## 15. Result matrix and exit criteria

Copy this table into `evidence/rule-positioning/<run-id>/RESULTS.md` and add rows for every catalog entry:

| Test ID | Priority | Data/import or fixture checksum | Status | Evidence | Defect | Rerun status | Notes |
| --- | --- | --- | --- | --- | --- | --- | --- |
| BASE-01 | P0 | commit | NOT-RUN |  |  |  |  |
| STD-01 | P0 | import/fixture | NOT-RUN |  |  |  |  |
| CUST-01 | P0 | import/fixture | NOT-RUN |  |  |  |  |
| FAIL-01 | P0 | import/fixture | NOT-RUN |  |  |  |  |
| FAIL-02 | P0 | import/fixture | NOT-RUN |  |  |  |  |

### GO

- All `P0` tests pass.
- No unresolved `P1` failure threatens calculation, persistence, workflow containment, or demonstration repeatability.
- CUST-01, FAIL-01, and FAIL-02 each pass twice from a documented reset state.
- No external Check Point mutation occurred.
- The acceptance-matrix input/checksum is preserved; fixture provenance and validation are complete when a fixture was required.
- Cleanup/restore is proven.
- The presenter has rehearsed all three demonstrations once without agent intervention.

### NO-GO

- Any `P0` failure.
- A demonstration depends on an unrecorded manual repair.
- A required fixture cannot be proven importer-produced.
- A failed calculation stores or retains a fallback position unexpectedly.
- Workflow creation fails because positioning failed.
- Customer data or credentials appear in diagnostics/evidence.
- Any firewall-side change is attributable to the positioning test.

## 16. Monday demonstration scripts

These are acceptance candidates, not promises. Use them only after the GO gate.

### DEMO-01 — Happy path: “the rule lands at the intended Section boundary”

**Backed by:** CUST-01.

**Preparation:**

- Use the immutable fixture or a frozen live import plus the preserved DATA-05A matrix that passed twice.
- Open the device-specific settings page and the read-only result query in advance.
- Reset to a fresh ticket/task state.
- Keep the selected layer UID, canonical Section name, and final rule UID in a private presenter note.

**Show:**

1. The gateway setting “Check Point: end of layer-guard zone-pair section.”
2. A simple access-rule request with the rehearsed source/destination ranges.
3. Successful creation of the gateway implementation task.
4. The persisted semantic JSON, explaining:
   - algorithm `7` selected this behavior;
   - `layer_uid` is the uniquely matching guarded inline layer;
   - `Below` plus the anchor rule UID means the end of the intended Section.
5. That nothing was pushed to Check Point—the feature calculated intent only.

**One-sentence explanation:** “FWO used the imported guards to find the only functional inline layer, used the designated zones to name the Section, and stored a stable semantic anchor for later execution.”

**Recovery:** If task creation or persistence does not finish within the rehearsed time, stop. Do not improvise with another gateway. Switch to the captured evidence and classify the live demo as blocked.

### DEMO-02 — Failure: “the required zone-pair Section does not exist”

**Backed by:** FAIL-01.

**Preparation:** Use the rehearsed second zone pair in the same validated dataset and acceptance matrix. Pre-show the read-only query proving its canonical Section is absent from the otherwise uniquely selected layer.

**Show:**

1. The same algorithm and a request whose zones are valid.
2. The unique guard-selected layer exists.
3. The canonical `[DESTINATION] < [SOURCE]` Section is absent in that layer.
4. Workflow task creation still succeeds.
5. `rule_position` remains null and the safe middleware outcome is contained.
6. No other layer or Section is used as a guess.

**One-sentence explanation:** “The traffic has a valid functional layer, but the required Section is missing, so FWO refuses to invent a placement and leaves the workflow intact.”

**Important:** The public log may say only `CalculationFailed`; explain that detailed policy/object content is deliberately excluded from diagnostics. The prepared data query proves the scenario.

### DEMO-03 — Failure: “none of the layer guards accepts this traffic”

**Backed by:** FAIL-02.

**Preparation:** Use ranges with valid zones but definite non-containment for every inline guard. Keep the redacted guard-coverage table ready.

**Show:**

1. The request and its valid zone resolution.
2. The read-only coverage evidence showing no guard contains both sides.
3. Successful workflow task creation.
4. Null `rule_position` and contained safe failure.
5. No fallback to the main layer or first inline layer.

**One-sentence explanation:** “No imported guard proves that this rule can function in an inline layer, so FWO fails closed instead of placing it somewhere merely convenient.”

### 16.1 Presenter reset checklist

Before every rehearsal/demo:

- [ ] Confirm repository/deployed commit and VM snapshot.
- [ ] Confirm the recorded task-creation workflow action and rule auto-planning setup.
- [ ] Confirm effective gateway algorithm.
- [ ] Confirm latest import ID or fixture checksum and acceptance-matrix checksum.
- [ ] Confirm the planned request ranges and expected scenario query.
- [ ] Use a fresh ticket/task name and record IDs.
- [ ] Confirm no pre-existing `rule_position` for the new task.
- [ ] Start bounded middleware log capture.
- [ ] Stop before any firewall implementation action.

After every rehearsal/demo:

- [ ] Save parsed result and bounded logs.
- [ ] Remove/close the test ticket through supported workflow behavior when appropriate.
- [ ] Restore changed settings.
- [ ] Confirm external Check Point state was not mutated.
- [ ] Reset the fixture/VM to the documented starting state.

## 17. Final reviewer checklist

- [ ] Both algorithms were tested through their exact production capabilities.
- [ ] Live-data suitability was decided from normalized data, not Check Point screenshots alone.
- [ ] Every fixture row came from real importer output and passed provenance/validation checks.
- [ ] No graph, guard, zone, recipe, or expected-position shortcut was used.
- [ ] Workflow creation was proven independently from calculation success.
- [ ] Positions were compared as parsed JSON with expected layer/anchor identities.
- [ ] Missing Section and unmatched layer both failed without fallback.
- [ ] Standard positioning remained independent of customer-only data.
- [ ] Safe logs and no firewall mutation were checked.
- [ ] Every `P0` result has evidence and a clean-state rerun where required.
- [ ] Cleanup/restore was proven.
- [ ] All three demonstrations were rehearsed from their written scripts.

