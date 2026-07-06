namespace FWO.Data
{
    public static class ProtocolNames
    {
        public const string Tcp = "tcp";
        public const string Udp = "udp";
        public const string Icmp = "icmp";
        public const string Esp = "esp";

        public static readonly string[] DefaultReducedProtocolNames = [Tcp, Udp, Icmp, Esp];
    }
}
