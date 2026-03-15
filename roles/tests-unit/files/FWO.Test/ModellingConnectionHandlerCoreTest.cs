using NUnit.Framework;
using NUnit.Framework.Legacy;
using FWO.Data;
using FWO.Data.Modelling;
using FWO.Services;
using FWO.Services.Modelling;
using FWO.Basics;
using System.Reflection;
using System.Collections.Generic;

namespace FWO.Test
{
    [TestFixture]
    [Parallelizable]
    internal class ModellingConnectionHandlerCoreTest
    {
        static readonly SimulatedUserConfig userConfig = new();
        static readonly Action<Exception?, string, string, bool> DisplayMessageInUi = DefaultInit.DoNothing;
        static readonly FwoOwner Application = new() { Id = 1, Name = "TestApp" };

        [OneTimeSetUp]
        public void EnsureRequiredTranslationKeys()
        {
            SimulatedUserConfig.DummyTranslate.TryAdd("save_connection", "Save Connection");
        }

        [Test]
        public void CalcVisibility_InterfaceWithDestinationFilled_ReadonlyFlags()
        {
            ModellingConnection connection = new()
            {
                Id = 1,
                IsInterface = true,
                DestinationAreas = [WrapArea(10, "Area10")]
            };

            ModellingConnectionHandler handler = CreateHandler(connection);

            bool result = handler.CalcVisibility();

            ClassicAssert.IsTrue(result);
            ClassicAssert.IsTrue(handler.SrcReadOnly);
            ClassicAssert.IsFalse(handler.DstReadOnly);
            ClassicAssert.IsFalse(handler.SvcReadOnly);
        }

        [Test]
        public void CalcVisibility_UsedInterfaceFlags()
        {
            ModellingConnection connection = new()
            {
                Id = 2,
                UsedInterfaceId = 5,
                SrcFromInterface = true,
                DstFromInterface = false
            };

            ModellingConnectionHandler handler = CreateHandler(connection);

            handler.CalcVisibility();

            ClassicAssert.IsTrue(handler.SrcReadOnly);
            ClassicAssert.IsFalse(handler.DstReadOnly);
            ClassicAssert.IsTrue(handler.SvcReadOnly);
        }

        [Test]
        public void CalcVisibility_DefaultFlags()
        {
            ModellingConnection connection = new() { Id = 3 };
            ModellingConnectionHandler handler = CreateHandler(connection);

            handler.CalcVisibility();

            ClassicAssert.IsFalse(handler.SrcReadOnly);
            ClassicAssert.IsFalse(handler.DstReadOnly);
            ClassicAssert.IsFalse(handler.SvcReadOnly);
        }

        [Test]
        public void CheckConn_FailsWhenNameOrReasonMissing()
        {
            ModellingConnection connection = new()
            {
                Id = 4,
                Name = "",
                Reason = ""
            };

            ModellingConnectionHandler handler = CreateHandler(connection);

            ClassicAssert.IsFalse(handler.CheckConn());
        }

        [Test]
        public void CheckConn_InterfaceWithSourceAndServicePasses()
        {
            ModellingConnection connection = new()
            {
                Id = 5,
                Name = "Interf",
                Reason = "Reason",
                IsInterface = true,
                SourceAreas = [WrapArea(11, "Area11")],
                Services = [WrapService(21, "Svc21")]
            };

            ModellingConnectionHandler handler = CreateHandler(connection);

            ClassicAssert.IsTrue(handler.CheckConn());
        }

        [Test]
        public void CheckConn_NonInterfaceMissingServiceFails()
        {
            ModellingConnection connection = new()
            {
                Id = 6,
                Name = "Conn",
                Reason = "Reason",
                SourceAreas = [WrapArea(12, "Area12")],
                DestinationAreas = [WrapArea(13, "Area13")]
            };

            ModellingConnectionHandler handler = CreateHandler(connection);

            ClassicAssert.IsFalse(handler.CheckConn());
        }

        [Test]
        public void SyncChanges_AppliesAddsAndDeletes()
        {
            ModellingConnection connection = new()
            {
                Id = 7,
                SourceAreas = [WrapArea(14, "AreaOld")],
                PermittedOwners = [new FwoOwner { Id = 1, Name = "Owner1" }]
            };

            ModellingConnectionHandler handler = CreateHandler(connection);
            handler.SrcAreasToDelete.Add(new ModellingNetworkArea { Id = 14, Name = "AreaOld", IdString = "NA14" });
            handler.SrcAreasToAdd.Add(new ModellingNetworkArea { Id = 15, Name = "AreaNew", IdString = "NA15" });
            handler.PermittedOwnersToDelete.Add(new FwoOwner { Id = 1, Name = "Owner1" });
            handler.PermittedOwnersToAdd.Add(new FwoOwner { Id = 2, Name = "Owner2" });

            InvokePrivateVoid(handler, "SyncChanges");

            ClassicAssert.IsFalse(handler.ActConn.SourceAreas.Any(a => a.Content.Id == 14));
            ClassicAssert.IsTrue(handler.ActConn.SourceAreas.Any(a => a.Content.Id == 15));
            ClassicAssert.IsFalse(handler.ActConn.PermittedOwners.Any(o => o.Id == 1));
            ClassicAssert.IsTrue(handler.ActConn.PermittedOwners.Any(o => o.Id == 2));
        }

