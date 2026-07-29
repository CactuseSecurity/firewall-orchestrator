using Bunit;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Workflow;
using FWO.Middleware.Client;
using FWO.Ui.Pages.Monitoring;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;

namespace FWO.Test
{
    [TestFixture]
    [FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
    public class UiMonitorExternalRequestsTest
    {
        private static MethodInfo GetPrivateMethod(string name, params Type[] parameterTypes)
        {
            return typeof(MonitorExternalRequests).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance, null, parameterTypes, null)
                ?? throw new MissingMethodException(typeof(MonitorExternalRequests).FullName, name);
        }

        private static T GetPrivateField<T>(MonitorExternalRequests component, string fieldName)
        {
            FieldInfo? field = typeof(MonitorExternalRequests).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null)
            {
                throw new MissingFieldException(typeof(MonitorExternalRequests).FullName, fieldName);
            }
            return (T)field.GetValue(component)!;
        }

        private static MonitorExternalRequests RenderComponent(MonitorExternalRequestsApiConn apiConn)
        {
            BunitContext context = new();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddAuthorizationCore();
            context.Services.AddLocalization();
            context.Services.AddSingleton<IAuthorizationService, AllowAllAuthorizationService>();
            context.Services.AddSingleton<AuthenticationStateProvider>(new MonitoringTestAuthStateProvider(Roles.Admin));
            context.Services.AddSingleton<ApiConnection>(apiConn);
            context.Services.AddSingleton(new MiddlewareClient("http://localhost/"));
            context.Services.AddSingleton<UserConfig>(new SimulatedUserConfig());

            IRenderedComponent<CascadingAuthenticationState> component = context.Render<CascadingAuthenticationState>(parameters =>
                parameters.AddChildContent<MonitorExternalRequests>());
            return component.FindComponent<MonitorExternalRequests>().Instance;
        }

        [Test]
        public void OnInitializedAsync_LoadsRelevantRequestsAndSortsDescending()
        {
            MonitorExternalRequestsApiConn apiConn = new();
            apiConn.OpenRequests.Add(CreateRequest(10, ExtStates.ExtReqRequested, false));
            apiConn.OpenRequests.Add(CreateRequest(20, ExtStates.ExtReqInProgress, true));

            MonitorExternalRequests component = RenderComponent(apiConn);

            List<ExternalRequest> storedRequests = GetPrivateField<List<ExternalRequest>>(component, "openRequests");
            Assert.Multiple(() =>
            {
                Assert.That(apiConn.OpenRequestQueryCount, Is.EqualTo(1));
                Assert.That(apiConn.LastRequestedStates, Has.Count.EqualTo(7));
                Assert.That(apiConn.LastRequestedStates[0], Is.EqualTo(ExtStates.ExtReqInitialized.ToString()));
                Assert.That(apiConn.LastRequestedStates[6], Is.EqualTo(ExtStates.ExtReqDiscarded.ToString()));
                Assert.That(GetPrivateField<bool>(component, "InitComplete"), Is.True);
                Assert.That(GetPrivateField<bool>(component, "displayAll"), Is.False);
                Assert.That(storedRequests, Has.Count.EqualTo(2));
                Assert.That(storedRequests[0].Id, Is.EqualTo(20));
                Assert.That(storedRequests[1].Id, Is.EqualTo(10));
            });
        }

        [Test]
        public async Task ChangeDisplay_TogglesAllStatesAndRefetches()
        {
            MonitorExternalRequestsApiConn apiConn = new();
            apiConn.OpenRequests.Add(CreateRequest(5, ExtStates.ExtReqRequested, false));
            MonitorExternalRequests component = RenderComponent(apiConn);

            await (Task)GetPrivateMethod("ChangeDisplay").Invoke(component, Array.Empty<object>())!;

            Assert.Multiple(() =>
            {
                Assert.That(GetPrivateField<bool>(component, "displayAll"), Is.True);
                Assert.That(apiConn.OpenRequestQueryCount, Is.EqualTo(2));
                Assert.That(apiConn.LastRequestedStates, Has.Count.EqualTo(9));
                Assert.That(apiConn.LastRequestedStates, Does.Contain(ExtStates.ExtReqAcknowledged.ToString()));
                Assert.That(apiConn.LastRequestedStates, Does.Contain(ExtStates.ExtReqAckRejected.ToString()));
            });
        }

        [Test]
        public void RequestChangeState_UsesAllowedStatesForRequestedRequest()
        {
            MonitorExternalRequestsApiConn apiConn = new();
            apiConn.OpenRequests.Add(CreateRequest(12, ExtStates.ExtReqRequested, false));
            MonitorExternalRequests component = RenderComponent(apiConn);
            ExternalRequest request = CreateRequest(12, ExtStates.ExtReqRequested, false);

            GetPrivateMethod("RequestChangeState", typeof(ExternalRequest)).Invoke(component, new object[] { request });

            List<ExtStates> availableStates = GetPrivateField<List<ExtStates>>(component, "availableStates");
            Assert.Multiple(() =>
            {
                Assert.That(GetPrivateField<ExternalRequest>(component, "actRequest").Id, Is.EqualTo(12));
                Assert.That(availableStates, Has.Count.EqualTo(4));
                Assert.That(availableStates[0], Is.EqualTo(ExtStates.ExtReqRejected));
                Assert.That(availableStates[1], Is.EqualTo(ExtStates.ExtReqDone));
                Assert.That(availableStates[2], Is.EqualTo(ExtStates.ExtReqAckRejected));
                Assert.That(availableStates[3], Is.EqualTo(ExtStates.ExtReqAcknowledged));
                Assert.That(GetPrivateField<ExtStates>(component, "actState"), Is.EqualTo(ExtStates.ExtReqRejected));
                Assert.That(GetPrivateField<bool>(component, "ChangeStateMode"), Is.True);
            });
        }

