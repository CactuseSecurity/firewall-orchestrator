using FWO.Data;
using FWO.Data.Modelling;
using FWO.Data.Report;
using Newtonsoft.Json;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    internal class ConnectionReportTest
    {
        // app servers created from owner ip addresses were stored without a name (#5572)
        private const string kNamelessAppServerJson = "{\"id\":44,\"name\":null,\"ip\":\"10.0.1.70/32\",\"ip_end\":\"10.0.1.79/32\"}";
        private const string kNamelessSubnetJson = "{\"id\":45,\"name\":null,\"ip\":\"10.0.2.128/32\",\"ip_end\":\"10.0.2.255/32\"}";

        private static ModellingAppServer CreateNamelessAppServer()
        {
            return JsonConvert.DeserializeObject<ModellingAppServer>(kNamelessAppServerJson)
                ?? throw new AssertionException("Could not deserialize app server.");
        }

        private static ModellingAppRole CreateAppRole(long id, string name, string idString, List<ModellingAppServerWrapper> appServers)
        {
            return new ModellingAppRole
            {
                Id = id,
                Name = name,
                IdString = idString,
                AppServers = appServers
            };
        }

        private static ConnectionReport CreateReport(List<ModellingAppRole> sourceAppRoles)
        {
            ModellingConnection connection = new()
            {
                SourceAppRoles = sourceAppRoles.ConvertAll(appRole => new ModellingAppRoleWrapper { Content = appRole })
            };
            List<ModellingConnection> connections = new() { connection };
            return new ConnectionReport
            {
                AllObjects = ConnectionReport.GetAllNetworkObjects(connections, resolved: true)
            };
        }

        [Test]
        public void ResolveObjId_DoesNotThrow_WhenAppRoleContainsNamelessAppServer()
        {
            ModellingAppServer namelessAppServer = CreateNamelessAppServer();
            List<ModellingAppServerWrapper> appServers = new() { new ModellingAppServerWrapper { Content = namelessAppServer } };
            ModellingAppRole emptyAppRole = CreateAppRole(1, "AR-empty", "AR5000001", new List<ModellingAppServerWrapper>());
            ModellingAppRole appRoleWithNamelessServer = CreateAppRole(2, "AR-nameless", "AR5000002", appServers);
            ConnectionReport report = CreateReport(new List<ModellingAppRole> { appRoleWithNamelessServer, emptyAppRole });

            long resolvedId = report.ResolveObjId(emptyAppRole);

            Assert.That(resolvedId, Is.EqualTo(1));
        }

        [Test]
        public void ResolveObjId_ResolvesNamelessAppServer()
        {
            ModellingAppServer namelessAppServer = CreateNamelessAppServer();
            List<ModellingAppServerWrapper> appServers = new() { new ModellingAppServerWrapper { Content = namelessAppServer } };
            ConnectionReport report = CreateReport(new List<ModellingAppRole> { CreateAppRole(2, "AR-nameless", "AR5000002", appServers) });

            long resolvedId = report.ResolveObjId(namelessAppServer);

            Assert.That(resolvedId, Is.EqualTo(44));
        }

        [Test]
        public void ResolveObjId_DistinguishesNamesSharingAPrefix()
        {
            ModellingAppRole longerName = CreateAppRole(10, "AR1", "AR5000010", new List<ModellingAppServerWrapper>());
            ModellingAppRole shorterName = CreateAppRole(1, "AR", "AR5000001", new List<ModellingAppServerWrapper>());
            ConnectionReport report = CreateReport(new List<ModellingAppRole> { longerName, shorterName });

            Assert.That(report.ResolveObjId(shorterName), Is.EqualTo(1));
            Assert.That(report.ResolveObjId(longerName), Is.EqualTo(10));
        }

        [Test]
        public void ResolveObjId_ReturnsZero_WhenObjectIsNotListed()
        {
            ConnectionReport report = CreateReport(new List<ModellingAppRole> { CreateAppRole(1, "AR", "AR5000001", new List<ModellingAppServerWrapper>()) });
            ModellingAppRole unlistedAppRole = CreateAppRole(3, "AR", "AR5000003", new List<ModellingAppServerWrapper>());

            long resolvedId = report.ResolveObjId(unlistedAppRole);

            Assert.That(resolvedId, Is.EqualTo(0));
        }

        [Test]
        public void ResolveObjId_ReturnsZero_WhenIdMatchesObjectWithOtherName()
        {
            ModellingAppServer appServer = new() { Id = 7, Name = "server-7", Ip = "10.0.0.7/32", IpEnd = "10.0.0.7/32" };
            List<ModellingAppServerWrapper> appServers = new() { new ModellingAppServerWrapper { Content = appServer } };
            ConnectionReport report = CreateReport(new List<ModellingAppRole> { CreateAppRole(1, "AR", "AR5000001", appServers) });
            ModellingAppRole appRoleWithSameId = CreateAppRole(7, "AR-other", "AR5000007", new List<ModellingAppServerWrapper>());

            long resolvedId = report.ResolveObjId(appRoleWithSameId);

            Assert.That(resolvedId, Is.EqualTo(0));
        }

        [Test]
        public void AppServerToNetworkObject_UsesEmptyName_WhenNameIsMissing()
        {
            NetworkObject networkObject = ModellingAppServer.ToNetworkObject(CreateNamelessAppServer());

            Assert.That(networkObject.Name, Is.EqualTo(""));
            Assert.That(networkObject.Id, Is.EqualTo(44));
        }

        [Test]
        public void SubnetToNetworkObject_UsesEmptyName_WhenNameIsMissing()
        {
            NetworkSubnet subnet = JsonConvert.DeserializeObject<NetworkSubnet>(kNamelessSubnetJson)
                ?? throw new AssertionException("Could not deserialize subnet.");

            NetworkObject networkObject = NetworkSubnet.ToNetworkObject(subnet);

            Assert.That(networkObject.Name, Is.EqualTo(""));
            Assert.That(networkObject.Id, Is.EqualTo(45));
        }
    }
}
