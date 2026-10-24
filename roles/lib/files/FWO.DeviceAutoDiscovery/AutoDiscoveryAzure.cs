using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Data;
using FWO.Encryption;
using FWO.Logging;

namespace FWO.DeviceAutoDiscovery
{
    /// <summary>
    /// Discovers the Azure Firewalls of an Azure management as its gateways.
    /// </summary>
    /// <remarks>
    /// The importer reads the firewall policies from the resource group given as config path and maps each gateway to
    /// the policy named in its local rulebase. So only firewalls whose policy lies in that resource group are discovered,
    /// with the policy name as local rulebase. Firewalls with classic rules (no firewall policy) are not supported.
    /// </remarks>
    public class AutoDiscoveryAzure : AutoDiscoveryBase
    {
        private const string kAutodiscovery = "Autodiscovery";
        private const string kResourceGroupsSegment = "resourceGroups";
        private readonly AzureClient azureClient;

        /// <summary>
        /// Creates an autodiscovery for the given Azure management.
        /// </summary>
        /// <param name="mgm">The Azure management to discover.</param>
        /// <param name="apiConn">Connection to the FWO API.</param>
        /// <param name="checkCertificates">Setting for firewall connections; Azure certificates are always checked.</param>
        public AutoDiscoveryAzure(Management mgm, ApiConnection apiConn, bool checkCertificates)
            : this(mgm, apiConn, checkCertificates, new AzureClient())
        { }

        /// <summary>
        /// Creates an autodiscovery for the given Azure management using the given Azure client.
        /// </summary>
        /// <param name="mgm">The Azure management to discover.</param>
        /// <param name="apiConn">Connection to the FWO API.</param>
        /// <param name="checkCertificates">Setting for firewall connections; Azure certificates are always checked.</param>
        /// <param name="client">The client for Azure Resource Manager.</param>
        public AutoDiscoveryAzure(Management mgm, ApiConnection apiConn, bool checkCertificates, AzureClient client)
            : base(mgm, apiConn, checkCertificates)
        {
            azureClient = client;
        }

        /// <inheritdoc />
        public override async Task<List<Management>> Run(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Log.WriteAudit(kAutodiscovery, $"starting discovery for {SuperManagement.Name} (id={SuperManagement.Id})");
            List<Device> gateways = await DiscoverGateways();
            // an incomplete discovery would report all gateways it did not reach as deleted
            cancellationToken.ThrowIfCancellationRequested();
            Management discoveredManagement = new(SuperManagement) { Devices = gateways.ToArray() };
            List<Management> discoveredManagements = [discoveredManagement];
            return await GetDeltas(discoveredManagements);
        }

        /// <summary>
        /// Gets the Azure Firewalls of the subscription whose policy lies in the configured resource group.
        /// </summary>
        /// <returns>The discovered gateways.</returns>
        public async Task<List<Device>> DiscoverGateways()
        {
            string tenantId = GetRequiredSetting(SuperManagement.CloudTenantId, "cloud tenant id");
            string subscriptionId = GetRequiredSetting(SuperManagement.CloudSubscriptionId, "cloud subscription id");
            string resourceGroup = GetRequiredSetting(SuperManagement.ConfigPath, "resource group (config path)");

            (string clientId, string clientSecret) = await GetClientCredentials();
            string accessToken = await azureClient.GetAccessToken(tenantId, clientId, clientSecret);
            List<AzureFirewall> firewalls = await azureClient.GetAzureFirewalls(subscriptionId, accessToken);
            Log.WriteDebug(kAutodiscovery, $"found {firewalls.Count} Azure Firewalls using api version {azureClient.ApiVersion}");
            return ConvertFirewallsToDevices(firewalls, resourceGroup);
        }

        /// <summary>
        /// Converts Azure Firewalls into gateways with their policy name as local rulebase.
        /// </summary>
        /// <param name="firewalls">The Azure Firewalls of the subscription.</param>
        /// <param name="resourceGroup">The resource group the importer reads the firewall policies from.</param>
        /// <returns>The gateways of the firewalls whose policy lies in the resource group.</returns>
        public static List<Device> ConvertFirewallsToDevices(List<AzureFirewall> firewalls, string resourceGroup)
        {
            List<Device> devices = [];
            foreach (AzureFirewall firewall in firewalls)
            {
                string? policyId = firewall.Properties.FirewallPolicy?.Id;
                if (string.IsNullOrEmpty(policyId))
                {
                    Log.WriteWarning(kAutodiscovery, $"Skipping Azure Firewall '{firewall.Name}': classic rules without firewall policy are not supported.");
                    continue;
                }
                if (!string.Equals(GetResourceGroup(policyId), resourceGroup, StringComparison.OrdinalIgnoreCase))
                {
                    Log.WriteInfo(kAutodiscovery, $"Skipping Azure Firewall '{firewall.Name}': its firewall policy is not in resource group '{resourceGroup}'.");
                    continue;
                }
                devices.Add(new Device
                {
                    Name = firewall.Name,
                    // Azure resource ids are case-insensitive, the importer uses the same uid for the gateway
                    Uid = firewall.Id.ToLowerInvariant(),
                    LocalRulebase = GetResourceName(policyId),
                    DeviceType = new DeviceType { Id = DeviceType.kAzureFirewallTypeId }
                });
            }
            return devices;
        }

