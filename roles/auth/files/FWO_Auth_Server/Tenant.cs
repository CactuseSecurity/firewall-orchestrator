using System;
using System.Collections.Generic;
using System.Text;

namespace FWO_Auth_Server
{
    public class Tenant
    {
        public string tenantName { get; set; }
        public int tenantId { get; set; }
        public int[] VisibleDevices { get; set; }
        public int[] VisibleManagements { get; set; }
    }
}
