using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FWO.Middleware.RequestParameters
{
    public class AuthenticationTokenGetParameters
    {
        public string? Username { get; set; }
        public string? Password { get; set; }
    }
}
