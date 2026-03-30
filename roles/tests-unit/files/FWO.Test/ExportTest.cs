using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Report;
using FWO.Report.Filter;
using FWO.Test.Mocks;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    [Parallelizable]
    internal partial class ExportTest
    {
        static readonly NetworkObject TestIp1 = new() { Id = 1, Name = "TestIp1", IP = "1.2.3.4/32", IpEnd = "1.2.3.4/32", Type = new NetworkObjectType() { Name = ObjectType.Network } };
        static readonly NetworkObject TestIp2 = new() { Id = 2, Name = "TestIp2", IP = "127.0.0.1/32", IpEnd = "127.0.0.1/32", Type = new NetworkObjectType() { Name = ObjectType.Network } };
        static readonly NetworkObject TestIpRange = new() { Id = 3, Name = "TestIpRange", IP = "1.2.3.4/32", IpEnd = "1.2.3.5/32", Type = new NetworkObjectType() { Name = ObjectType.IPRange } };
        static readonly NetworkObject TestIpNew = new() { Id = 4, Name = "TestIpNew", IP = "10.0.6.0/32", IpEnd = "10.0.6.255/32", Type = new NetworkObjectType() { Name = ObjectType.Network } };
        static readonly NetworkObject TestIp1Changed = new() { Id = 5, Name = "TestIp1Changed", IP = "2.3.4.5/32", IpEnd = "2.3.4.5/32", Type = new NetworkObjectType() { Name = ObjectType.Host } };

        static readonly NetworkService TestService1 = new() { Id = 1, DestinationPort = 443, DestinationPortEnd = 443, Name = "TestService1", Protocol = new NetworkProtocol { Id = 6, Name = "TCP" } };
        static readonly NetworkService TestService2 = new() { Id = 2, DestinationPort = 6666, DestinationPortEnd = 7777, Name = "TestService2", Protocol = new NetworkProtocol { Id = 17, Name = "UDP" } };

        static readonly NetworkUser TestUser1 = new() { Id = 1, Name = "TestUser1" };
        static readonly NetworkUser TestUser2 = new() { Id = 2, Name = "TestUser2", Type = new NetworkUserType() { Name = ObjectType.Group } };

        static Rule Rule1 = new();
        static Rule Rule1Changed = new();
        static Rule Rule2 = new();
        static Rule Rule2Changed = new();
        static Rule NatRule = new();
        static Rule RecertRule1 = new();
        static Rule RecertRule2 = new();

        private const string ToCAnkerIdGroupName = "ToCAnkerId";
        private readonly string ToCRegexPattern = $"<a href=\"#(?'{ToCAnkerIdGroupName}'.*?)\">(.*?)<\\/a>";
        private const string StaticAnkerId = "1234-1234-1234-1234";

        readonly SimulatedUserConfig userConfig = new();
        readonly DynGraphqlQuery query = new("TestFilter")
        {
            ReportTimeString = "2023-04-20T17:50:04",
        };
        readonly TimeFilter timeFilter = new()
        {
            TimeRangeType = TimeRangeType.Fixeddates,
            StartTime = DateTime.Parse("2023-04-19T17:00:04"),
            EndTime = DateTime.Parse("2023-04-20T17:00:04")
        };

        [SetUp]
        public void Initialize()
        {
        }
    }
}
