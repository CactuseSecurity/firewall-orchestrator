using Bunit;
using FWO.Api.Client;
using FWO.Config.Api;
using FWO.Data;
using FWO.Ui.Pages.Reporting;
using FWO.Ui.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    [FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
    internal class UiReportOwnerParamSelectionTest
    {
        private sealed class ThrowingOwnerParamApiConn : SimulatedApiConnection
        {
            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null)
            {
                throw new InvalidOperationException("field 'owner_lifecycle_state' not found in type: 'query_root'");
            }
        }

        [SetUp]
        public void SetUpTranslations()
        {
            SimulatedUserConfig.DummyTranslate.TryAdd("state", "State");
            SimulatedUserConfig.DummyTranslate.TryAdd("criticality", "Criticality");
            SimulatedUserConfig.DummyTranslate.TryAdd("all", "All");
            SimulatedUserConfig.DummyTranslate.TryAdd("inactive", "inactive");
        }

        [Test]
        public async Task ReportOwnerParamSelection_QueryFailure_IsCaughtAndReported()
        {
            await using Bunit.TestContext context = new();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddScoped<DomEventService>();
            context.Services.AddSingleton<ApiConnection>(new ThrowingOwnerParamApiConn());
            SimulatedUserConfig userConfig = new();
            context.Services.AddSingleton<UserConfig>(userConfig);

            List<(Exception? Exception, string Title)> messages = [];
            Action<Exception?, string, string, bool> displayMessageInUi = (exception, title, _, _) => messages.Add((exception, title));

            IRenderedComponent<CascadingValue<Action<Exception?, string, string, bool>>> wrapper =
                context.Render<CascadingValue<Action<Exception?, string, string, bool>>>(parameters => parameters
                    .Add(p => p.Value, displayMessageInUi)
                    .AddChildContent<ReportOwnerParamSelection>());

            wrapper.WaitForAssertion(() =>
            {
                Assert.That(messages, Has.Count.EqualTo(1));
                Assert.That(messages[0].Exception, Is.Not.Null);
                Assert.That(messages[0].Title, Is.EqualTo(userConfig.GetText("object_fetch")));
            });
        }
    }
}
