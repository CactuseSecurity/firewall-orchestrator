using FWO.Data;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    public class FwoOwnerIsNewTest
    {
        [Test]
        public void IsNew_ReturnsTrue_ForUnsavedRegularOwner()
        {
            FwoOwner owner = new() { Id = 0, Name = "New Owner", IsDefault = false };

            Assert.That(owner.IsNew, Is.True);
        }

        [Test]
        public void IsNew_ReturnsFalse_ForSuperOwner()
        {
            // The default super-owner has Id 0 but already exists in the database.
            FwoOwner owner = new() { Id = 0, Name = "super-owner", IsDefault = true };

            Assert.That(owner.IsNew, Is.False);
        }

        [Test]
        public void IsNew_ReturnsFalse_ForExistingOwner()
        {
            FwoOwner owner = new() { Id = 7, Name = "Owner A", IsDefault = false };

            Assert.That(owner.IsNew, Is.False);
        }
    }
}
