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
            Assert.That(BuiltInExternalTicketSystemTypes.AllBuiltInTicketTypes.Select(type => type.Id),
                Is.EqualTo(new[]
                {
                    BuiltInExternalTicketSystemTypes.GenericId,
                    BuiltInExternalTicketSystemTypes.TufinSecureChangeId,
                    BuiltInExternalTicketSystemTypes.AlgoSecId,
                    BuiltInExternalTicketSystemTypes.ServiceNowId
                }));
        }      
    }
}
