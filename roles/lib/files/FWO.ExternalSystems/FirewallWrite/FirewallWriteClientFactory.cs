namespace FWO.ExternalSystems.FirewallWrite
{
    /// <summary>Creates the vendor client for a firewall write target.</summary>
    public interface IFirewallWriteClientFactory
    {
        /// <summary>Returns a new client for the management of the context.</summary>
        FirewallWriteClient Create(FirewallWriteContext context);
    }

    /// <summary>
    /// Creates the vendor client of the target's device type, with certificate checks as configured by the device specific
    /// setting <c>fwWriteCheckCertificates</c> of the target.
    /// </summary>
    public sealed class FirewallWriteClientFactory : IFirewallWriteClientFactory
    {
        /// <inheritdoc/>
        public FirewallWriteClient Create(FirewallWriteContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            bool checkCertificates = context.Settings.FwWriteCheckCertificates;
            return context.Vendor switch
            {
                FirewallVendor.CheckPoint => new CheckPointWriteClient(context.Management, checkCertificates),
                FirewallVendor.FortiManager => new FortiManagerWriteClient(context.Management, checkCertificates),
                FirewallVendor.FortiOs => new FortiOsWriteClient(context.Management, checkCertificates),
                FirewallVendor.OpnSense => new OpnSenseWriteClient(context.Management, checkCertificates),
                FirewallVendor.Azure => new AzureWriteClient(context.Management, checkCertificates),
                _ => throw new NotSupportedException($"Device type {context.DeviceTypeId} is not supported by the firewall write library.")
            };
        }
    }
}
