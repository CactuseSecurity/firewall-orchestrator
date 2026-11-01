# Firewall rule-positioning presentation and practice guide

## Goal

Demonstrate, with a real FWO workflow, that the Check Point layer-guard/zone-pair positioning algorithm:

1. calculates and stores the intended semantic position when the imported topology, guards, zones, and Section agree;
2. leaves the position empty when the required Section is absent; and
3. leaves the position empty when no layer guard accepts the requested traffic.

The feature stops at a semantic position stored in FWO. It does **not** add a rule to Check Point, publish a Check Point session, or install policy. That boundary is part of the design, not a missing flourish.

This guide is based on the successful VM rehearsal captured in:

- `C:\Users\ek142\Downloads\2026-11-01T1414-codex-rehearsal.zip`
- fixture and acceptance evidence: `C:\Users\ek142\Downloads\2026-10-30T1908-codex-v2.zip`

The rehearsal's disposable VM was deleted afterward. Its success proves the procedure and fixture; it does not prepare the separate presentation laptop automatically.

## The story in thirty seconds

Use this as the opening explanation:

> A requested access rule first resolves to source and destination zones. Imported Check Point guard rules identify the one functional inline layer for that traffic. FWO then looks inside that layer for the canonical destination-to-source Section and stores a stable position relative to its last rule. If either the layer or Section cannot be identified safely, the workflow still succeeds, but FWO stores no position instead of guessing.

The memorable shorthand is:

```text
traffic -> zones -> guarded layer -> zone-pair Section -> semantic anchor
```

The canonical Section name is destination first:

```text
[DESTINATION_ZONE] < [SOURCE_ZONE]
```

For the successful demo that is:

```text
[RP_DEST] < [RP_SOURCE]
```

## Presentation division of labour

### Testing agent or technical helper

Before the presentation, the helper prepares the actual presentation VM, validates the fixture, opens a read-only database terminal, and takes a VM snapshot. This work should not be performed live in front of the audience.

### Presenter

You show the gateway setting once, create and approve three ordinary workflow requests, and display the stored outcome after each one. You do not touch fixture SQL, modify imported Check Point data, open the Implementations page, or perform any firewall action.

## Day-before preparation on the presentation laptop

Ask the testing agent to complete this checklist on the actual laptop and VM you will use on Monday:

- [ ] Install the exact branch/build intended for the presentation.
- [ ] Replay the importer-faithful fixture from the v2 evidence bundle.
- [ ] Confirm fixed import `45`, management ID `2`, and gateway ID `2`.
- [ ] Import and designate matrix `RP acceptance 2026-10-30T1908-codex-v2`.
- [ ] Select management `checkpoint_demo` and gateway `CP_SMS_GWCluster_Test4FWO`.
- [ ] Set gateway positioning to `Check Point: end of layer-guard zone-pair section`.
- [ ] Confirm **Auto plan implementation** is `yes`.
- [ ] Override **Auto plan implementation for rules** to `yes`.
- [ ] Confirm Approved state `99` has the `Create implementation tasks` action.
- [ ] Confirm the importer is stopped so the fixed fixture cannot drift.
- [ ] Confirm FWO services are healthy.
- [ ] Run one private happy-path smoke test and verify the semantic JSON.
- [ ] Reset only the disposable presentation data, leaving fixture, matrix, and settings intact.
- [ ] Take a clean VM snapshot after the smoke test and reset.
- [ ] Put the rehearsal screenshots and proof files somewhere reachable if the live VM misbehaves.

The matrix must contain these exact one-host zones:

| Zone | Subnet |
| --- | --- |
| `RP_SOURCE` | `1.2.3.4/32` |
| `RP_DEST` | `1.2.4.5/32` |
| `RP_SOURCE_MISSING` | `1.2.5.5/32` |
| `RP_DEST_MISSING` | `1.2.6.6/32` |
| `RP_OUT_SOURCE` | `8.8.8.8/32` |
| `RP_OUT_DEST` | `8.8.4.4/32` |

Do not replace the fixture with a hand-shaped ideal data model. It must remain the normalized structure that the importer would produce.

## Presenter setup ten minutes before the meeting

1. Start or restore the prepared VM.
2. Open `https://localhost:26443/` and log in as the test `admin`.
3. Confirm the header shows the expected FWO build. The rehearsal used `v9.8.5`.
4. Open **Settings > Network Matrix** and confirm the designated matrix is `RP acceptance 2026-10-30T1908-codex-v2`.
5. Open **Settings > Device specific settings**.
6. Expand **Check Point R8x > checkpoint_demo** and select `CP_SMS_GWCluster_Test4FWO`.
7. Confirm **Positioning algorithm** says `Check Point: end of layer-guard zone-pair section` and is marked **Set here**.
8. Scroll down and confirm **Auto plan implementation** and **Auto plan implementation for rules** both read `yes`.
9. In a terminal, open read-only PostgreSQL access:

   ```bash
   sudo -u postgres psql -d fworchdb -v ON_ERROR_STOP=1 -P pager=off
   ```

