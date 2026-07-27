using Bunit;
using FWO.Basics;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Report;
using FWO.Ui.Pages.Reporting;
using FWO.Ui.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Reflection;

namespace FWO.Test
{
    [TestFixture]
    [FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
    internal class UiReportOwnerParamSelectionTest
    {
        private sealed class ThrowingOwnerParamApiConn : SimulatedApiConnection
        {
            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, FWO.Api.Client.QueryChunkingOptions? chunkingOptions = null)
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
            await using BunitContext context = new();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddScoped<DomEventService>();
            context.Services.AddSingleton<ApiConnection>(new ThrowingOwnerParamApiConn());
            SimulatedUserConfig userConfig = new();
            context.Services.AddSingleton<UserConfig>(userConfig);

            List<(Exception? Exception, string Title)> messages = new List<(Exception? Exception, string Title)>();
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

        [Test]
        public async Task ReportOwnerParamSelection_LoadsAndUpdatesFilterValues()
        {
            await using BunitContext context = new();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddScoped<DomEventService>();
            context.Services.AddSingleton<ApiConnection>(new SuccessfulOwnerParamApiConn());
            SimulatedUserConfig userConfig = new();
            context.Services.AddSingleton<UserConfig>(userConfig);
            context.Services.AddLocalization();

            OwnerFilter ownerFilter = new();
            OwnerFilter? changedFilter = null;
            Action<Exception?, string, string, bool> noOpDisplayMessage = (_, _, _, _) => { };

            IRenderedComponent<CascadingValue<Action<Exception?, string, string, bool>>> wrapper =
                context.Render<CascadingValue<Action<Exception?, string, string, bool>>>(parameters => parameters
                    .Add(p => p.Value, noOpDisplayMessage)
                    .AddChildContent<ReportOwnerParamSelection>(childParameters => childParameters
                        .Add(p => p.OwnerFilter, ownerFilter)
                        .Add(p => p.OwnerFilterChanged, updated => changedFilter = updated)
                        .Add(p => p.UseFormLayout, true)
                        .Add(p => p.UseLightText, false)));

            wrapper.WaitForAssertion(() =>
            {
                ReportOwnerParamSelection component = wrapper.FindComponent<ReportOwnerParamSelection>().Instance;
                List<string> criticalities = GetPrivateMember<List<string>>(component, "AvailableCriticalities");

                Assert.Multiple(() =>
                {
                    Assert.That(wrapper.Markup, Does.Contain("form-group row mt-2"));
                    Assert.That(criticalities, Is.EqualTo(new List<string> { "alpha", "bravo" }));
                });
            });

            ReportOwnerParamSelection reportOwnerParamSelection = wrapper.FindComponent<ReportOwnerParamSelection>().Instance;

            await (Task)InvokePrivateMethod(reportOwnerParamSelection, "CriticalityChanged", "alpha")!;
            Assert.Multiple(() =>
            {
                Assert.That(ownerFilter.SelectedCriticality, Is.EqualTo("alpha"));
                Assert.That(changedFilter, Is.SameAs(ownerFilter));
            });

            await (Task)InvokePrivateMethod(reportOwnerParamSelection, "CriticalityChanged", "   ")!;
            Assert.That(ownerFilter.SelectedCriticality, Is.Null);

            await (Task)InvokePrivateMethod(reportOwnerParamSelection, "OwnerLifeCycleStateChanged", new OwnerLifeCycleState { Id = 2 })!;
            Assert.That(ownerFilter.SelectedOwnerLifeCycleStateId, Is.EqualTo(2));
        }

        private static object? InvokePrivateMethod(object instance, string methodName, params object?[] args)
        {
            MethodInfo? method = instance.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (method == null)
            {
                throw new MissingMethodException(instance.GetType().FullName, methodName);
            }

            return method.Invoke(instance, args);
        }

        private static T GetPrivateMember<T>(object instance, string memberName)
        {
            Type type = instance.GetType();
            PropertyInfo? property = type.GetProperty(memberName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (property != null)
            {
                return (T)property.GetValue(instance)!;
            }

            FieldInfo? field = type.GetField(memberName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null)
            {
                return (T)field.GetValue(instance)!;
            }

            throw new MissingMemberException(type.FullName, memberName);
        }
    }

    internal sealed class SuccessfulOwnerParamApiConn : SimulatedApiConnection
    {
        public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, FWO.Api.Client.QueryChunkingOptions? chunkingOptions = null)
        {
            if (typeof(QueryResponseType) == typeof(List<OwnerLifeCycleState>))
            {
                if (query == OwnerQueries.getOwnerLifeCycleStates)
                {
                    return Task.FromResult((QueryResponseType)(object)new List<OwnerLifeCycleState>
                    {
                        new() { Id = 1, Name = "alpha" },
                        new() { Id = 2, Name = "zeta" }
                    });
                }
            }

            if (typeof(QueryResponseType) == typeof(List<FwoOwner>))
            {
                if (query == OwnerQueries.getOwners)
                {
                    return Task.FromResult((QueryResponseType)(object)new List<FwoOwner>
                    {
                        new() { Id = 1, Criticality = "bravo" },
                        new() { Id = 2, Criticality = "alpha" },
                        new() { Id = 3, Criticality = "bravo" },
                        new() { Id = 4, Criticality = "   " }
                    });
                }
            }

            throw new NotImplementedException();
        }
    }
}
