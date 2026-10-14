using FWO.Basics.Exceptions;
using FWO.Data;
using FWO.Data.Middleware;
using FWO.Middleware.Server;
using FWO.Middleware.Server.Controllers;
using Microsoft.AspNetCore.Mvc;
using Novell.Directory.Ldap;
using NUnit.Framework;

namespace FWO.Test
{
    /// <summary>
    /// An LDAP connection lists all servers of its directory, which are replicas tried in order.
    /// </summary>
    [TestFixture]
    [Parallelizable]
    internal class LdapServerListTest
    {
        private const string kFirst = "ldap1.example.com";
        private const string kSecond = "ldap2.example.com";
        private const string kThird = "10.0.0.3";
        private static readonly List<string> kThreeServers = [kFirst, kSecond, kThird];
        private static readonly List<string> kFirstAndSecond = [kFirst, kSecond];
        private static readonly List<string> kSecondThenThird = [kSecond, kThird];
        private static readonly List<string> kNoServers = [];

        [Test]
        public void ParseAddresses_SplitsAtSeparatorsAndDropsEmptyAndDuplicateEntries()
        {
            List<string> addresses = LdapConnectionBase.ParseAddresses($" {kFirst}, {kSecond};{kThird}\n{kFirst.ToUpperInvariant()} ,, ");

            Assert.Multiple(() =>
            {
                Assert.That(addresses, Is.EqualTo(kThreeServers));
                Assert.That(LdapConnectionBase.ParseAddresses(null), Is.Empty);
            });
        }

        [Test]
        public void Host_ListsEveryServerWithThePort()
        {
            LdapConnectionBase ldap = new() { Addresses = [kFirst, kThird], Port = 636 };

            Assert.Multiple(() =>
            {
                Assert.That(ldap.Host(), Is.EqualTo($"{kFirst}:636, {kThird}:636"));
                Assert.That(new LdapConnectionBase().Host(), Is.Empty);
            });
        }

        [Test]
        public void Sanitize_TrimsAndDropsEmptyAndDuplicateServers()
        {
            LdapConnectionBase ldap = new() { Addresses = [$" {kFirst} ", "", kSecond, kFirst.ToUpperInvariant()] };

            ldap.Sanitize();

            Assert.That(ldap.Addresses, Is.EqualTo(kFirstAndSecond));
        }

        [Test]
        public void AddressText_ShowsAndParsesTheServerList()
        {
            UiLdapConnection ldap = new() { AddressText = $"{kFirst} {kSecond}" };

            Assert.Multiple(() =>
            {
                Assert.That(ldap.Addresses, Is.EqualTo(kFirstAndSecond));
                Assert.That(ldap.AddressText, Is.EqualTo($"{kFirst}, {kSecond}"));
                Assert.That(ldap.Name, Is.EqualTo($"{kFirst}:0, {kSecond}:0"), "the name falls back to the servers");
            });
        }

        [Test]
        public void Copies_DoNotShareTheServerList()
        {
            UiLdapConnection original = new() { Addresses = [kFirst] };
            UiLdapConnection copy = new(original);
            LdapGetUpdateParameters parameters = original.ToApiParams();
            LdapConnectionBase fromParameters = new(parameters);

            original.Addresses.Add(kSecond);

            Assert.Multiple(() =>
            {
                Assert.That(copy.Addresses, Has.Count.EqualTo(1));
                Assert.That(parameters.Addresses, Has.Count.EqualTo(1));
                Assert.That(fromParameters.Addresses, Has.Count.EqualTo(1));
                Assert.That(new LdapAddParameters(parameters).Addresses, Has.Count.EqualTo(1));
            });
        }

        [Test]
        public async Task Connect_UsesTheFirstServerThatAnswers_AndStaysWithIt()
        {
            ServerListLdap ldap = new(kThreeServers, kFirst);

            using (await ldap.OpenConnection()) { }
            using (await ldap.OpenConnection()) { }

            // the failed first server is only tried once: the next connection starts with the one that answered
            Assert.That(ldap.Attempts, Is.EqualTo(new List<string> { kFirst, kSecond, kSecond }));
        }

        [Test]
        public async Task Connect_TriesEachServerOnceInOrder()
        {
            ServerListLdap ldap = new(kThreeServers, kFirst, kSecond);

            using ILdapClient connection = await ldap.OpenConnection();

            Assert.That(ldap.Attempts, Is.EqualTo(kThreeServers));
        }

