namespace FWO.Data.DeviceSpecificSettings;

/// <summary>Compiled defaults and placeholders for Check Point LayerGuard settings.</summary>
public static class CheckPointLayerGuardSettingDefaults
{
    public const string kSourceZonePlaceholder = "@@SOURCE_ZONE@@";
    public const string kDestinationZonePlaceholder = "@@DESTINATION_ZONE@@";
    public const string kSectionNameTemplate = "[" + kSourceZonePlaceholder + "] -> [" + kDestinationZonePlaceholder + "]";
}
