namespace FWO.Mail
{

    public enum EmailEncryptionMethod
    {
        None,
        StartTls,
        Tls
    }

    public class EmailConnection
    {
        public string ServerAddress { get; set; } = "";
        public int Port { get; set; }
        public EmailEncryptionMethod Encryption { get; set; } = EmailEncryptionMethod.None;
        public string? User { get; set; }
        public string? Password { get; set; }
        public string? SenderEmailAddress { get; set; }

        /// <summary>
        /// Whether the TLS certificate of the email server is validated against the host trust store.
        /// Only relevant for StartTls and Tls.
        /// </summary>
        public bool CheckCertificates { get; set; } = true;

        public EmailConnection()
        { }
        public EmailConnection(string address, int port, EmailEncryptionMethod encryption, string user, string password, string senderAddress)
        {
            ServerAddress = address;
            Port = port;
            Encryption = encryption;
            User = user;
            Password = password;
            SenderEmailAddress = senderAddress;
        }
    }

}
