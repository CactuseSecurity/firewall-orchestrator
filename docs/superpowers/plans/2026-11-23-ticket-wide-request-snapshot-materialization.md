# Ticket-wide Request Snapshot Materialization Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Refresh and persist every stale ticket-local request-element snapshot before the Create Implementation Task action creates any implementation task.

**Architecture:** A pure shared service scans the complete in-memory ticket and produces a non-mutating replacement plan from exact `ObjectTaskId` references. `WfDbAccess` persists that complete plan with one narrow Hasura `update_many` mutation, applies the same values to the supplied ticket, and records ordinary request-task history; the existing implementation-task action invokes it once before selecting task scope, bundles, or devices.

**Tech Stack:** .NET 10, C#, NUnit, Hasura GraphQL/PostgreSQL, existing `FWO.Data` and `FWO.Services.Workflow` models.

**Spec:** `docs/superpowers/specs/2026-11-23-ticket-wide-request-snapshot-materialization-design.md`

## Global Constraints

- Support .NET 10 and introduce no new dependency.
- Keep source files at or below 1000 lines, methods at or below 100 lines, complexity at or below 10, and parameter counts at or below 7.
- Add XML documentation to new production methods and use specific exception types; never throw `Exception`.
- Put the GraphQL operation in its own file under `roles/common/files/fwo-api-calls/request/` and load it through `RequestQueries.GetQueryText`.
- Route persistence and history through `WfDbAccess`; changed request content uses `contentChange = true`.
- Preserve element IDs, owning task IDs, fields, request actions, `ObjectTaskId`, and catalog/Flow identities; update only copied value fields.
- Use NUnit `[TestFixture]` classes named `<Subject>Test.cs` and `Assert.That(...)` assertions.
- Follow the production constant-collection allocation rule in `.agents/AGENTS.md`; tests may use inline collections.
- Do not add schema, metadata permission, installer, external-ticket, middleware, or UI materialization changes.

## Review Focus

- A whole-group reference must stay unchanged while stale ticket-local member rows elsewhere in the same ticket are refreshed; Task 1 pins this.
- A ticket containing current, stale, and catalog-backed elements must submit only stale exact references while refreshing stale elements in unrelated tasks; Tasks 1 and 2 pin this.
- A null, short, zero-row, or multi-row batch result must leave memory unchanged and create no implementation tasks; Tasks 2 and 3 pin this.
- Equivalent CIDR notation and equal time instants with different offsets must not cause repeated writes; Task 1 pins this.
- Request-task scope, bundles, and multi-device creation must run one ticket-wide refresh before the first implementation insert; Task 3 pins this.

---

### Task 1: Pure ticket-wide refresh plan

**Files:**
- Modify: `roles/lib/files/FWO.Data/Workflow/WfObjectTaskHelper.cs:350-415`
- Create: `roles/lib/files/FWO.Services/Workflow/RequestSnapshotRefreshPlan.cs`
- Create: `roles/lib/files/FWO.Services/Workflow/RequestSnapshotRefresher.cs`
- Create: `roles/tests-unit/files/FWO.Test/RequestSnapshotRefresherTest.cs`

**Interfaces:**
- Consumes: `WfTicket`, `WfReqTask`, `WfReqElement`, `WfObjectTaskHelper.GetRequestedElement`, `WfObjectTaskHelper.HasValidElementStructure`.
- Produces: `WfObjectTaskHelper.CopyRequestedStateSnapshot(WfReqElement target, WfReqElement source)`, `RequestSnapshotRefresher.CreatePlan(WfTicket ticket)`, `RequestSnapshotRefresher.ApplyPlan(WfTicket ticket, RequestSnapshotRefreshPlan plan)`, and immutable plan properties `Replacements`, `ChangedTaskIds`, and `HasChanges`.

