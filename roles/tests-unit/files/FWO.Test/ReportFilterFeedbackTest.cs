using FWO.Report.Filter.Exceptions;
using FWO.Ui.Services;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    [NonParallelizable]
    internal class ReportFilterFeedbackTest
    {
        private const string kInput = "src=1.2.3.4 and dst=foo";
        private const string kInternalDetail = "secret internal detail";

        [Test]
        public void ValidFilterIsShownWithoutErrorMarker()
        {
            ReportFilterFeedback feedback = ReportFilterFeedback.Evaluate(kInput, _ => { });

            Assert.Multiple(() =>
            {
                Assert.That(feedback.Start, Is.EqualTo(kInput));
                Assert.That(feedback.Error, Is.Empty);
                Assert.That(feedback.End, Is.Empty);
                Assert.That(feedback.UnexpectedError, Is.Null);
            });
        }

        [Test]
        public void FilterErrorIsMarkedAtItsPosition()
        {
            ReportFilterFeedback feedback = ReportFilterFeedback.Evaluate(kInput,
                _ => throw new FilterException("unknown value", new Range(20, 23)));

            Assert.Multiple(() =>
            {
                Assert.That(feedback.Start, Is.EqualTo("src=1.2.3.4 and dst="));
                Assert.That(feedback.Error, Is.EqualTo("foo"));
                Assert.That(feedback.End, Is.Empty);
                Assert.That(feedback.UnexpectedError, Is.Null);
            });
        }

        /// <summary>
        /// SEC-29: an unexpected failure is logged in every build configuration and the user only sees the plain
        /// input, without internals of the failure.
        /// </summary>
        [Test]
        public void UnexpectedErrorIsLoggedAndNotShownAsFeedback()
        {
            InvalidOperationException failure = new(kInternalDetail);

            (ReportFilterFeedback feedback, string log) = EvaluateCapturingLog(_ => throw failure);

            Assert.Multiple(() =>
            {
                Assert.That(feedback.Start, Is.EqualTo(kInput));
                Assert.That(feedback.Error, Is.Empty);
                Assert.That(feedback.End, Is.Empty);
                Assert.That(feedback.UnexpectedError, Is.SameAs(failure));
                Assert.That(feedback.Start + feedback.Error + feedback.End, Does.Not.Contain(kInternalDetail));
                Assert.That(log, Does.Contain("Report filter"));
                Assert.That(log, Does.Contain(kInternalDetail));
            });
        }

        [Test]
        public void FilterErrorOutsideTheInputIsTreatedAsUnexpected()
        {
            (ReportFilterFeedback feedback, string log) = EvaluateCapturingLog(
                _ => throw new FilterException("bad position", new Range(30, 40)));

            Assert.Multiple(() =>
            {
                Assert.That(feedback.Start, Is.EqualTo(kInput));
                Assert.That(feedback.Error, Is.Empty);
                Assert.That(feedback.UnexpectedError, Is.InstanceOf<ArgumentOutOfRangeException>());
                Assert.That(log, Does.Contain("Report filter"));
            });
        }

        private static (ReportFilterFeedback Feedback, string Log) EvaluateCapturingLog(Action<string> validate)
        {
            TextWriter originalOut = Console.Out;
            using StringWriter capturedOut = new();
            Console.SetOut(capturedOut);
            try
            {
                ReportFilterFeedback feedback = ReportFilterFeedback.Evaluate(kInput, validate);
                return (feedback, capturedOut.ToString());
            }
            finally
            {
                Console.SetOut(originalOut);
            }
        }
    }
}
