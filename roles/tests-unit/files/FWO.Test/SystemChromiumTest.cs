using FWO.Basics;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    [Parallelizable]
    internal class SystemChromiumTest
    {
        [Test]
        public void GetPathReturnsFirstExistingCandidate()
        {
            List<string> candidates = ["/nonexisting/chromium", "/existing/headless_shell", "/existing/chromium"];

            string? path = SystemChromium.GetPath(candidates, p => p.StartsWith("/existing"));

            Assert.That(path, Is.EqualTo("/existing/headless_shell"));
        }

        [Test]
        public void GetPathReturnsNullWhenNoCandidateExists()
        {
            Assert.That(SystemChromium.GetPath(["/nonexisting/chromium"], _ => false), Is.Null);
        }

        [Test]
        public void DefaultCandidatesPreferInstallerSymlink()
        {
            Assert.That(SystemChromium.DefaultCandidatePaths.First(), Is.EqualTo(GlobalConst.ChromiumHeadlessBinPathLinux));
        }
    }
}
