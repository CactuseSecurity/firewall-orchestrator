using Bunit;
using FWO.Api.Client;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Report;
using FWO.Middleware.Client;
using FWO.Report;
using FWO.Services.RuleTreeBuilder;
using FWO.Ui.Pages.Reporting;
using FWO.Ui.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
// 'Report' on its own collides with the FWO.Report namespace
using ReportPage = FWO.Ui.Pages.Reporting.Report;

namespace FWO.Test
{
    [TestFixture]
    [FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
    internal class UiReportPageTest
    {
        private sealed class ReportPageApiConnection : SimulatedApiConnection
        {
            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                if (typeof(QueryResponseType).IsGenericType
                    && typeof(QueryResponseType).GetGenericTypeDefinition() == typeof(List<>))
                {
                    return Task.FromResult(Activator.CreateInstance<QueryResponseType>());
                }
                if (typeof(QueryResponseType).IsArray)
                {
                    return Task.FromResult((QueryResponseType)(object)Array.CreateInstance(typeof(QueryResponseType).GetElementType()!, 0));
                }
                return Task.FromResult(default(QueryResponseType)!);
            }
        }

        [SetUp]
        public void SetUpTranslations()
        {
            SimulatedUserConfig.DummyTranslate.TryAdd("report_type", "Report type");
            SimulatedUserConfig.DummyTranslate.TryAdd("generate_report", "Generate report");
            SimulatedUserConfig.DummyTranslate.TryAdd("no_device_selected", "No device selected");
            SimulatedUserConfig.DummyTranslate.TryAdd("E1001", "Select a device");
            SimulatedUserConfig.DummyTranslate.TryAdd("E1003", "Cancelled");
            SimulatedUserConfig.DummyTranslate.TryAdd("report_data_fetch", "Fetching");
            SimulatedUserConfig.DummyTranslate.TryAdd("report_elements", "Elements");
        }

        private static BunitContext CreateContext()
        {
            BunitContext context = new();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddAuthorizationCore();
            context.Services.AddLocalization();
            context.Services.AddSingleton<IAuthorizationService, AllowAllAuthorizationService>();
            context.Services.AddSingleton<AuthenticationStateProvider>(new MonitoringTestAuthStateProvider(Roles.Admin));
            context.Services.AddSingleton<ApiConnection>(new ReportPageApiConnection());
            context.Services.AddSingleton<UserConfig>(new SimulatedUserConfig());
            context.Services.AddSingleton<IRuleTreeBuilder>(new RuleTreeBuilder());
            context.Services.AddSingleton(new MiddlewareClient("http://localhost/"));
            context.Services.AddScoped<DomEventService>();
            return context;
        }

        private static IRenderedComponent<CascadingAuthenticationState> RenderPage(BunitContext context)
        {
            return context.Render<CascadingAuthenticationState>(parameters =>
                parameters.AddChildContent<ReportPage>());
        }

        [Test]
        public async Task TheReportPageRenders()
        {
            await using BunitContext context = CreateContext();

            IRenderedComponent<CascadingAuthenticationState> page = RenderPage(context);

            Assert.That(page.FindComponent<ReportPage>(), Is.Not.Null);
        }

        /// <summary>
        /// Reads the page's cancellation token source. It is private because nothing but the page
        /// itself should touch it, but its lifetime is exactly what these tests are about.
        /// </summary>
        private static CancellationTokenSource GetTokenSource(ReportPage reportPage)
        {
            System.Reflection.FieldInfo field = typeof(ReportPage)
                .GetField("tokenSource", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("tokenSource field not found");
            return (CancellationTokenSource)field.GetValue(reportPage)!;
        }

        private static bool IsDisposed(CancellationTokenSource tokenSource)
        {
            try
            {
                _ = tokenSource.Token;
                return false;
            }
            catch (ObjectDisposedException)
            {
                return true;
            }
        }

        [Test]
        public async Task DisposingThePageCancelsAGenerationThatIsStillRunning()
        {
            // without this an abandoned generation keeps building a complete report into memory for
            // a circuit that is already gone
            await using BunitContext context = CreateContext();
            IRenderedComponent<CascadingAuthenticationState> page = RenderPage(context);
            ReportPage reportPage = page.FindComponent<ReportPage>().Instance;
            CancellationTokenSource tokenSource = GetTokenSource(reportPage);

            reportPage.Dispose();

            Assert.That(tokenSource.IsCancellationRequested, Is.True);
        }

        [Test]
        public async Task DisposingThePageReleasesItsTokenSource()
        {
            await using BunitContext context = CreateContext();
            IRenderedComponent<CascadingAuthenticationState> page = RenderPage(context);
            ReportPage reportPage = page.FindComponent<ReportPage>().Instance;
            CancellationTokenSource tokenSource = GetTokenSource(reportPage);

            reportPage.Dispose();

            Assert.That(IsDisposed(tokenSource), Is.True);
        }

        [Test]
        public async Task DisposingThePageTwiceDoesNotThrow()
        {
            // the framework disposes the page, and losing the circuit disposes it again - a second
            // Cancel on the already disposed source would take the teardown down with it
            await using BunitContext context = CreateContext();
            IRenderedComponent<CascadingAuthenticationState> page = RenderPage(context);
            ReportPage reportPage = page.FindComponent<ReportPage>().Instance;
            CancellationTokenSource tokenSource = GetTokenSource(reportPage);

            reportPage.Dispose();

            Assert.DoesNotThrow(reportPage.Dispose);
            Assert.That(IsDisposed(tokenSource), Is.True);
        }

        [Test]
        public async Task DisposingThePageSurvivesAFailingCancellation()
        {
            // Cancel rethrows whatever a cancellation callback threw, and that must not escape the
            // teardown - the token source still has to be released
            await using BunitContext context = CreateContext();
            IRenderedComponent<CascadingAuthenticationState> page = RenderPage(context);
            ReportPage reportPage = page.FindComponent<ReportPage>().Instance;
            CancellationTokenSource tokenSource = GetTokenSource(reportPage);
            tokenSource.Token.Register(() => throw new InvalidOperationException("callback failed"));

            Assert.DoesNotThrow(reportPage.Dispose);
            Assert.That(IsDisposed(tokenSource), Is.True);
        }

        [Test]
        public async Task GeneratingAReportReleasesThePreviousTokenSource()
        {
            // every generation used to leave its predecessor behind undisposed
            await using BunitContext context = CreateContext();
            IRenderedComponent<CascadingAuthenticationState> page = RenderPage(context);
            IRenderedComponent<ReportPage> reportPage = page.FindComponent<ReportPage>();

            reportPage.Find("button.btn-primary").Click();
            CancellationTokenSource firstTokenSource = GetTokenSource(reportPage.Instance);
            reportPage.Find("button.btn-primary").Click();
            CancellationTokenSource secondTokenSource = GetTokenSource(reportPage.Instance);

            Assert.That(secondTokenSource, Is.Not.SameAs(firstTokenSource));
            Assert.That(IsDisposed(firstTokenSource), Is.True);
            Assert.That(IsDisposed(secondTokenSource), Is.False);
        }
    }
}