10. At the `psql` prompt, enable automatic expanded display and paste the proof query from the next section once. Re-run it later with the Up arrow and Enter.
11. Close unrelated applications and notifications. Keep only the browser, proof terminal, and this guide available.
12. Do not practice again on the clean presentation snapshot unless you will reset it afterward.

## Prepared proof queries

Use this compact query after each scenario. It distinguishes a missing implementation task from the expected fail-closed `NULL` position.

```sql
\x auto

SELECT
    t.id AS ticket,
    t.title,
    rt.id AS request_task,
    it.id AS implementation_task,
    CASE
        WHEN it.id IS NULL THEN 'ERROR: no implementation task'
        WHEN it.rule_position IS NULL THEN 'NULL: no safe position'
        ELSE 'SET: semantic position stored'
    END AS positioning_outcome,
    COALESCE(it.rule_position ->> 'positioning_algorithm', '-') AS algorithm,
    COALESCE(it.rule_position ->> 'anchor_kind', '-') AS anchor_kind
FROM request.ticket t
JOIN request.reqtask rt ON rt.ticket_id = t.id
LEFT JOIN request.impltask it ON it.reqtask_id = rt.id
WHERE t.title LIKE 'RP Demo 2026-11-02 %'
ORDER BY t.id;
```

After the happy path, use this second query to show the semantic object cleanly:

```sql
SELECT jsonb_pretty(it.rule_position)
FROM request.ticket t
JOIN request.reqtask rt ON rt.ticket_id = t.id
JOIN request.impltask it ON it.reqtask_id = rt.id
WHERE t.title = 'RP Demo 2026-11-02 01 - Happy path';
```

Expected happy-path object:

```json
{
  "layer_uid": "79e32522-5db5-441a-8227-e48725c626e4",
  "anchor_kind": "BELOW",
  "position_type": "check-point.rule-position.v1",
  "schema_version": 1,
  "anchor_rule_uid": "faf749e6-4059-4bae-b2f4-3f54d42c50c6",
  "positioning_algorithm": 7
}
```

The UUIDs are fixture-specific. For this prepared fixture they must match exactly.

## Suggested running order

Budget about 12 to 15 minutes:

| Time | Activity |
| --- | --- |
| 1 minute | Explain the problem, safety boundary, and algorithm |
| 1 minute | Show the selected gateway setting |
| 4 minutes | Happy path and stored semantic position |
| 3 minutes | Missing-Section fail-closed path |
| 3 minutes | No-matching-layer fail-closed path |
| 1–3 minutes | Summary and questions |

The ordinary standard algorithm was also covered by acceptance testing. Keep the live presentation focused on the new guarded-layer behavior unless your boss asks; three coherent stories beat a settings-dropdown safari.

## Common workflow used by all three demonstrations

Use these steps each time. The waits are the observed rehearsal timings, not ritual sacrifices to the UI gods.

1. Click top menu **Workflow** and wait about 3 seconds.
2. Click left menu **Create Ticket** and wait about 3 seconds.
3. Click the green **+** above the ticket list.
4. Enter the scenario's exact request title. Leave **State** as `Draft`, **Requester** as `admin`, and leave priority, deadline, and reason empty.
5. Under **Tasks**, click the green **+**.
6. In the Task dialog enter:

   - **Title:** scenario-specific title below.
   - **Type:** `Access`.
   - **Management:** `checkpoint_demo`.
   - **Gateway:** `CP_SMS_GWCluster_Test4FWO`.
   - **Rule Action:** `accept`.
   - **Logging:** `log`.
   - **Owner**, validity fields, and reason: empty.
   - **Source:** exact scenario `/32`, followed by its green **+**.
   - **Destination:** exact scenario `/32`, followed by its green **+**.
   - **Service:** `443`, protocol `TCP`, followed by its green **+**.

