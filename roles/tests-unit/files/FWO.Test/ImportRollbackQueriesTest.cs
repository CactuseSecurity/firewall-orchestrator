using FWO.Api.Client.Queries;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    internal class ImportRollbackQueriesTest
    {
        [Test]
        public void RollbackImportData_IsListBased_AndKeepsImportControl()
        {
            // data-only rollback is list-based and must not touch the import_control row
            Assert.That(ImportQueries.rollbackImportData, Does.Contain("$importIds: [bigint!]!"));
            Assert.That(ImportQueries.rollbackImportData, Does.Contain("_in: $importIds"));
            Assert.That(ImportQueries.rollbackImportData, Does.Not.Contain("delete_import_control"));
        }

        [Test]
        public void DeleteImportControl_IsListBased_AndDeletesImportControl()
        {
            // deleting the import_control rows is split into its own list-based mutation
            Assert.That(ImportQueries.deleteImportControl, Does.Contain("$importIds: [bigint!]!"));
            Assert.That(ImportQueries.deleteImportControl, Does.Contain("delete_import_control"));
            Assert.That(ImportQueries.deleteImportControl, Does.Contain("_in: $importIds"));
        }

        [Test]
        public void GetImportIdsByManagement_SelectsAllImportControlIdsForManagement()
        {
            Assert.That(ImportQueries.getImportIdsByManagement, Does.Contain("$mgmId: Int!"));
            Assert.That(ImportQueries.getImportIdsByManagement, Does.Contain("import_control"));
            Assert.That(ImportQueries.getImportIdsByManagement, Does.Contain("control_id"));
        }
    }
}
