# Rule Positioning Acceptance Runbook Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Create an extensive agent-first acceptance-testing runbook for standard and layer-guard/zone-pair rule positioning on an installed FWO VM.

**Architecture:** One authoritative Markdown runbook combines feature boundaries, environment discovery, importer-faithful fixtures, executable tests, evidence capture, cleanup, failure triage, and Monday demonstrations. Tests observe the persisted semantic position while keeping the external Check Point system read-only and excluding installer/importer correctness and firewall execution.

**Tech Stack:** Markdown, FWO.UI, GraphQL/Hasura, PostgreSQL, middleware logs, PowerShell/SSH, NUnit/.NET verification

**Spec:** `docs/superpowers/specs/2026-10-30-rule-positioning-acceptance-runbook-design.md`

## Global Constraints

- Do not test installation or upgrades.
- Do not modify the external Check Point testing system.
- Treat importer execution only as fixture preparation; do not claim to test importer correctness.
- Mock only normalized importer output in the isolated VM, never positioning readers, snapshots, zone results, recipes, or persisted expected positions.
- Cover both `DefaultEndOfRulebase` and `CheckPointLayerGuardEndOfZonePairSection`.
- Keep rendering, Check Point API writes, publish, and policy installation out of scope.
- Include one successful Monday demonstration plus missing-Section and unmatched-layer failures.

## Review Focus

- A fixture that looks convenient but could not be emitted by the importer must be rejected before positioning tests run.
- A missing Section or unmatched guard must fail safely without silently falling back to another Section/layer or breaking workflow creation.
- The standard algorithm must remain graph-only and must not acquire customer-only ticket, guard, or zone dependencies.
- Evidence must distinguish calculation failure, persistence failure, workflow failure, and unsuitable test data.
- Demonstration reset steps must prevent stale `rule_position` data from making a failed run appear successful.

---

### Task 1: Establish the executable runbook contract

**Files:**
- Create: `documentation/work-packages/firewall-rule-placement/ACCEPTANCE-TEST-RUNBOOK.md`
- Reference: `documentation/work-packages/firewall-rule-placement/README.md`
- Reference: `documentation/work-packages/firewall-rule-placement/DECISIONS.md`
- Reference: Phase 3 WP-05/WP-06 and Phase 4 WP-01/WP-02 implementation briefs

**Interfaces:**
- Consumes: Approved design and authoritative feature decisions.
- Produces: Runbook scope, terminology, safety rules, prerequisites, test-environment inventory, evidence conventions, and execution order.

- [x] **Step 1: Write the scope and feature-boundary sections**

State that semantic `request.impltask.rule_position` persistence is terminal, while rendering and firewall execution are excluded.

- [x] **Step 2: Add environment inventory and readiness gates**

Define placeholders and checks for VM/UI/API/middleware/database URLs, credentials, tunnel reachability, management/gateway IDs, successful import ID, log access, snapshot/backup, and evidence directory.

- [x] **Step 3: Add execution rules and severity model**

Define PASS/FAIL/BLOCKED/NOT-RUN, blocker versus non-blocker priority, test-data naming, read-only external-system policy, cleanup ownership, and stop conditions.

- [x] **Step 4: Verify traceability**

Check that every stated expected behavior cites the relevant decision/work-package section and does not claim firewall execution behavior.

### Task 2: Define importer-faithful data discovery and fixture preparation

**Files:**
- Modify: `documentation/work-packages/firewall-rule-placement/ACCEPTANCE-TEST-RUNBOOK.md`
- Reference: Check Point importer normalization and rulebase-link production code
- Reference: production graph/guard GraphQL queries and readers

**Interfaces:**
- Consumes: Runbook safety/readiness contract from Task 1.
- Produces: Live-data suitability checks and a deterministic normalized-fixture protocol consumed by all customer scenarios.

- [x] **Step 1: Document live normalized-data discovery**

Provide read-only queries that identify the latest successful rule import, initial/ordered/inline/section links, source guard rules, normalized source/destination objects, flattened group members, and candidate zone-pair Sections.

- [x] **Step 2: Define fixture provenance and generation**

Require sanitized Check Point API-shaped input to pass through the real importer once, followed by export of its actual normalized database output. Record importer revision, source shape, import ID, row counts, identifiers, and checksums.

- [x] **Step 3: Define fixture validation and replay**

List mandatory normalized tables/relationships and validation queries. Replay only inside the isolated VM using a dedicated ID/name namespace, snapshot first, and provide deterministic cleanup/restore steps.

- [x] **Step 4: Add anti-cheating assertions**

Explicitly reject mocked graph/guard snapshots, mocked zone/recipe results, direct recipe invocation, direct expected-position writes, invented historical intervals, and hand-polished rows that bypass importer behavior.

