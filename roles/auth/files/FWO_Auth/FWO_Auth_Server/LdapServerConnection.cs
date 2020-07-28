using System;
using System.Collections.Generic;
using System.DirectoryServices.Protocols;
using System.Text;

namespace FWO_Auth_Server
{
    class LdapServerConnection
    {
        LdapConnection Connection;
        
        public LdapServerConnection(string Address)
        {
            Connection = new LdapConnection(Address);
        }

        public bool CheckIfValid(string Username, string Password)
        {
            DirectoryRequest Request = new SearchRequest("NAME", "FILTER", SearchScope.Subtree, "ATTRIBUTES");
            DirectoryResponse Response =  Connection.SendRequest(Request);
            return Response.MatchedDN != "";
        }

        public IEnumerable<Role> GetRoles(string Username, string Password)
        {
            return null;
        }
    }
}