7. Verify that source and destination appear as blue chips with green validation checks, and service appears as `443/TCP`.
8. Click the blue **+** at the bottom-right of the Task dialog. Do **not** use the blue check button in the middle of the form.
9. Confirm the request and task are both `Draft`.
10. Click the blue diagonal-arrow **Change state** button at the bottom-right of the Request dialog.
11. Select **Requested**, click the blue disk **Save**, and wait about 5 seconds.
12. Open **Approvals** and find the row with the exact title.
13. Click its yellow pencil **Edit** button.
14. In the task row, click yellow **Start approval** and wait about 6 seconds.
15. Confirm the task is **In Approval**, with handler `admin`, the correct management/gateway, both address chips, and `443/TCP`.
16. Click the blue thumbs-up **Change state** button at the bottom-right.
17. Select **Approved**, click the blue disk **Save**, and wait 10 seconds.
18. Confirm the request and task both show **Approved**.
19. Stop UI interaction. Do not open **Implementations**.
20. Switch to the terminal and run the compact proof query.

## Demonstration 1: happy path

### Values

| Field | Value |
| --- | --- |
| Request title | `RP Demo 2026-11-02 01 - Happy path` |
| Task title | `Happy path` |
| Source | `1.2.3.4/32` |
| Destination | `1.2.4.5/32` |
| Service | `443/TCP` |
| Expected Section | `[RP_DEST] < [RP_SOURCE]` |

### What to say while creating it

> These addresses resolve through the designated matrix to RP_SOURCE and RP_DEST. The imported guard structure uniquely selects the functional inline layer, and that layer contains the corresponding destination-to-source Section.

### Expected proof

The compact query must show:

- an implementation-task ID;
- `SET: semantic position stored`;
- algorithm `7`; and
- anchor kind `BELOW`.

Then show the pretty JSON and say:

> FWO stored a versioned, vendor-aware semantic instruction: in this exact imported layer, position the later rule below this existing boundary rule. It did not call Check Point.

Do not spend time reading UUIDs aloud. Point out that they make the layer and boundary deterministic, then move on before everybody ages visibly.

## Demonstration 2: required Section is missing

### Values

| Field | Value |
| --- | --- |
| Request title | `RP Demo 2026-11-02 02 - Missing Section` |
| Task title | `Missing Section` |
| Source | `1.2.5.5/32` |
| Destination | `1.2.6.6/32` |
| Service | `443/TCP` |
| Expected Section | `[RP_DEST_MISSING] < [RP_SOURCE_MISSING]` |

### What to say before approving it

> This traffic still resolves to known zones and its guard identifies a functional layer. But the canonical Section for this zone pair is deliberately absent from that layer.

### Expected proof

The compact query must show:

- an implementation-task ID; and
- `NULL: no safe position`.

The algorithm and anchor columns should be `-`.

Say:

> The workflow and implementation-task creation remain intact, but positioning fails closed. FWO does not fall back to another Section or another layer and does not invent a placement.

The public outcome is intentionally coarse. The database result may not literally say “Section not found”; the controlled fixture proves that this pair has a unique guarded layer and no matching Section inside it.

## Demonstration 3: no matching layer guard

### Values

| Field | Value |
| --- | --- |
| Request title | `RP Demo 2026-11-02 03 - No matching layer` |
| Task title | `No matching layer` |
| Source | `8.8.8.8/32` |
| Destination | `8.8.4.4/32` |
| Service | `443/TCP` |

### What to say before approving it

> These are still valid addresses in the designated test matrix, but their ranges sit outside every imported inline-layer guard. There is therefore no defensible functional layer in which to search for a Section.

### Expected proof

The compact query must show:

- an implementation-task ID; and
- `NULL: no safe position`.

Say:

> Again, FWO preserves the workflow but refuses to guess. It does not fall back to the initial layer, the first inline layer, or any conveniently named Section.

This is more precise than saying the layer disappeared. The layers exist; none of their guards accepts this traffic.

## Final summary

End with this:

> We have shown the intended success path and the two most probable customer-data failures. With complete imported semantics, FWO produces a deterministic semantic anchor. With a missing Section or no matching guarded layer, it leaves the position empty while preserving the workflow. In all three cases it makes no external firewall change.

## Recovery rules during the live presentation

### The page is still working after approval

1. Wait up to 30 seconds total.
2. Navigate back to **Approvals** once and reopen the ticket.
3. Run the read-only proof query.
4. Do not click Approve a second time.

### No implementation task appears

Stop the live path. This is a workflow prerequisite problem, not evidence that the positioning algorithm returned `NULL`. Check afterward that:

- Approved state has the `Create implementation tasks` action; and
- both auto-plan settings are `yes`.

During the meeting, switch to the matching rehearsal screenshot and `proof-*.txt` result from the ZIP.

### The happy path unexpectedly returns `NULL`

Do not improvise database changes. Show the captured rehearsal proof, state that the prepared environment has drifted, and continue with the conceptual explanation. Afterward check the designated matrix, gateway override, exact addresses, fixture import, and auto-planning settings.

