using FWO.Data.DeviceSpecificSettings;

namespace FWO.ExternalSystems.FirewallWrite
{
    /// <summary>Shipped endpoint ("METHOD path") and body template of one operation; an empty template sends no body.</summary>
    public sealed record FirewallWriteDefault(string Endpoint, string Template);

    /// <summary>
    /// Endpoints and body templates shipped for every supported device type. They apply wherever the device specific
    /// settings leave the endpoint or template of an operation empty, so a later release can correct them. Vendors that keep
    /// IPv6 objects in separate tables (Fortinet) ship IPv6 variants of the network object operations.
    /// </summary>
    public static class FirewallWriteDefaults
    {
        private static readonly IReadOnlyDictionary<FirewallWriteOperation, FirewallWriteDefault> NoIpv6Defaults =
            new Dictionary<FirewallWriteOperation, FirewallWriteDefault>();

        /// <summary>
        /// Returns the shipped default of an operation for a device type, or null if the device type does not support it.
        /// For IPv6 objects the IPv6 variant is returned where the vendor has one.
        /// </summary>
        public static FirewallWriteDefault? Get(int deviceTypeId, FirewallWriteOperation operation, bool isIpv6 = false)
        {
            (IReadOnlyDictionary<FirewallWriteOperation, FirewallWriteDefault> Defaults,
                IReadOnlyDictionary<FirewallWriteOperation, FirewallWriteDefault> Ipv6Defaults)? vendorDefaults =
                FirewallVendors.FromDeviceTypeId(deviceTypeId) switch
                {
                    FirewallVendor.CheckPoint => (CheckPointWriteDefaults.Operations, NoIpv6Defaults),
                    FirewallVendor.FortiManager => (FortiManagerWriteDefaults.Operations, FortiManagerWriteDefaults.Ipv6Operations),
                    FirewallVendor.FortiOs => (FortiOsWriteDefaults.Operations, FortiOsWriteDefaults.Ipv6Operations),
                    FirewallVendor.OpnSense => (OpnSenseWriteDefaults.Operations, NoIpv6Defaults),
                    FirewallVendor.Azure => (AzureWriteDefaults.Operations, NoIpv6Defaults),
                    _ => null
                };
            if (vendorDefaults == null)
            {
                return null;
            }
            if (isIpv6 && vendorDefaults.Value.Ipv6Defaults.TryGetValue(operation, out FirewallWriteDefault? ipv6Default))
            {
                return ipv6Default;
            }
            return vendorDefaults.Value.Defaults.GetValueOrDefault(operation);
        }

        /// <summary>Returns the shipped endpoint of an operation for a device type, or null.</summary>
        public static string? GetEndpoint(int deviceTypeId, FirewallWriteOperation operation) => Get(deviceTypeId, operation)?.Endpoint;

        /// <summary>Returns the shipped body template of an operation for a device type, or null.</summary>
        public static string? GetTemplate(int deviceTypeId, FirewallWriteOperation operation) => Get(deviceTypeId, operation)?.Template;
    }
}
