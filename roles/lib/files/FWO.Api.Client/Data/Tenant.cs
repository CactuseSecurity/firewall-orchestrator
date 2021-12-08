﻿using System.Text.Json.Serialization;
using FWO.Middleware.RequestParameters;

namespace FWO.Api.Data
{
    public class Tenant
    {
        [JsonPropertyName("tenant_id")]
        public int Id { get; set; }

        [JsonPropertyName("tenant_name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("tenant_comment")]
        public string? Comment { get; set; }

        [JsonPropertyName("tenant_projekt")]
        public string? Project { get; set; }

        [JsonPropertyName("tenant_can_view_all_devices")]
        public bool ViewAllDevices { get; set; }

        [JsonPropertyName("tenant_is_superadmin")]
        public bool Superadmin { get; set; }

        [JsonPropertyName("tenant_to_devices")]
        public TenantDevice[] TenantDevices { get; set; } // TODO: Replace with Device[] (probably not possible)

        public int[] VisibleDevices { get; set; } // TODO: Remove later (probably not possible)
        public int[] VisibleManagements { get; set; } // TODO: Remove later (probably not possible)

        public Tenant()
        {
            TenantDevices = new TenantDevice[]{};
            VisibleDevices = new int[]{};
            VisibleManagements = new int[]{};
        }

        public Tenant(Tenant tenant)
        {
            Id = tenant.Id;
            Name = tenant.Name;
            Comment = tenant.Comment;
            Project = tenant.Project;
            ViewAllDevices = tenant.ViewAllDevices;
            Superadmin = tenant.Superadmin;
            TenantDevices = tenant.TenantDevices;
            VisibleDevices = tenant.VisibleDevices;
            VisibleManagements = tenant.VisibleManagements;
        }

        public Tenant(TenantGetReturnParameters tenantGetParameters)
        {
            Id = tenantGetParameters.Id;
            Name = tenantGetParameters.Name;
            Comment = tenantGetParameters.Comment;
            Project = tenantGetParameters.Project;
            ViewAllDevices = tenantGetParameters.ViewAllDevices;
            Superadmin = tenantGetParameters.Superadmin;
            List<TenantDevice> deviceList = new List<TenantDevice>();
            if (tenantGetParameters.Devices != null)
            {
                foreach(KeyValuePair<int,string> apiDevice in tenantGetParameters.Devices)
                {
                    Device visibleDevice = new Device(){Id = apiDevice.Key, Name = apiDevice.Value};
                    deviceList.Add(new TenantDevice(){VisibleDevice = visibleDevice});
                }
            }
            TenantDevices = deviceList.ToArray();
            VisibleDevices = new int[]{};
            VisibleManagements = new int[]{};
        }

        public string DeviceList()
        {
            List<string> deviceList = new List<string>();
            foreach (TenantDevice device in TenantDevices)
            {
                if (device.VisibleDevice.Name != null)
                    deviceList.Add(device.VisibleDevice.Name);
            }
            return string.Join(", ", deviceList);
        }

        public TenantGetReturnParameters ToApiParams()
        {
            TenantGetReturnParameters tenantGetParams = new TenantGetReturnParameters
            {
                Id = this.Id,
                Name = this.Name,
                Comment = this.Comment,
                Project = this.Project,
                ViewAllDevices = this.ViewAllDevices,
                Superadmin = this.Superadmin,
                Devices = new List<KeyValuePair<int,string>>()
            };
            foreach (TenantDevice device in TenantDevices)
            {
                tenantGetParams.Devices.Add(new KeyValuePair<int,string>(device.VisibleDevice.Id, (device.VisibleDevice.Name != null ? device.VisibleDevice.Name : "")));
            }
            return tenantGetParams;
        }
    }

    public class TenantDevice
    {
        [JsonPropertyName("device")]
        public Device VisibleDevice { get; set; } = new Device();
    }

    public class DeviceId
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }
    }

    public class ManagementId
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }
    }
}
