using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FWO.Middleware.RequestParameters
{
    public class RoleAddDeleteUserParameters
    {
        public string Role { get; set; }
        public string UserDn { get; set; }
    }
}
