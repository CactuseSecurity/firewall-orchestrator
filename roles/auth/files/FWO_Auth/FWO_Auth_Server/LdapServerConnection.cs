using System;
using System.Collections.Generic;
using System.DirectoryServices;
using System.Text;

namespace FWO_Auth_Server
{
    class LdapServerConnection
    {
        private readonly string Address;

        public LdapServerConnection(string Address)
        {
            this.Address = Address;
        }

        // tim@ubu1804:~$ ldapwhoami -x -w fworch.1  -D uid=admin,ou=systemuser,ou=user,dc=fworch,dc=internal  -H ldaps://localhost/
        // dn:uid=admin,ou=systemuser,ou=user,dc=fworch,dc=internal
        public bool Valid(string Username, string Password)
        {
            try
            {
                DirectoryEntry de = new DirectoryEntry(Address, Username, Password, AuthenticationTypes.Secure);
                DirectorySearcher ds = new DirectorySearcher(de);
                ds.FindOne();
                return true;
            }
            catch (DirectoryServicesCOMException ex)
            {
                return false;
            }

            //DirectoryRequest Request = new SearchRequest("NAME", "FILTER", SearchScope.Subtree, "ATTRIBUTES");
            //DirectoryResponse Response =  Connection.SendRequest(Request);
            //return Response.MatchedDN != "";
        }

        public IEnumerable<Role> GetRoles(string Username, string Password)
        {
            return null;
        }
    }
}
