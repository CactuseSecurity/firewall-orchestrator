# WP-02 — Configure Check Point native-Section placement

## Agent mission

Make the existing provisioning hierarchy resolve one validated Check Point native-Section placement method for the planning service. Preserve future extensibility without implementing unfinished vendor options.

## Current seam

Inspect:

- `FWO.Data/Provisioning/ProvisioningSettingsEnums.cs`
- `FWO.Data/Provisioning/GlobalProvisioningSettings.cs`
- `FWO.Data/Provisioning/ProvisioningSettingKeys.cs`
- `FWO.Config.Api/ProvisioningSettingsManager.cs`

The current enum contains historical Check Point and Fortinet choices plus `DefaultEndOfRulebase`, but no placement implementation consumes them.

## Scope

- Apply WP-00's compatibility decision for historical values.
- Resolve native-Section placement through device type, management, and gateway inheritance.
- Validate that the selected scope belongs to a supported Check Point device type.
- Treat Fortinet, Azure, and other unimplemented positioning values as unsupported by this planning feature.
- Reject unknown, out-of-scope default, and future method values with the appropriate typed failure before planning.
- Update configuration-resolution tests.
- Preserve stored values according to the ADR; migrate only when explicitly required.

## Acceptance criteria

- A Check Point gateway can inherit or override the supported method.
- An invalid vendor/method combination cannot be accepted by the placement coordinator.
- Existing stored settings remain readable without being silently reinterpreted.
- Selecting an unavailable future method produces `PositioningMethodNotImplemented` and no policy-snapshot query.
- Tests cover inheritance, overrides, legacy values, unknown values, and `DefaultEndOfRulebase` rejection.

## Out of scope

Do not add configuration UI, Fortinet/Azure settings, automatic Section creation, cleanup-rule detection, or new Chapter 12 settings.
