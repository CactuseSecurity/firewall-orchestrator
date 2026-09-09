using FWO.Data;
using Newtonsoft.Json;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    internal class NotificationLogEntryTest
    {
        [TestCase("None", NotificationDeadline.None)]
        [TestCase("RecertDate", NotificationDeadline.RecertDate)]
        [TestCase("RequestDate", NotificationDeadline.RequestDate)]
        [TestCase("RuleExpiry", NotificationDeadline.RuleExpiry)]
        [TestCase("DecommissionDate", NotificationDeadline.DecommissionDate)]
        public void DeserializeDeadlineType_ReadsDatabaseEnumValue(string value, NotificationDeadline expected)
        {
            NotificationLogEntry? entry = JsonConvert.DeserializeObject<NotificationLogEntry>(
                $"{{\"deadline_type\":\"{value}\"}}");

            Assert.That(entry, Is.Not.Null);
            Assert.That(entry!.DeadlineType, Is.EqualTo(expected));
        }

        [Test]
        public void SerializeDeadlineType_WritesDatabaseEnumValue()
        {
            string json = JsonConvert.SerializeObject(new NotificationLogEntry
            {
                DeadlineType = NotificationDeadline.RequestDate
            });

            Assert.That(json, Does.Contain("\"deadline_type\":\"RequestDate\""));
        }
    }
}
