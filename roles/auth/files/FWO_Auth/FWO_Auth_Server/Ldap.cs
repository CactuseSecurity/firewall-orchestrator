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

        public bool ValidateUser(User user)
        {
            // TODO: we need to replace ou=systemuser with ou=tenant<x>,ou=operator and keep x variable - need to look in all existing tenants
            string userSearchBase = $"ou=operator,ou=user,dc=fworch,dc=internal"; // TODO: read path from config

            Console.WriteLine($"Validating User: \"{user.Name}\" ...");

            // REMOVE IF NEW LDAP VERSION 
            try
            {
            // REMOVE IF NEW LDAP VERSION

                using (LdapConnection connection = Connect())
                {
                    connection.Bind($"uid=inspector,ou=systemuser,ou=user,dc=fworch,dc=internal", "fworch.1"); // TODO: read path from config

                    // Todo: Insert correct values
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
                                    // TODO: return DN to make role search easier and "correcter"?
                                    return true;
                                }

                            }
                            catch (LdapException ex) { } // Incorrect Ldap DN or credentials
#if DEBUG
                            Console.WriteLine($"Failure!");
#endif
                        }
                    }


                 // REMOVE IF NEW LDAP VERSION 
                    connection.Bind(userSearchBase, user.Password);                 

                    if (connection.Bound)
                        return true;
                 // REMOVE IF NEW LDAP VERSION  

                    connection.Disconnect();
                }

            // REMOVE IF NEW LDAP VERSION 
            }
            catch (LdapException ex)
            {
                Console.Write($"\n #### Message #### \n {ex.Message} \n #### Stack Trace #### \n {ex.StackTrace} \n");
                // Log exception
            }
            // REMOVE IF NEW LDAP VERSION 

            // Wrong Username / Password
            Console.WriteLine($"User \"{user.Name}\" could not be validated!");

            // Todo: Log Wrong Username / Password
            
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
