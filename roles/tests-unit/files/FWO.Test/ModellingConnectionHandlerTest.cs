using NUnit.Framework;
using NUnit.Framework.Legacy;
using FWO.Data;
using FWO.Data.Modelling;
using FWO.Services;
using FWO.Basics;
using System.Collections.Generic;
using System.Linq;

namespace FWO.Test
{
    [TestFixture]
    [Parallelizable]
    internal class ModellingConnectionHandlerTest
    {
        static readonly SimulatedUserConfig userConfig = new()
        {
            ModNamingConvention = "{\"networkAreaRequired\":true,\"fixedPartLength\":4,\"freePartLength\":5,\"networkAreaPattern\":\"NA\",\"appRolePattern\":\"AR\"}"
        };
        static readonly ModellingHandlerTestApiConn apiConnection = new();
        static readonly Action<Exception?, string, string, bool> DisplayMessageInUi = DefaultInit.DoNothing;
        static readonly FwoOwner Application = new() { Id = 1, Name = "TestApp" };

        static readonly ModellingNetworkArea Area1 = new() { Id = 1, Name = "Area1", IdString = "NA01" };
        static readonly ModellingNetworkArea Area2 = new() { Id = 2, Name = "Area2", IdString = "NA02" };
        static readonly ModellingNetworkArea Area3 = new() { Id = 3, Name = "Area3", IdString = "NA03" };

        static readonly ModellingAppRole Role1 = new() { Id = 10, Name = "Role1", IdString = "AR01" };
        static readonly ModellingAppRole Role2 = new() { Id = 11, Name = "Role2", IdString = "AR02" };
        static readonly ModellingAppRole Role3 = new() { Id = 12, Name = "Role3", IdString = "AR03" };

        [SetUp]
        public void Initialize()
        {
        }

        [Test]
        public async Task TestComSvcContainsOnlyNetworkAreasInDirection_ItemsMarkedForDeletionAreProperlyExcluded()
        {
            // ARRANGE
            ModellingConnection connection = new()
            {
                Id = 1,
                Name = "TestConnection",
                IsCommonService = true,
                SourceAreas = [new() { Content = Area1 }, new() { Content = Area2 }],
                SourceAppRoles = [new() { Content = Role1 }]
            };

            List<ModellingConnection> connections = [connection];
            ModellingConnectionHandler handler = new(
                apiConnection,
                userConfig,
                Application,
                connections,
                connection,
                false,
                false,
                DisplayMessageInUi,
                DefaultInit.DoNothing,
                true
            );

            await handler.Init();

            handler.SrcAreasToDelete.Add(Area2);
            handler.SrcAppRolesToDelete.Add(Role1);

            // ACT
            bool result = handler.ComSvcContainsOnlyNetworkAreasInDirection(Direction.Source, new List<ModellingNetworkArea>());

            // ASSERT
            ClassicAssert.AreEqual(true, result, "Items marked for deletion should be properly excluded from validation");
        }

        [Test]
        public async Task TestComSvcContainsOnlyNetworkAreasInDirection_ReturnsTrue_WhenOnlyAreasRemainAfterDeletion()
        {
            // ARRANGE
            ModellingConnection connection = new()
            {
                Id = 2,
                Name = "TestConnection2",
                IsCommonService = true,
                SourceAreas = [new() { Content = Area1 }, new() { Content = Area2 }],
                SourceAppRoles = [new() { Content = Role1 }, new() { Content = Role2 }]
            };

            List<ModellingConnection> connections = [connection];

            ModellingConnectionHandler handler = new(
                apiConnection,
                userConfig,
                Application,
                connections,
                connection,
                false,
                false,
                DisplayMessageInUi,
                DefaultInit.DoNothing,
                true
            );

            await handler.Init();

            handler.SrcAppRolesToDelete.Add(Role1);
            handler.SrcAppRolesToDelete.Add(Role2);

            // ACT
            bool result = handler.ComSvcContainsOnlyNetworkAreasInDirection(Direction.Source, new List<ModellingNetworkArea>());

            // ASSERT
            ClassicAssert.AreEqual(true, result, "Method should return true when only network areas remain after deletion");
        }

        [Test]
        public async Task TestComSvcContainsOnlyNetworkAreasInDirection_ReturnsFalse_WhenBothAreasAndRolesRemainAfterDeletion()
        {
            // ARRANGE
            ModellingConnection connection = new()
            {
                Id = 3,
                Name = "TestConnection3",
                IsCommonService = true,
                DestinationAreas = [new() { Content = Area1 }, new() { Content = Area2 }],
                DestinationAppRoles = [new() { Content = Role1 }, new() { Content = Role2 }]
            };

            List<ModellingConnection> connections = [connection];

            ModellingConnectionHandler handler = new(
                apiConnection,
                userConfig,
                Application,
                connections,
                connection,
                false,
                false,
                DisplayMessageInUi,
                DefaultInit.DoNothing,
                true
            );

            await handler.Init();

            handler.DstAppRolesToDelete.Add(Role1);

            // ACT
            bool result = handler.ComSvcContainsOnlyNetworkAreasInDirection(Direction.Destination, new List<ModellingNetworkArea>());

            // ASSERT
            ClassicAssert.AreEqual(false, result, "Method should return false when both areas and roles remain after deletion");
        }

