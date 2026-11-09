using FWO.ExternalSystems.CheckPoint;
using NUnit.Framework;
using RestSharp;
using System.Net;
using System.Reflection;
using System.Text.Json;

namespace FWO.Test
{
    [TestFixture]
    [Parallelizable]
    internal class CheckPointTicketHelperTest
    {
        private static readonly string[] kFindJsonValuePropertyNames = ["value", "missing"];

        [Test]
        public void JsonHelpers_FindNestedValuesAndMessages()
        {
            using JsonDocument document = JsonDocument.Parse("{\"outer\":[{\"value\":\"found\",\"message\":\"Expected failure\"}]}");

            string? value = InvokeStatic<string?>("FindJsonValue", document.RootElement, kFindJsonValuePropertyNames);
            bool messageFound = InvokeStatic<bool>("HasMatchingMessage", document.RootElement, "expected");
            bool messageMissing = InvokeStatic<bool>("HasMatchingMessage", document.RootElement, "different");

            Assert.Multiple(() =>
            {
                Assert.That(value, Is.EqualTo("found"));
                Assert.That(messageFound, Is.True);
                Assert.That(messageMissing, Is.False);
            });
        }

        [Test]
        public void ResponseAndAddressHelpers_CategorizeAndParseSupportedValues()
        {
            object warning = InvokeStatic<object>("CategorizeResponse", new RestResponse { StatusCode = HttpStatusCode.BadRequest, Content = "same IPv4 address" });
            object hardError = InvokeStatic<object>("CategorizeResponse", new RestResponse { StatusCode = HttpStatusCode.BadRequest, Content = "permission denied" });
            object success = InvokeStatic<object>("CategorizeResponse", new RestResponse { StatusCode = HttpStatusCode.Created });
            using JsonDocument document = JsonDocument.Parse("{\"text\":\"abc\",\"number\":\"24\",\"actualNumber\":25}");

            Assert.Multiple(() =>
            {
                Assert.That(warning.ToString(), Is.EqualTo("WarningCandidate"));
                Assert.That(hardError.ToString(), Is.EqualTo("HardError"));
                Assert.That(success.ToString(), Is.EqualTo("Success"));
                Assert.That(InvokeStatic<string?>("TryGetString", document.RootElement, "text"), Is.EqualTo("abc"));
                Assert.That(InvokeStatic<string?>("TryGetString", document.RootElement, "number"), Is.EqualTo("24"));
                Assert.That(InvokeStatic<int?>("TryGetInt", document.RootElement, "number"), Is.EqualTo(24));
                Assert.That(InvokeStatic<int?>("TryGetInt", document.RootElement, "actualNumber"), Is.EqualTo(25));
                Assert.That(InvokeStatic<int?>("TryGetInt", document.RootElement, "missing"), Is.Null);
                Assert.That(InvokeStatic<int>("NetMaskToPrefixLength", IPAddress.Parse("255.255.255.0")), Is.EqualTo(24));
            });
        }

        [TestCase("group_create", "add-group")]
        [TestCase("group_modify", "set-group")]
        [TestCase("group_delete", "delete-group")]
        [TestCase("host_create", "add-host")]
        [TestCase("host_modify", "set-host")]
        [TestCase("network_create", "add-network")]
        [TestCase("network_modify", "set-network")]
        [TestCase("address_range_create", "add-address-range")]
        [TestCase("address_range_modify", "set-address-range")]
        [TestCase("publish", "publish")]
        public void GetEndpoint_MapsAllCheckPointTaskTypes(string taskType, string endpoint)
        {
            Assert.That(InvokeStatic<string>("GetEndpoint", taskType), Is.EqualTo(endpoint));
        }

        [Test]
        public void TaskTypeHelpers_RecognizeRuleChangesAndSynchronousSuccesses()
        {
            Assert.Multiple(() =>
            {
                Assert.That(InvokeStatic<bool>("IsRuleChangeTaskType", "access"), Is.True);
                Assert.That(InvokeStatic<bool>("IsRuleChangeTaskType", "rule_modify"), Is.True);
                Assert.That(InvokeStatic<bool>("IsRuleChangeTaskType", "rule_delete"), Is.True);
                Assert.That(InvokeStatic<bool>("IsRuleChangeTaskType", "host_create"), Is.False);
                Assert.That(InvokeStatic<bool>("IsSynchronousSuccess", new RestResponse<int>(new()) { StatusCode = HttpStatusCode.OK }), Is.True);
                Assert.That(InvokeStatic<bool>("IsSynchronousSuccess", new RestResponse<int>(new()) { StatusCode = HttpStatusCode.Created }), Is.True);
                Assert.That(InvokeStatic<bool>("IsSynchronousSuccess", new RestResponse<int>(new()) { StatusCode = HttpStatusCode.Accepted }), Is.False);
            });
        }

        private static T InvokeStatic<T>(string methodName, params object[] parameters)
        {
            MethodInfo method = typeof(CheckPointTicket).GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException($"{methodName} not found.");
            return (T)method.Invoke(null, parameters)!;
        }
    }
}
