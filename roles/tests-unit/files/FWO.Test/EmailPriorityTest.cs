using FWO.Basics.Enums;
using FWO.Data;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    [Parallelizable]
    internal class EmailPriorityTest
    {
        private static readonly List<EmailPriority> kExpectedOrder = [EmailPriority.Low, EmailPriority.Normal, EmailPriority.High];

        [Test]
        public void OfferedOptions_ListsAllPrioritiesFromLowToHigh()
        {
            List<EmailPriority> options = EmailPriorityExtensions.OfferedOptions();

            Assert.That(options, Is.EqualTo(kExpectedOrder));
        }

        [Test]
        public void OfferedOptions_ReturnsIndependentCopy()
        {
            List<EmailPriority> options = EmailPriorityExtensions.OfferedOptions();
            options.Clear();

            Assert.That(EmailPriorityExtensions.OfferedOptions(), Is.EqualTo(kExpectedOrder));
        }

        [TestCase(EmailPriority.Low, "email_priority_low")]
        [TestCase(EmailPriority.Normal, "email_priority_normal")]
        [TestCase(EmailPriority.High, "email_priority_high")]
        public void TextKey_ReturnsLowerCaseKey(EmailPriority priority, string expectedKey)
        {
            Assert.That(priority.TextKey(), Is.EqualTo(expectedKey));
        }

        [Test]
        public void FwoNotification_DefaultsToNormalAndCopiesPriority()
        {
            FwoNotification notification = new() { EmailPriority = EmailPriority.High };

            FwoNotification copy = new(notification);

            Assert.Multiple(() =>
            {
                Assert.That(new FwoNotification().EmailPriority, Is.EqualTo(EmailPriority.Normal));
                Assert.That(copy.EmailPriority, Is.EqualTo(EmailPriority.High));
            });
        }

        [Test]
        public void FwoNotification_DeserializesPriorityFromApiString()
        {
            const string kJson = "{\"id\": 1, \"email_priority\": \"High\"}";

            // the GraphQL client deserializes with Newtonsoft default settings
            FwoNotification? notification = Newtonsoft.Json.JsonConvert.DeserializeObject<FwoNotification>(kJson);

            Assert.That(notification?.EmailPriority, Is.EqualTo(EmailPriority.High));
        }
    }
}
