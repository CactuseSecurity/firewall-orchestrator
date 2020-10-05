using System;
using System.Collections.Generic;
using System.Text;

namespace FWO.Auth.Server.Data
{
    public class Tenant
    {
        public string Name { get; set; }
        public int Id { get; set; }
        public int[] VisibleDevices { get; set; }
        public int[] VisibleManagements { get; set; }
    }
}