### A failure path unexpectedly produces a position

Stop. Do not present that as success and do not advance the implementation task. It indicates fixture or configuration drift and defeats the safety point of the demonstration.

### FWO middleware is unhealthy

The actual systemd service is:

```bash
systemctl status fworch-middleware.service
```

If a technical helper is present, let them handle service recovery. Never restart or write to Check Point as part of demo recovery.

## Practice and reset policy

Practice against a disposable snapshot, not the final clean presentation state.

After capturing proof for a practice scenario, the technical helper may use this transaction **only on the disposable test VM**:

```sql
BEGIN;
DELETE FROM request.state_change_execution;
DELETE FROM request.ticket;
COMMIT;

SELECT
  (SELECT count(*) FROM request.ticket) AS tickets,
  (SELECT count(*) FROM request.reqtask) AS request_tasks,
  (SELECT count(*) FROM request.impltask) AS implementation_tasks,
  (SELECT count(*) FROM request.state_change_execution) AS state_change_executions;
```

All four counts must be zero. The fixture, matrix, management, gateway, and imported topology must remain untouched.

For the actual presentation, do not reset between the three scenarios. The unique titles let all results coexist, and the compact query builds a useful three-row conclusion:

```text
Happy path        -> SET
Missing Section   -> NULL
No matching layer -> NULL
```

After the presentation, restore the snapshot or let the technical helper perform the documented disposable reset.

## Questions your boss is likely to ask

### “Why is `NULL` a successful result?”

It is not a successful placement; it is successful safety behavior. The workflow continues, but FWO records that it cannot determine a safe position instead of silently choosing the wrong location.

### “Does this already create the rule in Check Point?”

No. The current feature calculates and persists semantic intent in FWO. Rendering and executing an external Check Point API request are outside this feature boundary.

### “Why store a semantic anchor instead of a rule number?”

Rule numbers move when policies change. A layer UID plus `ABOVE`/`BELOW` and an anchor-rule UID describes the intended boundary more stably.

### “Why is the Section destination first?”

That is the defined naming contract for this policy structure: `[DESTINATION] < [SOURCE]`. The builder owns that canonical form so individual callers cannot casually reverse it.

### “What data does the calculation trust?”

It trusts normalized imported topology and guard semantics from one fixed successful import, the designated network matrix for zone resolution, and the final effective workflow request data.

### “What if two guards or two Sections match?”

Ambiguity fails closed. A deterministic position requires exactly one defensible structural path.

### “What did this rehearsal not prove?”

The rehearsal proved the three presentation scenarios. It did not execute every P1 edge case, did not test the importer itself, did not write to Check Point, and did not exercise the same-ticket object/group modification scenario (`CUST-05`) in this live run. Those boundaries should be stated plainly rather than inflated into a heroic testing legend.

### “Was the standard positioning approach checked?”

Yes, the acceptance run covered the standard end-of-rulebase path separately. This presentation concentrates on the new layer-guard/zone-pair behavior and its two likeliest fail-closed outcomes.

## Personal practice scorecard

Run the full presentation twice. You are ready when you can answer yes to all of these:

- [ ] I can explain the five-step mental model without reading it.
- [ ] I can enter each address pair without consulting another file.
- [ ] I remember to click each field's green **+** and see the blue chip.
- [ ] I use the bottom-right blue **+** in the Task dialog, not the middle check.
- [ ] I stop after approval and never open Implementations.
- [ ] I can explain why both negative scenarios still create implementation tasks.
- [ ] I can explain why `NULL` is safer than a fallback.
- [ ] I can state clearly that no Check Point write occurs.
- [ ] I can recover calmly using the captured evidence instead of improvising.
- [ ] The full run takes no more than 15 minutes.

## Rehearsal evidence index

The rehearsal ZIP contains:

- `03-matrix-designated.png` — correct designated matrix;
- `04-gateway-positioning.png` — algorithm selected at the gateway;
- `05-gateway-auto-plan.png` — rule auto-planning set to `yes`;
- `06-workflow-prerequisite.png` — Approved-state implementation-task action;
- `07` through `10` — happy path from Draft to Approved;
- `11` through `14` — missing-Section path from Draft to Approved;
- `15` through `18` — no-matching-layer path from Draft to Approved;
- three `proof-*.txt` files — exact database outcomes; and
- `19-final-clean-state.png` plus `CLEAN-STATE.txt` — confirmed disposable reset.

If the live environment misbehaves, these are your honest fallback evidence. Say it was the successful rehearsal, not the current live result.
