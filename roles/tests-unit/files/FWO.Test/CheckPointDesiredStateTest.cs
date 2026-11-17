using FWO.Data.DeviceSpecificSettings;
using FWO.ExternalSystems.FirewallWrite;
using NUnit.Framework;
using RestSharp;
using System.Net;
using System.Text.Json.Nodes;
using static FWO.Test.FirewallWriteTestSupport;

namespace FWO.Test
{
    /// <summary>
    /// Tests that a Check Point create call failing because the object already exists counts as successful if the object
    /// is present as requested, e.g. after a retry whose first attempt reached the firewall, and fails otherwise.
    /// </summary>
    [TestFixture]
    [Parallelizable]
    internal class CheckPointDesiredStateTest
    {
        private const string kCheckPointLogin = "{\"sid\":\"sid-1\"}";
        private const string kDuplicateName = "{\"code\":\"err_validation_failed\",\"message\":\"Validation failed with 1 error\",\"errors\":[{\"message\":\"More than one object named 'h1' exists.\"}]}";
        private const string kHostBody = "{\"name\":\"h1\",\"ip-address\":\"10.0.0.1\",\"comments\":\"\"}";
        private const string kNetworkBody = "{\"name\":\"n1\",\"subnet\":\"10.0.0.0\",\"mask-length\":24,\"comments\":\"\"}";
        private const string kRangeBody = "{\"name\":\"r1\",\"ip-address-first\":\"10.0.0.1\",\"ip-address-last\":\"10.0.0.9\",\"comments\":\"\"}";
        private const string kGroupBody = "{\"name\":\"g1\",\"members\":[\"h1\"],\"comments\":\"\"}";
        private static readonly List<string> kHostCalls = ["login", "add-host", "show-host"];

        private static RenderedFirewallRequest Request(FirewallWriteOperation operation, string path, string body)
        {
            return new RenderedFirewallRequest { Step = new FirewallWriteStep(operation) { TargetName = "x" }, Method = "POST", Path = path, Body = body };
        }

        private static async Task<(FirewallStepResult Result, FirewallSimulation Simulation)> Execute(RenderedFirewallRequest request, string? existing)
        {
            FirewallSimulation simulation = new()
            {
                Handler = recorded => recorded.Resource switch
                {
                    "login" => Ok(kCheckPointLogin),
                    _ when recorded.Resource.StartsWith("show-") => existing == null
                        ? Response(HttpStatusCode.NotFound, "{\"code\":\"generic_err_object_not_found\"}")
                        : Ok(existing),
                    _ => Response(HttpStatusCode.BadRequest, kDuplicateName)
                }
            };
            SimulatedCheckPointWriteClient client = new(Management(FirewallVendors.kCheckPointR8x), simulation);
            await client.Login();
            return (await client.Execute(request), simulation);
        }