- [ ] **Step 1: Write the failing ticket-wide planner tests**

  Add `RequestSnapshotRefresherTest` with these tests and assertions:

  - `CreatePlan_RefreshesEveryStaleExactReferenceInTicket`: place stale copies in a group task and an unrelated access task, keep another copy current, and assert the plan contains only the stale element IDs in ticket order and both owning task IDs.
  - `CreatePlan_LeavesWholeGroupAndCatalogReferencesUnchanged`: include an access element pointing to a group task and a catalog member without `ObjectTaskId`; assert neither becomes a replacement while a stale member inside the group does.
  - `CreatePlan_RefreshesNetworkServiceAndTimeValues`: assert name, IP/range, protocol/ports, group name, and start/end times come from each referenced task while IDs, field, action, and reference remain from each target.
  - `CreatePlan_TreatsEquivalentValuesAsCurrent`: cover equivalent CIDR notation and time values representing the same UTC instants; assert `HasChanges` is false.
  - `CreatePlan_RejectsInvalidExactReference`: parameterize a missing task, non-object task, invalid object-task element structure, incompatible field, and stale target with non-positive element ID; assert `InvalidDataException` and an empty caller-side mutation state.
  - `ApplyPlan_ChangesOnlyPlannedValueFields`: assert the supplied ticket is unchanged before application and updated afterward, with all identity fields preserved.

- [ ] **Step 2: Run the planner tests and verify they fail**

  Run:

  ```powershell
  dotnet test roles/tests-unit/files/FWO.Test/FWO.Test.csproj --filter FullyQualifiedName~RequestSnapshotRefresherTest
  ```

  Expected: FAIL because `RequestSnapshotRefresher` and `RequestSnapshotRefreshPlan` do not exist.

- [ ] **Step 3: Add the shared value-copy helper**

  In `WfObjectTaskHelper`, add:

  ```csharp
  public static void CopyRequestedStateSnapshot(WfReqElement target, WfReqElement source)
  ```

  Copy only `IpString`/`IpEnd` with valid `Cidr` fallbacks, `Cidr`, `CidrEnd`, `Port`, `PortEnd`, `ProtoId`, `GroupName`, `Name`, `StartTime`, and `EndTime`. Do not copy any identity, relationship, field, or action property.

- [ ] **Step 4: Implement the immutable plan contract**

  Create `RequestSnapshotRefreshPlan` with:

  ```csharp
  public IReadOnlyList<WfReqElement> Replacements { get; }
  public IReadOnlyList<long> ChangedTaskIds { get; }
  public bool HasChanges { get; }
  internal RequestSnapshotRefreshPlan(List<WfReqElement> replacements, List<long> changedTaskIds)
  ```

  Store read-only views of the supplied lists. `HasChanges` is true exactly when `Replacements` is non-empty.

- [ ] **Step 5: Implement ticket-wide planning and application**

  Create `RequestSnapshotRefresher` with:

  ```csharp
  public static RequestSnapshotRefreshPlan CreatePlan(WfTicket ticket)
  public static void ApplyPlan(WfTicket ticket, RequestSnapshotRefreshPlan plan)
  ```

  `CreatePlan` scans every task and every element with positive `ObjectTaskId`. A reference to `group_create`, `group_modify`, or `group_delete` is a valid no-op because that group's member rows are independently included in the ticket-wide scan. An exact single-object reference must resolve within `ticket.Tasks`, pass `HasValidElementStructure`, and have a compatible field; clone the target, call `CopyRequestedStateSnapshot`, and add it only when normalized snapshot values differ. Preserve first-encounter ticket/element order and list each changed task ID once.

  Compare every field copied by `CopyRequestedStateSnapshot`; normalize IP/range notation before comparison and compare time bounds as UTC instants. `ApplyPlan` locates each exact task/element pair and copies only the planned value fields into the existing element instance so existing in-memory references remain valid.

- [ ] **Step 6: Run the planner tests and verify they pass**

  Run the Step 2 command.

  Expected: PASS for every `RequestSnapshotRefresherTest` test.

- [ ] **Step 7: Commit Task 1**

  ```powershell
  git add -- roles/lib/files/FWO.Data/Workflow/WfObjectTaskHelper.cs roles/lib/files/FWO.Services/Workflow/RequestSnapshotRefreshPlan.cs roles/lib/files/FWO.Services/Workflow/RequestSnapshotRefresher.cs roles/tests-unit/files/FWO.Test/RequestSnapshotRefresherTest.cs
  git commit -m "feat: plan request snapshot refresh"
  ```

---

### Task 2: Atomic persistence, in-memory application, and history

