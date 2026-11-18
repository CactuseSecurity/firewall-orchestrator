namespace FWO.ExternalSystems.FirewallWrite
{
    /// <summary>Creates the vendor client for a firewall write target.</summary>
    public interface IFirewallWriteClientFactory
    {
        /// <summary>Returns a new client for the management of the context.</summary>
        FirewallWriteClient Create(FirewallWriteContext context);
    }

    /// <summary>
    /// Creates the vendor client of the target's device type, with certificate checks and response timeout as configured by
    /// the device specific settings <c>fwWriteCheckCertificates</c> and <c>requestResponseTimeout</c> of the target.
    /// </summary>
    public sealed class FirewallWriteClientFactory : IFirewallWriteClientFactory
    {
        /// <inheritdoc/>
        public FirewallWriteClient Create(FirewallWriteContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            bool checkCertificates = context.Settings.FwWriteCheckCertificates;
            int responseTimeout = context.Settings.RequestResponseTimeout;
            return context.Vendor switch
            {
                FirewallVendor.CheckPoint => new CheckPointWriteClient(context.Management, checkCertificates, responseTimeout),
                FirewallVendor.FortiManager => new FortiManagerWriteClient(context.Management, checkCertificates, responseTimeout),
                FirewallVendor.FortiOs => new FortiOsWriteClient(context.Management, checkCertificates, responseTimeout),
                FirewallVendor.OpnSense => new OpnSenseWriteClient(context.Management, checkCertificates, responseTimeout),
                FirewallVendor.Azure => new AzureWriteClient(context.Management, checkCertificates, responseTimeout),
                _ => throw new NotSupportedException($"Device type {context.DeviceTypeId} is not supported by the firewall write library.")
            };
        }
    }
}
