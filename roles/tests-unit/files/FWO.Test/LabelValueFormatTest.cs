using FWO.Data.Labelling;
using NUnit.Framework;

namespace FWO.Test
{
    /// <summary>
    /// Format of the values of App, AppRole, AppZone and NetworkArea labels: valid formats, the values they produce and the
    /// id read back from a value of any format.
    /// </summary>
    [TestFixture]
    internal class LabelValueFormatTest
    {
        private const string kNameAndId = "{name} ({id})";

        [TestCase("{id}")]
        [TestCase(kNameAndId)]
        [TestCase(" {id} - {name} ")]
        [TestCase("[{id}] {name}")]
        [TestCase("{id}: {name}")]
        [TestCase("{name} {id}")]
        public void ValidFormats_AreAccepted(string format)
        {
            Assert.That(LabelValueFormat.IsValid(format), Is.True);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("{name}")]
        [TestCase("{id} {id}")]
        [TestCase("{name} {name} {id}")]
        [TestCase("{id} {other}")]
        [TestCase("{id} }")]
        [TestCase("{name}-{id}")]
        [TestCase("{id}-{name}")]
        [TestCase("{name}{id}")]
        [TestCase("{id}{name}")]
        [TestCase("AR{id}")]
        [TestCase("{id}_x {name}")]
        [TestCase("{name}.{id}")]
        public void InvalidFormats_AreRejectedAndFallBackToTheDefault(string? format)
        {
            Assert.Multiple(() =>
            {
                Assert.That(LabelValueFormat.IsValid(format), Is.False);
                Assert.That(new LabelValueFormat(format).Format, Is.EqualTo(LabelValueFormat.kDefault));
            });
        }

        [Test]
        public void TooLongFormats_AreRejected()
        {
            string format = "{id} " + new string('x', LabelValueFormat.kMaxFormatLength);
            Assert.That(LabelValueFormat.IsValid(format), Is.False);
        }

        [Test]
        public void Values_FollowTheFormat()
        {
            LabelValueFormat nameAndId = new(kNameAndId);
            Assert.Multiple(() =>
            {
                Assert.That(new LabelValueFormat(null).Value("FWO-Produktion", " AR5003080-005 "), Is.EqualTo("AR5003080-005"));
                Assert.That(nameAndId.Value(" FWO-Produktion ", "AR5003080-005"), Is.EqualTo("FWO-Produktion (AR5003080-005)"));
                Assert.That(nameAndId.Value(" ", "AR5003080-005"), Is.EqualTo("AR5003080-005"), "without name the id alone");
                Assert.That(nameAndId.Value(null, "AR5003080-005"), Is.EqualTo("AR5003080-005"));
                Assert.That(new LabelValueFormat("{id}: {name}").Value("Billing", "APP-1"), Is.EqualTo("APP-1: Billing"));
            });
        }

        [Test]
        public void LongNames_AreShortenedToTheMaximumValueLength()
        {
            string value = new LabelValueFormat(kNameAndId).Value(new string('n', 400), "AR1");
            Assert.Multiple(() =>
            {
                Assert.That(value, Has.Length.EqualTo(LabelValueFormat.kMaxValueLength));
                Assert.That(value, Does.EndWith(" (AR1)"));
            });
        }

        [Test]
        public void IdCandidates_AreTheIdByFormatElseTheWholeValueAndTheLastParentheses()
        {
            LabelValueFormat nameAndId = new(kNameAndId);
            LabelValueFormat idFirst = new("{id} - {name}");
            Assert.Multiple(() =>
            {
                Assert.That(nameAndId.IdCandidates("FWO (Prod) (AR1)"), Is.EqualTo(new List<string> { "AR1" }));
                Assert.That(nameAndId.IdCandidates("AR1"), Is.EqualTo(new List<string> { "AR1" }), "a value of another format");
                Assert.That(idFirst.IdCandidates("AR1 - Billing (old)"), Is.EqualTo(new List<string> { "AR1" }));
                Assert.That(new LabelValueFormat("[{id}] {name}").IdCandidates("[AR1] Billing"), Is.EqualTo(new List<string> { "AR1" }));
                Assert.That(new LabelValueFormat(LabelValueFormat.kDefault).IdCandidates("Proxy (AR1)"), Is.EqualTo(new List<string> { "Proxy (AR1)", "AR1" }),
                    "the default format matches every value, so the last parentheses count as well");
                Assert.That(idFirst.IdCandidates("Web (AR1)"), Is.EqualTo(new List<string> { "Web (AR1)", "AR1" }), "a value of another format");
                Assert.That(nameAndId.IdCandidates(" "), Is.Empty);
                Assert.That(nameAndId.IdCandidates(null), Is.Empty);
            });
        }

        /// <summary>
        /// A value of the format stands for its id only: the name of app role AR2 may end with the id of another group, or
        /// two ids may share a part, without the value standing for that other group.
        /// </summary>
        [Test]
        public void ValuesOfTheFormat_StandForTheirIdOnly()
        {
            LabelValueFormat idFirst = new("{id} {name}");
            LabelValueFormat nameAndId = new(kNameAndId);
            Assert.Multiple(() =>
            {
                Assert.That(idFirst.Matches("AR2 Web (AR1)", "AR2"), Is.True);
                Assert.That(idFirst.Matches("AR2 Web (AR1)", "AR1"), Is.False);
                Assert.That(nameAndId.Matches("Copy of (AR1) (AR2)", "AR1"), Is.False);
                Assert.That(nameAndId.Matches("Prod (AR5003080-005)", "005"), Is.False);
            });
        }

        [Test]
        public void Matches_FindsTheIdInValuesOfEveryFormat()
        {
            LabelValueFormat bareId = new(LabelValueFormat.kDefault);
            Assert.Multiple(() =>
            {
                Assert.That(bareId.Matches("AR5011629-032", "AR5011629-032"), Is.True);
                Assert.That(bareId.Matches("PostgreSQL (COM-1629) - Proxy (AR5011629-032)", " AR5011629-032 "), Is.True, "a label of others");
                Assert.That(new LabelValueFormat(kNameAndId).Matches("AR5011629-032", "AR5011629-032"), Is.True, "a label of the former default");
                Assert.That(bareId.Matches("Proxy (AR5011629-032)", "COM-1629"), Is.False);
                Assert.That(bareId.Matches("AR1", " "), Is.False);
            });
        }

        [Test]
        public void NameChanges_AlterTheValueOnlyIfTheFormatHasTheName()
        {
            Assert.Multiple(() =>
            {
                Assert.That(new LabelValueFormat(kNameAndId).NameChangeAltersValue("Billing", "Invoicing"), Is.True);
                Assert.That(new LabelValueFormat(kNameAndId).NameChangeAltersValue("Billing", " Billing "), Is.False);
                Assert.That(new LabelValueFormat(null).NameChangeAltersValue("Billing", "Invoicing"), Is.False);
                Assert.That(new LabelValueFormat(kNameAndId).UsesName, Is.True);
            });
        }
    }
}
