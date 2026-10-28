using FWO.Basics.Exceptions;
using FWO.Data.DeviceSpecificSettings;
using FWO.ExternalSystems.FirewallWrite;
using NUnit.Framework;
using System.Text.Json.Nodes;

namespace FWO.Test
{
    [TestFixture]
    [Parallelizable]
    internal class FirewallTemplateRendererTest
    {
        private const string kElementPayload = "<script>alert(1)</script>";
        private const string kQuotePayload = "x\",\"admin\":true,\"y\":\"";
        private const string kPlaceholderPayload = "@@RULE_UID@@";
        private static readonly List<string> kMembers = ["a", "b\"c"];
        private static readonly List<string> kMergedMembers = ["m1", "m2"];

        private static FirewallWriteStep HostStep(string name) => new FirewallWriteStep(FirewallWriteOperation.HostCreate) { TargetName = name }
            .SetText(FirewallWritePlaceholders.ObjectName, name)
            .SetText(FirewallWritePlaceholders.Ip, "10.0.0.1")
            .SetText(FirewallWritePlaceholders.MaskLength, "32")
            .SetStringList(FirewallWritePlaceholders.Members, kMembers);

        [Test]
        public void Render_ReplacesTextListAndNumberPlaceholders()
        {
            RenderedFirewallRequest request = FirewallTemplateRenderer.Render(HostStep("host-1"), "post add-host",
                """{"name":"@@OBJECT_NAME@@","ip":"@@IP@@","mask":@@MASK_LENGTH@@,"members":@@MEMBERS@@}""");

            JsonNode body = JsonNode.Parse(request.Body!)!;
            using (Assert.EnterMultipleScope())
            {
                Assert.That(request.Method, Is.EqualTo("POST"));
                Assert.That(request.Path, Is.EqualTo("add-host"));
                Assert.That(body["name"]!.GetValue<string>(), Is.EqualTo("host-1"));
                Assert.That(body["mask"]!.GetValue<int>(), Is.EqualTo(32));
                Assert.That(body["members"]!.AsArray().Select(member => member!.GetValue<string>()), Is.EqualTo(kMembers));
            }
        }

        [TestCase(kElementPayload)]
        [TestCase(kQuotePayload)]
        [TestCase(kPlaceholderPayload)]
        public void Render_KeepsTaskDataInsideItsJsonString(string? payload)
        {
            Assert.That(payload, Is.Not.Null);
            RenderedFirewallRequest request = FirewallTemplateRenderer.Render(HostStep(payload!), "POST /api/x/@@OBJECT_NAME@@",
                """{"name":"@@OBJECT_NAME@@","uid":"fixed"}""");

            JsonObject body = JsonNode.Parse(request.Body!)!.AsObject();
            using (Assert.EnterMultipleScope())
            {
                Assert.That(body.Count, Is.EqualTo(2));
                Assert.That(body["name"]!.GetValue<string>(), Is.EqualTo(payload));
                Assert.That(body["uid"]!.GetValue<string>(), Is.EqualTo("fixed"));
                Assert.That(request.Path, Is.EqualTo("/api/x/" + Uri.EscapeDataString(payload!)));
                Assert.That(request.Body, Does.Not.Contain("@@"));
            }
        }

        [Test]
        public void Render_FailsForUnresolvedPlaceholderBeforeSending()
        {
            ConfigException? exception = Assert.Throws<ConfigException>(() =>
                FirewallTemplateRenderer.Render(HostStep("h"), "POST add-host", """{"name":"@@UNKNOWN_VALUE@@"}"""));
            Assert.That(exception?.Message, Does.Contain("Unhandled placeholder found: @@UNKNOWN_VALUE@@"));
        }

        [Test]
        public void Render_FailsForUnresolvedPlaceholderInPath()
        {
            Assert.Throws<ConfigException>(() => FirewallTemplateRenderer.Render(HostStep("h"), "DELETE /x/@@GROUP_NAME@@", ""));
        }

        [Test]
        public void Render_FailsForInvalidJson()
        {
            ConfigException? exception = Assert.Throws<ConfigException>(() =>
                FirewallTemplateRenderer.Render(HostStep("h"), "POST add-host", """{"name":"@@OBJECT_NAME@@",}"""));
            Assert.That(exception?.Message, Does.Contain("no valid JSON"));
        }

        [TestCase("")]
        [TestCase("add-host")]
        [TestCase("TRACE add-host")]
        [TestCase("POST two words")]
        public void Render_FailsForInvalidEndpoint(string endpoint)
        {
            Assert.Throws<ConfigException>(() => FirewallTemplateRenderer.Render(HostStep("h"), endpoint, "{}"));
        }

