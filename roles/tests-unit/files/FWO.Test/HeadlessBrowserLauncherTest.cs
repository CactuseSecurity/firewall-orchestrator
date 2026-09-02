using FWO.Basics.Exceptions;
using FWO.Services.HeadlessBrowser;
using NSubstitute;
using NUnit.Framework;
using PuppeteerSharp;
using PuppeteerSharp.BrowserData;

namespace FWO.Test
{
    [TestFixture]
    internal class HeadlessBrowserLauncherTest
    {
        private const string kChromePath = "/usr/local/fworch/bin/Chrome/Linux-152.0.7977.64/chrome-linux64/chrome";
        private const string kChromiumPath = "/usr/local/fworch/bin/chromium-headless";
        private const string kNoSandboxArg = "--no-sandbox";
        private const string kNoGpuArg = "--disable-gpu";
        private const int kLaunchTimeoutMs = 60_000;
        private const int kProtocolTimeoutMs = 180_000;

        [SetUp]
        public void ResetLauncher()
        {
            HeadlessBrowserLauncher.ResetSandboxState();
        }

        [TearDown]
        public void CleanUpLauncher()
        {
            HeadlessBrowserLauncher.ResetSandboxState();
        }

        [Test]
        public void BuildLaunchAttemptsStartsSandboxed()
        {
            List<BrowserLaunchAttempt> attempts = HeadlessBrowserLauncher.BuildLaunchAttempts(kChromePath, kChromiumPath, 0);

            Assert.That(attempts, Has.Count.EqualTo(4));
            Assert.Multiple(() =>
            {
                Assert.That(attempts[0].ExecutablePath, Is.EqualTo(kChromePath));
                Assert.That(attempts[0].SandboxDisabled, Is.False);
                Assert.That(attempts[0].Args, Is.Empty);
                Assert.That(attempts[1].ExecutablePath, Is.EqualTo(kChromePath));
                Assert.That(attempts[1].SandboxDisabled, Is.True);
                Assert.That(attempts[1].Args, Contains.Item(kNoSandboxArg));
                Assert.That(attempts[2].Args, Contains.Item(kNoGpuArg));
                Assert.That(attempts[3].ExecutablePath, Is.EqualTo(kChromiumPath));
                Assert.That(attempts[3].SandboxDisabled, Is.True);
            });
        }

        [Test]
        public void BuildLaunchAttemptsDumpsOutputOfLastAttemptOnly()
        {
            List<BrowserLaunchAttempt> attempts = HeadlessBrowserLauncher.BuildLaunchAttempts(kChromePath, kChromiumPath, 0);

            Assert.Multiple(() =>
            {
                Assert.That(attempts.Take(attempts.Count - 1).Select(attempt => attempt.DumpIo), Is.All.False);
                Assert.That(attempts[^1].DumpIo, Is.True);
            });
        }

        [Test]
        public void BuildLaunchAttemptsSkipsAttemptsKnownToFail()
        {
            List<BrowserLaunchAttempt> attempts = HeadlessBrowserLauncher.BuildLaunchAttempts(kChromePath, null, 1);

            Assert.That(attempts, Has.Count.EqualTo(2));
            Assert.Multiple(() =>
            {
                Assert.That(attempts[0].ExecutablePath, Is.EqualTo(kChromePath));
                Assert.That(attempts[0].SandboxDisabled, Is.True);
                Assert.That(attempts[0].Args, Contains.Item(kNoSandboxArg));
            });
        }

