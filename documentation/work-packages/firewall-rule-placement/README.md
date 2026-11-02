# Firewall rule positioning

Firewall rule positioning calculates a semantic target for a new firewall rule and stores it on the implementation task. It does not render or execute a vendor API request, publish a session, or install policy.

## Supported behavior

Positioning runs when the middleware creates a gateway-specific implementation task for a new access rule. The effective device-specific setting selects one non-overlapping strategy:

- `DefaultEndOfRulebase` places the rule after the final direct rule in the imported initial layer, or at the bottom of an empty layer.
- `CheckPointLayerGuardEndOfZonePairSection` resolves the requested addresses, selects exactly one Check Point inline layer from its imported guards, finds the canonical `[DESTINATION] < [SOURCE]` Section, and places the rule after its final direct rule or at the bottom of an empty Section.

The calculation uses normalized data from one completed import. Missing, malformed, ambiguous, or incomplete input fails closed: workflow creation continues, but the stored position is cleared instead of falling back to another layer or Section.

Stored positions are versioned JSON objects. Readers tolerate unknown or malformed payloads so one bad row cannot prevent a ticket from loading. Only the middleware may update `request.impltask.rule_position`.

## Main implementation locations

- Contracts and persisted position types: `roles/lib/files/FWO.Data/RulePositioning/`
- Strategy orchestration and imported graph readers: `roles/lib/files/FWO.Services/RulePositioning/`
- Address, group, and zone resolution: `roles/lib/files/FWO.Compliance/RulePositioning/`
- Workflow activation and persistence: `roles/lib/files/FWO.Services/Workflow/`
- Middleware registration: `roles/middleware/files/FWO.Middleware.Server/RulePositioningServiceCollectionExtensions.cs`
- GraphQL operations: `roles/common/files/fwo-api-calls/rulePositioning/`
- Database creation and upgrade paths: `roles/database/files/sql/creation/` and `roles/database/files/upgrade/9.8.5.sql`
- Hasura permissions: `roles/api/files/replace_metadata.json`
- Device-specific setting and help text: `roles/ui/files/FWO.UI/Pages/Settings/DeviceSpecificSettingsData.cs` and `roles/ui/files/FWO.UI/Pages/Help/`

## Validation

The unit suite under `roles/tests-unit/files/FWO.Test/` covers serialization, strategy selection, graph and guard mapping, zone resolution, workflow activation, persistence, malformed stored values, and fail-closed behavior. The 9.8.5 integration fixtures under `roles/tests-integration/files/database/` cover both the rule-position schema migration and the SecureChange migration sharing that release file.

The importer deliberately stores flattened network-group rows for the group itself, nested groups, and leaves. The Check Point guard reader ignores the group rows and evaluates only address-bearing leaves; genuinely addressless non-group leaves remain unresolved and cause safe indeterminate behavior.
