using NUnit.Framework;
using NUnit.Framework.Legacy;
using FWO.Api.Client;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Config.Api.Data;
using FWO.Data;
using FWO.Data.Modelling;
using FWO.Services;
using FWO.Middleware.Client;
using FWO.Api.Client.Queries;
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
        [Test]
        public async Task DecommissionInterface_NotifiesAndAddsPermissionsAndSelections()
        {
            DecommissionTestApiConn apiConnection = new();
            SimulatedUserConfig userConfig = new();
            userConfig.ModDecommEmailReceiver = nameof(EmailRecipientOption.OwnerMainResponsible);
            userConfig.ModDecommEmailSubject = $"Subject {Placeholder.INTERFACE_NAME}";
            userConfig.ModDecommEmailBody = $"Body {Placeholder.INTERFACE_NAME} {Placeholder.NEW_INTERFACE_NAME} {Placeholder.NEW_INTERFACE_LINK} {Placeholder.REASON} {Placeholder.USER_NAME}";
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

            TestEmailHelper emailHelper = new(userConfig);
            List<ModellingConnection> interfaceUsers =
            [
                new ModellingConnection { Id = 20, AppId = 2, App = new FwoOwner { Id = 2, Name = "Owner2" }, Name = "Conn2" },
                new ModellingConnection { Id = 21, AppId = 3, App = new FwoOwner { Id = 3, Name = "Owner3" }, Name = "Conn3" },
                new ModellingConnection { Id = 22, AppId = 1, App = owner, Name = "ConnOwn" }
            ];
            apiConnection.InterfaceUsers = interfaceUsers;
            apiConnection.ConnectionById = interfaceConn;

            DecommissionTestHandler handler = new(apiConnection, userConfig, owner, [interfaceConn], interfaceConn, addMode: false,
                readOnly: false, DefaultInit.DoNothing, DefaultInit.DoNothing, isOwner: true)
            {
                UsingConnections = interfaceUsers,
                ActConnNeedsRefresh = false,
                EmailHelperOverride = emailHelper
            };

            MiddlewareClient middlewareClient = new("http://localhost/");

            await handler.DecommissionInterface("Planned", true, proposedInterface, middlewareClient);

            ClassicAssert.IsTrue(emailHelper.InitCalled);
            ClassicAssert.AreEqual(2, emailHelper.SentEmails.Count);
            ClassicAssert.IsTrue(emailHelper.SentEmails.All(email => email.Owner.Id != owner.Id));
            ClassicAssert.IsTrue(emailHelper.SentEmails.All(email => email.Subject == $"Subject {interfaceConn.Name}"));
            ClassicAssert.IsTrue(emailHelper.SentEmails.All(email => email.Body.Contains($"<b>{interfaceConn.Name}</b>")));
            ClassicAssert.IsTrue(emailHelper.SentEmails.All(email => email.Body.Contains($"<b>{proposedInterface.Name}</b>")));
            ClassicAssert.IsTrue(emailHelper.SentEmails.All(email => email.Body.Contains($"<b>{userConfig.User.Name}</b>")));
            ClassicAssert.IsTrue(emailHelper.SentEmails.All(email => email.Body.Contains($"<b>Planned</b>")));
            ClassicAssert.IsTrue(emailHelper.SentEmails.All(email => email.Body.Contains($"{userConfig.UiHostName}/{PageName.Modelling}/{proposedInterface.App.ExtAppId}/{proposedInterface.Id}")));

            CollectionAssert.AreEquivalent(new[] { 2, 3 }, apiConnection.AddedPermittedOwnerAppIds);
            CollectionAssert.AreEquivalent(new[] { 2, 3 }, apiConnection.AddedSelectedConnectionAppIds);
            ClassicAssert.IsTrue(apiConnection.AddedSelectedConnections.All(c => c.ConnectionId == proposedInterface.Id));
            ClassicAssert.IsTrue(apiConnection.RemovedSelectedConnections.Contains(interfaceConn.Id));
        }

        private sealed class DecommissionTestHandler : ModellingConnectionHandler
        {
            public TestEmailHelper? EmailHelperOverride { get; set; }

            public DecommissionTestHandler(ApiConnection apiConnection, SimulatedUserConfig userConfig, FwoOwner application,
                List<ModellingConnection> connections, ModellingConnection conn, bool addMode, bool readOnly,
                Action<Exception?, string, string, bool> displayMessageInUi, Func<Task> refreshParent, bool isOwner)
                : base(apiConnection, userConfig, application, connections, conn, addMode, readOnly, displayMessageInUi, refreshParent, isOwner)
            {
            }

            protected override EmailHelper CreateEmailHelper(MiddlewareClient middlewareClient)
            {
                return EmailHelperOverride ?? base.CreateEmailHelper(middlewareClient);
            }
        }

        private sealed class TestEmailHelper : EmailHelper
        {
            public bool InitCalled { get; private set; }
            public List<(FwoOwner Owner, string Subject, string Body, EmailRecipientOption Recipient)> SentEmails { get; } = [];

            public TestEmailHelper(UserConfig userConfig)
                : base(new SimulatedApiConnection(), null, userConfig, DefaultInit.DoNothing)
            {
            }

            public override Task Init(string? scopedUserTo = null, string? scopedUserCc = null)
            {
                InitCalled = true;
                return Task.CompletedTask;
            }

            public override Task<bool> SendEmailToOwnerResponsibles(FwoOwner owner, string subject, string body, EmailRecipientOption recOpt, bool reqInCc = false)
            {
                SentEmails.Add((owner, subject, body, recOpt));
                return Task.FromResult(true);
            }

            public override Task<bool> SendEmailToOwnerResponsibles(FwoOwner owner, string subject, string body, string recipientConfig, bool reqInCc = false, List<string>? otherAddresses = null)
            {
                ModellingEmailRecipientSelection parsedSelection = ModellingEmailRecipientSelection.Parse(recipientConfig);
                EmailRecipientOption recipient = parsedSelection.None ? EmailRecipientOption.None : EmailRecipientOption.OwnerMainResponsible;
                SentEmails.Add((owner, subject, body, recipient));
                return Task.FromResult(true);
            }
        }

        private sealed class DecommissionTestApiConn : SimulatedApiConnection
        {
            public List<int> AddedPermittedOwnerAppIds { get; } = [];
            public List<int> AddedSelectedConnectionAppIds { get; } = [];
            public List<(int AppId, int ConnectionId)> AddedSelectedConnections { get; } = [];
            public List<int> RemovedSelectedConnections { get; } = [];
            public List<ModellingConnection> InterfaceUsers { get; set; } = [];
            public List<FwoOwner> PermittedOwners { get; set; } = [];
            public ModellingConnection? ConnectionById { get; set; }

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null)
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
                    ReturnIdWrapper wrapper = new() { ReturnIds = [new ReturnId { InsertedId = connId }] };
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
                if (query == ModellingQueries.addHistoryEntry)
                {
                    ReturnIdWrapper wrapper = new() { ReturnIds = [new ReturnId { AffectedRows = 1 }] };
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
        }
    }
}