**Files:**
- Create: `roles/common/files/fwo-api-calls/request/updateRequestElementSnapshots.graphql`
- Modify: `roles/lib/files/FWO.Api.Client/Queries/RequestQueries.cs:45-55,145-160`
- Create: `roles/lib/files/FWO.Services/Workflow/WfDbAccessRequestSnapshotRefresh.cs`
- Create: `roles/tests-unit/files/FWO.Test/WfDbAccessRequestSnapshotRefreshTest.cs`

**Interfaces:**
- Consumes: `RequestSnapshotRefresher.CreatePlan`, `RequestSnapshotRefresher.ApplyPlan`, `RequestSnapshotRefreshPlan`, `WfDbAccess.LogWorkflowChange`, and `WfDbAccess.RequestTaskHistorySnapshot`.
- Produces: `RequestQueries.updateRequestElementSnapshots` and `WfDbAccess.RefreshTicketRequestSnapshots(WfTicket ticket) : Task`.

- [ ] **Step 1: Write the failing strict-persistence tests**

  Add `WfDbAccessRequestSnapshotRefreshTest` with a focused `SimulatedApiConnection` that records query order and update dictionaries. Add:

  - `RefreshTicketRequestSnapshots_WritesOneNarrowBatchThenUpdatesMemoryAndHistory`: assert the first query is `updateRequestElementSnapshots`, every update has only `where` and `_set`, `_set` has only `ip`, `ip_end`, `port`, `port_end`, `ip_proto_id`, `group_name`, `name`, `start_time`, and `end_time`, memory changes afterward, and one content-history call is made per changed task.
  - `RefreshTicketRequestSnapshots_CurrentTicketPerformsNoCalls`: assert no mutation and no history.
  - `RefreshTicketRequestSnapshots_InvalidBatchProofLeavesMemoryUnchanged`: parameterize null response, wrong result count, null result, zero rows, and more than one row; assert `InvalidOperationException`, no history, and original values remain.
  - `RefreshTicketRequestSnapshots_TransportFailureLeavesMemoryUnchanged`: assert the original transport exception propagates, no history runs, and memory remains stale.
  - `RefreshTicketRequestSnapshots_HistoryFailureKeepsSuccessfulRefresh`: make the existing history call fail and assert persistence and memory stay refreshed while `LogWorkflowChange` retains its established log-and-continue behavior.

- [ ] **Step 2: Run the persistence tests and verify they fail**

  Run:

  ```powershell
  dotnet test roles/tests-unit/files/FWO.Test/FWO.Test.csproj --filter FullyQualifiedName~WfDbAccessRequestSnapshotRefreshTest
  ```

  Expected: FAIL because the query and `RefreshTicketRequestSnapshots` do not exist.

- [ ] **Step 3: Add the narrow batch GraphQL operation**

  Create `updateRequestElementSnapshots.graphql`:

  ```graphql
  mutation updateRequestElementSnapshots($updates: [request_reqelement_updates!]!) {
    update_request_reqelement_many(updates: $updates) {
      affected_rows
    }
  }
  ```

  Declare and load `RequestQueries.updateRequestElementSnapshots` beside the existing request-element operations. Confirm against the pinned Hasura `v2.50.3` behavior that one `update_many` mutation is transactional; if it is not, stop and revise the design rather than adding partial-write recovery.

- [ ] **Step 4: Implement strict batch persistence**

  Create the `WfDbAccess` partial method:

  ```csharp
  public async Task RefreshTicketRequestSnapshots(WfTicket ticket)
  ```

  Build the complete plan first and return immediately when it has no changes. Capture independent before-state copies of only the changed request tasks for history. Build one ordered update entry per replacement using an exact ID predicate and only the nine approved value columns, normalizing CIDRs and UTC timestamps.

  Send one `RequestQueries.updateRequestElementSnapshots` call and require a non-null result list with exactly one non-null entry per update and `AffectedRows == 1` for every entry. Let transport/authorization failures propagate. Throw `InvalidOperationException` for an invalid success proof. Do not apply the plan or write history before this proof succeeds.

- [ ] **Step 5: Apply the proven plan and write ordinary history**

  After the exact batch proof, call `RequestSnapshotRefresher.ApplyPlan(ticket, plan)`. For each `ChangedTaskIds` entry, call existing `LogWorkflowChange` with change type `Update`, object type `RequestTask`, text `Refreshed ticket-local request snapshots`, the captured before task, the updated task, `ticket.Requester`, and `contentChange: true`.

  If needed, add a private helper in this partial file to create independent task/element copies for history. Do not add readback, recovery, alerting, or a second persistence path.

