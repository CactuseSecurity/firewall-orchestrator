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

        // tim@ubu1804:~$ ldapwhoami -x -w fworch.1  -D uid=admin,ou=tenant0,ou=operator,ou=user,dc=fworch,dc=internal  -H ldaps://localhost/
        // dn:uid=admin,ou=tenant0,ou=operator,ou=user,dc=fworch,dc=internal
        public bool ValidateUser(User user)
        {
            // TODO: we need to replace ou=systemuser with ou=tenant<x>,ou=operator and keep x variable - need to look in all existing tenants
            string userDn = $"uid={user.Name},ou=systemuser,ou=user,dc=fworch,dc=internal";
#if DEBUG
            Console.WriteLine($"FWO_Auth_Server::Ldap.cs: ValidateUser called for user {userDn}");
            Console.WriteLine($"FWO_Auth_Server::Ldap.cs: LdapServerPort={Port}");
#endif
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
                    connection.Disconnect();
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
            switch (user.Name)
            {
                case "":
                case "fgreporter":
                case "fgcheck":
                    return new Role[] { new Role { Name = "reporter" } };

                case "admin":
                    return new Role[] { new Role { Name = "reporter-viewall" }, new Role { Name = "reporter" } };

                default:
                    return new Role[0];
            }
            // Fake role REMOVE LATER
        }
    }
}
