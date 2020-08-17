using Novell.Directory.Ldap;
using System;
using System.Collections.Generic;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace FWO_Auth_Server
{
    class Ldap
    {
        private readonly string Address;
        private readonly int Port;

        public Ldap(string Address, int Port)
        {
            this.Address = Address;
            this.Port = Port;
        }

        // tim@ubu1804:~$ ldapwhoami -x -w fworch.1  -D uid=admin,ou=systemuser,ou=user,dc=fworch,dc=internal  -H ldaps://localhost/
        // dn:uid=admin,ou=systemuser,ou=user,dc=fworch,dc=internal
        public bool ValidateUser(User user)
        {
            string userDn = $"uid={user.Name},ou=user,dc=fworch,dc=internal";
            try
            {
                using (var connection = new LdapConnection { SecureSocketLayer = true })
                {
                    connection.UserDefinedServerCertValidationDelegate +=
                    (object sen, X509Certificate cer, X509Chain cha, SslPolicyErrors err) => true;
                    connection.Connect(Address, Port);
                    connection.Bind(userDn, user.Password);
                    if (connection.Bound)
                        return true;
                }
            }
            catch (LdapException ex)
            {
                Console.Write($"\n #### Message #### \n {ex.Message} \n #### Stack Trace #### \n {ex.StackTrace} \n");
                // Log exception
            }
            return false;
        }

        public Role[] GetRoles(User user)
        {
            // Fake role REMOVE LATER
            if (user.Name == "" && user.Password == "")
                return new Role[] { new Role { Name = "forti" } };
            // Fake role REMOVE LATER

            return null;
        }
    }
}
