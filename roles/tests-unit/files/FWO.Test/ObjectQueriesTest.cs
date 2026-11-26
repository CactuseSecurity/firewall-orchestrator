using FWO.Api.Client.Queries;
using NUnit.Framework;
using System.Text.RegularExpressions;

namespace FWO.Test
{
    /// <summary>
    /// Tests the object queries. They are only accessed in test bodies: loading them during test discovery (e.g. in a
    /// test case source) would happen before the <see cref="TestInitializer"/> points FWO_BASE_DIR at the repository.
    /// </summary>
    [TestFixture]
    [Parallelizable]
    internal class ObjectQueriesTest
    {
        private const string kQueryKeyword = "query ";
        private const string kRemovedAfterStartFilter = "removed: { _gt: $import_id_start }";
        private const string kRemovedAtStartPattern = @"removed:\s*\{\s*_gte:";

        [Test]
        public void GetAllObjectDetails_ShouldExcludeVersionsRemovedByStartImport()
        {
            AssertVersionsRemovedByStartImportAreExcluded(ObjectQueries.getAllObjectDetails, 3);
        }

        [Test]
        public void GetNetworkObjectDetails_ShouldExcludeVersionsRemovedByStartImport()
        {
            AssertVersionsRemovedByStartImportAreExcluded(ObjectQueries.getNetworkObjectDetails, 1);
        }

        [Test]
        public void GetNetworkServiceDetails_ShouldExcludeVersionsRemovedByStartImport()
        {
            AssertVersionsRemovedByStartImportAreExcluded(ObjectQueries.getNetworkServiceDetails, 1);
        }

        [Test]
        public void GetUserDetails_ShouldExcludeVersionsRemovedByStartImport()
        {
            AssertVersionsRemovedByStartImportAreExcluded(ObjectQueries.getUserDetails, 1);
        }

        /// <summary>
        /// An import replacing an object removes the old version and creates the new one with the same import id.
        /// Versions removed by the start import must therefore be excluded, or both versions are returned (#4730).
        /// </summary>
        private static void AssertVersionsRemovedByStartImportAreExcluded(string query, int expectedFilterCount)
        {
            string operation = ExtractOperation(query);

            Assert.That(operation, Does.Not.Match(kRemovedAtStartPattern));
            Assert.That(Regex.Count(operation, Regex.Escape(kRemovedAfterStartFilter)), Is.EqualTo(expectedFilterCount));
        }

        /// <summary>
        /// Returns the query without the fragments prepended to it, which contain filters of their own.
        /// </summary>
        private static string ExtractOperation(string query)
        {
            int operationStart = query.IndexOf(kQueryKeyword, StringComparison.Ordinal);
            Assert.That(operationStart, Is.GreaterThanOrEqualTo(0), "No query operation found.");
            return query[operationStart..];
        }
    }
}