- [x] **Step 5: Test the fixture protocol on paper**

Trace one initial layer, guarded inline layer, populated zone-pair Section, rule objects/groups, and historical import through every production query required by the graph and guard readers.

### Task 3: Add the standard and customer acceptance suites

**Files:**
- Modify: `documentation/work-packages/firewall-rule-placement/ACCEPTANCE-TEST-RUNBOOK.md`

**Interfaces:**
- Consumes: Environment gates and validated importer-faithful data from Tasks 1-2.
- Produces: Executable tests with stable IDs, actions, expected JSON, evidence, cleanup, and triage.

- [x] **Step 1: Add baseline and configuration tests**

Cover build/unit baseline, UI localization, settings visibility/inheritance at supported scopes, enum persistence, and no-overlap capability checks.

- [x] **Step 2: Add standard algorithm tests**

Cover populated initial layer (`Below` final direct rule), empty initial layer (`Bottom`), per-gateway creation, multi-gateway behavior, graph-only isolation, missing/malformed graph safe failure, and stale-position clearing.

- [x] **Step 3: Add customer happy-path tests**

Cover unique guarded inline layer, complete single persisted zones, canonical destination-first Section matching, populated and representative empty-Section boundaries, same-zone placement, and same-ticket effective group resolution.

- [x] **Step 4: Add representative customer failure tests**

Cover missing zone-pair Section, no matching guard, ambiguous guard, incomplete/multiple zones, unresolved/cyclic same-ticket group, and fixed-import inconsistency. Assert no fallback and successful surrounding workflow behavior.

- [x] **Step 5: Add lifecycle and containment tests**

Cover creation-only activation, replay suppression, no recalculation on edits, recreation with new task ID, 30-second containment, safe logs, persistence failure distinction, and absence of firewall writes.

- [x] **Step 6: Add per-test evidence and triage**

For each test specify UI screenshots, IDs, queries, expected `rule_position`, relevant logs, data provenance, cleanup, likely failure owner, and whether failure blocks Monday.

### Task 4: Add weekend execution and Monday demonstrations

**Files:**
- Modify: `documentation/work-packages/firewall-rule-placement/ACCEPTANCE-TEST-RUNBOOK.md`

**Interfaces:**
- Consumes: Test catalog from Task 3.
- Produces: Agent execution batches, result summary, final release gate, and teachable demonstration scripts.

- [x] **Step 1: Define weekend batches and handoff format**

Order non-mutating discovery, baseline, standard, fixture preparation, customer success, controlled failures, lifecycle, cleanup, and reruns. Require agents to leave a concise evidence-backed handoff.

- [x] **Step 2: Add the result matrix and exit criteria**

Provide a table for status, evidence path, defect link, rerun status, blocker severity, and final GO/NO-GO. Require all critical tests and all three demo candidates to pass twice from reset state.

- [x] **Step 3: Write the successful placement demonstration**

Use a stable known dataset and show setting, request/implementation-task creation, expected guarded layer and Section, persisted semantic position, and absence of firewall mutation.

- [x] **Step 4: Write the missing-Section failure demonstration**

Use importer-faithful data with a unique matching layer but no canonical zone-pair Section. Show workflow success, null/cleared position, safe diagnostic evidence, and no fallback.

- [x] **Step 5: Write the unmatched-layer failure demonstration**

Use importer-faithful data whose guards definitely do not contain the request ranges. Show workflow success, null/cleared position, safe diagnostic evidence, and no main/first-layer fallback.

- [x] **Step 6: Add presenter recovery notes**

Give reset commands/checks, expected screen/query outputs, a backup demonstration dataset, and plain-language explanations of what each result proves.

### Task 5: Validate the completed documentation

**Files:**
- Verify: `documentation/work-packages/firewall-rule-placement/ACCEPTANCE-TEST-RUNBOOK.md`

**Interfaces:**
- Consumes: Completed runbook.
- Produces: A self-contained, internally consistent document ready for weekend agents.

- [x] **Step 1: Check required coverage by search**

Confirm explicit coverage of both algorithms, fixture provenance, missing Section, unmatched layer, evidence, cleanup, and demonstrations.

- [x] **Step 2: Check commands and schema names against source**

Verify GraphQL operation names, table/column names, enum values, ports, paths, persisted JSON fields, and expected anchor semantics against repository files.

- [x] **Step 3: Check links and Markdown structure**

Resolve every relative link and ensure headings, tables, checklists, and code fences render correctly.

- [x] **Step 4: Review scope exclusions**

Search for accidental installer testing, importer assertions, external Check Point mutations, renderer claims, publish/install steps, or direct expected-position seeding.

- [x] **Step 5: Review the final diff**

Run `git diff --check` and confirm only the approved specification, plan, and acceptance runbook were added, without modifying unrelated worktree changes.

