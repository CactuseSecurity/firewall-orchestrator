using FWO.Basics;
using FWO.Ui.Services;
using NUnit.Framework;

namespace FWO.Test
{
    /// <summary>
    /// Tests the client address of a UI session.
    /// </summary>
    [TestFixture]
    [Parallelizable]
    internal class UiClientAddressTest
    {
        private const string kClientAddress = "192.0.2.10";

        [Test]
        public void NewInstance_HasNoAddress()
        {
            UiClientAddress clientAddress = new();

            Assert.Multiple(() =>
            {
                Assert.That(clientAddress.Value, Is.Empty);
                Assert.That(clientAddress.LoginOption, Is.Null);
                Assert.That(clientAddress.Description, Is.EqualTo(ClientAddressFormat.kUnknown));
            });
        }

        [Test]
        public void Set_StoresACanonicalAddress()
        {
            UiClientAddress clientAddress = new();

            clientAddress.Set(kClientAddress);

            Assert.Multiple(() =>
            {
                Assert.That(clientAddress.Value, Is.EqualTo(kClientAddress));
                Assert.That(clientAddress.LoginOption, Is.EqualTo(kClientAddress));
                Assert.That(clientAddress.Description, Is.EqualTo(kClientAddress));
            });
        }

        [Test]
        public void Set_StoresAnIpv4MappedAddressAsIpv4()
        {
            UiClientAddress clientAddress = new();

            clientAddress.Set($"::ffff:{kClientAddress}");

            Assert.That(clientAddress.Value, Is.EqualTo(kClientAddress));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("192.0.2.10, 192.0.2.11")]
        [TestCase("<script>alert(1)</script>")]
        [TestCase("192.0.2.10\" onmouseover=\"alert(1)")]
        public void Set_TakesAnythingElseAsUnknown(string? address)
        {
            UiClientAddress clientAddress = new();
            clientAddress.Set(kClientAddress);

            clientAddress.Set(address);

            Assert.Multiple(() =>
            {
                Assert.That(clientAddress.Value, Is.Empty);
                Assert.That(clientAddress.LoginOption, Is.Null);
                Assert.That(clientAddress.Description, Is.EqualTo(ClientAddressFormat.kUnknown));
            });
        }

        [Test]
        public void Set_RaisesChanged()
        {
            UiClientAddress clientAddress = new();
            int changes = 0;
            clientAddress.Changed += () => changes++;

            clientAddress.Set(kClientAddress);

            Assert.That(changes, Is.EqualTo(1));
        }
    }
}
