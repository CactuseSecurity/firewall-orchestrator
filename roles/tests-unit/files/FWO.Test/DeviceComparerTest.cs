using FWO.Data;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    internal class DeviceComparerTest
    {
        private readonly Device _comparer = new();

        [Test]
        public void Equals_ReturnsTrue_ForSameReference()
        {
            Device device = new() { Name = "DeviceA", Uid = "uid-1" };

            bool result = _comparer.Equals(device, device);

            Assert.That(result, Is.True);
        }

        [Test]
        public void Equals_ReturnsFalse_WhenOneIsNull()
        {
            Device device = new() { Name = "DeviceA", Uid = "uid-1" };

            bool result = _comparer.Equals(device, null);

            Assert.That(result, Is.False);
        }

        [Test]
        public void Equals_ReturnsTrue_WhenNameAndUidMatch()
        {
            Device first = new() { Name = "DeviceA", Uid = "uid-1" };
            Device second = new() { Name = "DeviceA", Uid = "uid-1" };

            bool result = _comparer.Equals(first, second);

            Assert.That(result, Is.True);
        }

        [Test]
        public void Equals_ReturnsFalse_WhenNameOrUidDiffer()
        {
            Device first = new() { Name = "DeviceA", Uid = "uid-1" };
            Device second = new() { Name = "DeviceB", Uid = "uid-1" };

            bool result = _comparer.Equals(first, second);

            Assert.That(result, Is.False);
        }

        [Test]
        public void Equals_InstanceMethod_ReturnsFalse_WhenDeviceIsNull()
        {
            Device device = new() { Name = "DeviceA", Uid = "uid-1" };

            bool result = device.Equals(null);

            Assert.That(result, Is.False);
        }

        [Test]
        public void GetHashCode_AlignsWithEquality_WhenValuesMatch()
        {
            Device first = new() { Name = "DeviceA", Uid = "uid-1" };
            Device second = new() { Name = "DeviceA", Uid = "uid-1" };

            int firstHash = _comparer.GetHashCode(first);
            int secondHash = _comparer.GetHashCode(second);

            Assert.That(firstHash, Is.EqualTo(secondHash));
        }

        [Test]
        public void GetHashCode_TreatsNullAndEmptyAsEqual()
        {
            Device first = new() { Name = null, Uid = "" };
            Device second = new() { Name = "", Uid = null };

            int firstHash = _comparer.GetHashCode(first);
            int secondHash = _comparer.GetHashCode(second);

            Assert.That(_comparer.Equals(first, second), Is.True);
            Assert.That(firstHash, Is.EqualTo(secondHash));
        }
    }
}
