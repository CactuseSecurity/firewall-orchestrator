using FWO.Api.Client.Queries;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    internal class ImportRollbackQueriesTest
    {
        [Test]
        public void RollbackImport_IsListBased_AndDeletesImportControl()
        {
            // the ui rollback is list based so multiple imports are rolled back in one call
            Assert.That(ImportQueries.rollbackImport, Does.Contain("$importIds: [bigint!]!"));
            Assert.That(ImportQueries.rollbackImport, Does.Contain("_in: $importIds"));
            Assert.That(ImportQueries.rollbackImport, Does.Contain("delete_import_control"));
        }

        [Test]
        public void RollbackImport_IsASingleDocument_SoTheRollbackStaysAtomic()
        {
            // data rollback and import_control deletion have to travel in one mutation document,
            // otherwise a failure in between leaves imported data deleted but the records behind
            Assert.That(ImportQueries.rollbackImport, Does.Contain("...rollbackImportDataFields"));
            Assert.That(ImportQueries.rollbackImport, Does.Contain("fragment rollbackImportDataFields on mutation_root"));
            Assert.That(CountOccurrences(ImportQueries.rollbackImport, "mutation "), Is.EqualTo(1));
        }

        [Test]
        public void RollbackImportDataFragment_CarriesTheDataStatements_WithoutDeletingImportControl()
        {
            // the importer reuses the same fragment but must keep the import_control row
            string fragment = Queries.Compact(" " + File.ReadAllText(
                Path.Combine(QueryBasePath, "import", "fragments", "rollbackImportDataFields.graphql")) + " ");

            Assert.That(fragment, Does.Contain("fragment rollbackImportDataFields on mutation_root"));
            Assert.That(fragment, Does.Not.Contain("delete_import_control"));
            Assert.That(fragment, Does.Contain("_in: $importIds"));
        }

        private static string QueryBasePath =>
            Path.Combine(Environment.GetEnvironmentVariable("FWO_BASE_DIR") ?? "", "fwo-api-calls");

        private static int CountOccurrences(string text, string value)
        {
            int count = 0;
            for (int index = text.IndexOf(value, StringComparison.Ordinal); index >= 0;
                 index = text.IndexOf(value, index + value.Length, StringComparison.Ordinal))
            {
                count++;
            }
            return count;
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