        [Test]
        public async Task HostWithTheRequestedAddress_CountsAsCreated()
        {
            (FirewallStepResult result, FirewallSimulation simulation) = await Execute(Request(FirewallWriteOperation.HostCreate, "add-host", kHostBody),
                "{\"name\":\"h1\",\"ipv4-address\":\"10.0.0.1\"}");

            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.Success, Is.True, result.Message);
                Assert.That(result.Error, Is.Null);
                Assert.That(result.Message, Does.StartWith("Already present as requested."));
                Assert.That(simulation.Requests.Select(request => request.Resource), Is.EqualTo(kHostCalls));
                Assert.That(JsonNode.Parse(simulation.Requests[2].Body!)!["name"]!.GetValue<string>(), Is.EqualTo("h1"));
            }
        }

        [Test]
        public async Task HostWithAnotherAddress_FailsAsAlreadyExisting()
        {
            (FirewallStepResult result, _) = await Execute(Request(FirewallWriteOperation.HostCreate, "add-host", kHostBody),
                "{\"name\":\"h1\",\"ipv4-address\":\"10.0.0.2\"}");

            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.Success, Is.False);
                Assert.That(result.Error?.Kind, Is.EqualTo(FirewallWriteErrorKind.AlreadyExists));
            }
        }

        [Test]
        public async Task ObjectThatCannotBeRead_FailsAsAlreadyExisting()
        {
            (FirewallStepResult result, _) = await Execute(Request(FirewallWriteOperation.HostCreate, "add-host", kHostBody), null);

            Assert.That(result.Error?.Kind, Is.EqualTo(FirewallWriteErrorKind.AlreadyExists));
        }

        [Test]
        public async Task ExistingGroupWithTheRequestedMembers_CountsAsCreated()
        {
            (FirewallStepResult result, _) = await Execute(Request(FirewallWriteOperation.GroupCreate, "add-group", kGroupBody),
                "{\"name\":\"g1\",\"members\":[{\"name\":\"H1\",\"uid\":\"u1\"}]}");

            Assert.That(result.Success, Is.True, result.Message);
        }

        /// <summary>
        /// A group of the same name with other members is not the requested group: the task fails instead of being done
        /// with members that were never written.
        /// </summary>
        [Test]
        public async Task ExistingGroupWithOtherMembers_FailsAsAlreadyExisting()
        {
            (FirewallStepResult result, _) = await Execute(Request(FirewallWriteOperation.GroupCreate, "add-group", kGroupBody),
                "{\"name\":\"g1\",\"members\":[{\"name\":\"h9\",\"uid\":\"u9\"}]}");

            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.Success, Is.False);
                Assert.That(result.Error?.Kind, Is.EqualTo(FirewallWriteErrorKind.AlreadyExists));
            }
        }

        [Test]
        public async Task OperationWithoutComparison_FailsAsAlreadyExisting()
        {
            (FirewallStepResult result, FirewallSimulation simulation) = await Execute(
                Request(FirewallWriteOperation.ServiceCreate, "add-service-tcp", "{\"name\":\"s1\",\"port\":\"80\"}"), "{\"name\":\"s1\"}");

            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.Error?.Kind, Is.EqualTo(FirewallWriteErrorKind.AlreadyExists));
                Assert.That(simulation.Requests.Any(request => request.Resource.StartsWith("show-")), Is.False);
            }
        }

        [Test]
        public async Task OtherError_IsNotCompared()
        {
            FirewallSimulation simulation = new()
            {
                Handler = recorded => recorded.Resource == "login" ? Ok(kCheckPointLogin) : Response(HttpStatusCode.BadRequest, "{\"code\":\"generic_err_invalid_parameter\"}")
            };
            SimulatedCheckPointWriteClient client = new(Management(FirewallVendors.kCheckPointR8x), simulation);
            await client.Login();

            FirewallStepResult result = await client.Execute(Request(FirewallWriteOperation.HostCreate, "add-host", kHostBody));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.Success, Is.False);
                Assert.That(simulation.Requests.Any(request => request.Resource.StartsWith("show-")), Is.False);
            }
        }

        [Test]
        public async Task OtherVendor_KeepsTheAlreadyExistingError()
        {
            FirewallSimulation simulation = new() { Handler = _ => Response(HttpStatusCode.InternalServerError, "{\"status\":\"error\",\"error\":-5}") };
            SimulatedFortiOsWriteClient client = new(Management(FirewallVendors.kFortiOsGateway), simulation);
            await client.Login();

            FirewallStepResult result = await client.Execute(Request(FirewallWriteOperation.HostCreate, "api/v2/cmdb/firewall/address", "{\"name\":\"h1\"}"));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.Error?.Kind, Is.EqualTo(FirewallWriteErrorKind.AlreadyExists));
                Assert.That(simulation.Requests, Has.Count.EqualTo(1));
            }
        }

        private static async Task<(FirewallStepResult Result, FirewallSimulation Simulation)> DeleteMissingRule(string body, bool layerExists)
        {
            FirewallSimulation simulation = new()
            {
                Handler = recorded => recorded.Resource switch
                {
                    "login" => Ok(kCheckPointLogin),
                    "show-access-layer" when layerExists => Ok("{\"name\":\"Network\"}"),
                    _ => Response(HttpStatusCode.NotFound, "{\"code\":\"generic_err_object_not_found\"}")
                }
            };
            SimulatedCheckPointWriteClient client = new(Management(FirewallVendors.kCheckPointR8x), simulation);
            await client.Login();
            return (await client.Execute(Request(FirewallWriteOperation.RuleDelete, "delete-access-rule", body)), simulation);
        }

        [Test]
        public async Task RuleAlreadyDeletedInAnExistingLayer_CountsAsDeleted()
        {
            const string layerUid = "8f1c2a7e-3b4d-4c5e-9f60-1a2b3c4d5e6f";
            (FirewallStepResult result, FirewallSimulation simulation) = await DeleteMissingRule(
                $"{{\"uid\":\"r1\",\"layer\":\"{layerUid}\"}}", true);
            RecordedFirewallRequest layerCheck = simulation.Requests.Single(request => request.Resource == "show-access-layer");
            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.Success, Is.True, result.Message);
                Assert.That(result.Message, Does.StartWith("Already deleted."));
                Assert.That(JsonNode.Parse(layerCheck.Body!)!["uid"]!.GetValue<string>(), Is.EqualTo(layerUid));
            }
        }

        [Test]
        public async Task RuleNotFoundInAMissingLayer_FailsAsNotFound()
        {
            (FirewallStepResult result, FirewallSimulation simulation) = await DeleteMissingRule("{\"uid\":\"r1\",\"layer\":\"Network\"}", false);
            RecordedFirewallRequest layerCheck = simulation.Requests.Single(request => request.Resource == "show-access-layer");
            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.Success, Is.False);
                Assert.That(result.Error?.Kind, Is.EqualTo(FirewallWriteErrorKind.NotFound));
                Assert.That(JsonNode.Parse(layerCheck.Body!)!["name"]!.GetValue<string>(), Is.EqualTo("Network"));
            }
        }

        [Test]
        public async Task RuleDeleteWithoutLayer_IsNotChecked()
        {
            (FirewallStepResult result, FirewallSimulation simulation) = await DeleteMissingRule("{\"uid\":\"r1\"}", true);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.Error?.Kind, Is.EqualTo(FirewallWriteErrorKind.NotFound));
                Assert.That(simulation.Requests.Any(request => request.Resource == "show-access-layer"), Is.False);
            }
        }

        [Test]
        public async Task OtherVendor_KeepsTheNotFoundErrorOfARuleDelete()
        {
            FirewallSimulation simulation = new() { Handler = _ => Response(HttpStatusCode.NotFound, "{}") };
            SimulatedFortiOsWriteClient client = new(Management(FirewallVendors.kFortiOsGateway), simulation);
            await client.Login();

            FirewallStepResult result = await client.Execute(Request(FirewallWriteOperation.RuleDelete, "api/v2/cmdb/firewall/policy/1", "{}"));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.Error?.Kind, Is.EqualTo(FirewallWriteErrorKind.NotFound));
                Assert.That(simulation.Requests, Has.Count.EqualTo(1));
            }
        }

        [TestCase("{\"members\":[{\"name\":\"h1\"},{\"name\":\"H2\"}]}", true)]
        [TestCase("{\"members\":[\"h2\",\"h1\"]}", true)]
        [TestCase("{\"members\":[{\"name\":\"h1\"}]}", false)]
        [TestCase("{\"members\":[{\"name\":\"h1\"},{\"name\":\"h2\"},{\"name\":\"h3\"}]}", false)]
        [TestCase("{\"members\":[{\"uid\":\"u1\"},{\"name\":\"h2\"}]}", false)]
        [TestCase("{}", false)]
        public void GroupMatches_ComparesTheMemberNames(string existing, bool expected)
        {
            JsonNode requested = JsonNode.Parse("{\"name\":\"g1\",\"members\":[\"h1\",\"h2\"]}")!;
            Assert.That(CheckPointObjectComparison.GroupMatches(requested, JsonNode.Parse(existing)), Is.EqualTo(expected));
        }

        [TestCase("{\"ipv4-address\":\"10.0.0.1\"}", true)]
        [TestCase("{\"ipv6-address\":\"10.0.0.1\"}", true)]
        [TestCase("{\"ipv4-address\":\"10.0.0.2\"}", false)]
        [TestCase("{}", false)]
        public void HostMatches_ComparesTheAddress(string existing, bool expected)
        {
            Assert.That(CheckPointObjectComparison.HostMatches(JsonNode.Parse(kHostBody), JsonNode.Parse(existing)), Is.EqualTo(expected));
        }

        [TestCase("{\"subnet4\":\"10.0.0.0\",\"mask-length4\":24}", true)]
        [TestCase("{\"subnet\":\"10.0.0.0\",\"mask-length\":\"24\"}", true)]
        [TestCase("{\"subnet4\":\"10.0.0.0\",\"subnet-mask\":\"255.255.255.0\"}", true)]
        [TestCase("{\"subnet4\":\"10.0.0.0\",\"mask-length4\":16}", false)]
        [TestCase("{\"subnet4\":\"10.0.1.0\",\"mask-length4\":24}", false)]
        [TestCase("{\"subnet4\":\"10.0.0.0\"}", false)]
        public void NetworkMatches_ComparesSubnetAndMaskLength(string existing, bool expected)
        {
            Assert.That(CheckPointObjectComparison.NetworkMatches(JsonNode.Parse(kNetworkBody), JsonNode.Parse(existing)), Is.EqualTo(expected));
        }

        [TestCase("{\"subnet6\":\"2001:db8::\",\"mask-length6\":64}", true)]
        [TestCase("{\"subnet6\":\"2001:db8::\",\"mask-length6\":48}", false)]
        [TestCase("{\"subnet4\":\"10.0.0.0\",\"mask-length4\":64}", false)]
        public void NetworkMatches_ComparesIpv6Networks(string existing, bool expected)
        {
            JsonNode requested = JsonNode.Parse("{\"name\":\"n6\",\"subnet\":\"2001:db8::\",\"mask-length\":64}")!;

            Assert.That(CheckPointObjectComparison.NetworkMatches(requested, JsonNode.Parse(existing)), Is.EqualTo(expected));
        }

        [TestCase("{\"ipv4-address-first\":\"10.0.0.1\",\"ipv4-address-last\":\"10.0.0.9\"}", true)]
        [TestCase("{\"ip-address-first\":\"10.0.0.1\",\"ip-address-last\":\"10.0.0.9\"}", true)]
        [TestCase("{\"ipv4-address-first\":\"10.0.0.1\",\"ipv4-address-last\":\"10.0.0.8\"}", false)]
        [TestCase("{\"ipv4-address-first\":\"10.0.0.1\"}", false)]
        public void AddressRangeMatches_ComparesFirstAndLastAddress(string existing, bool expected)
        {
            Assert.That(CheckPointObjectComparison.AddressRangeMatches(JsonNode.Parse(kRangeBody), JsonNode.Parse(existing)), Is.EqualTo(expected));
        }

        [Test]
        public void Comparison_WithoutRequestedValues_IsNoMatch()
        {
            JsonNode? empty = JsonNode.Parse("{}");

            using (Assert.EnterMultipleScope())
            {
                Assert.That(CheckPointObjectComparison.HostMatches(empty, JsonNode.Parse("{\"ipv4-address\":\"10.0.0.1\"}")), Is.False);
                Assert.That(CheckPointObjectComparison.NetworkMatches(empty, JsonNode.Parse("{\"subnet4\":\"10.0.0.0\",\"mask-length4\":24}")), Is.False);
                Assert.That(CheckPointObjectComparison.AddressRangeMatches(empty, null), Is.False);
            }
        }
    }
}
