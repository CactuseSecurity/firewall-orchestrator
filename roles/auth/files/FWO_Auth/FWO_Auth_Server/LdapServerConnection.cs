using Novell.Directory.Ldap;
using System;
using System.Collections.Generic;
using System.Text;

namespace FWO_Auth_Server
{
    class LdapServerConnection
    {
        private readonly string Domain;
        private readonly int Port;

        public LdapServerConnection(string Domain, int Port)
        {
            this.Domain = Domain;
            this.Port = Port;
        }

        // tim@ubu1804:~$ ldapwhoami -x -w fworch.1  -D uid=admin,ou=systemuser,ou=user,dc=fworch,dc=internal  -H ldaps://localhost/
        // dn:uid=admin,ou=systemuser,ou=user,dc=fworch,dc=internal
        public bool ValidateUser(string Username, string Password)
        {
            string userDn = $"{Username}@{Domain}";
            try
            {
                using (var connection = new LdapConnection { SecureSocketLayer = true })
                {
                    connection.UserDefinedServerCertValidationDelegate += Connection_UserDefinedServerCertValidationDelegate;

                    connection.Connect(Domain, Port);
                    connection.Bind(userDn, Password);
                    if (connection.Bound)
                        return true;
                }
            }
            catch (LdapException ex)
            {
                // Log exception
            }
            return false;
        }

        private bool Connection_UserDefinedServerCertValidationDelegate(object sender, System.Security.Cryptography.X509Certificates.X509Certificate certificate, System.Security.Cryptography.X509Certificates.X509Chain chain, System.Net.Security.SslPolicyErrors sslPolicyErrors)
        {
            return true;
        }

        public IEnumerable<Role> GetRoles(string Username, string Password)
        {
            return null;
        }
    }
}
