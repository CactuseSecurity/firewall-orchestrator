using FWO.Middleware.Server;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    public class AuthLdapSelectionTest
    {
        [Test]
        public void GetPreferredLdapIndexReturnsFirstSuccessfulIndex()
        {
            int selected = AuthLdapSelection.GetPreferredLdapIndex([false, true, true]);

            Assert.That(selected, Is.EqualTo(1));
        }

        [Test]
        public void GetPreferredLdapIndexReturnsZeroWhenFirstSucceeds()
        {
            int selected = AuthLdapSelection.GetPreferredLdapIndex([true, false, true]);

            Assert.That(selected, Is.EqualTo(0));
        }

        [Test]
        public void GetPreferredLdapIndexReturnsMinusOneWhenNoneSucceeds()
        {
            int selected = AuthLdapSelection.GetPreferredLdapIndex([false, false, false]);

            Assert.That(selected, Is.EqualTo(-1));
        }

        [Test]
        public void GetPreferredLdapIndexReturnsMinusOneForNullOrEmpty()
        {
            int selectedForNull = AuthLdapSelection.GetPreferredLdapIndex(null);
            int selectedForEmpty = AuthLdapSelection.GetPreferredLdapIndex([]);

            Assert.That(selectedForNull, Is.EqualTo(-1));
            Assert.That(selectedForEmpty, Is.EqualTo(-1));
        }
    }
}
