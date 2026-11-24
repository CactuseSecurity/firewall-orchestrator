using FWO.Data.Modelling;
using FWO.Services.Modelling;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    internal class ModellingVarianceConnectionSelectorTest
    {
        private static ModellingConnection CreateConnection(int id, bool deletedAppServer = false)
        {
            ModellingAppServer appServer = new() { Id = id, Name = $"Server{id}", Ip = "10.0.0.1/32", IsDeleted = deletedAppServer };
            ModellingAppRole appRole = new() { Id = id, Name = $"AR{id}", AppServers = [new() { Content = appServer }] };
            return new() { Id = id, Name = $"Conn{id}", SourceAppRoles = [new() { Content = appRole }] };
        }

        [Test]
        public void Select_SplitsAnalysedAndExcludedConnections()
        {
            ModellingConnection clean = CreateConnection(1);
            ModellingConnection withDeletedAppServer = CreateConnection(2, true);
            List<ModellingConnection> connections = [clean, withDeletedAppServer];

            ModellingVarianceConnectionSelection selection = ModellingVarianceConnectionSelector.Select(connections, 0, false);

            Assert.Multiple(() =>
            {
                Assert.That(selection.Analysed, Has.Count.EqualTo(1));
                Assert.That(selection.Analysed[0], Is.SameAs(clean));
                Assert.That(selection.Excluded, Has.Count.EqualTo(1));
                Assert.That(selection.Excluded[0].Connection, Is.SameAs(withDeletedAppServer));
                Assert.That(selection.Excluded[0].Reason, Is.EqualTo(ConState.DeletedObjects));
                Assert.That(selection.Excluded[0].ReasonTextKey, Is.EqualTo("C9014"));
                Assert.That(selection.IsExcluded(withDeletedAppServer), Is.True);
                Assert.That(selection.IsExcluded(clean), Is.False);
            });
        }

        [Test]
        public void Select_AnalysesDeletedAppServersWithRolloutRemoved()
        {
            ModellingConnection withDeletedAppServer = CreateConnection(2, true);
            List<ModellingConnection> connections = [withDeletedAppServer];

            ModellingVarianceConnectionSelection selection = ModellingVarianceConnectionSelector.Select(connections, 0, true);

            Assert.Multiple(() =>
            {
                Assert.That(selection.Analysed, Has.Count.EqualTo(1));
                Assert.That(selection.Excluded, Is.Empty);
            });
        }

        [Test]
        public void Select_LeavesInterfacesOutOfBothLists()
        {
            ModellingConnection interfaceConnection = CreateConnection(3);
            interfaceConnection.IsInterface = true;
            interfaceConnection.IsPublished = true;
            List<ModellingConnection> connections = [interfaceConnection];

            ModellingVarianceConnectionSelection selection = ModellingVarianceConnectionSelector.Select(connections, 0, false);

            Assert.Multiple(() =>
            {
                Assert.That(selection.Analysed, Is.Empty);
                Assert.That(selection.Excluded, Is.Empty);
            });
        }

        [Test]
        public void Select_AgreesWithRequestSelection()
        {
            ModellingConnection clean = CreateConnection(1);
            ModellingConnection withDeletedAppServer = CreateConnection(2, true);
            ModellingConnection rejectedInterfaceUser = CreateConnection(4);
            rejectedInterfaceUser.AddProperty(ConState.InterfaceRejected.ToString());
            List<ModellingConnection> connections = [clean, withDeletedAppServer, rejectedInterfaceUser];

            foreach (bool rolloutRemoved in RolloutRemovedValues)
            {
                SimulatedUserConfig userConfig = new() { ModRolloutRemovedAppServers = rolloutRemoved };
                List<int> varianceIds = [.. ModellingVarianceConnectionSelector.Select(connections, 0, rolloutRemoved).Analysed.Select(c => c.Id)];
                List<int> requestIds = [.. ModellingRequestConnectionSelector.ForRegularRequests(connections, userConfig, 0).Select(c => c.Id)];

                Assert.That(requestIds, Is.EquivalentTo(varianceIds), $"rolloutRemoved = {rolloutRemoved}");
            }
        }

        private static readonly List<bool> RolloutRemovedValues = [false, true];
    }
}
