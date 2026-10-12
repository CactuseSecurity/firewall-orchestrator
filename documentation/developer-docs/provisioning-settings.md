# Hierarchical provisioning settings

Provisioning values are stored as typed JSON in `provisioning_config_value`.
The hierarchy is Global > Device type > Manager > Gateway. An absent override
inherits the nearest ancestor value, or the compiled default. Override scope
restrictions apply to editing; inherited values remain available at descendant
levels through `ProvisioningSettingsManager.LoadEffectiveValueAsync`.

The settings page exposes the configuration contract for Guardicore integration.
The Guardicore writer is separate work. Path analysis remains configured under
General settings; no additional provisioning path-analysis setting is introduced.

| Key | Default | Override levels |
| --- | --- | --- |
| implementationMode | FwoAuto | All |
| installOn | ANY | All |
| positioningAlgorithm | DefaultEndOfRulebase | All |
| ruleLoggingOverride | log | All |
| serviceObjectCreation | Supermanager | Global, device type, manager |
| addressObjectCreation | Supermanager | Global, device type, manager |
| timeObjectCreation | Supermanager | Global, device type, manager |
| labelCreation | supermanager | Global, device type, manager |
| ruleType | access | All |
| ruleCategory | app | Global, device type, manager |
| ruleZoneFromOverride | ANY | All (Fortinet editor only) |
| ruleZoneToOverride | ANY | All (Fortinet editor only) |
| implementationModeOverrideForRules | Automatic | All |
| securityProfiles | [] | Device type, manager, gateway |
| ownerIdCustomFieldName | OwnerId | Global, device type, manager |
| changeNumberCustomFieldName | ChangeId | Global, device type, manager |
| planImplementation | Enable | All |
| planImplementationOverrideForRules | Enable | All |
| modellingNamingConventions | JSON below | Global |
| modellingCreateAppZones | Yes | Global, device type, manager |
| modellingImplementNetworkAreaObjects | No | Global, device type, manager |
| modellingDynIpLabelsVisibleAsGroups | Yes | Global, device type, manager |
| templates | empty string | Device type, manager |

`installOn` offers `ANY` and `gatewaysInPath`. `ruleType` additionally offers
`accessAndNat`, `accessAndIPS`, `accessNatIPS`, and `AppSecurity`. Logging offers
`log`, `log_track`, and `none`. Time objects and labels can additionally be
`ignore`; service and address objects cannot. Labels use lowercase manager names.
Templates remain configurable at manager as well as device type; the issue marks manager availability as uncertain.

Implementation modes are `FwoAuto`, `Manual`, and
`reroute_to_external_ticket_system`; the rule override uses `Automatic`, `Manual`,
and `rerouteToExternalTicketSystem`. Planning offers `Enable` and `Disable`.

Positioning offers `DefaultEndOfRulebase`, `cpInlineLayerPerZonePair`, `endOfZone`,
`endOfAppSection`, `endOfAppSectionDistinguishCommonServices`, and
`allowCategoryEndOfAppSectionDistinguishCommonServices`.

Naming conventions require exactly these six keys with non-empty string values:

```json
{"appRole":"{idString}","appZone":"{idString}","network":"{name}","host":"{name}","networkArea":"{idString}","range":"{name}"}
```

`{idString}` represents the existing managed identifier, such as AR1234567-123
or AZ1234567; `{name}` represents the object name. These templates describe the
configuration contract for the future writer. They do not change the existing
modelling ID-generation scheme.

## Upgrade to 9.7.2

The upgrade saves the original provisioning rows once in `public.provisioning_config_backup`
under version `9.7.2` before making any changes. It renames
`logging`, `zoneFrom`, and `zoneTo` and converts the old enum member names to the
wire values above. If old and canonical keys both exist, the canonical key wins.
`TufinSc` becomes the external-ticket mode; `None` becomes `Manual` with planning
set to `Disable` unless an explicit planning override already exists.

Overrides at newly forbidden levels are removed from active provisioning data.
Custom install-on targets other than the two permitted choices are also removed.
Before upgrading, review gateway object-creation and rule-category overrides and
global and gateway templates. After upgrading, reconfigure these at permitted levels where
appropriate. The backup retains their node IDs, types, object keys and values so
no original setting data is lost. Re-running the migration preserves the original
backup and the converted values. The archive is intentionally absent from Hasura
metadata, so anonymous and ordinary GraphQL roles cannot read it. Administrators
can inspect it through PostgreSQL:

```sql
SELECT config_values FROM public.provisioning_config_backup WHERE version = '9.7.2';
```

The migration regression probe uses temporary tables and rolls back its transaction:

```bash
psql -v upgrade_file="$PWD/roles/database/files/upgrade/9.7.2.sql" \
  -f roles/tests-integration/files/database/test-upgrade-9.7.2.sql
```
