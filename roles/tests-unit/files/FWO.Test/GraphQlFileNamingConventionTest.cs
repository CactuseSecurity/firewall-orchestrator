using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    [Parallelizable]
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
            "config/subscribeAutodiscoveryConfigChanges.graphql",
            "config/subscribeComplianceCheckConfigChanges.graphql",
            "config/subscribeConfigChangesByUser.graphql",
            "config/subscribeDailyCheckConfigChanges.graphql",
            "config/subscribeExternalRequestConfigChanges.graphql",
            "config/subscribeFlowSyncConfigChanges.graphql",
            "config/subscribeImportAppDataConfigChanges.graphql",
            "config/subscribeImportNotifyConfigChanges.graphql",
            "config/subscribeImportSubnetDataConfigChanges.graphql",
            "config/subscribeUpdateRuleOwnerMappingConfigChanges.graphql",
            "config/subscribeVarianceAnalysisConfigChanges.graphql",
            "device/deleteCredential.graphql",
            "device/deleteDevice.graphql",
            "device/deleteManagement.graphql",
            "device/fragments/importCredentials.graphql",
            "device/fragments/importCredentialsWithoutSecrets.graphql",
            "device/fragments/managementDetails.graphql",
            "device/fragments/managementDetailsWithoutSecrets.graphql",
            "device/getManagementDetailsWithoutSecrets.graphql",
            "device/getManagementsDetails.graphql",
            "device/getMgmtNumberUsingCred.graphql",
            "device/getSingleManagementDetails.graphql",
            "device/newManagement.graphql",
            "device/updateGatewayUid.graphql",
            "extRequest/subscribeExtRequestStateUpdate.graphql",
            "import/deleteImport.graphql",
            "modelling/addNwAppZone.graphql",
            "monitor/subscribeAlertChanges.graphql",
            "networking/getAllNetworkInfosTable.graphql",
            "recertification/refreshViewRuleWithOwner.graphql",
            "report/addReportScheduleFileFormats.graphql",
            "report/getAllObjectDetailsInReport.graphql",
            "report/getReportById.graphql",
            "report/getReportsOverview.graphql",
            "report/subscribeGeneratedReportsChanges.graphql",
            "report/subscribeReportScheduleChanges.graphql",
            "request/subscribeTaskChanges.graphql",
            "request/subscribeTicketStateChanges.graphql",
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
            string pattern = $@"\b(query|mutation|fragment)\s+{escapedFileName}\b";
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
                $"Expected '{filePath}' to contain 'query {fileName}', 'mutation {fileName}', or 'fragment {fileName}'.");
        }

        /// <summary>
        /// Resolves the repository path to the shared GraphQL API call files.
        /// </summary>
        private static string FindGraphQlRootDirectory()
        {
            DirectoryInfo? currentDirectory = new(AppContext.BaseDirectory);

            while (currentDirectory is not null)
            {
                string candidatePath = Path.Combine(currentDirectory.FullName, "roles", "common", "files", "fwo-api-calls");
                if (Directory.Exists(candidatePath))
                {
                    return candidatePath;
                }

                currentDirectory = currentDirectory.Parent;
            }

            Assert.Fail("Could not locate roles/common/files/fwo-api-calls from the test output directory.");
            return string.Empty;
        }
    }
}
