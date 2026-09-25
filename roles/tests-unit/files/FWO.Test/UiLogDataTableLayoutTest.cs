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
            Assert.That(LogDataTableLayout.ResolvePageSize(LogDataTableLayout.kCouldNotMeasure, 40), Is.EqualTo(40),
                "a measurement that could not be taken must not make the table jump back to the default");
        }

        [Test]
        public void ResolvePageSize_RaisesAWindowTooShortForOneRowToTheMinimum()
        {
            Assert.That(LogDataTableLayout.ResolvePageSize(0, 40), Is.EqualTo(LogDataTableLayout.kMinPageSize),
                "zero rows fitting is a measurement, not a failure, so the floor applies to it");
        }

        [Test]
        public void ResolvePageSize_TreatsAnyNegativeAnswerAsNotMeasured()
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
        public void MayApplyPageSize_AllowsTheFirstPage()
        {
            Assert.That(LogDataTableLayout.MayApplyPageSize(LogDataTableLayout.kFirstPageNumber), Is.True,
                "the first page has nothing above it that a changed page size could push out of view");
        }

        [Test]
        public void MayApplyPageSize_AllowsATableWithoutPagingStateYet()
        {
            Assert.That(LogDataTableLayout.MayApplyPageSize(null), Is.True,
                "a table which has not paged yet shows its first rows, so nothing can be moved");
        }

        [Test]
        public void MayApplyPageSize_RefusesAnyLaterPage()
        {
            Assert.Multiple(() =>
            {
                Assert.That(LogDataTableLayout.MayApplyPageSize(LogDataTableLayout.kFirstPageNumber + 1), Is.False);
                Assert.That(LogDataTableLayout.MayApplyPageSize(7), Is.False,
                    "page 2 of 10 rows starts at row 20, page 2 of 20 rows at row 40 - the user would be moved");
            });
        }

        [Test]
        public void ResolvePageSize_KeepsTheMaximumItself()
        {
            Assert.That(LogDataTableLayout.ResolvePageSize(LogDataTableLayout.kMaxPageSize, LogDataTableLayout.kDefaultPageSize),
                Is.EqualTo(LogDataTableLayout.kMaxPageSize));
        }
    }
}