        [TestCase(-1, 3)]
        [TestCase(0, 3)]
        [TestCase(2, 1)]
        [TestCase(7, 1)]
        public void BuildLaunchAttemptsClampsFirstAttempt(int firstAttempt, int expectedCount)
        {
            List<BrowserLaunchAttempt> attempts = HeadlessBrowserLauncher.BuildLaunchAttempts(kChromePath, null, firstAttempt);

            Assert.That(attempts, Has.Count.EqualTo(expectedCount));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        [TestCase(kChromePath)]
        public void BuildLaunchAttemptsOmitsUnusableSystemChromium(string? systemChromiumPath)
        {
            List<BrowserLaunchAttempt> attempts = HeadlessBrowserLauncher.BuildLaunchAttempts(kChromePath, systemChromiumPath, 0);

            Assert.That(attempts, Has.Count.EqualTo(3));
            Assert.That(attempts.Select(attempt => attempt.ExecutablePath), Is.All.EqualTo(kChromePath));
        }

        [Test]
        public void BuildLaunchOptionsMapsAttempt()
        {
            BrowserLaunchAttempt attempt = HeadlessBrowserLauncher.BuildLaunchAttempts(kChromePath, null, 1)[0];

            LaunchOptions options = HeadlessBrowserLauncher.BuildLaunchOptions(attempt, kLaunchTimeoutMs, kProtocolTimeoutMs);

            Assert.Multiple(() =>
            {
                Assert.That(options.ExecutablePath, Is.EqualTo(kChromePath));
                Assert.That(options.Headless, Is.True);
                Assert.That(options.Timeout, Is.EqualTo(kLaunchTimeoutMs));
                Assert.That(options.ProtocolTimeout, Is.EqualTo(kProtocolTimeoutMs));
                Assert.That(options.Args, Contains.Item(kNoSandboxArg));
            });
        }

        [TestCase(PlatformID.Unix, Platform.Linux)]
        [TestCase(PlatformID.Win32NT, Platform.Win32)]
        [TestCase(PlatformID.Other, Platform.Unknown)]
        public void ResolvePlatformMapsOperatingSystem(PlatformID platformId, Platform expected)
        {
            Assert.That(HeadlessBrowserLauncher.ResolvePlatform(new OperatingSystem(platformId, new Version(1, 0))), Is.EqualTo(expected));
        }

        [Test]
        public void PreferAccessibleBrowserKeepsAccessibleBrowser()
        {
            string selected = HeadlessBrowserLauncher.PreferAccessibleBrowser(kChromePath, kChromiumPath, _ => true);

            Assert.That(selected, Is.EqualTo(kChromePath));
        }

        [Test]
        public void PreferAccessibleBrowserFallsBackToSystemChromium()
        {
            string selected = HeadlessBrowserLauncher.PreferAccessibleBrowser(kChromePath, kChromiumPath, path => path == kChromiumPath);

            Assert.That(selected, Is.EqualTo(kChromiumPath));
        }

        [TestCase(null)]
        [TestCase("")]
        public void PreferAccessibleBrowserKeepsBrowserWithoutSystemChromium(string? systemChromiumPath)
        {
            string selected = HeadlessBrowserLauncher.PreferAccessibleBrowser(kChromePath, systemChromiumPath, _ => false);

            Assert.That(selected, Is.EqualTo(kChromePath));
        }

        [Test]
        public void PreferAccessibleBrowserKeepsBrowserWhenSystemChromiumIsAlsoInaccessible()
        {
            string selected = HeadlessBrowserLauncher.PreferAccessibleBrowser(kChromePath, kChromiumPath, _ => false);

            Assert.That(selected, Is.EqualTo(kChromePath));
        }

        [Test]
        public void ResolveSystemChromiumFallbackReturnsSystemChromium()
        {
            Assert.That(HeadlessBrowserLauncher.ResolveSystemChromiumFallback(kChromiumPath), Is.EqualTo(kChromiumPath));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void ResolveSystemChromiumFallbackThrowsWithoutSystemChromium(string? systemChromiumPath)
        {
            Assert.Throws<EnvironmentException>(() => HeadlessBrowserLauncher.ResolveSystemChromiumFallback(systemChromiumPath));
        }

        [Test]
        public async Task LaunchAsyncUsesSandboxedAttemptWhenItWorks()
        {
            List<BrowserLaunchAttempt> usedAttempts = [];
            IBrowser expectedBrowser = Substitute.For<IBrowser>();

            IBrowser browser = await HeadlessBrowserLauncher.LaunchAsync(attempt =>
            {
                usedAttempts.Add(attempt);
                return Task.FromResult(expectedBrowser);
            }, kChromePath, kChromiumPath);

            Assert.That(browser, Is.SameAs(expectedBrowser));
            Assert.That(usedAttempts, Has.Count.EqualTo(1));
            Assert.That(usedAttempts[0].SandboxDisabled, Is.False);
        }

        [Test]
        public async Task LaunchAsyncFallsBackToSandboxlessStart()
        {
            List<BrowserLaunchAttempt> usedAttempts = [];
            IBrowser expectedBrowser = Substitute.For<IBrowser>();

            IBrowser browser = await HeadlessBrowserLauncher.LaunchAsync(attempt =>
            {
                usedAttempts.Add(attempt);
                return attempt.SandboxDisabled
                    ? Task.FromResult(expectedBrowser)
                    : Task.FromException<IBrowser>(new ProcessException("Failed to launch browser"));
            }, kChromePath, null);

            Assert.That(browser, Is.SameAs(expectedBrowser));
            Assert.That(usedAttempts, Has.Count.EqualTo(2));
            Assert.That(usedAttempts[1].Args, Contains.Item(kNoSandboxArg));
        }

        [Test]
        public async Task LaunchAsyncFallsBackToSystemChromium()
        {
            List<BrowserLaunchAttempt> usedAttempts = [];
            IBrowser expectedBrowser = Substitute.For<IBrowser>();

            IBrowser browser = await HeadlessBrowserLauncher.LaunchAsync(attempt =>
            {
                usedAttempts.Add(attempt);
                return attempt.ExecutablePath == kChromiumPath
                    ? Task.FromResult(expectedBrowser)
                    : Task.FromException<IBrowser>(new ProcessException("Failed to launch browser"));
            }, kChromePath, kChromiumPath);

            Assert.That(browser, Is.SameAs(expectedBrowser));
            Assert.That(usedAttempts, Has.Count.EqualTo(4));
            Assert.That(usedAttempts[^1].ExecutablePath, Is.EqualTo(kChromiumPath));
        }

        [Test]
        public async Task LaunchAsyncSkipsSandboxedAttemptAfterSandboxlessSuccess()
        {
            IBrowser expectedBrowser = Substitute.For<IBrowser>();
            await HeadlessBrowserLauncher.LaunchAsync(attempt => attempt.SandboxDisabled
                ? Task.FromResult(expectedBrowser)
                : Task.FromException<IBrowser>(new ProcessException("Failed to launch browser")), kChromePath, null);

            List<BrowserLaunchAttempt> usedAttempts = [];
            await HeadlessBrowserLauncher.LaunchAsync(attempt =>
            {
                usedAttempts.Add(attempt);
                return Task.FromResult(expectedBrowser);
            }, kChromePath, null);

            Assert.That(usedAttempts, Has.Count.EqualTo(1));
            Assert.That(usedAttempts[0].SandboxDisabled, Is.True);
        }

        [Test]
        public async Task LaunchAsyncKeepsSandboxAfterSandboxedSuccess()
        {
            IBrowser expectedBrowser = Substitute.For<IBrowser>();
            await HeadlessBrowserLauncher.LaunchAsync(_ => Task.FromResult(expectedBrowser), kChromePath, null);

            List<BrowserLaunchAttempt> usedAttempts = [];
            await HeadlessBrowserLauncher.LaunchAsync(attempt =>
            {
                usedAttempts.Add(attempt);
                return Task.FromResult(expectedBrowser);
            }, kChromePath, null);

            Assert.That(usedAttempts, Has.Count.EqualTo(1));
            Assert.That(usedAttempts[0].SandboxDisabled, Is.False);
        }

        [Test]
        public void LaunchAsyncKeepsOriginalErrorWhenEveryAttemptFails()
        {
            ProcessException launchFailure = new("Failed to launch browser! spawn ENOENT");

            EnvironmentException? exception = Assert.ThrowsAsync<EnvironmentException>(async () =>
                await HeadlessBrowserLauncher.LaunchAsync(_ => Task.FromException<IBrowser>(launchFailure), kChromePath, kChromiumPath));

            Assert.That(exception, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(exception!.InnerException, Is.SameAs(launchFailure));
                Assert.That(exception.Message, Does.Contain("spawn ENOENT"));
            });
        }
    }
}
