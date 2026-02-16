using NUnit.Framework;
using NUnit.Framework.Legacy;
using FWO.Data;
using FWO.Data.Modelling;
using FWO.Services;
using FWO.Services.Modelling;
using FWO.Basics;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace FWO.Test
{
    [TestFixture]
    [Parallelizable]
    internal class ModellingConnectionHandlerSvcHandlingTest
    {
        static readonly SimulatedUserConfig userConfig = new();
        static readonly Action<Exception?, string, string, bool> DisplayMessageInUi = DefaultInit.DoNothing;
        static readonly FwoOwner Application = new() { Id = 1, Name = "TestApp" };

        [Test]
        public void SyncSvcChanges_AddsAndRemovesServicesAndGroups()
        {
            ModellingConnection connection = new()
            {
                Id = 1,
                Services = [WrapService(1, "SvcOld")],
                ServiceGroups = [WrapServiceGroup(2, "GrpOld")]
            };
            ModellingConnectionHandler handler = CreateHandler(connection);

            handler.SvcToDelete.Add(new ModellingService { Id = 1 });
            handler.SvcGrpToDelete.Add(new ModellingServiceGroup { Id = 2 });
            handler.SvcToAdd.Add(new ModellingService { Id = 3, Name = "SvcNew" });
            handler.SvcGrpToAdd.Add(new ModellingServiceGroup { Id = 4, Name = "GrpNew" });

            InvokePrivateVoid(handler, "SyncSvcChanges");

            ClassicAssert.AreEqual(1, handler.ActConn.Services.Count);
            ClassicAssert.AreEqual(3, handler.ActConn.Services[0].Content.Id);
            ClassicAssert.AreEqual(1, handler.ActConn.ServiceGroups.Count);
            ClassicAssert.AreEqual(4, handler.ActConn.ServiceGroups[0].Content.Id);
        }

        [Test]
        public void ServicesToConn_AddsWhenNotExisting()
        {
            ModellingConnection connection = new()
            {
                Id = 2,
                Services = [WrapService(1, "SvcExisting")]
            };
            ModellingConnectionHandler handler = CreateHandler(connection);

            handler.ServicesToConn(
            [
                new ModellingService { Id = 1, Name = "SvcExisting" },
                new ModellingService { Id = 2, Name = "SvcNew" }
            ]);

            ClassicAssert.AreEqual(1, handler.SvcToAdd.Count);
            ClassicAssert.AreEqual(2, handler.SvcToAdd[0].Id);
        }

        [Test]
        public void ServiceGrpsToConn_AddsWhenNotExisting()
        {
            ModellingConnection connection = new()
            {
                Id = 3,
                ServiceGroups = [WrapServiceGroup(1, "GrpExisting")]
            };
            ModellingConnectionHandler handler = CreateHandler(connection);

            handler.ServiceGrpsToConn(
            [
                new ModellingServiceGroup { Id = 1, Name = "GrpExisting" },
                new ModellingServiceGroup { Id = 2, Name = "GrpNew" }
            ]);

            ClassicAssert.AreEqual(1, handler.SvcGrpToAdd.Count);
            ClassicAssert.AreEqual(2, handler.SvcGrpToAdd[0].Id);
        }

        private static ModellingConnectionHandler CreateHandler(ModellingConnection connection)
        {
            return new ModellingConnectionHandler(new ModellingHandlerTestApiConn(), userConfig, Application, [connection], connection, false, false, DisplayMessageInUi, DefaultInit.DoNothing, true);
        }

        private static void InvokePrivateVoid(ModellingConnectionHandler handler, string methodName)
        {
            MethodInfo? method = typeof(ModellingConnectionHandler).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
            ClassicAssert.IsNotNull(method, $"Expected to find method '{methodName}'.");
            method!.Invoke(handler, null);
        }

        private static ModellingServiceWrapper WrapService(int id, string name)
        {
            return new ModellingServiceWrapper { Content = new ModellingService { Id = id, Name = name } };
        }

        private static ModellingServiceGroupWrapper WrapServiceGroup(int id, string name)
        {
            return new ModellingServiceGroupWrapper { Content = new ModellingServiceGroup { Id = id, Name = name } };
        }
    }
}