        /// <inheritdoc />
        protected override void AdoptExistingIdentities(List<Management> discoveredManagements, List<Management> existingManagements)
        {
            Management? storedManagement = existingManagements.FirstOrDefault(mgmt => mgmt.Id == SuperManagement.Id);
            if (storedManagement == null)
            {
                return;
            }
            foreach (Management discoveredManagement in discoveredManagements)
            {
                AdoptStoredDeviceIdentities(discoveredManagement.Devices, storedManagement.Devices);
            }
        }

        /// <summary>
        /// Gives discovered firewalls the name and uid of the stored gateways they correspond to.
        /// </summary>
        /// <remarks>
        /// Gateways created before the autodiscovery may carry the resource id in its original casing, another uid or
        /// none at all. A stored gateway matches a discovered firewall by uid ignoring case, otherwise by name ignoring
        /// case if its uid matches no discovered firewall. Without this, such gateways would be proposed for deletion
        /// and the same firewalls for addition.
        /// </remarks>
        /// <param name="discoveredDevices">The discovered firewalls, changed in place.</param>
        /// <param name="storedDevices">The gateways of the stored Azure management.</param>
        public static void AdoptStoredDeviceIdentities(IEnumerable<Device> discoveredDevices, IEnumerable<Device> storedDevices)
        {
            List<Device> unmatchedStoredDevices = [.. storedDevices];
            List<Device> unmatchedDiscoveredDevices = [];
            foreach (Device discoveredDevice in discoveredDevices)
            {
                Device? storedDevice = unmatchedStoredDevices.FirstOrDefault(device => string.Equals(device.Uid, discoveredDevice.Uid, StringComparison.OrdinalIgnoreCase));
                if (storedDevice == null)
                {
                    unmatchedDiscoveredDevices.Add(discoveredDevice);
                    continue;
                }
                AdoptStoredDeviceIdentity(discoveredDevice, storedDevice, unmatchedStoredDevices);
            }
            foreach (Device discoveredDevice in unmatchedDiscoveredDevices)
            {
                Device? storedDevice = unmatchedStoredDevices.FirstOrDefault(device => !string.IsNullOrEmpty(device.Name)
                    && string.Equals(device.Name, discoveredDevice.Name, StringComparison.OrdinalIgnoreCase));
                if (storedDevice != null)
                {
                    Log.WriteWarning(kAutodiscovery, $"Azure gateway '{storedDevice.Name}' (id={storedDevice.Id}) matches the Azure Firewall " +
                        $"by name only. Set its uid to the firewall's resource id '{discoveredDevice.Uid}' to match it by uid.");
                    AdoptStoredDeviceIdentity(discoveredDevice, storedDevice, unmatchedStoredDevices);
                }
            }
        }

        private static void AdoptStoredDeviceIdentity(Device discoveredDevice, Device storedDevice, List<Device> unmatchedStoredDevices)
        {
            discoveredDevice.Name = storedDevice.Name;
            discoveredDevice.Uid = storedDevice.Uid;
            unmatchedStoredDevices.Remove(storedDevice);
        }

        /// <summary>
        /// Gets the resource group of an Azure resource id.
        /// </summary>
        /// <param name="resourceId">The Azure resource id.</param>
        /// <returns>The resource group, empty if the id contains none.</returns>
        public static string GetResourceGroup(string resourceId)
        {
            List<string> segments = [.. resourceId.Split('/', StringSplitOptions.RemoveEmptyEntries)];
            int index = segments.FindIndex(segment => segment.Equals(kResourceGroupsSegment, StringComparison.OrdinalIgnoreCase));
            return index >= 0 && index + 1 < segments.Count ? segments[index + 1] : "";
        }

        /// <summary>
        /// Gets the name of an Azure resource from its id.
        /// </summary>
        /// <param name="resourceId">The Azure resource id.</param>
        /// <returns>The last segment of the id.</returns>
        public static string GetResourceName(string resourceId)
        {
            return resourceId.TrimEnd('/').Split('/')[^1];
        }

        /// <summary>
        /// Gets the service principal client id and secret like the importer does: from the additional credential if
        /// set (migrated cloud credentials), otherwise from the import credential itself.
        /// </summary>
        private async Task<(string, string)> GetClientCredentials()
        {
            Credential importCredential = SuperManagement.ImportCredential;
            if (importCredential.AdditionalCredentialId is not int additionalCredentialId)
            {
                return (importCredential.ImportUser ?? "", importCredential.Secret);
            }
            Credential? additionalCredential = await apiConnection.SendQueryAsync<Credential?>(DeviceQueries.getCredentialById, new { id = additionalCredentialId })
                ?? throw new InvalidOperationException($"Additional credential {additionalCredentialId} of management '{SuperManagement.Name}' not found.");
            string clientSecret = AesEnc.TryDecrypt(additionalCredential.Secret, true, kAutodiscovery,
                $"Could not decrypt secret in credential named '{additionalCredential.Name}'.", true);
            return (additionalCredential.ImportUser ?? "", clientSecret);
        }

        private string GetRequiredSetting(string? value, string settingName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException($"Azure management '{SuperManagement.Name}' needs the {settingName} for the autodiscovery.");
            }
            return value;
        }
    }
}