        [Test]
        public void Connect_FailsWhenNoServerAnswers()
        {
            ServerListLdap ldap = new(kFirstAndSecond, kFirst, kSecond);

            LdapConnectionException exception = Assert.ThrowsAsync<LdapConnectionException>(async () => await ldap.OpenConnection())!;

            Assert.Multiple(() =>
            {
                Assert.That(exception.Message, Does.Contain(kFirst).And.Contain(kSecond));
                Assert.That(exception.InnerException, Is.InstanceOf<LdapConnectionException>());
                Assert.That(ldap.Attempts, Is.EqualTo(kFirstAndSecond));
            });
        }

        [Test]
        public void Connect_FailsWithoutServer()
        {
            ServerListLdap ldap = new(kNoServers);

            Assert.ThrowsAsync<LdapConnectionException>(async () => await ldap.OpenConnection());
            Assert.That(ldap.Attempts, Is.Empty);
        }

        [Test]
        public async Task TestConnection_BindsOnEveryServer()
        {
            ServerListLdap ldap = new(kSecondThenThird) { SearchUser = "cn=search", SearchUserPwd = "secret" };

            await ldap.TestConnection();

            Assert.Multiple(() =>
            {
                Assert.That(ldap.Attempts, Is.EqualTo(kSecondThenThird));
                Assert.That(ldap.Clients.Select(client => client.BindCalls.Count), Is.All.EqualTo(1));
            });
        }

        [Test]
        public void TestConnection_NamesTheServerThatCannotBeReached()
        {
            ServerListLdap ldap = new(kSecondThenThird, kThird);

            LdapConnectionException exception = Assert.ThrowsAsync<LdapConnectionException>(async () => await ldap.TestConnection())!;

            Assert.That(exception.Message, Does.Contain(kThird));
        }

        [Test]
        public void TestConnection_FailsWithoutServer()
        {
            Assert.ThrowsAsync<LdapConnectionException>(async () => await new ServerListLdap(kNoServers).TestConnection());
        }

        [TestCase("")]
        [TestCase("ldap1.example.com, ,ldap2.example.com")]
        [TestCase("ldap1.example.com,LDAP1.example.com")]
        [TestCase("s1,s2,s3,s4,s5,s6,s7,s8,s9,s10,s11")]
        [TestCase("ldap 1.example.com")]
        public async Task PostAsync_RejectsAnInvalidServerList(string addresses)
        {
            List<Ldap> ldaps = [];
            AuthenticationServerController controller = new(new SimulatedApiConnection(), ldaps);
            LdapAddParameters parameters = new()
            {
                Addresses = addresses.Split(',').ToList(),
                SearchpathForUsers = "ou=users,dc=example,dc=com",
                Active = true
            };

            ActionResult<int> result = await controller.PostAsync(parameters);

            Assert.Multiple(() =>
            {
                Assert.That(result.Result, Is.InstanceOf<BadRequestObjectResult>());
                ValidationProblemDetails details = (ValidationProblemDetails)((BadRequestObjectResult)result.Result!).Value!;
                Assert.That(details.Errors.Keys, Does.Contain("addresses"));
                Assert.That(ldaps, Is.Empty);
            });
        }

        [Test]
        public async Task TestConnectionEndpoint_RejectsAnEmptyServerList()
        {
            List<Ldap> ldaps = [];
            AuthenticationServerController controller = new(new SimulatedApiConnection(), ldaps);

            ActionResult<string> result = await controller.TestConnection(new LdapGetUpdateParameters());

            Assert.That(result.Result, Is.InstanceOf<BadRequestObjectResult>());
        }

        /// <summary>
        /// An ldap whose servers either answer with a recording client or cannot be reached.
        /// </summary>
        private sealed class ServerListLdap : Ldap
        {
            private readonly HashSet<string> unreachableServers;

            public List<string> Attempts { get; } = [];
            public List<RecordingLdapClient> Clients { get; } = [];

            public ServerListLdap(List<string> addresses, params string[] unreachableServers)
            {
                Addresses = addresses;
                Port = 636;
                this.unreachableServers = [.. unreachableServers];
            }

            public Task<ILdapClient> OpenConnection()
            {
                return Connect();
            }

            protected override Task<ILdapClient> ConnectToServer(string address, CancellationToken cancellationToken = default)
            {
                Attempts.Add(address);
                if (unreachableServers.Contains(address))
                {
                    throw new LdapConnectionException($"Error while trying to reach LDAP server {address}:{Port}");
                }
                RecordingLdapClient client = new();
                Clients.Add(client);
                return Task.FromResult<ILdapClient>(client);
            }
        }
    }
}