- [ ] **Step 6: Run the persistence tests and verify they pass**

  Run the Step 2 command.

  Expected: PASS for every `WfDbAccessRequestSnapshotRefreshTest` test.

- [ ] **Step 7: Commit Task 2**

  ```powershell
  git add -- roles/common/files/fwo-api-calls/request/updateRequestElementSnapshots.graphql roles/lib/files/FWO.Api.Client/Queries/RequestQueries.cs roles/lib/files/FWO.Services/Workflow/WfDbAccessRequestSnapshotRefresh.cs roles/tests-unit/files/FWO.Test/WfDbAccessRequestSnapshotRefreshTest.cs
  git commit -m "feat: persist request snapshot refresh"
  ```

---

### Task 3: Create Implementation Task action boundary

**Files:**
- Modify: `roles/lib/files/FWO.Services/Workflow/WfHandlerImplTaskTargets.cs:50-90`
- Modify: `roles/tests-unit/files/FWO.Test/ImplTaskCreationApiConnection.cs:1-75`
- Create: `roles/tests-unit/files/FWO.Test/WfHandlerRequestSnapshotRefreshTest.cs`

**Interfaces:**
- Consumes: `WfDbAccess.RefreshTicketRequestSnapshots(WfTicket ticket)` from Task 2 and the existing `CreateMissingImplTasksForTicket` workflow.
- Produces: one ticket-wide refresh per Create Implementation Task action, completed before the first implementation-task insert.

- [ ] **Step 1: Write failing action-boundary tests**

  Extend `ImplTaskCreationApiConnection` only as needed to record snapshot-batch and implementation-insert query order. Add `WfHandlerRequestSnapshotRefreshTest` with:

  - `CreateMissingImplTasksForTicket_RefreshesWholeTicketBeforeFirstInsert`: execute in request-task scope while an unrelated task also contains a stale reference; assert both copies are in the one batch, both are current in memory, and the batch query precedes every implementation insert.
  - `CreateMissingImplTasksForTicket_BundleAndMultipleDevicesRefreshOnce`: create a bundle producing multiple implementation tasks and assert exactly one refresh mutation occurs before all inserts.
  - `CreateMissingImplTasksForTicket_RefreshFailureCreatesNothing`: return an invalid affected-row result and assert the exception propagates, the ticket stays unchanged, and implementation insert count is zero.
  - `CreateMissingImplTasksForTicket_CurrentTicketSkipsRefreshMutation`: assert existing implementation creation behavior remains and no refresh query is sent.

- [ ] **Step 2: Run the action-boundary tests and verify they fail**

  Run:

  ```powershell
  dotnet test roles/tests-unit/files/FWO.Test/FWO.Test.csproj --filter FullyQualifiedName~WfHandlerRequestSnapshotRefreshTest
  ```

  Expected: FAIL because implementation creation does not call the refresh method.

- [ ] **Step 3: Invoke refresh once at the action boundary**

  In `CreateMissingImplTasks`, after `SetTicketEnv(ticket)` and synchronizing the optional scope task into `ActTicket`, call:

  ```csharp
  await dbAcc.RefreshTicketRequestSnapshots(ActTicket);
  ```

  when `dbAcc` is present. Place it before counting or selecting open/scoped/bundled tasks so the scan is ticket-wide and runs once regardless of how many implementation tasks or devices follow. Preserve the existing null-`dbAcc` behavior used by in-memory unit-test handlers.

- [ ] **Step 4: Run the action-boundary and existing implementation-task tests**

  Run:

  ```powershell
  dotnet test roles/tests-unit/files/FWO.Test/FWO.Test.csproj --filter "FullyQualifiedName~WfHandlerRequestSnapshotRefreshTest|FullyQualifiedName~WfHandlerImplTasksTest|FullyQualifiedName~WfHandlerImplTaskTargetsTest|FullyQualifiedName~WfHandlerAutoPlanTest|FullyQualifiedName~WfHandlerImplementationModeTest"
  ```

  Expected: PASS, with unchanged scope, bundle, target, rerun, and auto-plan behavior.

