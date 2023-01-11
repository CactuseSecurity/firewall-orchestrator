using System.Net.Mail;

namespace FWO.Mail
{
    public class EmailConnection
    {
        public string ServerAddress { get; set; } = "";
        public int Port { get; set; }
        public bool Tls { get; set; }
        public string? User { get; set; }
        public string? Password { get; set; }
        public string? SenderEmailAddress { get; set; }

        public EmailConnection()
        {}
        public EmailConnection(string address, int port, bool tls, string user, string password, string senderAddress)
        {
            ServerAddress = address;
            Port= port;
            Tls = tls;
            User = user;
            Password = password;
            SenderEmailAddress = senderAddress;
        }
    }

}
