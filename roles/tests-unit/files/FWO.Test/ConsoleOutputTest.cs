using NUnit.Framework;
using FWO.Test.Helpers;

namespace FWO.Test
{
    [TestFixture]
    [NonParallelizable]
    internal class ConsoleOutputTest
    {
        /// <summary>
        /// Short enough to keep the suite fast, long enough that a genuinely blocked capture
        /// cannot slip through by finishing early.
        /// </summary>
        private static readonly TimeSpan kShortCaptureTimeout = TimeSpan.FromMilliseconds(500);

        /// <summary>
        /// Keeps the first capture busy while the second one asks for the lock.
        /// </summary>
        private static readonly TimeSpan kInterleaveDelay = TimeSpan.FromMilliseconds(200);

        /// <summary>
        /// Enough writes to keep a background thread busy across many snapshots, few enough to
        /// keep every snapshot cheap.
        /// </summary>
        private static readonly int kBackgroundWrites = 5000;

        [Test]
        public async Task CaptureAsyncReturnsWrittenOutput()
        {
            string output = await ConsoleOutput.CaptureAsync(() =>
            {
                Console.Write("captured output");
                return Task.CompletedTask;
            });

            Assert.That(output, Is.EqualTo("captured output"));
        }

        [Test]
        public void CaptureAsyncRestoresOutputWhenActionFails()
        {
            TextWriter originalOutput = Console.Out;

            Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await ConsoleOutput.CaptureAsync(() => throw new InvalidOperationException("failure")));

            Assert.That(Console.Out, Is.SameAs(originalOutput));
        }

        /// <summary>
        /// The capture lock is the only reason this helper exists rather than a plain
        /// try/finally, so the isolation it buys is asserted rather than assumed.
        /// </summary>
        [Test]
        public async Task CaptureAsyncKeepsConcurrentCapturesApart()
        {
            Task<string> firstCapture = ConsoleOutput.CaptureAsync(async () =>
            {
                Console.Write("first-start");
                await Task.Delay(kInterleaveDelay);
                Console.Write("|first-end");
            });

            Task<string> secondCapture = ConsoleOutput.CaptureAsync(() =>
            {
                Console.Write("second");
                return Task.CompletedTask;
            });

            string firstOutput = await firstCapture;
            string secondOutput = await secondCapture;

            Assert.Multiple(() =>
            {
                Assert.That(firstOutput, Is.EqualTo("first-start|first-end"));
                Assert.That(secondOutput, Is.EqualTo("second"));
            });
        }

        /// <summary>
        /// A leaked lock would not fail here, it would hang the next capture and be blamed on
        /// an unrelated test, so the release path gets its own assertion.
        /// </summary>
        [Test]
        public async Task CaptureAsyncReleasesTheLockAfterAFailedAction()
        {
            Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await ConsoleOutput.CaptureAsync(() => throw new InvalidOperationException("failure")));

            string output = await ConsoleOutput.CaptureAsync(_ =>
            {
                Console.Write("after failure");
                return Task.CompletedTask;
            }, kShortCaptureTimeout);

            Assert.That(output, Is.EqualTo("after failure"));
        }

        [Test]
        public void CaptureAsyncReportsANestedCaptureInsteadOfDeadlocking()
        {
            InvalidOperationException? exception = Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await ConsoleOutput.CaptureAsync(async () =>
                    await ConsoleOutput.CaptureAsync(_ => Task.CompletedTask, kShortCaptureTimeout)));

            Assert.That(exception?.Message, Does.Contain("nested"));
        }

        [Test]
        public async Task SnapshotReturnsOnlyTheOutputWrittenSoFar()
        {
            string intermediate = string.Empty;

            string output = await ConsoleOutput.CaptureAsync(capture =>
            {
                Console.Write("early");
                intermediate = capture.Snapshot();
                Console.Write("-late");
                return Task.CompletedTask;
            });

            Assert.Multiple(() =>
            {
                Assert.That(intermediate, Is.EqualTo("early"));
                Assert.That(output, Is.EqualTo("early-late"));
            });
        }

        /// <summary>
        /// A background thread holding a stale Console.Out must not be able to corrupt a
        /// snapshot, which is what an unsynchronized StringBuilder read allowed.
        /// </summary>
        [Test]
        public async Task CaptureAsyncToleratesAConcurrentBackgroundWriter()
        {
            bool snapshotCorrupted = false;

            string output = await ConsoleOutput.CaptureAsync(async capture =>
            {
                TextWriter capturedConsole = Console.Out;
                Task backgroundWriter = Task.Run(() =>
                {
                    for (int writeNumber = 0; writeNumber < kBackgroundWrites; writeNumber++)
                    {
                        capturedConsole.Write('x');
                    }
                });

                while (!backgroundWriter.IsCompleted)
                {
                    // reading the buffer while it is being written was the actual race: it threw
                    // ArgumentOutOfRangeException or returned a torn string
                    if (!capture.Snapshot().All(character => character == 'x'))
                    {
                        snapshotCorrupted = true;
                        break;
                    }
                }

                await backgroundWriter;
            });

            Assert.Multiple(() =>
            {
                Assert.That(snapshotCorrupted, Is.False);
                Assert.That(output, Has.Length.EqualTo(kBackgroundWrites));
            });
        }
    }
}
