using FWO.Api.Client;
using FWO.Basics;
using FWO.Data;
using FWO.Services;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    public class EmailHelperRecipientTest
    {
        private static EmailHelper CreateEmailHelper()
        {
            SimulatedUserConfig userConfig = new()
            {
                UseDummyEmailAddress = true,
                DummyEmailAddress = "dummy@example.test"
            };
            return new EmailHelper(new SimulatedApiConnection(), null, userConfig, DefaultInit.DoNothing);
        }

        [Test]
        public async Task GetRecipientsReturnsOwnerGroupAndMainForFallbackSelection()
        {
            EmailHelper helper = CreateEmailHelper();
            FwoOwner owner = new();
            owner.AddOwnerResponsible(GlobalConst.kOwnerResponsibleTypeSupporting, "cn=supporting,dc=test");
            owner.AddOwnerResponsible(GlobalConst.kOwnerResponsibleTypeMain, "cn=main,dc=test");

            List<string> recipients = await helper.GetRecipients(
                EmailRecipientOption.FallbackToMainResponsibleIfOwnerGroupEmpty,
                null,
                owner,
                null,
                null);

            Assert.That(recipients, Has.Count.EqualTo(2));
            Assert.That(recipients, Is.All.EqualTo("dummy@example.test"));
        }

        [Test]
        public async Task GetRecipientsReturnsOtherAddressesForOtherAddressesOption()
        {
            EmailHelper helper = CreateEmailHelper();
            List<string> recipients = await helper.GetRecipients(
                EmailRecipientOption.OtherAddresses,
                null,
                null,
                null,
                ["a@test", "b@test"]);

            Assert.That(recipients, Is.EquivalentTo(new[] { "a@test", "b@test" }));
        }
    }
}
