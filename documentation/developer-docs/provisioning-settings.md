# Hierarchical provisioning settings

Provisioning values are stored as typed JSON in `provisioning_config_value`.
The hierarchy is Global > Device type > Manager > Gateway. An absent override
inherits the nearest ancestor value, or the compiled default. Override scope
restrictions apply to editing; inherited values remain available at descendant
levels through `ProvisioningSettingsManager.LoadEffectiveValueAsync`.

The settings page exposes the configuration contract for Guardicore integration.
The Guardicore writer is separate work. Path analysis remains configured under
General settings; no additional provisioning path-analysis setting is introduced.

"Config levels" are the levels at which the API accepts an override. The settings
page offers a setting only down to its "UI levels"; an override stored deeper
remains effective and is shown as a dormant override that can be cleared there.

| Key | Type | Default | Config levels | UI levels |
| --- | --- | --- | --- | --- |
| installOn | enum | ANY | All | All |
| positioningAlgorithm | enum | DefaultEndOfRulebase | All | All |
| ruleLoggingOverride | enum | log | All | All |
| serviceObjectCreation | enum | Supermanager | All | Global to manager |
| addressObjectCreation | enum | Supermanager | All | Global to manager |
| timeObjectCreation | enum | Supermanager | All | Global to manager |
| labelCreation | enum | supermanager | All | Global to manager |
| ruleType | enum | access | All | Global to manager |
| commonServiceSeparation | boolean | false | All | Global to manager |
| ruleZoneFromOverride | string | ANY | All | Global to manager (Fortinet only) |
| ruleZoneToOverride | string | ANY | All | Global to manager (Fortinet only) |
| implementationMode | enum | Automatic | All | All |
| implementationModeOverrideForRules | enum | Automatic | All | All |
| rerouteToExternalTicketSystem | integer | undefined | All | All |
| securityProfiles | string array | [] | Device type, manager, gateway | Device type, manager |
| ownerIdCustomFieldName | string | OwnerId | Global, device type, manager | Global to manager |
| changeIdCustomFieldName | string | ChangeId | Global, device type, manager | Global to manager |
| importerHostName | string | empty (host of the provisioning importer) | Global, device type, manager | Global to manager |
| planImplementation | boolean | true | All | All |
| planImplementationOverrideForRules | boolean | true | All | All |
| modellingCreateAppZones | boolean | true | Global, device type, manager | Global to manager |
| modellingImplementNetworkAreaObjects | boolean | false | Global, device type, manager | Global to manager |
| modellingDynIpLabelsVisibleAsGroups | boolean | true | Global, device type, manager | Global to manager |
| modellingConnectionIdField | enum | Name | Global, device type, manager | Global to manager |
| modellingConnectionIdTemplate | string | FWOC{ID} | Global, device type, manager | Global to manager |
| templates | string | {} | Device type | Device type |

Booleans are stored as JSON `true`/`false`. `installOn` offers `ANY` and `gatewaysInPath`.
`ruleType` additionally offers `accessAndNat`, `accessAndIPS`, `accessNatIPS`, and
`AppSecurity`. Logging offers `log`, `log_track`, and `none`. Time objects and labels
can additionally be `ignore`; service and address objects cannot. Labels use
lowercase manager names.

Implementation modes are `Automatic`, `Manual`, and
`reroute_to_external_ticket_system`; the rule override uses `Automatic`, `Manual`,
and `rerouteToExternalTicketSystem`. `rerouteToExternalTicketSystem` stores the
positive ID of a configured external ticket system (JSON integer); undefined is not
stored, so the level inherits instead.

Positioning offers `DefaultEndOfRulebase`, `cpInlineLayerPerZonePair`, `endOfZone`,
`endOfAppSection`, `endOfAppSectionDistinguishCommonServices`, and
`allowCategoryEndOfAppSectionDistinguishCommonServices`.

`modellingConnectionIdField` offers `Name`, `Comment`, and `CustomField`: the field of
a provisioned object that carries the ID of the modelling connection, formatted by
`modellingConnectionIdTemplate`. The compiled `importerHostName` default is empty: the
importer that runs the provisioning then uses its own host name. The default therefore
does not depend on which FWO host (UI, middleware or importer) resolves the settings.

## Upgrade to 9.7.4

The upgrade saves the original provisioning rows once in `public.provisioning_config_backup`
under version `9.7.4` before making any changes. It renames
`logging`, `zoneFrom`, and `zoneTo` and converts the old enum member names to the
wire values above. If old and canonical keys both exist, the canonical key wins.
`FwoAuto` becomes `Automatic` and `TufinSc` the external-ticket mode; `None` becomes
`Manual` with planning set to `false` unless an explicit planning override already
exists. The other old modes always planned the implementation: a level that overrides
`FwoAuto`, `Manual` or `TufinSc` and would inherit the disabled planning of a `None`
level above it gets `planImplementation` set to `true`. If exactly one Tufin SecureChange
system is configured in the external ticket systems, former `TufinSc` levels reroute to
it via `rerouteToExternalTicketSystem`; otherwise select the ticket system on those
levels after upgrading. `ruleCategory` becomes `commonServiceSeparation` (`common_service`
is `true`, `app` is `false`).

Templates outside the device type level and custom install-on targets other than the
two permitted choices are removed from active provisioning data. Before upgrading,
review global, manager and gateway templates; after upgrading, reconfigure them at
device type level where appropriate. The backup retains their node IDs, types, object
keys and values so no original setting data is lost. Re-running the migration
preserves the original backup and the converted values. The archive is intentionally
absent from Hasura metadata, so anonymous and ordinary GraphQL roles cannot read it.
Administrators can inspect it through PostgreSQL:

```sql
SELECT config_values FROM public.provisioning_config_backup WHERE version = '9.7.4';
```

The migration regression probe `roles/tests-integration/files/database/test-upgrade-9.7.4.sql`
uses temporary tables and rolls back its transaction. The database integration tests
(`roles/tests-integration/tasks/test-database.yml`) run it with the upgrade file inserted at
its two `-- @run-upgrade-9.7.4` markers.
