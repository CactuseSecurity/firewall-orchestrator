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
    internal class ModellingConnectionHandlerInterfaceInternalTest
    {
        static readonly SimulatedUserConfig userConfig = new();
        static readonly Action<Exception?, string, string, bool> DisplayMessageInUi = DefaultInit.DoNothing;
        static readonly FwoOwner Application = new() { Id = 1, Name = "TestApp" };

        [Test]
        public void CheckInterface_FailsWhenOnlyDummyAndNoServices()
        {
            ModellingAppRole dummy = new() { Id = 999, Name = "Dummy" };
            ModellingConnection connection = new()
            {
                Id = 1,
                IsInterface = true,
                SourceAppRoles = [new() { Content = dummy }]
            };

            ModellingConnectionHandler handler = CreateHandler(connection, addMode: true);
            handler.DummyAppRole = dummy;

            bool result = InvokePrivateBool(handler, "CheckInterface");

            ClassicAssert.IsFalse(result);
        }

        [Test]
        public void CheckInterface_FailsWhenPermissionMissing_ShowsE9021()
        {
            ModellingConnection connection = new()
            {
                Id = 7,
                IsInterface = true,
                InterfacePermission = ""
            };

            string? message = null;
            ModellingConnectionHandler handler = CreateHandler(connection, addMode: true,
                (exception, title, text, isError) => message = text);

            bool result = InvokePrivateBool(handler, "CheckInterface");

            ClassicAssert.IsFalse(result);
            ClassicAssert.AreEqual(userConfig.GetText("E9021"), message);
        }

        [Test]
        public void CheckInterface_FailsWhenPrivateAndUsedByOtherApp()
        {
            ModellingConnection connection = new()
            {
                Id = 2,
                IsInterface = true,
                AppId = 1,
                InterfacePermission = InterfacePermissions.Public.ToString(),
                SourceAreas = [WrapArea(10, "Area1")],
                Services = [WrapService(20, "Svc1")]
            };

            ModellingConnectionHandler handler = CreateHandler(connection, addMode: false);
            handler.UsingConnections = [new ModellingConnection { AppId = 2 }];
            handler.ActConn.InterfacePermission = InterfacePermissions.Private.ToString();

            bool result = InvokePrivateBool(handler, "CheckInterface");

            ClassicAssert.IsFalse(result);
        }

        [Test]
        public void CheckInterface_FailsWhenSrcDstChanged_ShowsE9005()
        {
            ModellingConnection connection = new()
            {
                Id = 8,
                IsInterface = true,
                AppId = 1,
                InterfacePermission = InterfacePermissions.Public.ToString(),
                Services = [WrapService(30, "Svc1")]
            };

            string? message = null;
            ModellingConnectionHandler handler = CreateHandler(connection, addMode: false,
                (exception, title, text, isError) => message = text);

            handler.ActConn.SourceAreas = [WrapArea(30, "Area1")];

            bool result = InvokePrivateBool(handler, "CheckInterface");

            ClassicAssert.IsFalse(result);
            ClassicAssert.AreEqual(userConfig.GetText("E9005"), message);
        }

        [Test]
        public void CheckInterface_PassesWhenFilledAndNoPrivateConflict()
        {
            ModellingConnection connection = new()
            {
                Id = 3,
                IsInterface = true,
                AppId = 1,
                InterfacePermission = InterfacePermissions.Public.ToString(),
                SourceAreas = [WrapArea(11, "Area1")],
                Services = [WrapService(21, "Svc1")]
            };

            ModellingConnectionHandler handler = CreateHandler(connection, addMode: false);

            bool result = InvokePrivateBool(handler, "CheckInterface");

            ClassicAssert.IsTrue(result);
        }

        [Test]
        public void SyncSrcChanges_RemovesDummyMarkerWhenFilled()
        {
            ModellingAppRole dummy = new() { Id = 999, Name = "Dummy" };
            ModellingConnection connection = new()
            {
                Id = 4,
                IsInterface = true,
                SourceAppRoles = [new() { Content = dummy }]
            };

            ModellingConnectionHandler handler = CreateHandler(connection, addMode: false);
            handler.DummyAppRole = dummy;
            handler.SrcAreasToAdd.Add(new ModellingNetworkArea { Id = 10, Name = "Area1", IdString = "NA10" });

            InvokePrivateVoid(handler, "SyncSrcChanges");

            ClassicAssert.IsTrue(handler.SrcAppRolesToDelete.Any(r => r.Id == dummy.Id));
            ClassicAssert.IsFalse(handler.ActConn.SourceAppRoles.Any(r => r.Content.Id == dummy.Id));
            ClassicAssert.IsTrue(handler.ActConn.SourceAreas.Any(a => a.Content.Id == 10));
        }

        [Test]
        public void SyncDstChanges_RemovesDummyMarkerWhenFilled()
        {
            ModellingAppRole dummy = new() { Id = 999, Name = "Dummy" };
            ModellingConnection connection = new()
            {
                Id = 5,
                IsInterface = true,
                DestinationAppRoles = [new() { Content = dummy }]
            };

            ModellingConnectionHandler handler = CreateHandler(connection, addMode: false);
            handler.DummyAppRole = dummy;
            handler.DstAreasToAdd.Add(new ModellingNetworkArea { Id = 20, Name = "Area2", IdString = "NA20" });

            InvokePrivateVoid(handler, "SyncDstChanges");

            ClassicAssert.IsTrue(handler.DstAppRolesToDelete.Any(r => r.Id == dummy.Id));
            ClassicAssert.IsFalse(handler.ActConn.DestinationAppRoles.Any(r => r.Content.Id == dummy.Id));
            ClassicAssert.IsTrue(handler.ActConn.DestinationAreas.Any(a => a.Content.Id == 20));
        }

        [Test]
        public async Task CreateNewRequestedInterface_AddsDummyAndPreselects()
        {
            InterfaceInternalTestApiConn apiConnection = new() { NewConnectionId = 101 };
            ModellingConnection connection = new() { Id = 6, Name = "Base" };
            ModellingConnectionHandler handler = new(apiConnection, userConfig, Application, [connection], connection, true, false, DisplayMessageInUi, DefaultInit.DoNothing, true)
            {
                DummyAppRole = new ModellingAppRole { Id = 999, Name = "Dummy" },
                RequesterId = 42
            };

            long newId = await handler.CreateNewRequestedInterface(123, true, "NewInterface", "Need it");

            ClassicAssert.AreEqual(101, newId);
            ClassicAssert.IsTrue(handler.ActConn.IsInterface);
            ClassicAssert.IsTrue(handler.ActConn.IsRequested);
            ClassicAssert.IsTrue(handler.ActConn.Name?.Contains("Ticket: 123") ?? false);
            ClassicAssert.IsTrue(handler.ActConn.Reason?.Contains("Need it") ?? false);
            ClassicAssert.IsTrue(handler.ActConn.SourceAppRoles.Any(r => r.Content.Id == 999));
            ClassicAssert.AreEqual(42, handler.ActConn.AppId);
            ClassicAssert.AreEqual(1, handler.PreselectedInterfaces.Count);
            ClassicAssert.AreEqual(42, apiConnection.SelectedAppId);
            ClassicAssert.AreEqual(101, apiConnection.SelectedConnectionId);
        }

        private static ModellingConnectionHandler CreateHandler(ModellingConnection connection, bool addMode,
            Action<Exception?, string, string, bool>? displayMessageInUi = null)
        {
            return new ModellingConnectionHandler(new ModellingHandlerTestApiConn(), userConfig, Application, [connection], connection, addMode, false, displayMessageInUi ?? DisplayMessageInUi, DefaultInit.DoNothing, true);
        }

        private static bool InvokePrivateBool(ModellingConnectionHandler handler, string methodName)
        {
            MethodInfo? method = typeof(ModellingConnectionHandler).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
            ClassicAssert.IsNotNull(method, $"Expected to find method '{methodName}'.");
            return (bool)method!.Invoke(handler, null)!;
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
    }

    internal class InterfaceInternalTestApiConn : SimulatedApiConnection
    {
        public int NewConnectionId { get; set; } = 100;
        public int SelectedAppId { get; private set; }
        public int SelectedConnectionId { get; private set; }

        public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null)
        {
            Type responseType = typeof(QueryResponseType);
            if (query == ModellingQueries.newConnection)
            {
                return Task.FromResult((QueryResponseType)(object)new ReturnIdWrapper
                {
                    ReturnIds = [new ReturnId { NewId = NewConnectionId }]
                });
            }
            if (query == ModellingQueries.addSelectedConnection)
            {
                SelectedAppId = GetIntVariable(variables, "appId");
                SelectedConnectionId = GetIntVariable(variables, "connectionId");
                return Task.FromResult((QueryResponseType)(object)new ReturnIdWrapper
                {
                    ReturnIds = [new ReturnId { InsertedId = SelectedConnectionId }]
                });
            }
            if (query == ModellingQueries.addNwGroupToConnection)
            {
                return Task.FromResult((QueryResponseType)(object)new ReturnId { AffectedRows = 1 });
            }
            if (query == ModellingQueries.addHistoryEntry)
            {
                return Task.FromResult((QueryResponseType)(object)new ReturnIdWrapper
                {
                    ReturnIds = [new ReturnId { AffectedRows = 1 }]
                });
            }

            throw new NotImplementedException();
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