- [ ] **Step 5: Commit Task 3**

  ```powershell
  git add -- roles/lib/files/FWO.Services/Workflow/WfHandlerImplTaskTargets.cs roles/tests-unit/files/FWO.Test/ImplTaskCreationApiConnection.cs roles/tests-unit/files/FWO.Test/WfHandlerRequestSnapshotRefreshTest.cs
  git commit -m "fix: refresh snapshots before impl tasks"
  ```

---

### Task 4: Repository verification

**Files:**
- Verify all files changed by Tasks 1-3.
- Modify only files changed automatically by the mandated formatter, if any.

**Interfaces:**
- Consumes: the completed implementation from Tasks 1-3.
- Produces: formatted, building, fully tested code with measured coverage above 80% for the new refresh classes.

- [ ] **Step 1: Run the formatter**

  ```powershell
  dotnet format roles/FWO.sln
  ```

  Expected: exit code 0; review any formatter changes and keep them limited to task files.

- [ ] **Step 2: Run static diff checks**

  ```powershell
  git diff --check
  rg -n "Console\.WriteLine|Debug\.WriteLine|throw new Exception" roles/lib/files/FWO.Data/Workflow/WfObjectTaskHelper.cs roles/lib/files/FWO.Services/Workflow/RequestSnapshotRefreshPlan.cs roles/lib/files/FWO.Services/Workflow/RequestSnapshotRefresher.cs roles/lib/files/FWO.Services/Workflow/WfDbAccessRequestSnapshotRefresh.cs roles/lib/files/FWO.Services/Workflow/WfHandlerImplTaskTargets.cs
  ```

  Expected: `git diff --check` succeeds and the forbidden-pattern search returns no new production hits. Run the exact multiline constant-collection allocation scan required by `.agents/AGENTS.md` against the same changed production files and resolve every hit whose callee takes an array or read-only collection.

- [ ] **Step 3: Build the complete solution**

  ```powershell
  dotnet build --configuration Debug roles/FWO.sln
  ```

  Expected: exit code 0 with no errors.

- [ ] **Step 4: Run the complete C# unit suite**

  ```powershell
  dotnet test roles/tests-unit/files/FWO.Test/FWO.Test.csproj
  ```

  Expected: exit code 0 with no failed tests.

- [ ] **Step 5: Measure coverage for the new code**

  ```powershell
  dotnet test roles/tests-unit/files/FWO.Test/FWO.Test.csproj --filter "FullyQualifiedName~RequestSnapshot" --collect:"XPlat Code Coverage" --results-directory TestResults/RequestSnapshot
  ```

  Expected: the generated Cobertura report shows greater than 80% line coverage for `RequestSnapshotRefresher`, `RequestSnapshotRefreshPlan`, and `WfDbAccessRequestSnapshotRefresh`. Add focused tests rather than excluding uncovered production code if the threshold is missed.

- [ ] **Step 6: Commit formatter-only changes if needed**

  If Step 1 changed task files after the Task 3 commit:

  ```powershell
  git add -- roles/lib/files/FWO.Data/Workflow/WfObjectTaskHelper.cs roles/lib/files/FWO.Services/Workflow/RequestSnapshotRefreshPlan.cs roles/lib/files/FWO.Services/Workflow/RequestSnapshotRefresher.cs roles/lib/files/FWO.Services/Workflow/WfDbAccessRequestSnapshotRefresh.cs roles/lib/files/FWO.Services/Workflow/WfHandlerImplTaskTargets.cs roles/lib/files/FWO.Api.Client/Queries/RequestQueries.cs roles/tests-unit/files/FWO.Test/RequestSnapshotRefresherTest.cs roles/tests-unit/files/FWO.Test/WfDbAccessRequestSnapshotRefreshTest.cs roles/tests-unit/files/FWO.Test/WfHandlerRequestSnapshotRefreshTest.cs roles/tests-unit/files/FWO.Test/ImplTaskCreationApiConnection.cs
  git commit -m "style: format request snapshot refresh"
  ```

- [ ] **Step 7: Confirm final scope**

  ```powershell
  git status --short
  git diff --stat Cactus/develop...HEAD
  git log --oneline Cactus/develop..HEAD
  ```

  Expected: clean worktree; only the design, plan, focused shared-service/query/action files, and their tests differ from `Cactus/develop`.