        [Test]
        public async Task SavePropertiesOnly_UpdatesMatchingConnectionAndReturnsTrue()
        {
            SavePropertiesOnlyTestApiConn apiConnection = new();
            ModellingConnection listedConnection = new() { Id = 100, Properties = "{\"old\":true}" };
            ModellingConnection actConnection = new() { Id = 100, Properties = "{\"new\":true}" };
            ModellingConnectionHandler handler = CreateHandler(actConnection, [listedConnection], apiConnection);

            bool result = await handler.SavePropertiesOnly();

            ClassicAssert.IsTrue(result);
            ClassicAssert.IsTrue(apiConnection.UpdateConnectionPropertiesCalled);
            ClassicAssert.AreEqual(100, GetVariable<int>(apiConnection.LastVariables, "id"));
            ClassicAssert.AreEqual("{\"new\":true}", GetVariable<string>(apiConnection.LastVariables, "connProp"));
            ClassicAssert.AreEqual("{\"new\":true}", listedConnection.Properties);
        }

        [Test]
        public async Task SavePropertiesOnly_WithoutMatchingConnection_ReturnsTrueWithoutListUpdate()
        {
            SavePropertiesOnlyTestApiConn apiConnection = new();
            ModellingConnection listedConnection = new() { Id = 101, Properties = "{\"old\":true}" };
            ModellingConnection actConnection = new() { Id = 999, Properties = "{\"new\":true}" };
            ModellingConnectionHandler handler = CreateHandler(actConnection, [listedConnection], apiConnection);

            bool result = await handler.SavePropertiesOnly();

            ClassicAssert.IsTrue(result);
            ClassicAssert.IsTrue(apiConnection.UpdateConnectionPropertiesCalled);
            ClassicAssert.AreEqual("{\"old\":true}", listedConnection.Properties);
        }

        [Test]
        public async Task SavePropertiesOnly_OnApiException_ReturnsFalseAndKeepsListUnchanged()
        {
            SavePropertiesOnlyTestApiConn apiConnection = new() { ThrowOnUpdateConnectionProperties = true };
            ModellingConnection listedConnection = new() { Id = 102, Properties = "{\"old\":true}" };
            ModellingConnection actConnection = new() { Id = 102, Properties = "{\"new\":true}" };
            ModellingConnectionHandler handler = CreateHandler(actConnection, [listedConnection], apiConnection);

            bool result = await handler.SavePropertiesOnly();

            ClassicAssert.IsFalse(result);
            ClassicAssert.AreEqual("{\"old\":true}", listedConnection.Properties);
        }

        private static ModellingConnectionHandler CreateHandler(ModellingConnection connection)
        {
            return new ModellingConnectionHandler(new ModellingHandlerTestApiConn(), userConfig, Application, [connection], connection, false, false, DisplayMessageInUi, DefaultInit.DoNothing, true);
        }

        private static ModellingConnectionHandler CreateHandler(ModellingConnection actConnection, List<ModellingConnection> connections, SimulatedApiConnection apiConnection)
        {
            return new ModellingConnectionHandler(apiConnection, userConfig, Application, connections, actConnection, false, false, DisplayMessageInUi, DefaultInit.DoNothing, true);
        }

        private static void InvokePrivateVoid(ModellingConnectionHandler handler, string methodName)
        {
            MethodInfo? method = typeof(ModellingConnectionHandler).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
            ClassicAssert.IsNotNull(method, $"Expected to find method '{methodName}'.");
            method!.Invoke(handler, null);
        }

        private static ModellingNetworkAreaWrapper WrapArea(long id, string name)
        {
            return new ModellingNetworkAreaWrapper { Content = new ModellingNetworkArea { Id = id, Name = name, IdString = $"NA{id}" } };
        }

        private static ModellingServiceWrapper WrapService(int id, string name)
        {
            return new ModellingServiceWrapper { Content = new ModellingService { Id = id, Name = name } };
        }

        private static T GetVariable<T>(object? variables, string name)
        {
            object? value = variables?.GetType().GetProperty(name)?.GetValue(variables);
            return value == null ? default! : (T)value;
        }

        private sealed class SavePropertiesOnlyTestApiConn : SimulatedApiConnection
        {
            public bool ThrowOnUpdateConnectionProperties { get; set; }
            public bool UpdateConnectionPropertiesCalled { get; private set; }
            public object? LastVariables { get; private set; }

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null)
            {
                if (query == FWO.Api.Client.Queries.ModellingQueries.updateConnectionProperties && typeof(QueryResponseType) == typeof(ReturnId))
                {
                    UpdateConnectionPropertiesCalled = true;
                    LastVariables = variables;
                    if (ThrowOnUpdateConnectionProperties)
                    {
                        throw new InvalidOperationException("Simulated update failure.");
                    }
                    return Task.FromResult((QueryResponseType)(object)new ReturnId { AffectedRows = 1 });
                }
                throw new NotImplementedException($"Unhandled query: {query}");
            }
        }
    }
}
