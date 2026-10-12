using NUnit.Framework;
using NUnit.Framework.Legacy;
using FWO.Api.Client;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Config.Api.Data;
using FWO.Data;
using FWO.Data.Modelling;
using FWO.Services;
using FWO.Services.Modelling;
using FWO.Middleware.Client;
using FWO.Api.Client.Queries;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FWO.Test
{
    [TestFixture]
    [Parallelizable]
    internal class ModellingConnectionHandlerDecommissionTest
    {
        private static readonly Func<int, ReturnId[]> kInsertedConnectionReturnIds =
            connectionId => [new ReturnId { InsertedId = connectionId }];
        private static readonly ReturnId[] kHistoryReturnIds = [new ReturnId { AffectedRows = 1 }];

        [Test]
        public async Task DecommissionInterface_NotifiesAndAddsPermissionsAndSelections()
        {
            DecommissionTestApiConn apiConnection = new();
            SimulatedUserConfig userConfig = new();
            userConfig.UiHostName = "https://ui.example.test";
            userConfig.User.Name = "Tester";

            FwoOwner owner = new() { Id = 1, Name = "Owner1", ExtAppId = "APP1" };
            ModellingConnection interfaceConn = new()
            {
                Id = 10,
                AppId = owner.Id,
                App = owner,
                Name = "Interface1",
                IsInterface = true
            };

            ModellingConnection proposedInterface = new()
            {
                Id = 99,
                Name = "InterfaceNew",
                App = new FwoOwner { Id = 4, Name = "Owner4", ExtAppId = "APP4" }
            };

            FwoNotification notification = new()
            {
                Id = 10,
                NotificationClient = NotificationClient.InterfaceDecomm,
                Deadline = NotificationDeadline.None,
                Layout = NotificationLayout.HtmlInBody,
                RecipientTo = EmailRecipientOption.OwnerMainResponsible,
                EmailSubject = $"Subject {Placeholder.INTERFACE_NAME}",
                EmailBody = $"Body {Placeholder.INTERFACE_NAME} {Placeholder.NEW_INTERFACE_NAME} {Placeholder.NEW_INTERFACE_LINK} {Placeholder.REASON} {Placeholder.USER_NAME}"
            };
            apiConnection.Notifications = new List<FwoNotification> { notification };

            List<ModellingConnection> interfaceUsers = new()
            {
                new ModellingConnection { Id = 20, AppId = 2, App = new FwoOwner { Id = 2, Name = "Owner2" }, Name = "Conn2" },
                new ModellingConnection { Id = 21, AppId = 3, App = new FwoOwner { Id = 3, Name = "Owner3" }, Name = "Conn3" },
                new ModellingConnection { Id = 22, AppId = 1, App = owner, Name = "ConnOwn" }
            };
            apiConnection.InterfaceUsers = interfaceUsers;
            apiConnection.ConnectionById = interfaceConn;

            ModellingConnectionHandler handler = new(apiConnection, userConfig, owner, new List<ModellingConnection> { interfaceConn }, interfaceConn, addMode: false,
                readOnly: false, DefaultInit.DoNothing, DefaultInit.DoNothing, isOwner: true)
            {
                UsingConnections = interfaceUsers,
                ActConnNeedsRefresh = false
            };

            RecordingNotificationMiddlewareClient middlewareClient = new();

            await handler.DecommissionInterface("Planned", true, proposedInterface, middlewareClient);

            ClassicAssert.AreEqual(interfaceConn.Id, middlewareClient.ConnectionId);
            ClassicAssert.AreEqual(proposedInterface.Id, middlewareClient.ReplacementConnectionId);
            ClassicAssert.AreEqual("Planned", middlewareClient.Reason);

            CollectionAssert.AreEquivalent(new List<int> { 2, 3 }, apiConnection.AddedPermittedOwnerAppIds);
            CollectionAssert.AreEquivalent(new List<int> { 2, 3 }, apiConnection.AddedSelectedConnectionAppIds);
            ClassicAssert.IsTrue(apiConnection.AddedSelectedConnections.All(c => c.ConnectionId == proposedInterface.Id));
            ClassicAssert.IsTrue(apiConnection.RemovedSelectedConnections.Contains(interfaceConn.Id));
        }

        [Test]
        public async Task NotifyUsers_FailureDoesNotClaimExactRecipientCount()
        {
            SimulatedUserConfig userConfig = new();
            List<(string Title, string Message, bool IsError)> messages = [];
            userConfig.UiHostName = "https://ui.example.test";
            FwoOwner owner = new() { Id = 1, Name = "Owner1", ExtAppId = "APP1" };
            ModellingConnection connection = new() { Id = 10, AppId = owner.Id, App = owner, IsInterface = true };
            List<ModellingConnection> connections = [connection];
            TestableModellingConnectionHandler handler = new(
                new DecommissionTestApiConn(), userConfig, owner, connections, connection,
                (exception, title, message, isError) => messages.Add((title, message, isError)));
            RecordingNotificationMiddlewareClient middlewareClient = new()
            {
                DeliveryResult = NotificationDeliveryResult.Failed
            };

            await handler.NotifyUsersForTest("reason", null, middlewareClient);

            Assert.That(messages, Has.Count.EqualTo(1));
            Assert.That(messages[0].IsError, Is.True);
            Assert.That(messages[0].Message, Is.EqualTo(userConfig.GetText("E9031")));
            Assert.That(messages[0].Message, Does.Not.Contain("2"));
        }

        private sealed class TestableModellingConnectionHandler : ModellingConnectionHandler
        {
            public TestableModellingConnectionHandler(DecommissionTestApiConn apiConnection, SimulatedUserConfig userConfig,
                FwoOwner application, List<ModellingConnection> connections, ModellingConnection activeConnection,
                Action<Exception?, string, string, bool> displayMessage)
                : base(apiConnection, userConfig, application, connections, activeConnection, false, false,
                    displayMessage, DefaultInit.DoNothing, true)
            {
            }

            public Task NotifyUsersForTest(string reason, ModellingConnection? proposedInterface,
                MiddlewareClient middlewareClient)
            {
                return NotifyUsers(reason, proposedInterface, middlewareClient);
            }
        }

        private sealed class RecordingNotificationMiddlewareClient : MiddlewareClient
        {
            public NotificationDeliveryResult DeliveryResult { get; init; } = NotificationDeliveryResult.Delivered;
            public int ConnectionId { get; private set; }
            public int? ReplacementConnectionId { get; private set; }
            public string Reason { get; private set; } = "";

            public RecordingNotificationMiddlewareClient() : base("http://localhost/")
            { }

            public override Task<RestResponse<NotificationDeliveryResult>> SendInterfaceDecommissionNotification(
                int connectionId, int? replacementConnectionId, string reason)
            {
                ConnectionId = connectionId;
                ReplacementConnectionId = replacementConnectionId;
                Reason = reason;
                return Task.FromResult(new RestResponse<NotificationDeliveryResult>(new RestRequest())
                {
                    StatusCode = System.Net.HttpStatusCode.OK,
                    ResponseStatus = ResponseStatus.Completed,
                    IsSuccessStatusCode = true,
                    Data = DeliveryResult
                });
            }
        }

        private sealed class DecommissionTestApiConn : SimulatedApiConnection
        {
            public List<int> AddedPermittedOwnerAppIds { get; } = new();
            public List<int> AddedSelectedConnectionAppIds { get; } = new();
            public List<(int AppId, int ConnectionId)> AddedSelectedConnections { get; } = new();
            public List<int> RemovedSelectedConnections { get; } = new();
            public List<int> UpdatedNotificationIds { get; } = new();
            public List<ModellingConnection> InterfaceUsers { get; set; } = new();
            public List<FwoOwner> PermittedOwners { get; set; } = new();
            public List<FwoNotification> Notifications { get; set; } = new();
            public ModellingConnection? ConnectionById { get; set; }

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, FWO.Api.Client.QueryChunkingOptions? chunkingOptions = null)
            {
                Type responseType = typeof(QueryResponseType);
                if (query == ModellingQueries.addPermittedOwner)
                {
                    AddedPermittedOwnerAppIds.Add(GetIntVariable(variables, "appId"));
                    return Task.FromResult((QueryResponseType)(object)new ReturnId { AffectedRows = 1 });
                }
                if (query == ModellingQueries.addSelectedConnection)
                {
                    int appId = GetIntVariable(variables, "appId");
                    int connId = GetIntVariable(variables, "connectionId");
                    AddedSelectedConnectionAppIds.Add(appId);
                    AddedSelectedConnections.Add((appId, connId));
                    ReturnIdWrapper wrapper = new() { ReturnIds = kInsertedConnectionReturnIds(connId) };
                    return Task.FromResult((QueryResponseType)(object)wrapper);
                }
                if (query == ModellingQueries.removeSelectedConnection)
                {
                    RemovedSelectedConnections.Add(GetIntVariable(variables, "connectionId"));
                    return Task.FromResult((QueryResponseType)(object)new ReturnId { AffectedRows = 1 });
                }
                if (query == ModellingQueries.updateConnectionDecommission ||
                    query == ModellingQueries.updateConnectionProperties)
                {
                    return Task.FromResult((QueryResponseType)(object)new ReturnId { AffectedRows = 1 });
                }
                if (typeof(QueryResponseType) == typeof(List<FwoNotification>) && query == NotificationQueries.getNotifications)
                {
                    return Task.FromResult((QueryResponseType)(object)Notifications);
                }
                if (query == NotificationQueries.updateNotificationsLastSent)
                {
                    UpdatedNotificationIds.AddRange(GetIntListVariable(variables, "ids"));
                    return Task.FromResult((QueryResponseType)(object)new ReturnId { AffectedRows = UpdatedNotificationIds.Count });
                }
                if (query == ModellingQueries.addHistoryEntry)
                {
                    ReturnIdWrapper wrapper = new() { ReturnIds = kHistoryReturnIds };
                    return Task.FromResult((QueryResponseType)(object)wrapper);
                }
                if (responseType == typeof(List<ModellingConnectionWrapper>) && query == ModellingQueries.getSelectedConnections)
                {
                    return Task.FromResult((QueryResponseType)(object)new List<ModellingConnectionWrapper>());
                }
                if (responseType == typeof(List<FwoOwner>) && query == ModellingQueries.getPermittedOwnersForConnection)
                {
                    return Task.FromResult((QueryResponseType)(object)PermittedOwners);
                }
                if (responseType == typeof(List<ModellingAppServer>) && query == ModellingQueries.getAppServersForOwner)
                {
                    return Task.FromResult((QueryResponseType)(object)new List<ModellingAppServer>());
                }
                if (responseType == typeof(List<ModellingAppRole>) && query == ModellingQueries.getAppRoles)
                {
                    return Task.FromResult((QueryResponseType)(object)new List<ModellingAppRole>());
                }
                if (responseType == typeof(List<ModellingNetworkArea>) && query == ModellingQueries.getNwGroupObjects)
                {
                    return Task.FromResult((QueryResponseType)(object)new List<ModellingNetworkArea>());
                }
                if (responseType == typeof(List<ModellingNwGroupWrapper>) && query == ModellingQueries.getSelectedNwGroupObjects)
                {
                    return Task.FromResult((QueryResponseType)(object)new List<ModellingNwGroupWrapper>());
                }
                if (responseType == typeof(List<ModellingServiceGroup>) &&
                    (query == ModellingQueries.getGlobalServiceGroups || query == ModellingQueries.getServiceGroupsForApp))
                {
                    return Task.FromResult((QueryResponseType)(object)new List<ModellingServiceGroup>());
                }
                if (responseType == typeof(List<ModellingService>) &&
                    (query == ModellingQueries.getGlobalServices || query == ModellingQueries.getServicesForApp))
                {
                    return Task.FromResult((QueryResponseType)(object)new List<ModellingService>());
                }
                if (responseType == typeof(List<ModellingConnection>))
                {
                    if (query == ModellingQueries.getInterfaceUsers)
                    {
                        return Task.FromResult((QueryResponseType)(object)InterfaceUsers);
                    }
                    if (query == ModellingQueries.getConnectionById && ConnectionById != null)
                    {
                        return Task.FromResult((QueryResponseType)(object)new List<ModellingConnection> { ConnectionById });
                    }
                }

                throw new NotImplementedException($"Unhandled query: {query}");
            }

            private static int GetIntVariable(object? variables, string name)
            {
                if (variables == null)
                {
                    return 0;
                }
                object? value = variables.GetType().GetProperties().FirstOrDefault(p => p.Name == name)?.GetValue(variables);
                return value == null ? 0 : Convert.ToInt32(value);
            }

            private static List<int> GetIntListVariable(object? variables, string name)
            {
                System.Reflection.PropertyInfo? property = variables?.GetType().GetProperties().FirstOrDefault(p => p.Name == name);
                if (property?.GetValue(variables) is IEnumerable<int> values)
                {
                    return [.. values];
                }

                return [];
            }
        }
    }
}
