using System;
using System.Collections.Generic;
using System.Text;

namespace FWO_Auth_Server
{
    public class UserData
    {
        public string tenant { get; set; }
        public int[] VisibleDevices { get; set; }
        public int[] VisibleManagements { get; set; }
    }
}
