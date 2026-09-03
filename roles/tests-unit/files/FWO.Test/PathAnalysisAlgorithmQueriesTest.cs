using System.Reflection;
using System.Runtime.Loader;
using FWO.Api.Client.Queries;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    [NonParallelizable]
    internal class PathAnalysisAlgorithmQueriesTest
    {
        private const string kBaseDirectoryEnvironmentVariable = "FWO_BASE_DIR";
        private const string kQueryTypeName =
            "FWO.Api.Client.Queries.PathAnalysisAlgorithmQueries";
        private const string kQueryFieldName =
            "getPathAnalysisAlgorithms";

        /// <summary>
        /// Verifies that a missing GraphQL file is logged and rethrown
        /// by the static query initialization.
        /// </summary>
        [Test]
        public void Initialization_WhenQueryFileIsMissing_RethrowsException()
        {
            string? originalBaseDirectory =
                Environment.GetEnvironmentVariable(
                    kBaseDirectoryEnvironmentVariable);

            string temporaryBaseDirectory = Path.Combine(
                TestContext.CurrentContext.WorkDirectory,
                $"{nameof(PathAnalysisAlgorithmQueriesTest)}-{Guid.NewGuid():N}");

            string pathAnalysisDirectory = Path.Combine(
                temporaryBaseDirectory,
                "fwo-api-calls",
                "path_analysis");

            Directory.CreateDirectory(pathAnalysisDirectory);

            AssemblyLoadContext loadContext = new(
                nameof(PathAnalysisAlgorithmQueriesTest),
                isCollectible: true);

            try
            {
                Environment.SetEnvironmentVariable(
                    kBaseDirectoryEnvironmentVariable,
                    temporaryBaseDirectory);

                Assembly isolatedAssembly =
                    loadContext.LoadFromAssemblyPath(
                        typeof(PathAnalysisAlgorithmQueries).Assembly.Location);

                Type queryType = isolatedAssembly.GetType(
                    kQueryTypeName,
                    throwOnError: true)!;

                FieldInfo queryField = queryType.GetField(
                    kQueryFieldName,
                    BindingFlags.Public | BindingFlags.Static)
                    ?? throw new MissingFieldException(
                        kQueryTypeName,
                        kQueryFieldName);

                TypeInitializationException exception =
                    Assert.Throws<TypeInitializationException>(
                        () => queryField.GetValue(null))!;

                Assert.That(
                    exception.InnerException,
                    Is.TypeOf<FileNotFoundException>());
            }
            finally
            {
                Environment.SetEnvironmentVariable(
                    kBaseDirectoryEnvironmentVariable,
                    originalBaseDirectory);

                loadContext.Unload();

                if (Directory.Exists(temporaryBaseDirectory))
                {
                    Directory.Delete(
                        temporaryBaseDirectory,
                        recursive: true);
                }
            }
        }
    }
}