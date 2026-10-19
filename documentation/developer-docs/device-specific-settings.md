# Hierarchical device specific settings

Device specific settings were called provisioning settings before 9.7.4. They are not
limited to provisioning: importer-related values such as `importerHostName` live here as well.

Device specific setting values are stored as typed JSON in `device_specific_config_value`.
The hierarchy is Global > Device type > Manager > Gateway. An absent override
inherits the nearest ancestor value, or the compiled default. Override scope
restrictions apply to editing; inherited values remain available at descendant
levels through `DeviceSpecificSettingsManager.LoadEffectiveValueAsync`.

The settings page exposes the configuration contract for Guardicore integration.
The Guardicore writer is separate work. Path analysis remains configured under
General settings; no additional device specific path-analysis setting is introduced.

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
| importerHostName | string | empty (every importer is responsible) | Global, device type, manager | Global to manager |
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
`modellingConnectionIdTemplate`.

`importerHostName` is the host name of the server whose importer imports the
managements of a level; it replaces the former column `management.importer_hostname`.
The importer resolves it for each management along Global > device type > management
(query `deviceSpecificSettings/getImporterHostName.graphql`; the Hasura role `importer`
may read only this setting). An importer imports a management if the effective value is
empty or equals its own host name. The compiled default is empty, so with a single
importer nothing has to be configured; with several distributed importers set the host
on the levels that each importer is responsible for.

## Upgrade to 9.7.4

The upgrade renames the tables `provisioning_config_node`, `provisioning_config_value` and
`provisioning_config_backup` (with their constraints, indexes and sequence) to
`device_specific_config_*`. It saves the original setting rows once in `public.device_specific_config_backup`
under version `9.7.4` before making any changes. It renames
`logging`, `zoneFrom`, and `zoneTo` and converts the old enum member names to the
wire values above. If old and canonical keys both exist, the canonical key wins.
`FwoAuto` becomes `Automatic` and `TufinSc` the external-ticket mode; `None` becomes
`Manual` with planning set to `false` unless an explicit planning override already
exists. The other old modes always planned the implementation: a level that overrides
`FwoAuto`, `Manual` or `TufinSc` and would inherit the disabled planning of a `None`
level above it gets `planImplementation` set to `true`. Only a level that still has the old
`None` mode counts, so a repeated upgrade leaves planning that was disabled after the upgrade
unchanged. If exactly one Tufin SecureChange
system is configured in the external ticket systems, former `TufinSc` levels reroute to
it via `rerouteToExternalTicketSystem`; otherwise select the ticket system on those
levels after upgrading. `ruleCategory` becomes `commonServiceSeparation` (`common_service`
is `true`, `app` is `false`).

The upgrade moves `management.importer_hostname` into `importerHostName` and then drops
the column. The most common non-empty host name becomes the global value (unless a global
value already exists), and each management whose host name differs from the value it
would inherit gets its own override, creating its device type and management nodes if
needed. Managements without a host name inherit the global value; previously no importer
imported them unless forced, so disable their import if that is still wanted. The old
column values are saved once in `public.device_specific_config_backup` under version
`9.7.4 management.importer_hostname`.

Templates outside the device type level and custom install-on targets other than the
two permitted choices are removed from active device specific settings data. Before upgrading,
review global, manager and gateway templates; after upgrading, reconfigure them at
device type level where appropriate. The backup retains their node IDs, types, object
keys and values so no original setting data is lost. Re-running the migration
preserves the original backup and the converted values. The archive is intentionally
absent from Hasura metadata, so anonymous and ordinary GraphQL roles cannot read it.
Administrators can inspect it through PostgreSQL:

```sql
SELECT config_values FROM public.device_specific_config_backup WHERE version = '9.7.4';
```

The migration regression probe `roles/tests-integration/files/database/test-upgrade-9.7.4.sql`
uses temporary tables and rolls back its transaction. The database integration tests
(`roles/tests-integration/tasks/test-database.yml`) run it with the upgrade file inserted at
its two `-- @run-upgrade-9.7.4` markers.
