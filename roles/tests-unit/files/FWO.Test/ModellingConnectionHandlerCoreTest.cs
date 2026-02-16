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

        private static ModellingNetworkAreaWrapper WrapArea(long id, string name)
        {
            return new ModellingNetworkAreaWrapper { Content = new ModellingNetworkArea { Id = id, Name = name, IdString = $"NA{id}" } };
        }

        private static ModellingServiceWrapper WrapService(int id, string name)
        {
            return new ModellingServiceWrapper { Content = new ModellingService { Id = id, Name = name } };
        }
    }
}
