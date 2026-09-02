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
            List<BrowserLaunchAttempt> attempts = HeadlessBrowserLauncher.BuildLaunchAttempts(kChromePath, kChromiumPath, false);

            Assert.That(attempts, Has.Count.EqualTo(3));
            Assert.Multiple(() =>
            {
                Assert.That(attempts[0].ExecutablePath, Is.EqualTo(kChromePath));
                Assert.That(attempts[0].SandboxDisabled, Is.False);
                Assert.That(attempts[0].Args, Is.Empty);
                Assert.That(attempts[1].ExecutablePath, Is.EqualTo(kChromePath));
                Assert.That(attempts[1].SandboxDisabled, Is.True);
                Assert.That(attempts[1].Args, Contains.Item(kNoSandboxArg));
                Assert.That(attempts[2].ExecutablePath, Is.EqualTo(kChromiumPath));
                Assert.That(attempts[2].SandboxDisabled, Is.True);
            });
        }

        [Test]
        public void BuildLaunchAttemptsSkipsSandboxedAttemptWhenKnownToFail()
        {
            List<BrowserLaunchAttempt> attempts = HeadlessBrowserLauncher.BuildLaunchAttempts(kChromePath, null, true);

            Assert.That(attempts, Has.Count.EqualTo(1));
            Assert.Multiple(() =>
            {
                Assert.That(attempts[0].ExecutablePath, Is.EqualTo(kChromePath));
                Assert.That(attempts[0].SandboxDisabled, Is.True);
            });
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        [TestCase(kChromePath)]
        public void BuildLaunchAttemptsOmitsUnusableSystemChromium(string? systemChromiumPath)
        {
            List<BrowserLaunchAttempt> attempts = HeadlessBrowserLauncher.BuildLaunchAttempts(kChromePath, systemChromiumPath, false);

            Assert.That(attempts, Has.Count.EqualTo(2));
            Assert.That(attempts.Select(attempt => attempt.ExecutablePath), Is.All.EqualTo(kChromePath));
        }

        [Test]
        public void BuildLaunchOptionsMapsAttempt()
        {
            BrowserLaunchAttempt attempt = HeadlessBrowserLauncher.BuildLaunchAttempts(kChromePath, null, true)[0];

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
            Assert.That(usedAttempts, Has.Count.EqualTo(3));
            Assert.That(usedAttempts[2].ExecutablePath, Is.EqualTo(kChromiumPath));
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
