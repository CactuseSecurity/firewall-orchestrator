using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FWO.Middleware.RequestParameters
{
    public class UserGetParameters
    {
        public string LdapHostname { get; set; }
        public string SearchPattern { get; set; }
    }

    public class UserAddParameters
    {
        public string LdapHostname { get; set; }
        public string UserDn { get; set; }
        public string Password { get; set; }
        public string Email { get; set; }
    }

    public class UserEditParameters
    {
        public string LdapHostname { get; set; }
        public string UserDn { get; set; }
        public string Email { get; set; }
    }

    public class UserChangePasswordParameters
    {
        public string LdapHostname { get; set; }
        public string UserDn { get; set; }
        public string OldPassword { get; set; }
        public string NewPassword { get; set; }
    }

    public class UserResetPasswordParameters
    {
        public string LdapHostname { get; set; }
        public string UserDn { get; set; }
        public string NewPassword { get; set; }
    }

    public class UserDeleteParameters
    {
        public string LdapHostname { get; set; }
        public string UserDn { get; set; }
    }

    public class UserDeleteAllEntriesParameters
    {
        public string UserDn { get; set; }
    }
}
