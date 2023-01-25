using System.Net.Mail;

namespace FWO.Mail
{
    public class EmailConnection
    {
        public string ServerAddress { get; set; } = "";
        public int Port { get; set; }
        public string Encryption { get; set; } = "plain"; // possible values: plain, tls, starttls
        public string? User { get; set; }
        public string? Password { get; set; }
        public string? SenderEmailAddress { get; set; }

        public EmailConnection()
        {}
        public EmailConnection(string address, int port, string encryption, string user, string password, string senderAddress)
        {
            ServerAddress = address;
            Port= port;
            Encryption = encryption;
            User = user;
            Password = password;
            SenderEmailAddress = senderAddress;
        }
    }

}
