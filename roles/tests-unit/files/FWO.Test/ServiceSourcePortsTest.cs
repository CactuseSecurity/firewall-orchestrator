using System.Text;
using FWO.Basics;
using FWO.Data;
using FWO.Data.Modelling;
using FWO.Data.Report;
using FWO.Report;
using FWO.Report.Filter;
using FWO.Services;
using FWO.Ui.Display;
using Newtonsoft.Json;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    [Parallelizable]
    public class ServiceSourcePortsTest
    {
        private const string kPayload = "<img src=x onerror=\"alert('quote')\">";
        private static readonly NetworkService[] kReportServices = [CreateService()];
        private static readonly NetworkService kModelledGroup = new() { Name = "grp", ServiceGroupFlats = [new GroupFlat<NetworkService>() { Object = CreateHttpsService(null, null) }] };
        private static readonly NetworkService kSourceRestrictedGroup = new() { Name = "grp", ServiceGroupFlats = [new GroupFlat<NetworkService>() { Object = CreateHttpsService(1024, 1024) }] };
        private static readonly NetworkService kFullSourceRangeGroup = new() { Name = "grp", ServiceGroupFlats = [new GroupFlat<NetworkService>() { Object = CreateHttpsService(1, 65535) }] };

        private sealed class RulesReportProbe() : ReportRules(new DynGraphqlQuery(""), new SimulatedUserConfig(), ReportType.Rules)
        {
            /// <summary>Renders the service table through the production HTML export.</summary>
            public string RenderServices()
            {
                StringBuilder output = new();
                ManagementReport management = new() { ReportServices = kReportServices };
                AppendNetworkServicesForManagementHtml(ref output, 1, management);
                return output.ToString();
            }
        }

        private sealed class ConnectionsReportProbe() : ReportConnections(new DynGraphqlQuery(""), new SimulatedUserConfig(), ReportType.Connections)
        {
            /// <summary>Renders the service table through the production connection export.</summary>
            public string RenderServices()
            {
                StringBuilder output = new();
                List<NetworkService> services = new(kReportServices);
                AppendNetworkServicesHtml(services, 1, ref output);
                return output.ToString();
            }
        }

        /// <summary>Creates a service carrying both port pairs and untrusted display values.</summary>
        private static NetworkService CreateService()
        {
            return new NetworkService
            {
                Id = 1,
                Name = kPayload,
                Uid = kPayload,
                Comment = kPayload,
                Type = new() { Name = ServiceType.SimpleService },
                Protocol = new() { Id = 6, Name = kPayload },
                DestinationPort = 443,
                DestinationPortEnd = 444,
                SourcePort = 7937,
                SourcePortEnd = 10000
            };
        }

        /// <summary>Checks single, range, absent and zero-valued source ports without changing legacy output.</summary>
        [TestCase(null, null, 443, 443, "443")]
        [TestCase(1024, 1024, 443, 443, "1024 → 443")]
        [TestCase(7937, 10000, 443, 444, "7937-10000 → 443-444")]
        [TestCase(0, 0, null, null, "0 → *")]
        public void DisplayPortsRetainsBothDirections(int? source, int? sourceEnd, int? destination, int? destinationEnd, string expected)
        {
            NetworkService service = new()
            {
                Name = "service",
                Protocol = new() { Id = 6, Name = "TCP" },
                SourcePort = source,
                SourcePortEnd = sourceEnd,
                DestinationPort = destination,
                DestinationPortEnd = destinationEnd
            };
            Assert.Multiple(() =>
            {
                Assert.That(DisplayBase.DisplayServicePorts(service), Is.EqualTo(expected));
                Assert.That(DisplayBase.DisplayServicePorts(service, true), Is.EqualTo($" ({expected})"));
                Assert.That(DisplayBase.DisplayService(service, true).ToString(), Is.EqualTo($"{expected}/TCP"));
                Assert.That(DisplayBase.DisplayService(service, false).ToString(), Is.EqualTo($"service ({expected}/TCP)"));
            });
        }

        /// <summary>Checks the selection list format keeps legacy output and prefixes source restrictions.</summary>
        [TestCase(null, null, 443, 443, "TCP", "443/TCP")]
        [TestCase(null, null, null, null, "TCP", "TCP")]
        [TestCase(null, null, null, null, null, "")]
        [TestCase(1024, 2048, 443, 444, "TCP", "1024-2048 → 443-444/TCP")]
        [TestCase(1024, 1024, null, null, "UDP", "1024 → */UDP")]
        [TestCase(1024, 1024, null, null, null, "1024 → *")]
        public void SelectionListPortsShowSourceRestrictions(int? source, int? sourceEnd, int? destination, int? destinationEnd, string? protocol, string expected)
        {
            NetworkService service = new()
            {
                SourcePort = source,
                SourcePortEnd = sourceEnd,
                DestinationPort = destination,
                DestinationPortEnd = destinationEnd
            };
            Assert.That(DisplayBase.DisplayServicePortsWithProtocol(service, protocol), Is.EqualTo(expected));
        }

        /// <summary>Creates a TCP service on port 443 with the given source port range.</summary>
        private static NetworkService CreateHttpsService(int? sourcePort, int? sourcePortEnd)
        {
            return new NetworkService
            {
                Name = "https",
                ProtoId = 6,
                DestinationPort = 443,
                DestinationPortEnd = 443,
                SourcePort = sourcePort,
                SourcePortEnd = sourcePortEnd
            };
        }

        /// <summary>Checks the variance comparer treats a source restriction as different from an unrestricted service.</summary>
        [TestCase(null, null, true)]
        [TestCase(1, 65535, true)]
        [TestCase(0, 65535, true)]
        [TestCase(1024, 1024, false)]
        [TestCase(1024, 65535, false)]
        [TestCase(1, 1023, false)]
        public void ComparerRegardsSourcePortsAgainstUnrestrictedService(int? sourcePort, int? sourcePortEnd, bool expectedEqual)
        {
            NetworkServiceComparer comparer = new(new RuleRecognitionOption() { SvcRegardPortAndProt = true });
            NetworkService unrestricted = CreateHttpsService(null, null);
            NetworkService imported = CreateHttpsService(sourcePort, sourcePortEnd);
            Assert.Multiple(() =>
            {
                Assert.That(comparer.Equals(unrestricted, imported), Is.EqualTo(expectedEqual));
                Assert.That(comparer.Equals(imported, unrestricted), Is.EqualTo(expectedEqual));
                Assert.That(comparer.GetHashCode(unrestricted) == comparer.GetHashCode(imported), Is.EqualTo(expectedEqual));
            });
        }

        /// <summary>Checks equal source ranges match, different ones do not, and ports are ignored when configured.</summary>
        [Test]
        public void ComparerComparesSourceRanges()
        {
            NetworkServiceComparer comparer = new(new RuleRecognitionOption() { SvcRegardPortAndProt = true });
            NetworkServiceComparer portBlindComparer = new(new RuleRecognitionOption() { SvcRegardPortAndProt = false });
            NetworkService source1024 = CreateHttpsService(1024, 1024);
            NetworkService sameSource = CreateHttpsService(1024, null);
            NetworkService otherSource = CreateHttpsService(2048, 2048);
            Assert.Multiple(() =>
            {
                Assert.That(comparer.Equals(source1024, sameSource), Is.True);
                Assert.That(comparer.GetHashCode(source1024), Is.EqualTo(comparer.GetHashCode(sameSource)));
                Assert.That(comparer.Equals(source1024, otherSource), Is.False);
                Assert.That(portBlindComparer.Equals(source1024, otherSource), Is.True);
            });
        }

        /// <summary>Checks service groups differing only in a member's source restriction are not equal.</summary>
        [Test]
        public void GroupComparerRegardsMemberSourcePorts()
        {
            NetworkServiceGroupComparer comparer = new(new RuleRecognitionOption() { SvcRegardPortAndProt = true });
            Assert.Multiple(() =>
            {
                Assert.That(comparer.Equals(kModelledGroup, kSourceRestrictedGroup), Is.False);
                Assert.That(comparer.Equals(kModelledGroup, kFullSourceRangeGroup), Is.True);
            });
        }

        /// <summary>Checks both JSON serializers preserve source ranges in normalized exports and read-back.</summary>
        [Test]
        public void NormalizedServiceRoundTripPreservesSourcePorts()
        {
            NormalizedServiceObject normalized = NormalizedServiceObject.FromNetworkService(CreateService());
            string legacy = JsonConvert.SerializeObject(normalized);
            string modern = System.Text.Json.JsonSerializer.Serialize(normalized);
            Assert.Multiple(() =>
            {
                Assert.That(normalized.SvcSourcePort, Is.EqualTo(7937));
                Assert.That(normalized.SvcSourcePortEnd, Is.EqualTo(10000));
                Assert.That(JsonConvert.DeserializeObject<NormalizedServiceObject>(legacy)?.SvcSourcePortEnd, Is.EqualTo(10000));
                Assert.That(System.Text.Json.JsonSerializer.Deserialize<NormalizedServiceObject>(modern)?.SvcSourcePort, Is.EqualTo(7937));
                Assert.That(legacy, Does.Contain("\"svc_source_port\":7937"));
                Assert.That(modern, Does.Contain("\"svc_source_port_end\":10000"));
            });
        }

        /// <summary>Checks both HTML service tables keep port columns and encode element, attribute and quote payloads.</summary>
        [Test]
        public void HtmlServiceTablesRetainPortsAndEncodeData()
        {
            List<string> tables = new() { new RulesReportProbe().RenderServices(), new ConnectionsReportProbe().RenderServices() };
            foreach (string html in tables)
            {
                Assert.Multiple(() =>
                {
                    Assert.That(html, Does.Contain("<th>source_port</th>"));
                    Assert.That(html, Does.Contain("<th>destination_port</th>"));
                    Assert.That(html, Does.Contain("<td>7937-10000</td>"));
                    Assert.That(html, Does.Contain("<td>443-444</td>"));
                    Assert.That(html, Does.Contain(HtmlOutputEncoder.EncodeText(kPayload)));
                    Assert.That(html, Does.Not.Contain(kPayload));
                });
            }
        }

        /// <summary>Checks source-only service changes remain visible in every change export.</summary>
        [TestCase('I')]
        [TestCase('D')]
        [TestCase('C')]
        public void ChangeExportsRetainSourcePorts(char action)
        {
            ServiceChange change = new() { ChangeAction = action, OldService = CreateService(), NewService = CreateService() };
            change.NewService.SourcePort = 8000;
            SimulatedUserConfig config = new();
            string expected = action == 'I' ? "8000-10000" : "7937-10000";
            Assert.Multiple(() =>
            {
                Assert.That(new RuleChangeDisplayCsv(config).DisplayServicePort(change), Does.Contain(expected));
                Assert.That(new RuleChangeDisplayHtml(config).DisplayServicePort(change), Does.Contain(expected));
                Assert.That(new RuleChangeDisplayJson(config).DisplayServicePort(change), Does.Contain(expected));
            });
        }
    }
}
