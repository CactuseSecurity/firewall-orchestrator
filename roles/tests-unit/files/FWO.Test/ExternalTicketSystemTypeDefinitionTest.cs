using FWO.Data;
using NUnit.Framework;
using System.Text.Json;

namespace FWO.Test
{
    [TestFixture]
    [Parallelizable]
    internal class ExternalTicketSystemTypeDefinitionTest
    {
        [Test]
        public void BuiltInDefinitionsContainReservedIds()
        {
            Assert.That(BuiltInExternalTicketSystemTypes.AllBuildInTicketTypes.Select(type => type.Id),
                Is.EqualTo(new[]
                {
                    BuiltInExternalTicketSystemTypes.GenericId,
                    BuiltInExternalTicketSystemTypes.TufinSecureChangeId,
                    BuiltInExternalTicketSystemTypes.AlgoSecId,
                    BuiltInExternalTicketSystemTypes.ServiceNowId
                }));
        }

        [Test]
        public void DeserializingLegacyEnumMapsToTypeId()
        {
            const string legacyJson = """
                {
                  "Id": 1,
                  "ExternalTicketSystemType": 1
                }
                """;

            ExternalTicketSystem? system = JsonSerializer.Deserialize<ExternalTicketSystem>(legacyJson);

            Assert.That(system, Is.Not.Null);
            Assert.That(system!.TypeId, Is.EqualTo(BuiltInExternalTicketSystemTypes.TufinSecureChangeId));
        }
    }
}
