using FWO.Data.Flow;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    internal class FlowHashGeneratorTest
    {
        private const int kCentralEuropeanOffsetHours = 1;
        private static readonly DateTime kUtcEndTime = new(2026, 1, 15, 14, 0, 0, DateTimeKind.Utc);

        [Test]
        public void GenerateTimeObjectHash_UsesSameHashForEquivalentTimeZones()
        {
            DateTime localEndTime = new DateTimeOffset(2026, 1, 15, 15, 0, 0, TimeSpan.FromHours(kCentralEuropeanOffsetHours)).LocalDateTime;

            string localHash = FlowHashGenerator.GenerateTimeObjectHash(null, localEndTime);
            string utcHash = FlowHashGenerator.GenerateTimeObjectHash(null, kUtcEndTime);

            Assert.That(localEndTime.ToUniversalTime(), Is.EqualTo(kUtcEndTime));
            Assert.That(localHash, Is.EqualTo(utcHash));
        }
    }
}
