using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace FWO.Middleware.Server.Data
{
    public class Tenant
    {
        public string Name { get; set; }

        [JsonPropertyName("tenant_id")]
        public int Id { get; set; }

        public int[] VisibleDevices { get; set; }
        public int[] VisibleManagements { get; set; }
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
