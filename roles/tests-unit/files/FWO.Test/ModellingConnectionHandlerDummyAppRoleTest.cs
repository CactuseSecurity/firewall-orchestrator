using NUnit.Framework;
using NUnit.Framework.Legacy;
using FWO.Data;
using FWO.Data.Modelling;
using FWO.Services;
using FWO.Services.Modelling;
using FWO.Basics;
using FWO.Api.Client.Queries;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;

namespace FWO.Test
{
    [TestFixture]
    [Parallelizable]
    internal class ModellingConnectionHandlerDummyAppRoleTest
    {
        static readonly SimulatedUserConfig userConfig = new();
        static readonly Action<Exception?, string, string, bool> DisplayMessageInUi = DefaultInit.DoNothing;
        static readonly FwoOwner Application = new() { Id = 1, Name = "TestApp" };

        [Test]
        public async Task AddNwObjects_LogsDummyAppRoleMarker()
        {
            DummyAppRoleTestApiConn apiConnection = new();
            ModellingConnection connection = new()
            {
                Id = 1,
                Name = "ReqInterf",
                IsInterface = true
            };

            ModellingConnectionHandler handler = CreateHandler(apiConnection, connection);
            handler.DummyAppRole = new ModellingAppRole { Id = 999, Name = "Dummy" };

            List<ModellingAppRole> appRoles = [new ModellingAppRole { Id = 999, Name = "Dummy" }];

            await InvokePrivateAsync(handler, "AddNwObjects",
                new object[] { new List<ModellingAppServer>(), appRoles, new List<ModellingNetworkArea>(), new List<ModellingNwGroup>(), ModellingTypes.ConnectionField.Source });

            ClassicAssert.AreEqual(1, apiConnection.HistoryTexts.Count);
            ClassicAssert.AreEqual("Marked requested Interface: ReqInterf as Source", apiConnection.HistoryTexts[0]);
        }

        [Test]
        public async Task RemoveNwObjects_LogsDummyAppRoleMarkerRemoval()
        {
            DummyAppRoleTestApiConn apiConnection = new();
            ModellingConnection connection = new()
            {
                Id = 2,
                Name = "ReqInterf",
                IsInterface = true
            };

            ModellingConnectionHandler handler = CreateHandler(apiConnection, connection);
            handler.DummyAppRole = new ModellingAppRole { Id = 999, Name = "Dummy" };

            List<ModellingAppRole> appRoles = [new ModellingAppRole { Id = 999, Name = "Dummy" }];

            await InvokePrivateAsync(handler, "RemoveNwObjects",
                new object[] { new List<ModellingAppServer>(), appRoles, new List<ModellingNetworkArea>(), new List<ModellingNwGroup>(), ModellingTypes.ConnectionField.Source });

            ClassicAssert.AreEqual(1, apiConnection.HistoryTexts.Count);
            ClassicAssert.AreEqual("Removed Source marker from requested Interface: ReqInterf", apiConnection.HistoryTexts[0]);
        }

        [Test]
        public async Task AddNwObjects_LogsDummyAndRealAppRoleMessages()
        {
            DummyAppRoleTestApiConn apiConnection = new();
            ModellingConnection connection = new()
            {
                Id = 3,
                Name = "ReqInterf",
                IsInterface = true
            };

            ModellingConnectionHandler handler = CreateHandler(apiConnection, connection);
            handler.DummyAppRole = new ModellingAppRole { Id = 999, Name = "Dummy" };

            List<ModellingAppRole> appRoles =
            [
                new ModellingAppRole { Id = 999, Name = "Dummy" },
                new ModellingAppRole { Id = 200, Name = "RealAppRole", IdString = "App123" }
            ];

            await InvokePrivateAsync(handler, "AddNwObjects",
                new object[] { new List<ModellingAppServer>(), appRoles, new List<ModellingNetworkArea>(), new List<ModellingNwGroup>(), ModellingTypes.ConnectionField.Source });

            ClassicAssert.AreEqual(2, apiConnection.HistoryTexts.Count);
            ClassicAssert.IsTrue(apiConnection.HistoryTexts.Any(t => t == "Marked requested Interface: ReqInterf as Source"));
            ClassicAssert.IsTrue(apiConnection.HistoryTexts.Any(t => t == "Added App Role RealAppRole (App123) to Interface: ReqInterf: Source"));
        }

        private static ModellingConnectionHandler CreateHandler(DummyAppRoleTestApiConn apiConnection, ModellingConnection connection)
        {
            return new ModellingConnectionHandler(apiConnection, userConfig, Application, [connection], connection, false, false, DisplayMessageInUi, DefaultInit.DoNothing, true);
        }

        private static async Task InvokePrivateAsync(ModellingConnectionHandler handler, string methodName, object[] args)
        {
            MethodInfo? method = typeof(ModellingConnectionHandler).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
            ClassicAssert.IsNotNull(method, $"Expected to find method '{methodName}'.");
            Task task = (Task)method!.Invoke(handler, args)!;
            await task;
        }
    }

    internal class DummyAppRoleTestApiConn : SimulatedApiConnection
    {
        public List<string> HistoryTexts { get; } = [];

        public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null)
        {
            Type responseType = typeof(QueryResponseType);
            if (query == ModellingQueries.addNwGroupToConnection || query == ModellingQueries.removeNwGroupFromConnection)
            {
                return Task.FromResult((QueryResponseType)(object)new ReturnId { AffectedRows = 1 });
            }
            if (query == ModellingQueries.addHistoryEntry)
            {
                if (variables != null)
                {
                    string? text = variables.GetType().GetProperties().FirstOrDefault(p => p.Name == "changeText")?.GetValue(variables)?.ToString();
                    if (!string.IsNullOrEmpty(text))
                    {
                        HistoryTexts.Add(text);
                    }
                }
                return Task.FromResult((QueryResponseType)(object)new ReturnIdWrapper { ReturnIds = [new ReturnId { AffectedRows = 1 }] });
            }
            return Task.FromResult((QueryResponseType)(object)new ReturnId { AffectedRows = 1 });
        }
    }
}