        /// <summary>A placeholder that has no value for the step stops rendering with its reason, in body and path.</summary>
        [Test]
        public void Render_FailsForPlaceholderWithoutValue()
        {
            FirewallWriteStep step = HostStep("h").SetMissing(FirewallWritePlaceholders.LayerName, "no layer known");
            FirewallWriteException? inBody = Assert.Throws<FirewallWriteException>(() =>
                FirewallTemplateRenderer.Render(step, "POST add-host", """{"layer":"@@LAYER_NAME@@"}"""));
            FirewallWriteException? inPath = Assert.Throws<FirewallWriteException>(() => FirewallTemplateRenderer.Render(step, "DELETE /x/@@LAYER_NAME@@", ""));
            using (Assert.EnterMultipleScope())
            {
                Assert.That(inBody?.Message, Does.Contain("No value for @@LAYER_NAME@@ (no layer known)"));
                Assert.That(inBody?.Error.Kind, Is.EqualTo(FirewallWriteErrorKind.InvalidTaskData));
                Assert.That(inBody?.Error.Operation, Is.EqualTo(FirewallWriteOperation.HostCreate));
                Assert.That(inPath?.Message, Does.Contain("No value for @@LAYER_NAME@@"));
                Assert.DoesNotThrow(() => FirewallTemplateRenderer.Render(step, "POST add-host", """{"name":"@@OBJECT_NAME@@"}"""));
            }
        }

        [Test]
        public void Render_WithoutTemplateSendsNoBody()
        {
            RenderedFirewallRequest request = FirewallTemplateRenderer.Render(HostStep("h"), "DELETE /x/@@OBJECT_NAME@@", "  ");
            Assert.That(request.Body, Is.Null);
        }

        [Test]
        public void RuntimePlaceholders_AreKeptValidatedAndResolvedLater()
        {
            FirewallWriteStep step = HostStep("grp");
            RenderedFirewallRequest request = FirewallTemplateRenderer.Render(step, "POST /alias/setItem/@@OBJECT_UUID@@",
                """{"content":"@@OBJECT_UUID@@","members":@@MERGED_MEMBERS@@}""");
            Assert.That(FirewallTemplateRenderer.Contains(request, FirewallWritePlaceholders.MergedMembers), Is.True);
            Assert.Throws<ConfigException>(() => FirewallTemplateRenderer.EnsureResolved(request));

            FirewallTemplateRenderer.ResolveRuntimePlaceholders(request, new Dictionary<string, FirewallPlaceholderValue>
            {
                { FirewallWritePlaceholders.ObjectUuid, new("u/1", false) },
                { FirewallWritePlaceholders.MergedMembers, new(System.Text.Json.JsonSerializer.Serialize(kMergedMembers), true) }
            });

            using (Assert.EnterMultipleScope())
            {
                Assert.That(request.Path, Is.EqualTo("/alias/setItem/u%2F1"));
                Assert.That(JsonNode.Parse(request.Body!)!["members"]!.AsArray().Count, Is.EqualTo(2));
                Assert.DoesNotThrow(() => FirewallTemplateRenderer.EnsureResolved(request));
            }
        }

        [Test]
        public void ResolveRuntimePlaceholders_FailsWhenValueMissing()
        {
            RenderedFirewallRequest request = FirewallTemplateRenderer.Render(HostStep("grp"), "POST /x/@@OBJECT_UUID@@", "");
            Assert.Throws<ConfigException>(() => FirewallTemplateRenderer.ResolveRuntimePlaceholders(request, new Dictionary<string, FirewallPlaceholderValue>()));
        }

        [TestCase("post add-host", "POST", "add-host")]
        [TestCase("  DELETE   /api/v2/x  ", "DELETE", "/api/v2/x")]
        [TestCase("Patch /a?b=c", "PATCH", "/a?b=c")]
        [TestCase("GET /api/v2/cmdb/firewall/address?filter=name==a:b", "GET", "/api/v2/cmdb/firewall/address?filter=name==a:b")]
        public void Endpoint_ParsesMethodAndPath(string value, string method, string path)
        {
            FirewallWriteEndpoint endpoint = FirewallWriteEndpoint.Parse(value);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(endpoint.Method, Is.EqualTo(method));
                Assert.That(endpoint.Path, Is.EqualTo(path));
                Assert.That(endpoint.ToString(), Is.EqualTo($"{method} {path}"));
                Assert.That(FirewallWriteEndpoint.IsValidSetting(value), Is.True);
            }
        }

        [TestCase("POST")]
        [TestCase("CONNECT x")]
        [TestCase("POST a b")]
        [TestCase("POST https://other-host/add-host")]
        [TestCase("PUT http://other-host:8080/api/v2/cmdb/firewall/address")]
        [TestCase("POST HTTPS:other-host")]
        [TestCase("POST //other-host/add-host")]
        [TestCase("POST \\\\other-host\\add-host")]
        public void Endpoint_RejectsInvalidValues(string value)
        {
            Assert.Throws<ArgumentException>(() => FirewallWriteEndpoint.Parse(value));
            Assert.That(FirewallWriteEndpoint.IsValidSetting(value), Is.False);
        }

        [Test]
        public void Endpoint_EmptySettingMeansShippedDefault()
        {
            Assert.That(FirewallWriteEndpoint.IsValidSetting(" "), Is.True);
        }
    }
}
