using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.DirectoryServices.Protocols;

namespace FWO
{
    public class Program
    {
        public static void Main(string[] args)
        {
            // CreateHostBuilder(args).Build().Run();
            string UserName = "itsecorg";
            string Password = "st8chel";
            string Domain = "cactus.de";
            const string ActDirServer = "192.168.100.8";

            NetworkCredential myCred = new NetworkCredential(UserName, Password, Domain);

            LdapDirectoryIdentifier ldapDirId = new  LdapDirectoryIdentifier(ActDirServer, false, false);

            LdapConnection ldapConn = new LdapConnection(ldapDirId, myCred);
            
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                });
    }
}
