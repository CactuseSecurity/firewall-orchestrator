using FWO.Api.Client.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FWO.Services
{
    internal static class AppZoneStateHelper
    {
        //int = mgtId = Management Id
        private static Dictionary<int, List<ModellingAppZone>> ManagementAppZones = [];

        internal static void SetManagementAppZones(Dictionary<int, List<ModellingAppZone>> managementAppZones)
        {
            ManagementAppZones = managementAppZones;
        }

        internal static void SetManagementAppZonesList(int mgtId, List<ModellingAppZone> appZones)
        {
            if (ManagementAppZones.ContainsKey(mgtId))
            {
                ManagementAppZones[mgtId] = appZones;
            }
        }

        internal static Dictionary<int, List<ModellingAppZone>> GetManagementAppZones()
        {
            if(ManagementAppZones is not null)
            {
                return ManagementAppZones;
            }

            return [];
        }

        internal static List<ModellingAppZone> GetManagementAppZonesList(int mgtId)
        {
            if (ManagementAppZones.TryGetValue(mgtId, out List<ModellingAppZone>? value))
            {
                return value;
            }

            return [];
        }
    }
}
