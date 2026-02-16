using NUnit.Framework;
using NUnit.Framework.Legacy;
using FWO.Data;
using FWO.Data.Modelling;
using FWO.Services;
using FWO.Services.Modelling;
using FWO.Basics;
using FWO.Api.Client.Queries;
using System.Reflection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FWO.Test
{
    [TestFixture]
    [Parallelizable]
    internal class ModellingConnectionHandlerPermissionHandlingTest
    {
        static readonly SimulatedUserConfig userConfig = new();
        static readonly Action<Exception?, string, string, bool> DisplayMessageInUi = DefaultInit.DoNothing;
        static readonly FwoOwner Application = new() { Id = 1, Name = "TestApp" };

        [Test]
        public async Task EnsureUsingAppsPermittedIfRestricted_AddsMissingOwners()
        {
            ModellingConnectionHandlerPermissionTestApiConn apiConnection = new();
            apiConnection.InterfaceUsers =
            [
                new() { AppId = 1 },
                new() { AppId = 2 },
                new() { AppId = 3 },
                new() { AppId = 3 },
                new() { AppId = null }
            ];

            FwoOwner owner1 = new() { Id = 1, Name = "Owner1" };
            FwoOwner owner2 = new() { Id = 2, Name = "Owner2" };
            FwoOwner owner3 = new() { Id = 3, Name = "Owner3" };

            ModellingConnection connection = new()
            {
                Id = 10,
                IsInterface = true,
                InterfacePermission = InterfacePermissions.Public.ToString(),
                PermittedOwners = [owner1]
            };

            ModellingConnectionHandler handler = CreateHandler(apiConnection, connection);
            handler.AllApps = [owner1, owner2, owner3];
            handler.PermittedOwnersToAdd.Add(owner2);
            handler.ActConn.InterfacePermission = InterfacePermissions.Restricted.ToString();

            await InvokePrivateAsync(handler, "EnsureUsingAppsPermittedIfRestricted");

            ClassicAssert.IsTrue(handler.PermittedOwnersToAdd.Any(o => o.Id == 3));
            ClassicAssert.IsTrue(handler.ActConn.PermittedOwners.Any(o => o.Id == 3));
            ClassicAssert.IsFalse(handler.ActConn.PermittedOwners.Any(o => o.Id == 2));
            ClassicAssert.AreEqual(2, handler.PermittedOwnersToAdd.Count);
        }

        [Test]
        public async Task ApplyPermittedOwnersOnInsert_NotRestrictedClearsLists()
        {
            ModellingConnectionHandlerPermissionTestApiConn apiConnection = new();
            ModellingConnection connection = new()
            {
                Id = 11,
                IsInterface = true,
                InterfacePermission = InterfacePermissions.Public.ToString()
            };

            ModellingConnectionHandler handler = CreateHandler(apiConnection, connection);
            handler.PermittedOwnersToAdd.Add(new() { Id = 1 });
            handler.PermittedOwnersToDelete.Add(new() { Id = 2 });

            await InvokePrivateAsync(handler, "ApplyPermittedOwnersOnInsert");

            ClassicAssert.AreEqual(0, handler.PermittedOwnersToAdd.Count);
            ClassicAssert.AreEqual(0, handler.PermittedOwnersToDelete.Count);
            ClassicAssert.AreEqual(0, apiConnection.AddedOwners.Count);
        }

        [Test]
        public async Task ApplyPermittedOwnersOnUpdate_NotRestrictedRemovesAllAndClears()
        {
            ModellingConnectionHandlerPermissionTestApiConn apiConnection = new();
            apiConnection.ExistingPermittedOwners =
            [
                new() { Id = 1 },
                new() { Id = 3 }
            ];

            ModellingConnection connection = new()
            {
                Id = 12,
                IsInterface = true,
                InterfacePermission = InterfacePermissions.Public.ToString(),
                PermittedOwners = [new() { Id = 1 }, new() { Id = 2 }]
            };

            ModellingConnectionHandler handler = CreateHandler(apiConnection, connection);
            handler.PermittedOwnersToAdd.Add(new() { Id = 4 });
            handler.PermittedOwnersToDelete.Add(new() { Id = 5 });

            await InvokePrivateAsync(handler, "ApplyPermittedOwnersOnUpdate");

            ClassicAssert.AreEqual(0, handler.ActConn.PermittedOwners.Count);
            ClassicAssert.AreEqual(0, handler.PermittedOwnersToAdd.Count);
            ClassicAssert.AreEqual(0, handler.PermittedOwnersToDelete.Count);
            ClassicAssert.AreEqual(0, apiConnection.AddedOwners.Count);
            ClassicAssert.AreEqual(2, apiConnection.DeletedOwners.Count);
            ClassicAssert.IsTrue(apiConnection.DeletedOwners.Contains(1));
            ClassicAssert.IsTrue(apiConnection.DeletedOwners.Contains(3));
        }

        [Test]
        public async Task ApplyPermittedOwnersOnUpdate_RestrictedAddsAndRemoves()
        {
            ModellingConnectionHandlerPermissionTestApiConn apiConnection = new();
            ModellingConnection connection = new()
            {
                Id = 13,
                IsInterface = true,
                InterfacePermission = InterfacePermissions.Restricted.ToString()
            };

            ModellingConnectionHandler handler = CreateHandler(apiConnection, connection);
            handler.PermittedOwnersToAdd.Add(new() { Id = 1 });
            handler.PermittedOwnersToAdd.Add(new() { Id = 2 });
            handler.PermittedOwnersToDelete.Add(new() { Id = 3 });

            await InvokePrivateAsync(handler, "ApplyPermittedOwnersOnUpdate");

            ClassicAssert.AreEqual(2, apiConnection.AddedOwners.Count);
            ClassicAssert.IsTrue(apiConnection.AddedOwners.Contains(1));
            ClassicAssert.IsTrue(apiConnection.AddedOwners.Contains(2));
            ClassicAssert.AreEqual(1, apiConnection.DeletedOwners.Count);
            ClassicAssert.IsTrue(apiConnection.DeletedOwners.Contains(3));
        }

        private static ModellingConnectionHandler CreateHandler(ModellingConnectionHandlerPermissionTestApiConn apiConnection, ModellingConnection connection)
        {
            return new ModellingConnectionHandler(apiConnection, userConfig, Application, [connection], connection, false, false, DisplayMessageInUi, DefaultInit.DoNothing, true);
        }

        private static async Task InvokePrivateAsync(ModellingConnectionHandler handler, string methodName)
        {
            MethodInfo? method = typeof(ModellingConnectionHandler).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
            ClassicAssert.IsNotNull(method, $"Expected to find method '{methodName}'.");
            Task task = (Task)method!.Invoke(handler, null)!;
            await task;
        }
    }

    internal class ModellingConnectionHandlerPermissionTestApiConn : SimulatedApiConnection
    {
        public List<int> AddedOwners { get; } = [];
        public List<int> DeletedOwners { get; } = [];
        public List<FwoOwner> ExistingPermittedOwners { get; set; } = [];
        public List<ModellingConnection> InterfaceUsers { get; set; } = [];

        public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null)
        {
            Type responseType = typeof(QueryResponseType);
            if (query == ModellingQueries.getInterfaceUsers && responseType == typeof(List<ModellingConnection>))
            {
                return Task.FromResult((QueryResponseType)(object)InterfaceUsers);
            }
            if (query == ModellingQueries.getPermittedOwnersForConnection && responseType == typeof(List<FwoOwner>))
            {
                return Task.FromResult((QueryResponseType)(object)ExistingPermittedOwners);
            }
            if (query == ModellingQueries.addPermittedOwner)
            {
                int appId = GetIntVariable(variables, "appId");
                AddedOwners.Add(appId);
                return Task.FromResult((QueryResponseType)CreateResponse(responseType, appId));
            }
            if (query == ModellingQueries.deletePermittedOwner)
            {
                int appId = GetIntVariable(variables, "appId");
                DeletedOwners.Add(appId);
                return Task.FromResult((QueryResponseType)CreateResponse(responseType, appId));
            }
            if (query == ModellingQueries.addHistoryEntry)
            {
                return Task.FromResult((QueryResponseType)CreateResponse(responseType, 0));
            }

            throw new NotImplementedException();
        }

        private static object CreateResponse(Type responseType, int appId)
        {
            if (responseType == typeof(ReturnId))
            {
                return new ReturnId { AffectedRows = 1, InsertedId = appId, InsertedIdLong = appId };
            }
            if (responseType == typeof(ReturnIdWrapper))
            {
                return new ReturnIdWrapper { ReturnIds = [new ReturnId { AffectedRows = 1, InsertedId = appId, InsertedIdLong = appId }] };
            }
            if (responseType == typeof(FwoOwner))
            {
                return new FwoOwner { Id = appId };
            }
            return Activator.CreateInstance(responseType) ?? new object();
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
