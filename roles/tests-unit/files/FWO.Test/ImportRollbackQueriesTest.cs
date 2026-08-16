using FWO.Api.Client.Queries;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    internal class ImportRollbackQueriesTest
    {
        [Test]
        public void RollbackImport_TakesImportIdList_AndDeletesImportControl()
        {
            // full rollback is list-based so multiple imports are removed in a single mutation call
            Assert.That(ImportQueries.rollbackImport, Does.Contain("$importIds: [bigint!]!"));
            Assert.That(ImportQueries.rollbackImport, Does.Contain("_in: $importIds"));
            Assert.That(ImportQueries.rollbackImport, Does.Contain("delete_import_control"));
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