        [Test]
        public async Task TestComSvcContainsOnlyNetworkAreasInDirection_WithNewItemsToAdd_ReturnsTrue()
        {
            // ARRANGE
            ModellingConnection connection = new()
            {
                Id = 4,
                Name = "TestConnection4",
                IsCommonService = true,
                SourceAreas = [],
                SourceAppRoles = []
            };

            List<ModellingConnection> connections = [connection];

            ModellingConnectionHandler handler = new(
                apiConnection,
                userConfig,
                Application,
                connections,
                connection,
                false,
                false,
                DisplayMessageInUi,
                DefaultInit.DoNothing,
                true
            );

            await handler.Init();

            handler.SrcAreasToAdd.Add(Area1);
            handler.SrcAreasToAdd.Add(Area2);

            // ACT
            bool result = handler.ComSvcContainsOnlyNetworkAreasInDirection(Direction.Source, new List<ModellingNetworkArea>());

            // ASSERT
            ClassicAssert.AreEqual(true, result, "Method should return true when only areas are added");
        }

        [Test]
        public async Task TestComSvcContainsOnlyNetworkAreasInDirection_WithNewItemsToAdd_ReturnsFalse()
        {
            // ARRANGE
            ModellingConnection connection = new()
            {
                Id = 5,
                Name = "TestConnection5",
                IsCommonService = true,
                SourceAreas = [],
                SourceAppRoles = []
            };

            List<ModellingConnection> connections = [connection];

            ModellingConnectionHandler handler = new(
                apiConnection,
                userConfig,
                Application,
                connections,
                connection,
                false,
                false,
                DisplayMessageInUi,
                DefaultInit.DoNothing,
                true
            );

            await handler.Init();

            handler.SrcAreasToAdd.Add(Area1);
            handler.SrcAppRolesToAdd.Add(Role1);

            // ACT
            bool result = handler.ComSvcContainsOnlyNetworkAreasInDirection(Direction.Source, new List<ModellingNetworkArea>());

            // ASSERT
            ClassicAssert.AreEqual(false, result, "Method should return false when both areas and roles are added");
        }

        [Test]
        public async Task TestComSvcContainsOnlyNetworkAreasInDirection_WithInitialAreasParameter_ReturnsTrue()
        {
            // ARRANGE
            ModellingConnection connection = new()
            {
                Id = 6,
                Name = "TestConnection6",
                IsCommonService = true,
                SourceAreas = [],
                SourceAppRoles = []
            };

            List<ModellingConnection> connections = [connection];

            ModellingConnectionHandler handler = new(
                apiConnection,
                userConfig,
                Application,
                connections,
                connection,
                false,
                false,
                DisplayMessageInUi,
                DefaultInit.DoNothing,
                true
            );

            await handler.Init();

            List<ModellingNetworkArea> selectedAreas = [Area1, Area2];

            // ACT
            bool result = handler.ComSvcContainsOnlyNetworkAreasInDirection(Direction.Source, selectedAreas);

            // ASSERT
            ClassicAssert.AreEqual(true, result, "Method should return true when only network areas are provided via parameter");
        }

        [Test]
        public async Task TestComSvcContainsOnlyNetworkAreasInDirection_WithInitialRolesParameter_ReturnsFalse()
        {
            // ARRANGE
            ModellingConnection connection = new()
            {
                Id = 7,
                Name = "TestConnection7",
                IsCommonService = true,
                SourceAreas = [new() { Content = Area1 }],
                SourceAppRoles = []
            };

            List<ModellingConnection> connections = [connection];

            ModellingConnectionHandler handler = new(
                apiConnection,
                userConfig,
                Application,
                connections,
                connection,
                false,
                false,
                DisplayMessageInUi,
                DefaultInit.DoNothing,
                true
            );

            await handler.Init();

            List<ModellingAppRole> selectedRoles = [Role1];

            // ACT
            bool result = handler.ComSvcContainsOnlyNetworkAreasInDirection(Direction.Source, selectedRoles);

            // ASSERT
            ClassicAssert.AreEqual(false, result, "Method should return false when roles are added to existing areas");
        }

        [Test]
        public async Task TestComSvcContainsOnlyNetworkAreasInDirection_EmptyAfterDeletion_ReturnsTrue()
        {
            // ARRANGE
            ModellingConnection connection = new()
            {
                Id = 8,
                Name = "TestConnection8",
                IsCommonService = true,
                DestinationAreas = [new() { Content = Area1 }],
                DestinationAppRoles = [new() { Content = Role1 }]
            };

            List<ModellingConnection> connections = [connection];

            ModellingConnectionHandler handler = new(
                apiConnection,
                userConfig,
                Application,
                connections,
                connection,
                false,
                false,
                DisplayMessageInUi,
                DefaultInit.DoNothing,
                true
            );

            await handler.Init();

            handler.DstAreasToDelete.Add(Area1);
            handler.DstAppRolesToDelete.Add(Role1);

            // ACT
            bool result = handler.ComSvcContainsOnlyNetworkAreasInDirection(Direction.Destination, new List<ModellingNetworkArea>());

            // ASSERT
            ClassicAssert.AreEqual(true, result, "Method should return true when both lists are empty after deletion");
        }
    }
}