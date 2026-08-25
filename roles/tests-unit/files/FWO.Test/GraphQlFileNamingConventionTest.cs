using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    [NonParallelizable]
    internal class GraphQlFileNamingConventionTest
    {
        private static readonly HashSet<string> kKnownInconsistentFiles =
        [
            "allObjects/deleteOldObjectsCascading.graphql",
            "auth/deleteLdapConnection.graphql",
            "auth/deleteTenant.graphql",
            "auth/deleteUser.graphql",
            "auth/getLdapConnectionsSubscription.graphql",
            "auth/getTenantVisibleDeviceIds.graphql",
            "auth/getTenantVisibleManagementIds.graphql",
            "compliance/getViolationCount.graphql",
            "compliance/getViolationsByRuleUid.graphql",
            "compliance/updateViolationById.graphql",
            "config/deleteCustomText.graphql",
            "config/getTexts.graphql",
            "device/deleteCredential.graphql",
            "device/deleteDevice.graphql",
            "device/deleteManagement.graphql",
            "device/fragments/importCredentials.graphql",
            "device/fragments/importCredentialsWithoutSecrets.graphql",
            "device/fragments/managementDetails.graphql",
            "device/fragments/managementDetailsWithoutSecrets.graphql",
            "device/getManagementDetailsWithoutSecrets.graphql",
            "device/getManagementsDetails.graphql",
            "device/getSingleManagementDetails.graphql",
            "device/newManagement.graphql",
            "device/updateGatewayUid.graphql",
            "import/deleteImport.graphql",
            "modelling/addNwAppZone.graphql",
            "networking/getAllNetworkInfosTable.graphql",
            "recertification/refreshViewRuleWithOwner.graphql",
            "report/addReportScheduleFileFormats.graphql",
            "report/getAllObjectDetailsInReport.graphql",
            "report/getReportById.graphql",
            "report/getReportsOverview.graphql",
            "rule/fragments/natRuleDetailsForReport.graphql",
            "rule/getNatRuleOverview.graphql",
            "rule/getRuleDetailByID.graphql",
            "rule/getRuleIdsByRuleOwner.graphql",
            "rule/getRuleOverview.graphql",
            "rule/getRulesForSelectedManagements.graphql",
            "rule/insertRulebaseLinks.graphql",
            "rule_metadata/updateLastHits.graphql"
        ];

        /// <summary>
        /// Enumerates GraphQL files below the shared API calls directory.
        /// </summary>
        private static IEnumerable<TestCaseData> GraphQlFiles()
        {
            string graphQlRoot = FindGraphQlRootDirectory();

            foreach (string filePath in Directory.EnumerateFiles(graphQlRoot, "*.graphql", SearchOption.AllDirectories))
            {
                string relativePath = Path.GetRelativePath(graphQlRoot, filePath).Replace('\\', '/');
                yield return new TestCaseData(filePath).SetName($"GraphQlFile_{relativePath}_UsesMatchingOperationName");
            }
        }

        [TestCaseSource(nameof(GraphQlFiles))]
        public void GraphQlFile_UsesQueryMutationOrFragmentNameMatchingFileName(string filePath)
        {
            string graphQlRoot = FindGraphQlRootDirectory();
            string relativePath = Path.GetRelativePath(graphQlRoot, filePath).Replace('\\', '/');
            string fileName = Path.GetFileNameWithoutExtension(filePath);
            string fileContent = File.ReadAllText(filePath);
            string escapedFileName = Regex.Escape(fileName);
            string pattern = $@"\b(query|mutation|subscription|fragment)\s+{escapedFileName}\b";
            bool hasMatchingOperationName = Regex.IsMatch(fileContent, pattern);

            if (kKnownInconsistentFiles.Contains(relativePath))
            {
                Assert.That(
                    hasMatchingOperationName,
                    Is.False,
                    $"'{relativePath}' now follows the naming convention and should be removed from the ignore list.");
                return;
            }

            Assert.That(
                hasMatchingOperationName,
                Is.True,
                $"Expected '{relativePath}' to contain 'query {fileName}', 'mutation {fileName}', 'subscription {fileName}', or 'fragment {fileName}'.");
        }

        /// <summary>
        /// Resolves the repository or installed path to the shared GraphQL API call files.
        /// </summary>
        private static string FindGraphQlRootDirectory()
        {
            DirectoryInfo? currentDirectory = new(AppContext.BaseDirectory);

            while (currentDirectory is not null)
            {
                string repositoryPath = Path.Combine(currentDirectory.FullName, "roles", "common", "files", "fwo-api-calls");
                if (Directory.Exists(repositoryPath))
                {
                    return repositoryPath;
                }

                string installedPath = Path.Combine(currentDirectory.FullName, "fwo-api-calls");
                if (Directory.Exists(installedPath))
                {
                    return installedPath;
                }

                currentDirectory = currentDirectory.Parent;
            }

            Assert.Fail("Could not locate the repository or installed fwo-api-calls directory from the test output directory.");
            return string.Empty;
        }
    }
}
