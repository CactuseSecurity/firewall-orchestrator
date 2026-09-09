using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Data;

namespace FWO.Middleware.Server
{
    /// <summary>
    /// Resolves devices referenced in an import file by their management name and device name.
    /// </summary>
    public class DeviceNameResolver
    {
        private readonly Dictionary<(string MgmtName, string DeviceName), int> deviceIds = [];
        private readonly HashSet<(string MgmtName, string DeviceName)> ambiguousDevices = [];

        /// <summary>
        /// Loads all devices with their management from the api.
        /// </summary>
        public static async Task<DeviceNameResolver> ConstructAsync(ApiConnection apiConnection)
        {
            DeviceNameResolver lookup = new();
            List<Management> managements =
                await apiConnection.SendQueryAsync<List<Management>>(DeviceQueries.getManagementNames);
            foreach (Management management in managements)
            {
                lookup.AddDevicesOf(management);
            }
            return lookup;
        }

        /// <summary>
        /// Returns the id of the named device, or null if it is unknown.
        /// </summary>
        public int? Resolve(string mgmtName, string deviceName)
        {
            return deviceIds.TryGetValue((mgmtName, deviceName), out int deviceId) ? deviceId : null;
        }

        /// <summary>
        /// True if more than one device in FWO carries this management and device name.
        /// </summary>
        public bool IsAmbiguous(string mgmtName, string deviceName)
        {
            return ambiguousDevices.Contains((mgmtName, deviceName));
        }

        /// <summary>
        /// Formats a management and device name pair for messages.
        /// </summary>
        public static string Describe(string mgmtName, string deviceName)
        {
            return $"{mgmtName}/{deviceName}";
        }

        /// <summary>
        /// Adds device to deviceIds. If it already exists add it to ambiguousDevices
        /// to catch errors due to bad naming later
        /// </summary>
        private void AddDevicesOf(Management management)
        {
            foreach (Device device in management.Devices)
            {
                if (string.IsNullOrWhiteSpace(device.Name))
                {
                    continue;
                }
                if (!deviceIds.TryAdd((management.Name, device.Name), device.Id))
                {
                    ambiguousDevices.Add((management.Name, device.Name));
                }
            }
        }
    }
}
