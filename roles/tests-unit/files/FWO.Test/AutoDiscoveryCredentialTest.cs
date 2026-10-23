using FWO.Data;
using FWO.DeviceAutoDiscovery;
using NUnit.Framework;
using System.Text.Json;

namespace FWO.Test
{
    /// <summary>
    /// The action data of a discovered management is stored in an alert and written to the log, so it must not carry
    /// the decrypted secret of the super management's import credential.
    /// </summary>
    [TestFixture]
    internal class AutoDiscoveryCredentialTest
    {
        private const int kCredentialId = 7;
        private const string kCredentialName = "mds-import";
        private const string kPasswordMarker = "decrypted-password-marker";
        private const string kPrivateKeyMarker = "-----BEGIN OPENSSH PRIVATE KEY-----\nprivate-key-marker\n-----END OPENSSH PRIVATE KEY-----";

        [TestCase(false, kPasswordMarker)]
        [TestCase(true, kPrivateKeyMarker)]
        public void ConvertToActions_NewManagement_CarriesCredentialIdButNoSecret(bool isKeyPair, string decryptedSecret)
        {
            Management superManagement = new()
            {
                Id = 1,
                Name = "mds",
                Hostname = "mds.example",
                DeviceType = new DeviceType { Id = 13, Name = "CheckPoint" },
                ImportCredential = new Credential { Id = kCredentialId, Name = kCredentialName, IsKeyPair = isKeyPair, ImportUser = "api" }
            };
            AutoDiscoveryBase discovery = new(superManagement, new SimulatedApiConnection(), checkCertificates: true);
            // the state after the constructor decrypted the secret
            discovery.SuperManagement.ImportCredential.Secret = decryptedSecret;
            List<Management> diffList = [new Management { Name = "domain-a", Uid = "domain-a-uid", DeviceType = new DeviceType { Id = 9 } }];

            List<ActionItem> actions = discovery.ConvertToActions(diffList);

            Assert.That(actions, Has.Count.EqualTo(1));
            string jsonData = actions[0].JsonData?.ToString() ?? "";
            Management alertManagement = JsonSerializer.Deserialize<Management>(jsonData)!;
            Assert.Multiple(() =>
            {
                Assert.That(actions[0].ActionType, Is.EqualTo(ActionCode.AddManagement.ToString()));
                Assert.That(jsonData, Does.Not.Contain("marker"));
                Assert.That(alertManagement.ImportCredential.Id, Is.EqualTo(kCredentialId), "the dialog creates the management with this id");
                Assert.That(alertManagement.ImportCredential.Name, Is.EqualTo(kCredentialName));
                Assert.That(alertManagement.ImportCredential.Secret, Is.Empty);
                Assert.That(alertManagement.Name, Is.EqualTo("domain-a"));
                Assert.That(discovery.SuperManagement.ImportCredential.Secret, Is.EqualTo(decryptedSecret),
                    "the running discovery still needs the secret to log in");
            });
        }
    }
}
