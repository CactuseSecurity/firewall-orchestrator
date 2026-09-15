using FWO.Ui.Services;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    internal class UiLogDataTableLayoutTest
    {
        [Test]
        public void ResolvePageSize_KeepsTheCurrentSizeWhenNothingCouldBeMeasured()
        {
            Assert.That(LogDataTableLayout.ResolvePageSize(0, 40), Is.EqualTo(40),
                "a failed measurement must not make the table jump back to the default");
        }

        [Test]
        public void ResolvePageSize_KeepsTheCurrentSizeForAnImpossibleMeasurement()
        {
            Assert.That(LogDataTableLayout.ResolvePageSize(-3, LogDataTableLayout.kDefaultPageSize),
                Is.EqualTo(LogDataTableLayout.kDefaultPageSize),
                "a negative window must not turn into a negative page size");
        }

        [Test]
        public void ResolvePageSize_FollowsTheWindowBetweenTheBounds()
        {
            Assert.That(LogDataTableLayout.ResolvePageSize(37, LogDataTableLayout.kDefaultPageSize), Is.EqualTo(37));
        }

        [Test]
        public void ResolvePageSize_RaisesAShortWindowToTheMinimum()
        {
            Assert.That(LogDataTableLayout.ResolvePageSize(LogDataTableLayout.kMinPageSize - 1, LogDataTableLayout.kDefaultPageSize),
                Is.EqualTo(LogDataTableLayout.kMinPageSize),
                "a pager below the minimum would cost more space than the rows it pages through");
        }

        [Test]
        public void ResolvePageSize_KeepsTheMinimumItself()
        {
            Assert.That(LogDataTableLayout.ResolvePageSize(LogDataTableLayout.kMinPageSize, LogDataTableLayout.kDefaultPageSize),
                Is.EqualTo(LogDataTableLayout.kMinPageSize));
        }

        [Test]
        public void ResolvePageSize_CapsAVeryTallWindowAtTheMaximum()
        {
            Assert.That(LogDataTableLayout.ResolvePageSize(LogDataTableLayout.kMaxPageSize + 1, LogDataTableLayout.kDefaultPageSize),
                Is.EqualTo(LogDataTableLayout.kMaxPageSize),
                "a page must not render the whole loaded result set in one DOM update");
        }

        [Test]
        public void ResolvePageSize_KeepsTheMaximumItself()
        {
            Assert.That(LogDataTableLayout.ResolvePageSize(LogDataTableLayout.kMaxPageSize, LogDataTableLayout.kDefaultPageSize),
                Is.EqualTo(LogDataTableLayout.kMaxPageSize));
        }
    }
}
