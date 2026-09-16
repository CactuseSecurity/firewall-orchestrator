using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    [NonParallelizable]
    internal class ConsoleOutputTest
    {
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
    }
}
