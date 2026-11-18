using FWO.Api.Client.Queries;
using FWO.Data;
using NUnit.Framework;

namespace FWO.Test
{
    /// <summary>
    /// Tests the mapping of the implementation task reference of an external request, which tells a request writing an
    /// implementation task directly to the firewall from one based on request tasks.
    /// </summary>
    [TestFixture]
    [Parallelizable]
    internal class ExternalRequestTest
    {
        private const long kImplTaskId = 4711;
        private const string kRequestWithImplTask = """{"id":1,"ticket_id":2,"impltask_id":4711}""";
        private const string kRequestWithoutImplTask = """{"id":1,"ticket_id":2}""";
        private const string kRequestWithNullImplTask = """{"id":1,"ticket_id":2,"impltask_id":null}""";
        private const string kRequestWithNullOwner = """{"id":1,"ticket_id":2,"owner":null}""";

        [Test]
        public void ImplTaskId_IsReadByBothSerializers()
        {
            ExternalRequest? newtonsoftRequest = Newtonsoft.Json.JsonConvert.DeserializeObject<ExternalRequest>(kRequestWithImplTask);
            ExternalRequest? systemTextRequest = System.Text.Json.JsonSerializer.Deserialize<ExternalRequest>(kRequestWithImplTask);

            Assert.Multiple(() =>
            {
                Assert.That(newtonsoftRequest?.ImplTaskId, Is.EqualTo(kImplTaskId));
                Assert.That(systemTextRequest?.ImplTaskId, Is.EqualTo(kImplTaskId));
            });
        }

        [Test]
        public void ImplTaskId_IsNullForARequestBasedOnRequestTasks()
        {
            ExternalRequest? missingRequest = System.Text.Json.JsonSerializer.Deserialize<ExternalRequest>(kRequestWithoutImplTask);
            ExternalRequest? nullRequest = Newtonsoft.Json.JsonConvert.DeserializeObject<ExternalRequest>(kRequestWithNullImplTask);

            Assert.Multiple(() =>
            {
                Assert.That(new ExternalRequest().ImplTaskId, Is.Null);
                Assert.That(missingRequest?.ImplTaskId, Is.Null);
                Assert.That(nullRequest?.ImplTaskId, Is.Null);
            });
        }

        [Test]
        public void Owner_IsEmptyForARequestWithoutOwner()
        {
            ExternalRequest? newtonsoftRequest = Newtonsoft.Json.JsonConvert.DeserializeObject<ExternalRequest>(kRequestWithNullOwner);
            ExternalRequest? systemTextRequest = System.Text.Json.JsonSerializer.Deserialize<ExternalRequest>(kRequestWithNullOwner);

            Assert.Multiple(() =>
            {
                Assert.That(newtonsoftRequest?.Owner, Is.Not.Null);
                Assert.That(systemTextRequest?.Owner, Is.Not.Null);
                Assert.That(systemTextRequest?.Owner.Display(""), Is.Empty);
            });
        }

        [Test]
        public void AddExtRequest_AcceptsARequestWithoutOwner()
        {
            Assert.That(ExtRequestQueries.addExtRequest, Does.Contain("$ownerId: Int").And.Not.Contain("$ownerId: Int!"));
        }

        [Test]
        public void ImplTaskId_IsWrittenUnderTheColumnName()
        {
            ExternalRequest request = new() { Id = 1, ImplTaskId = kImplTaskId };

            string json = System.Text.Json.JsonSerializer.Serialize(request);

            Assert.That(json, Does.Contain("\"impltask_id\":4711"));
        }
    }
}
