# Decisions needed for Check Point rule placement planning

The planning service will use imported FWO policy data and will not connect to Check Point. Three choices remain before the implementation packages can use one consistent contract. This note is intended for the product and engineering coworkers who own imports, workflow tasks, path analysis, and provisioning configuration.

## Maximum snapshot age

FWO records the completion time and control ID of each successful import, but the current normalized-config result does not expose them. The new snapshot reader can expose those values and calculate a fingerprint. It still needs a rule for when an import is too old to support placement planning.

Decision needed:

- What is the maximum permitted age of the latest completed successful import?
- Should this be one global duration or configurable per management?
- When the snapshot is too old, should planning fail immediately or request an import and require the caller to retry later?

Recommendation: define a configurable maximum age per management with a global default. Return `PolicyReadFailed` when the snapshot is missing, incomplete, in progress, or too old; do not silently use older data.

## Functional target identifiers

Before matching a Section, FWO must select exactly one policy package, access layer, and optional inline layer for the target gateway. Imported `RulebaseLink` data contains stable topology and rulebase UIDs, but a gateway can have more than one candidate. Choosing the first name or graph node could place a rule in the wrong policy.

Decision needed:

- Which existing or new configuration fields select the policy package and access layer?
- How is an optional inline layer selected, and what indicates that it is required?
- Are exact UIDs required, are configured names resolved uniquely to UIDs, or may FWO select automatically only when the imported topology has exactly one candidate?
- At which scopes may these selectors be configured: device type, management, gateway, or a subset?

Recommendation: use UIDs for the resolved target. Permit automatic resolution only when the imported gateway topology proves one candidate; otherwise require an explicit selector and fail on missing or ambiguous data.

This decision directly blocks WP-05 and shapes WP-01, WP-02, WP-04, WP-06, WP-07, and WP-08.

## Gateway and zone input ownership

Placement requires one target gateway and exactly one source and destination security zone. Path analysis already determines relevant gateways, while the compliance-zone service can derive zones from request object trees. The boundary must say whether placement receives those resolved results or performs either resolution itself.

Decision needed:

- Which workflow component binds one implementation task to one target gateway?
- Does the placement coordinator receive that gateway, or rerun path analysis?
- Does WP-03 receive the source and destination object trees and resolve zones at planning time, or receive an already resolved exact zone pair?
- If zones are resolved upstream, where are their stable IDs, names, resolution timestamp, and Security Matrix provenance stored?
- If the Security Matrix changes after approval, should placement use the original request-time zones or recalculate current zones?

Recommendation: the workflow/path-analysis stage supplies one gateway. WP-03 receives the task object trees and independently validates exactly one current source zone and destination zone. This keeps gateway discovery outside placement while enforcing the zone invariant at the placement boundary.

## Reply template

1. Maximum snapshot age and scope:
2. Behavior when the snapshot is stale:
3. Package and access-layer selectors:
4. Inline-layer selector and requirement rule:
5. Component that supplies the target gateway:
6. Zone input and handling of later Security Matrix changes:
