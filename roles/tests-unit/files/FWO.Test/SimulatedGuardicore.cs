using System.Globalization;
using System.Text.Json.Nodes;
using FWO.Basics;
using FWO.Data;
using FWO.Data.Labelling;
using RestSharp;

namespace FWO.Test
{
    /// <summary>
    /// A Guardicore that keeps its labels, segmentation rules and policy revisions in memory and answers the REST calls of
    /// the Guardicore write client as the real API does: label lookup and bulk create/update, label delete, rule list,
    /// create, replace and delete, and revisions. It also returns its state as the importer would import it: labels as
    /// groups of their addresses and rules with label groups, subnets and services.
    /// </summary>
    internal sealed class SimulatedGuardicore
    {
        private const string kAuthenticatePath = "/api/v3.0/authenticate";
        private const string kLabelsPath = "/api/v4.0/labels";
        private const string kLabelsBulkPath = "/api/v4.0/labels/bulk";
        private const string kRulesPath = "/api/v4.0/visibility/policy/rules";
        private const string kRevisionsPath = "/api/v4.0/visibility/policy/revisions";
        private const int kTcp = 6;
        private const int kUdp = 17;
        private const int kIcmp = 1;

        private int nextId = 1;

        /// <summary>Labels by id: key, value and dynamic criteria.</summary>
        public Dictionary<string, JsonObject> Labels { get; } = [];

        /// <summary>Segmentation rules by id, as written.</summary>
        public Dictionary<string, JsonObject> Rules { get; } = [];

        /// <summary>Published policy revisions, with their comments.</summary>
        public List<string> Revisions { get; } = [];

        /// <summary>The simulation recording every call, to be passed to the simulated write client.</summary>
        public FirewallSimulation Simulation { get; } = new();

        public SimulatedGuardicore()
        {
            Simulation.Handler = Answer;
        }

        /// <summary>Adds a label with one SUBNET criterion per address, e.g. a label of the app data import.</summary>
        public string AddLabel(string key, string value, params string[] subnets)
        {
            string id = NewId("lab");
            JsonArray criteria = [.. subnets.Select(subnet => (JsonNode)Criterion("SUBNET", subnet))];
            Labels[id] = new JsonObject { ["id"] = id, ["key"] = key, ["value"] = value, ["dynamic_criteria"] = criteria };
            return id;
        }

        /// <summary>The calls that changed the policy or labels: everything but logins and reads.</summary>
        public List<RecordedFirewallRequest> Writes() =>
            [.. Simulation.Requests.Where(request => request.Method != Method.Get && request.Resource != kAuthenticatePath)];

        /// <summary>The labels as the importer imports them: groups named "key: value" with one member per criterion.</summary>
        public List<NetworkObject> ImportedLabels() => [.. Labels.Values.Select((label, index) => new NetworkObject
        {
            Id = 1000 + index,
            Name = LabelName(label),
            Type = new() { Name = ObjectType.Group },
            ObjectGroupFlats = [.. Criteria(label).Select((address, member) => new GroupFlat<NetworkObject> { Object = AddressObject(2000 + (index * 100) + member, address) })]
        })];

        /// <summary>
        /// The rules as the importer imports them for a management: the ruleset as rule name, the comment, label groups
        /// (ANDed labels named as one group), subnets and one service per port, range or protocol without ports; the
        /// importer assigns them to the gateway it creates for the Guardicore management.
        /// </summary>
        public List<Rule> ImportedRules(int managementId, Device gateway) => [.. Rules.Values.Select((rule, index) => new Rule
        {
            Id = 5000 + index,
            Uid = GuardicoreText(rule["id"]),
            Name = GuardicoreText(rule["ruleset_name"]),
            Comment = GuardicoreText(rule["comments"]),
            MgmtId = managementId,
            Rulebase = new() { Name = $"04 Allow {GuardicoreText(rule["ruleset_name"])}" },
            Action = "accept",
            EnforcingGateways = [new() { Content = gateway }],
            Froms = [.. Side(rule["source"], 6000 + (index * 100))],
            Tos = [.. Side(rule["destination"], 7000 + (index * 100))],
            Services = [.. Services(rule, 8000 + (index * 100)).Select(service => new ServiceWrapper { Content = service })]
        })];

        private RestResponse Answer(RecordedFirewallRequest request)
        {
            string resource = request.Resource;
            return (request.Method, resource) switch
            {
                (_, kAuthenticatePath) => FirewallWriteTestSupport.Ok("""{"access_token":"tok"}"""),
                (Method.Get, kLabelsPath) => FirewallWriteTestSupport.Ok(FindLabels(request).ToJsonString()),
                (Method.Post, kLabelsBulkPath) => FirewallWriteTestSupport.Ok(WriteLabels(request.Body).ToJsonString()),
                (Method.Delete, _) when resource.StartsWith(kLabelsPath + "/", StringComparison.Ordinal) => Delete(Labels, resource[(kLabelsPath.Length + 1)..]),
                (Method.Get, kRulesPath) => FirewallWriteTestSupport.Ok(new JsonObject
                {
                    ["objects"] = new JsonArray([.. Rules.Values.Select(rule => rule.DeepClone())]),
                    ["total_count"] = Rules.Count
                }.ToJsonString()),
                (Method.Post, kRulesPath) => FirewallWriteTestSupport.Ok(CreateRule(request.Body).ToJsonString()),
                (Method.Put, _) when resource.StartsWith(kRulesPath + "/", StringComparison.Ordinal) => ReplaceRule(resource[(kRulesPath.Length + 1)..], request.Body),
                (Method.Delete, _) when resource.StartsWith(kRulesPath + "/", StringComparison.Ordinal) => Delete(Rules, resource[(kRulesPath.Length + 1)..]),
                (Method.Post, kRevisionsPath) => Publish(request.Body),
                _ => throw new InvalidOperationException($"Unexpected call {request.Method} {resource}")
            };
        }

        private JsonObject FindLabels(RecordedFirewallRequest request)
        {
            string key = request.QueryParameters.GetValueOrDefault("key") ?? "";
            string value = request.QueryParameters.GetValueOrDefault("value") ?? "";
            List<JsonObject> found = [.. Labels.Values.Where(label => GuardicoreText(label["key"]) == key && GuardicoreText(label["value"]) == value)];
            return new JsonObject { ["objects"] = new JsonArray([.. found.Select(label => label.DeepClone())]), ["total_count"] = found.Count };
        }

        /// <summary>Creates or updates (override) the labels of a bulk call by key and value; the criteria replace the old ones.</summary>
        private JsonObject WriteLabels(string? body)
        {
            JsonArray written = [];
            foreach (JsonObject item in JsonNode.Parse(body!)!.AsArray().OfType<JsonObject>())
            {
                string key = GuardicoreText(item["key"]);
                string value = GuardicoreText(item["value"]);
                string id = Labels.FirstOrDefault(label => GuardicoreText(label.Value["key"]) == key && GuardicoreText(label.Value["value"]) == value).Key
                    ?? NewId("lab");
                Labels[id] = new JsonObject { ["id"] = id, ["key"] = key, ["value"] = value, ["dynamic_criteria"] = item["criteria"]!.DeepClone() };
                written.Add(id);
            }
            return new JsonObject { ["succeeded"] = written, ["failed"] = new JsonArray() };
        }

        private JsonObject CreateRule(string? body)
        {
            string id = NewId("rule");
            JsonObject rule = JsonNode.Parse(body!)!.AsObject();
            rule["id"] = id;
            Rules[id] = rule;
            return new JsonObject { ["id"] = id };
        }

        private RestResponse ReplaceRule(string id, string? body)
        {
            if (!Rules.ContainsKey(id))
            {
                return FirewallWriteTestSupport.Response(System.Net.HttpStatusCode.NotFound, """{"error":"not found"}""");
            }
            JsonObject rule = JsonNode.Parse(body!)!.AsObject();
            rule["id"] = id;
            Rules[id] = rule;
            return FirewallWriteTestSupport.Ok("{}");
        }

        private static RestResponse Delete(Dictionary<string, JsonObject> objects, string id) => objects.Remove(id)
            ? FirewallWriteTestSupport.Ok("{}")
            : FirewallWriteTestSupport.Response(System.Net.HttpStatusCode.NotFound, """{"error":"not found"}""");

        private RestResponse Publish(string? body)
        {
            Revisions.Add(GuardicoreText(JsonNode.Parse(body!)?["comments"]));
            return FirewallWriteTestSupport.Ok("{}");
        }

        private List<NetworkLocation> Side(JsonNode? side, long firstId)
        {
            List<NetworkLocation> locations = [];
            foreach (JsonNode? andGroup in side?["labels"]?["or_labels"]?.AsArray() ?? [])
            {
                List<string> names = [.. (andGroup?["and_labels"]?.AsArray() ?? []).Select(id => LabelName(Labels[GuardicoreText(id)]))];
                locations.Add(new(new(), new NetworkObject { Id = firstId + locations.Count, Name = LabelAndGroups.GroupName(names), Type = new() { Name = ObjectType.Group } }));
            }
            foreach (JsonNode? subnet in side?["subnets"]?.AsArray() ?? [])
            {
                locations.Add(new(new(), AddressObject(firstId + locations.Count, GuardicoreText(subnet))));
            }
            return locations;
        }

        private static List<NetworkService> Services(JsonObject rule, long firstId)
        {
            int protoId = GuardicoreText(rule["ip_protocols"]?[0]) switch { "TCP" => kTcp, "UDP" => kUdp, _ => kIcmp };
            List<NetworkService> services = [];
            foreach (JsonNode? port in rule["ports"]?.AsArray() ?? [])
            {
                int number = port!.GetValue<int>();
                services.Add(new() { Id = firstId + services.Count, Name = $"{protoId}/{number}", ProtoId = protoId, DestinationPort = number, DestinationPortEnd = number });
            }
            foreach (JsonNode? range in rule["port_ranges"]?.AsArray() ?? [])
            {
                int start = range!["start"]!.GetValue<int>();
                int end = range["end"]!.GetValue<int>();
                services.Add(new() { Id = firstId + services.Count, Name = $"{protoId}/{start}-{end}", ProtoId = protoId, DestinationPort = start, DestinationPortEnd = end });
            }
            if (services.Count == 0)
            {
                services.Add(new() { Id = firstId, Name = $"{protoId}", ProtoId = protoId });
            }
            return services;
        }

        /// <summary>A host, network or range object of a criterion argument ("1.2.3.4/32", "10.0.0.0/28" or "a-b").</summary>
        private static NetworkObject AddressObject(long id, string address)
        {
            if (!IpAddressSet.TryParseInterval(address, out IpInterval interval))
            {
                throw new InvalidOperationException($"Unexpected address {address}");
            }
            string start = IpAddressSet.Format(new IpInterval(interval.Family, interval.Start, interval.Start));
            string end = IpAddressSet.Format(new IpInterval(interval.Family, interval.End, interval.End));
            return new NetworkObject { Id = id, Name = address, IP = start, IpEnd = end, Type = new() { Name = interval.Start == interval.End ? ObjectType.Host : ObjectType.Network } };
        }

        private static List<string> Criteria(JsonObject label) =>
            [.. (label["dynamic_criteria"]?.AsArray() ?? []).Select(criterion => GuardicoreText(criterion?["argument"]))];

        private static JsonObject Criterion(string op, string argument) =>
            new() { ["field"] = "numeric_ip_addresses", ["op"] = op, ["argument"] = argument };

        private static string LabelName(JsonObject label) => $"{GuardicoreText(label["key"])}: {GuardicoreText(label["value"])}";

        private static string GuardicoreText(JsonNode? node) => FWO.ExternalSystems.FirewallWrite.GuardicoreLabelCriteria.Text(node);

        private string NewId(string prefix) => $"{prefix}-{(nextId++).ToString(CultureInfo.InvariantCulture)}";
    }
}
