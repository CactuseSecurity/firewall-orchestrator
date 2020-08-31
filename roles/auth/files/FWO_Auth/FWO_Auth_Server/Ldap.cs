using Novell.Directory.Ldap;
using System;
using System.IO;
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

            Console.WriteLine($"New Ldap created: LdapServerAdress={Address} LdapServerPort={Port}");

            Connect();
        }

        private LdapConnection Connect()
        {
            LdapConnection connection = null;

            try
            {
                connection = new LdapConnection { SecureSocketLayer = true };

                connection.UserDefinedServerCertValidationDelegate +=
                (object sen, X509Certificate cer, X509Chain cha, SslPolicyErrors err) => true;

                connection.Connect(Address, Port);
            }

            catch (Exception e)
            {
                // TODO: Ldap Server not reachable
            }

            return connection;
        }

        public String ValidateUser(User user)
        {
            string userSearchBase = $"ou=operator,ou=user,dc=fworch,dc=internal"; // TODO: read path from config

            Console.WriteLine($"Validating User: \"{user.Name}\" ...");
            try
            {
                using (LdapConnection connection = Connect())
                {
                    String InspectorPassword = File.ReadAllText("/usr/local/fworch/etc/secrets/ldap_inspector_pw.txt").TrimEnd(); // or check if -y paramter for password file exists

                    connection.Bind($"uid=inspector,ou=systemuser,ou=user,dc=fworch,dc=internal", InspectorPassword);

                    LdapSearchResults possibleUsers = (LdapSearchResults)connection.Search(userSearchBase, LdapConnection.ScopeSub, $"(&(objectClass=inetOrgPerson)(uid:dn:={user.Name}))", null, typesOnly: false);

                    connection.Bind("", ""); // Unbind not authenticated anymore

                    if (possibleUsers.Count != 0)
                    {
                        foreach (var currentUser in possibleUsers)
                        {
#if DEBUG
                            Console.WriteLine($"Trying distinguished name: \"{ currentUser.Dn}\" ...");
#endif
                            try
                            {
                                connection.Bind(currentUser.Dn, user.Password);
                                if (connection.Bound)
                                {
                                    Console.WriteLine($"Successful authentication for \"{ currentUser.Dn}\"");
                                    return currentUser.Dn;
                                }
                            }
                            catch (LdapException exInner) {
#if DEBUG
                                Console.WriteLine($"Found user with same uid but different pwd distinguished name: \"{ currentUser.Dn}\" ...");
                                Console.Write($"\n Error while trying LDAP Connection #### Message #### \n {exInner.Message} \n #### Stack Trace #### \n {exInner.StackTrace} \n");
#endif
                            } // Incorrect password - do nothing, assuming another user with the same username
                        }
                    }
                }

            }
            catch (LdapException ex)
            {
                Console.Write($"\n Error while trying LDAP Connection #### Message #### \n {ex.Message} \n #### Stack Trace #### \n {ex.StackTrace} \n");
                // Log exception
            }
            Console.WriteLine($"User \"{user.Name}\" could not be validated!");
            return "";
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
