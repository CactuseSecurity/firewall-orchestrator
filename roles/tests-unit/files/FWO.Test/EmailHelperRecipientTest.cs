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

        [Test]
        public async Task GetRecipientsReturnsJsonOtherAddressList()
        {
            EmailHelper helper = CreateEmailHelper();
            EmailRecipientSelection selection = new()
            {
                None = false,
                OtherAddresses = true,
                OtherAddressList = ["json-a@test", "json-b@test"]
            };

            List<string> recipients = await helper.GetRecipients(selection, null, ["legacy@test"]);

            Assert.That(recipients, Is.EquivalentTo(new[] { "json-a@test", "json-b@test", "legacy@test" }));
        }

        [Test]
        public async Task GetRecipientsReturnsJsonOtherAddressListFromConfigStringWithoutLegacyAddresses()
        {
            EmailHelper helper = CreateEmailHelper();
            EmailRecipientSelection selection = new()
            {
                None = false,
                OtherAddresses = true,
                OtherAddressList = ["json-a@test", "json-b@test"]
            };

            List<string> recipients = await helper.GetRecipients(selection.ToConfigValue(), null, []);

            Assert.That(recipients, Is.EquivalentTo(new[] { "json-a@test", "json-b@test" }));
        }

        [Test]
        public async Task GetRecipientsReturnsConfiguredResponsibleTypes()
        {
            EmailHelper helper = CreateEmailHelper();
            FwoOwner owner = new();
            owner.AddOwnerResponsible(3, "cn=escalation,dc=test");
            owner.AddOwnerResponsible(GlobalConst.kOwnerResponsibleTypeMain, "cn=main,dc=test");
            EmailRecipientSelection selection = new()
            {
                None = false,
                OwnerResponsibleTypeIds = [3]
            };

            List<string> recipients = await helper.GetRecipients(selection, owner, null);

            Assert.That(recipients, Has.Count.EqualTo(1));
            Assert.That(recipients[0], Is.EqualTo("dummy@example.test"));
        }
    }
}
