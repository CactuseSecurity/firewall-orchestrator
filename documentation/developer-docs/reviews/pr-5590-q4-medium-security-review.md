# Review of PR #5590: Q4 Medium-Severity Security Advisories

**Review date:** 2026-10-29  
**Reviewed head:** `910f0600c78f8c1dec344b53efe5320d29d99275`  
**Target branch:** `develop`  
**Latest reviewed `develop`:** `8933a3dd142148d1e106c8a90d0442fbfe3d1688`  
**Merge base:** `c900b800f8501ff06eb06cd1c18b6a8d54affb0a`  
**Pull request:** [CactuseSecurity/firewall-orchestrator#5590](https://github.com/CactuseSecurity/firewall-orchestrator/pull/5590)  
**Previous reviews:** [Round 1](https://github.com/CactuseSecurity/firewall-orchestrator/pull/5590#issuecomment-6480481552), [Round 2](https://github.com/CactuseSecurity/firewall-orchestrator/pull/5590#issuecomment-6491136722), [Round 3](https://github.com/CactuseSecurity/firewall-orchestrator/pull/5590#issuecomment-6492096456)

## Verdict

**Request changes.**

The current head has two new medium findings, F15 and F16. F1 remains accepted, F13 remains deferred to #5592, and F2-F12 plus F14 were verified as fixed.

The PR is also conflicted with the latest `develop` and is not currently mergeable. The refreshed version-gate run fails because GitHub cannot create a current merge ref for the conflicting branch.

## Findings

| ID | Severity | Status | Finding |
| --- | --- | --- | --- |
| F1 | high | accepted | Enabling ASA SSH host-key checking breaks imports until verified host keys are registered; the upgrade and UI now warn administrators. |
| F2 | medium | fixed | Workflow email links rendered a caller-supplied URL unencoded when no display text was provided. |
| F3 | medium | fixed | Recertification emails inserted owner and rule names into HTML bodies without encoding. |
| F4 | medium | fixed | Check Point fetch-phase API sessions were not logged out. |
| F5 | medium | fixed | External-ticket values could carry placeholder tokens into later raw-JSON substitutions. |
| F6 | low | fixed | ASA host-key checking also trusted system-wide known-hosts files. |
| F7 | low | fixed | User-visible security changes were absent from `whats_new_facts`. |
| F8 | low | fixed | New Python tests lacked return type annotations. |
| F9 | low | fixed | `checkpointR8x/cp_getter.py` exceeded the repository file-size limit. |
| F10 | low | fixed | `Report/Get` returned HTTP 200 with an empty body for filters rejected during query generation. |
| F11 | high | fixed | The product version was not raised after 9.8.3 was sealed. |
| F12 | low | fixed | Tests did not cover the notification-layout selection used by recertification send paths. |
| F13 | low | deferred | An ASA host-key warning can remain after a management is paused, deleted, or changed to another device type; tracked in #5592. |
| F14 | low | fixed | Functions moved to `cp_object_resolver.py` lacked return type annotations. |
| F15 | medium | new | FortiOS response bodies and JSON can still reflect the access token into importer errors, alerts, data issues, and logs. |
| F16 | medium | new | An explicit `--clear` of a super-manager silently retains data belonging to paused sub-managers. |

## Detailed Findings

### F15 (medium): FortiOS responses can still leak the access token

[`fos_getter.py` lines 63-72](https://github.com/CactuseSecurity/firewall-orchestrator/blob/910f0600c78f8c1dec344b53efe5320d29d99275/roles/importer/files/importer/fw_modules/fortiosmanagementREST/fos_getter.py#L63-L72) remove the token from transport exceptions, but include two untrusted values without scrubbing the known token:

- `response.text` for a non-200 response;
- the complete response JSON when `results` is absent.

The access token is sent as the `access_token` query parameter. A firewall, reverse proxy, or generated HTTP error page can echo the requested URL into either response. The resulting `FwApiCallFailedError` then reaches [`complete_import`](https://github.com/CactuseSecurity/firewall-orchestrator/blob/910f0600c78f8c1dec344b53efe5320d29d99275/roles/importer/files/importer/fwo_api_call.py#L394-L426), which copies the exception text into a data issue, an alert, and the importer log.

This leaves a response-reflection path for the credential that GHSA-vmp8-pwg3-h2qv is intended to keep out of those lower-trust outputs.

Required changes:

- Scrub the literal `access_token` from every response-derived error string before constructing the exception.
- Continue applying key-based redaction to structured JSON, but also handle the token embedded in arbitrary strings.
- Add tests where a non-200 `response.text` reflects the token.
- Add a test where a JSON response without `results` reflects the token in a field or message.

### F16 (medium): Clearing a super-manager retains paused sub-manager data

The [`--clear` option](https://github.com/CactuseSecurity/firewall-orchestrator/blob/910f0600c78f8c1dec344b53efe5320d29d99275/roles/importer/files/importer/import_mgm.py#L136-L142) promises to delete all data for the selected management instead of importing it.

For a super-manager, `clear_management()` builds an empty configuration for the super-manager and every sub-manager, then marks the operation as a clearing import. However, [`import_management_set`](https://github.com/CactuseSecurity/firewall-orchestrator/blob/910f0600c78f8c1dec344b53efe5320d29d99275/roles/importer/files/importer/model_controllers/fwconfig_import.py#L81-L96) unconditionally skips every sub-manager whose normal import is disabled.

As a result, running `--clear` without also knowing to pass `--force` clears the super-manager and active sub-managers but silently preserves all rules, objects, rulebases, and gateways belonging to paused Check Point domains or FortiManager ADOMs.

Required changes:

- Apply the paused-sub-manager skip only to normal imports, not when `is_clearing_import` is true; alternatively, make `get_paused_sub_manager_uids()` return an empty set for clearing imports.
- Add a regression test that clears a super-manager containing one active and one paused sub-manager and verifies that both empty configurations are imported.
- Keep the existing behavior for ordinary non-forced imports: paused sub-managers must retain their previous data.

## Prior Finding Disposition

- **Accepted:** F1. The breaking ASA behavior is deliberate and is now announced by the upgrade and the UI.
- **Deferred:** F13. The remaining alert lifecycle case is tracked in [#5592](https://github.com/CactuseSecurity/firewall-orchestrator/issues/5592) and is not re-raised here.
- **Verified fixed:** F2-F12 and F14.
- **New in this review:** F15 and F16.

The PR history contains four top-level issue comments, no submitted reviews, and no inline review comments. There are therefore no older inline review threads requiring a separate resolved/outdated interpretation.

## Merge and CI State

The reviewed head is based on `c900b800f`, while `develop` has advanced to `8933a3dd1`. GitHub reports the PR as conflicted and not mergeable.

The current conflicts include:

- `documentation/revision-history.md`, where both branches add a 9.8.4 section;
- `roles/tests-unit/files/FWO.Test/ApiRateLimitingTest.cs`, where both branches add endpoints to the expensive-policy expectation.

The resolution should preserve both sets of 9.8.4 release notes and both rate-limited endpoint assertions.

The refreshed [version-gate run](https://github.com/CactuseSecurity/firewall-orchestrator/actions/runs/40845443867) fails at `Evaluate version gate`. This is consistent with the gate being unable to fetch a current PR merge ref for a conflicting branch, rather than a new recurrence of F11.

Checks that passed on the current head before the base-branch conflict was introduced include:

- CodeQL for C#, Python, and Actions;
- Python code checks;
- release build;
- SonarQube/SonarCloud quality gates;
- GitGuardian;
- installation tests on `ubuntu-latest` with both default Ansible and ansible-core 2.18.

These results do not validate the eventual merge result with the latest `develop` and must be rerun after rebasing.

## Security Review

Security-sensitive paths inspected:

- report-filter parsing, query construction, request validation, size limits, and rate limiting;
- JSON rendering for SecureChange and Check Point ticket templates;
- workflow, notification, and recertification email HTML rendering;
- report HTML exports;
- Check Point, FortiManager, and FortiOS credential and session-token error paths;
- Check Point session cleanup;
- Cisco ASA SSH host-key verification and warning lifecycle;
- rsyslog receiver removal and local-input routing;
- importer handling of paused sub-managers and explicit clearing imports;
- installer defaults, upgrade warnings, idempotency, and Ubuntu installation checks.

Results:

- The earlier JSON and HTML injection findings are fixed at the reviewed head.
- Report filter length, nesting, list-size, text-size, IP, service, and semantic validation are present.
- Check Point sessions are closed across the reviewed object, rule, MDS, and global-domain paths.
- ASA strict host-key checking uses only the FWO known-hosts file when enabled.
- F15 leaves one response-reflection path for a FortiOS access token.
- F16 causes an explicit destructive maintenance workflow to produce incomplete results.
- No authentication, JWT-claim, LDAP, or role-permission changes are introduced by this PR.

## Installer and Upgrade Review

Change type: installer automation, upgrade behavior, and versioned feature.

Install/upgrade impact: both fresh installations and upgrades.

Verified points:

- The known-hosts creation task is idempotent and does not overwrite operator-managed content.
- The rsyslog block is removed using an idempotent Ansible module.
- The certificate and ASA upgrade warnings are read-only database queries.
- No database schema change is introduced, so no new SQL upgrade file is required.
- Product version, revision history, feature overview, and ASA warning threshold are aligned at 9.8.4 on the reviewed head.
- Both Ubuntu installer checks passed before `develop` advanced.

Required follow-up:

- Rebase on current `develop` and combine the two 9.8.4 documentation sections without losing either feature set.
- Rerun the version gate and both installation jobs on the mergeable result.

## Validation

| Validation | Result |
| --- | --- |
| `git diff --check` on the PR diff | Passed |
| CodeQL | Passed on reviewed head |
| Python code check | Passed on reviewed head |
| Release build | Passed on reviewed head |
| SonarQube/SonarCloud | Passed on reviewed head; 99.2% coverage reported on new code |
| GitGuardian | Passed on reviewed head |
| Ubuntu installation tests | Both passed on reviewed head |
| Refreshed version gate | Failed because the PR is conflicted |
| Mergeability | Failed; GitHub reports merge conflicts |

The previous review reports successful full local suites on equivalent code: 9,785 C# tests passed with 20 skipped, and 1,250 importer tests passed. These runs were not repeated during this review because the shared worktree contains unrelated uncommitted changes and does not represent PR #5590.

Not validated:

- a real FortiOS error response reflecting the requested URL;
- an explicit clear against a live super-manager with a paused domain or ADOM;
- real ASA, Check Point, or FortiManager connections;
- the warning components against a live Hasura subscription;
- the rebased merge result against current `develop`.

## Recommendations

### Fix before merge

1. **F15:** scrub the actual FortiOS access token from all response-derived error content and add reflection tests.
2. **F16:** ensure clearing imports process paused sub-managers and add the combined clear/pause regression test.
3. Rebase on current `develop` and preserve both sides of the revision-history and rate-limit-test conflicts.
4. Rerun the version gate, full unit suites, release build, static analysis, and both installation jobs.

### Already accepted or tracked separately

1. **F1:** retain the documented breaking behavior and administrator warnings.
2. **F13:** continue in #5592; do not expand this PR unless the maintainer chooses to pull that follow-up back in.

## Summary

The PR substantially improves the security posture of the report API, notification rendering, external-ticket templates, firewall API logging, SSH host verification, and syslog configuration. The previous review findings are resolved or explicitly dispositioned, but F15 still permits FortiOS response reflection to expose an API token, and F16 makes the documented clear operation incomplete for paused sub-managers.

Fix F15 and F16, resolve the current `develop` conflicts, and rerun CI before merging.
