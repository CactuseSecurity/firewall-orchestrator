namespace FWO.ExternalSystems.FirewallWrite
{
    /// <summary>
    /// Vendor API family a device type is written through. Several device types share one family,
    /// e.g. Check Point R8x and MDS, or FortiManager with its ADOMs and FortiGates.
    /// </summary>
    public enum FirewallVendor
    {
        Unsupported,
        CheckPoint,
        FortiManager,
        FortiOs,
        OpnSense,
        Azure
    }

    /// <summary>
    /// Maps the device types (stm_dev_typ ids) supported by the firewall write library to their vendor API family.
    /// </summary>
    public static class FirewallVendors
    {
        public const int kCheckPointR8x = 9;
        public const int kFortiGate5ff = 10;
        public const int kFortiAdom5ff = 11;
        public const int kFortiManager5ff = 12;
        public const int kCheckPointMds = 13;
        public const int kAzure = 19;
        public const int kAzureFirewall = 20;
        public const int kFortiOsManagement = 24;
        public const int kFortiOsGateway = 25;
        public const int kOpnSense = 32;

        private static readonly Dictionary<int, FirewallVendor> VendorByDeviceType = new()
        {
            { kCheckPointR8x, FirewallVendor.CheckPoint },
            { kCheckPointMds, FirewallVendor.CheckPoint },
            { kFortiGate5ff, FirewallVendor.FortiManager },
            { kFortiAdom5ff, FirewallVendor.FortiManager },
            { kFortiManager5ff, FirewallVendor.FortiManager },
            { kFortiOsManagement, FirewallVendor.FortiOs },
            { kFortiOsGateway, FirewallVendor.FortiOs },
            { kOpnSense, FirewallVendor.OpnSense },
            { kAzure, FirewallVendor.Azure },
            { kAzureFirewall, FirewallVendor.Azure }
        };

        /// <summary>Device type ids the firewall write library can write to.</summary>
        public static IReadOnlyCollection<int> SupportedDeviceTypeIds => VendorByDeviceType.Keys;

        /// <summary>Returns the vendor family of a device type, <see cref="FirewallVendor.Unsupported"/> for all others.</summary>
        public static FirewallVendor FromDeviceTypeId(int deviceTypeId)
        {
            return VendorByDeviceType.GetValueOrDefault(deviceTypeId, FirewallVendor.Unsupported);
        }
    }
}
