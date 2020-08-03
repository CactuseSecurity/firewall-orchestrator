using FWO_Auth_Client;
using System;
using System.Threading.Tasks;

namespace Test_Auth
{
    class Program
    {
        static async Task Main(string[] args)
        {
            AuthClient authClient = new AuthClient("http://localhost:8888/");
            string jwt = await authClient.GetJWT("User", "Password");
            Console.WriteLine("JWT: " + jwt);
            Console.ReadLine();
        }
    }
}