        [Test]
        public void Details_OpensRequestDialog()
        {
            MonitorExternalRequestsApiConn apiConn = new();
            apiConn.OpenRequests.Add(CreateRequest(9, ExtStates.ExtReqFailed, false));
            MonitorExternalRequests component = RenderComponent(apiConn);
            ExternalRequest request = CreateRequest(9, ExtStates.ExtReqFailed, false);

            GetPrivateMethod("Details", typeof(ExternalRequest)).Invoke(component, new object[] { request });

            Assert.Multiple(() =>
            {
                Assert.That(GetPrivateField<ExternalRequest>(component, "actRequest").Id, Is.EqualTo(9));
                Assert.That(GetPrivateField<bool>(component, "DetailsMode"), Is.True);
            });
        }

        [Test]
        public async Task Unlock_QueriesResetAndRefreshes()
        {
            MonitorExternalRequestsApiConn apiConn = new();
            apiConn.OpenRequests.Add(CreateRequest(44, ExtStates.ExtReqInProgress, true));
            MonitorExternalRequests component = RenderComponent(apiConn);
            ExternalRequest request = CreateRequest(44, ExtStates.ExtReqInProgress, true);

            await (Task)GetPrivateMethod("Unlock", typeof(ExternalRequest)).Invoke(component, new object[] { request })!;

            Assert.Multiple(() =>
            {
                Assert.That(apiConn.UnlockQueryCount, Is.EqualTo(1));
                Assert.That(apiConn.LastUnlockedId, Is.EqualTo(44));
                Assert.That(apiConn.LastUnlockedLocked, Is.False);
                Assert.That(apiConn.OpenRequestQueryCount, Is.EqualTo(2));
            });
        }

        [Test]
        public void ConvertQuotes_ReplacesEscapedDoubleQuotes()
        {
            MonitorExternalRequestsApiConn apiConn = new();
            MonitorExternalRequests component = RenderComponent(apiConn);

            object[] arguments = new object[1];
            arguments[0] = @"{\u0022hello\u0022}";
            string converted = (string)GetPrivateMethod("ConvertQuotes", typeof(string)).Invoke(component, arguments)!;

            Assert.That(converted, Is.EqualTo("{\"hello\"}"));
        }

        private static ExternalRequest CreateRequest(long id, ExtStates state, bool locked)
        {
            return new ExternalRequest
            {
                Id = id,
                Owner = new FwoOwner
                {
                    Id = 1,
                    Name = "Owner"
                },
                TicketId = 100 + id,
                TaskNumber = (int)id,
                WaitCycles = 2,
                Attempts = 1,
                ExtTicketSystem = "system",
                ExtRequestType = "type",
                ExtRequestContent = @"{\u0022content\u0022}",
                ExtQueryVariables = "{}",
                ExtRequestState = state.ToString(),
                ExtTicketId = $"EXT-{id}",
                LastCreationResponse = "created",
                LastProcessingResponse = "processed",
                CreationDate = new DateTime(2026, 1, 1),
                Locked = locked
            };
        }
    }

    internal sealed class MonitorExternalRequestsApiConn : SimulatedApiConnection
    {
        public List<ExternalRequest> OpenRequests { get; } = new();
        public List<string> LastRequestedStates { get; private set; } = new();
        public int OpenRequestQueryCount { get; private set; }
        public int UnlockQueryCount { get; private set; }
        public long LastUnlockedId { get; private set; }
        public bool LastUnlockedLocked { get; private set; }

        public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
        {
            if (typeof(QueryResponseType) == typeof(List<ExternalRequest>) && query == ExtRequestQueries.getOpenRequests)
            {
                OpenRequestQueryCount++;
                LastRequestedStates = GetStringList(variables, "states");
                return Task.FromResult((QueryResponseType)(object)OpenRequests);
            }

            if (typeof(QueryResponseType) == typeof(ReturnId) && query == ExtRequestQueries.updateExternalRequestLock)
            {
                UnlockQueryCount++;
                LastUnlockedId = GetValue<long>(variables, "id");
                LastUnlockedLocked = GetValue<bool>(variables, "locked");
                return Task.FromResult((QueryResponseType)(object)new ReturnId { UpdatedIdLong = LastUnlockedId });
            }

            throw new NotImplementedException();
        }

        private static List<string> GetStringList(object? variables, string propertyName)
        {
            PropertyInfo? property = variables?.GetType().GetProperty(propertyName);
            if (property == null)
            {
                return new List<string>();
            }

            return (List<string>)property.GetValue(variables)!;
        }

        private static TValue GetValue<TValue>(object? variables, string propertyName)
        {
            PropertyInfo? property = variables?.GetType().GetProperty(propertyName);
            if (property == null)
            {
                throw new MissingMemberException(variables?.GetType().FullName, propertyName);
            }

            return (TValue)property.GetValue(variables)!;
        }
    }
}
