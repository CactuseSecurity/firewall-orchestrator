using FWO.Data.DeviceSpecificSettings;

namespace FWO.ExternalSystems.FirewallWrite
{
    /// <summary>Shipped endpoint ("METHOD path") and body template of one operation; an empty template sends no body.</summary>
    public sealed record FirewallWriteDefault(string Endpoint, string Template);

    /// <summary>
    /// Endpoints and body templates shipped for every supported device type. They apply wherever the device specific
    /// settings leave the endpoint or template of an operation empty, and are what "Reset to default" restores.
    /// </summary>
    public static class FirewallWriteDefaults
    {
        /// <summary>Returns the shipped default of an operation for a device type, or null if the device type does not support it.</summary>
        public static FirewallWriteDefault? Get(int deviceTypeId, FirewallWriteOperation operation)
        {
            IReadOnlyDictionary<FirewallWriteOperation, FirewallWriteDefault>? defaults = FirewallVendors.FromDeviceTypeId(deviceTypeId) switch
            {
                FirewallVendor.CheckPoint => CheckPointWriteDefaults.Operations,
                FirewallVendor.FortiManager => FortiManagerWriteDefaults.Operations,
                FirewallVendor.FortiOs => FortiOsWriteDefaults.Operations,
                FirewallVendor.OpnSense => OpnSenseWriteDefaults.Operations,
                FirewallVendor.Azure => AzureWriteDefaults.Operations,
                _ => null
            };
            return defaults?.GetValueOrDefault(operation);
        }

        /// <summary>Returns the shipped endpoint of an operation for a device type, or null.</summary>
        public static string? GetEndpoint(int deviceTypeId, FirewallWriteOperation operation) => Get(deviceTypeId, operation)?.Endpoint;

        /// <summary>Returns the shipped body template of an operation for a device type, or null.</summary>
        public static string? GetTemplate(int deviceTypeId, FirewallWriteOperation operation) => Get(deviceTypeId, operation)?.Template;
    }
}
