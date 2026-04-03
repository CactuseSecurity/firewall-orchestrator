using FWO.Basics;
using FWO.Data.Report;
using FWO.Logging;
using FWO.Report;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace FWO.Test
{
    internal partial class ExportTest
    {
        [Test]
        public void ResolvedRulesGenerateCsv()
        {
            Log.WriteInfo("Test Log", "starting rules report resolved csv generation");
            ReportRules reportRules = ConstructReportRules(query, userConfig, ReportType.ResolvedRules, ConstructRuleReportRules(true));

            string expectedCsvResult = "# report type: Rules Report (resolved)" +
                                       "# report generation date: Z (UTC)" +
                                       "# date of configuration shown: 2023-04-20T15:50:04Z (UTC)" +
                                       "# device filter: TestMgt [Mock Device 1]" +
                                       "# other filters: TestFilter" +
                                       "# report generator: Firewall Orchestrator - https://fwo.cactus.de/en" +
                                       "# data protection level: For internal use only#" +
                                       "\"management-name\",\"device-name\",\"rule-number\",\"rule-name\",\"source-zone\",\"source\",\"destination-zone\",\"destination\",\"service\",\"action\",\"track\",\"rule-enabled\",\"rule-uid\",\"rule-comment\",\"last-modified\"" +
                                       "\"TestMgt\",\"Mock Device 1\",\"1\",\"TestRule1\",\"\"srczn1\",\"srczn2\",\"srczn3\"\",\"TestIp1 (1.2.3.4/32),TestIp2 (127.0.0.1/32)\",\"\"dstzn1\",\"dstzn2\",\"dstzn3\"\",\"TestIpRange (1.2.3.4-1.2.3.5)\",\"TestService1 (443/TCP)\",\"accept\",\"none\",\"enabled\",\"uid1\",\"comment1\",\"2023-04-05\"" +
                                       "\"TestMgt\",\"Mock Device 1\",\"\",\"TestRule2\",\"\",\"not(TestUser1@TestIp1 (1.2.3.4/32),TestUser1@TestIp2 (127.0.0.1/32))\",\"\",\"not(TestUser2@TestIpRange (1.2.3.4-1.2.3.5))\",\"not(TestService2 (6666-7777/UDP))\",\"deny\",\"none\",\"enabled\",\"uid2:123\",\"comment2\",\"2023-04-05\"";
            string csvExport = RemoveLinebreaks(RemoveGenDate(reportRules.ExportToCsv()));
            ClassicAssert.AreEqual(expectedCsvResult, csvExport);
        }

        [Test]
        public void ResolvedRulesTechGenerateCsv()
        {
            Log.WriteInfo("Test Log", "starting rules report tech csv generation");
            ReportRules reportRules = ConstructReportRules(query, userConfig, ReportType.ResolvedRulesTech, ConstructRuleReportRules(true));

            string expectedCsvResult = "# report type: Rules Report (technical)" +
            "# report generation date: Z (UTC)" +
            "# date of configuration shown: 2023-04-20T15:50:04Z (UTC)" +
            "# device filter: TestMgt [Mock Device 1]" +
            "# other filters: TestFilter" +
            "# report generator: Firewall Orchestrator - https://fwo.cactus.de/en" +
            "# data protection level: For internal use only#" +
            "\"management-name\",\"device-name\",\"rule-number\",\"rule-name\",\"source-zone\",\"source\",\"destination-zone\",\"destination\",\"service\",\"action\",\"track\",\"rule-enabled\",\"rule-uid\",\"rule-comment\",\"last-modified\"" +
            "\"TestMgt\",\"Mock Device 1\",\"1\",\"TestRule1\",\"\"srczn1\",\"srczn2\",\"srczn3\"\",\"1.2.3.4/32,127.0.0.1/32\",\"\"dstzn1\",\"dstzn2\",\"dstzn3\"\",\"1.2.3.4-1.2.3.5\",\"443/TCP\",\"accept\",\"none\",\"enabled\",\"uid1\",\"comment1\",\"2023-04-05\"" +
            "\"TestMgt\",\"Mock Device 1\",\"\",\"TestRule2\",\"\",\"not(TestUser1@1.2.3.4/32,TestUser1@127.0.0.1/32)\",\"\",\"not(TestUser2@1.2.3.4-1.2.3.5)\",\"not(6666-7777/UDP)\",\"deny\",\"none\",\"enabled\",\"uid2:123\",\"comment2\",\"2023-04-05\"";
            ClassicAssert.AreEqual(expectedCsvResult, RemoveLinebreaks(RemoveGenDate(reportRules.ExportToCsv())));
        }

        [Test]
        public void ResolvedChangesGenerateCsv()
        {
            Log.WriteInfo("Test Log", "starting changes report resolved csv generation");
            ReportChanges reportChanges = new(query, userConfig, ReportType.ResolvedChanges, new TimeFilter(), false, true)
            {
                ReportData = ConstructChangeReport(true)
            };

            string expectedCsvResult = "# report type: Changes Report (resolved)" +
            "# report generation date: Z (UTC)" +
            "# device filter: TestMgt [TestDev]" +
            "# other filters: TestFilter" +
            "# report generator: Firewall Orchestrator - https://fwo.cactus.de/en" +
            "# data protection level: For internal use only#\"Rules\"" +
            "\"management-name\",\"change-time\",\"change-type\",\"rule-name\",\"source-zone\",\"source\",\"destination-zone\",\"destination\",\"service\",\"action\",\"track\",\"rule-enabled\",\"enforcing_device\",\"rule-uid\",\"rule-comment\"" +
            "\"TestMgt\",\"05.04.2023 12:00:00\",\"Rule added\",\"TestRule1\",\"\"srczn1\",\"srczn2\",\"srczn3\"\",\"TestIp1 (1.2.3.4/32),TestIp2 (127.0.0.1/32)\"," +
            "\"\"dstzn1\",\"dstzn2\",\"dstzn3\"\",\"TestIpRange (1.2.3.4-1.2.3.5)\",\"TestService1 (443/TCP)\",\"accept\",\"none\",\"enabled\",\"\",\"uid1\",\"comment1\"" +
            "\"TestMgt\",\"05.04.2023 12:00:00\",\"Rule modified\",\"TestRule1\",\"\"srczn1\",\"srczn2\",\"srczn3\"\",\"TestIp2 (127.0.0.1/32) deleted: TestIp1 (1.2.3.4/32) added: TestIp1Changed (2.3.4.5)\"," +
            "\"\"dstzn1\",\"dstzn2\",\"dstzn3\"\",\"TestIpRange (1.2.3.4-1.2.3.5) added: TestIpNew (10.0.6.0/24)\"," +
            "\" deleted: TestService1 (443/TCP) added: not(TestService1 (443/TCP))\",\"accept\",\"none\",\"enabled\",\"\",\" deleted: uid1\",\" deleted: comment1 added: new comment\"" +
            "\"TestMgt\",\"05.04.2023 12:00:00\",\"Rule modified\",\"TestRule2\",\"\",\"not(TestUser1@TestIp1 (1.2.3.4/32),TestUser1@TestIp2 (127.0.0.1/32))\"," +
            "\"\",\" deleted: not(TestUser2@TestIpRange (1.2.3.4-1.2.3.5)) added: TestUser2@TestIpRange (1.2.3.4-1.2.3.5)\"," +
            "\" deleted: not(TestService2 (6666-7777/UDP)) added: TestService2 (6666-7777/UDP)\",\"deny\",\"none\",\" deleted: enabled added: disabled\",\"\",\"uid2:123\",\"comment2\"" +
            "\"TestMgt\",\"05.04.2023 12:00:00\",\"Rule deleted\",\"TestRule2\",\"\",\"not(TestUser1@TestIp1 (1.2.3.4/32),TestUser1@TestIp2 (127.0.0.1/32))\"," +
            "\"\",\"not(TestUser2@TestIpRange (1.2.3.4-1.2.3.5))\",\"not(TestService2 (6666-7777/UDP))\",\"deny\",\"none\",\"enabled\",\"\",\"uid2:123\",\"comment2\"";
            ClassicAssert.AreEqual(expectedCsvResult, RemoveLinebreaks(RemoveGenDate(reportChanges.ExportToCsv())));
        }

        [Test]
        public void ResolvedChangesGenerateCsvIncludeObjects()
        {
            Log.WriteInfo("Test Log", "starting changes report resolved csv generation");
            ReportChanges reportChanges = new(query, userConfig, ReportType.ResolvedChanges, new TimeFilter(), true, true)
            {
                ReportData = ConstructChangeReport(true)
            };

            string expectedCsvResult = "# report type: Changes Report (resolved)" +
            "# report generation date: Z (UTC)" +
            "# device filter: TestMgt [TestDev]" +
            "# other filters: TestFilter" +
            "# report generator: Firewall Orchestrator - https://fwo.cactus.de/en" +
            "# data protection level: For internal use only#\"Rules\"" +
            "\"management-name\",\"change-time\",\"change-type\",\"rule-name\",\"source-zone\",\"source\",\"destination-zone\",\"destination\",\"service\",\"action\",\"track\",\"rule-enabled\",\"enforcing_device\",\"rule-uid\",\"rule-comment\"" +
            "\"TestMgt\",\"05.04.2023 12:00:00\",\"Rule added\",\"TestRule1\",\"\"srczn1\",\"srczn2\",\"srczn3\"\",\"TestIp1 (1.2.3.4/32),TestIp2 (127.0.0.1/32)\"," +
            "\"\"dstzn1\",\"dstzn2\",\"dstzn3\"\",\"TestIpRange (1.2.3.4-1.2.3.5)\",\"TestService1 (443/TCP)\",\"accept\",\"none\",\"enabled\",\"\",\"uid1\",\"comment1\"" +
            "\"TestMgt\",\"05.04.2023 12:00:00\",\"Rule modified\",\"TestRule1\",\"\"srczn1\",\"srczn2\",\"srczn3\"\",\"TestIp2 (127.0.0.1/32) deleted: TestIp1 (1.2.3.4/32) added: TestIp1Changed (2.3.4.5)\"," +
            "\"\"dstzn1\",\"dstzn2\",\"dstzn3\"\",\"TestIpRange (1.2.3.4-1.2.3.5) added: TestIpNew (10.0.6.0/24)\"," +
            "\" deleted: TestService1 (443/TCP) added: not(TestService1 (443/TCP))\",\"accept\",\"none\",\"enabled\",\"\",\" deleted: uid1\",\" deleted: comment1 added: new comment\"" +
            "\"TestMgt\",\"05.04.2023 12:00:00\",\"Rule modified\",\"TestRule2\",\"\",\"not(TestUser1@TestIp1 (1.2.3.4/32),TestUser1@TestIp2 (127.0.0.1/32))\"," +
            "\"\",\" deleted: not(TestUser2@TestIpRange (1.2.3.4-1.2.3.5)) added: TestUser2@TestIpRange (1.2.3.4-1.2.3.5)\"," +
            "\" deleted: not(TestService2 (6666-7777/UDP)) added: TestService2 (6666-7777/UDP)\",\"deny\",\"none\",\" deleted: enabled added: disabled\",\"\",\"uid2:123\",\"comment2\"" +
            "\"TestMgt\",\"05.04.2023 12:00:00\",\"Rule deleted\",\"TestRule2\",\"\",\"not(TestUser1@TestIp1 (1.2.3.4/32),TestUser1@TestIp2 (127.0.0.1/32))\"," +
            "\"\",\"not(TestUser2@TestIpRange (1.2.3.4-1.2.3.5))\",\"not(TestService2 (6666-7777/UDP))\",\"deny\",\"none\",\"enabled\",\"\",\"uid2:123\",\"comment2\"" +
            "#\"Network objects\"\"management-name\",\"change-time\",\"change-type\",\"object-name\",\"type\",\"ip_address\",\"members\",\"object-uid\",\"object-comment\"\"TestMgt\",\"05.04.2023 12:00:00\"," +
            "\"network_object_added\",\"TestIp1\",\"network\",\" (1.2.3.4)\",\"\",\"\",\"\"\"TestMgt\",\"05.04.2023 12:00:00\",\"network_object_modified\",\" deleted: TestIp1 added: TestIp1Changed\",\" deleted: network added: host\"," +
            "\" deleted:  (1.2.3.4) added:  (2.3.4.5)\",\"\",\"\",\"\"\"TestMgt\",\"05.04.2023 12:00:00\",\"network_object_deleted\",\"TestIp2\",\"network\",\" (127.0.0.1)\",\"\",\"\",\"\"#\"Service objects\"\"management-name\",\"change-time\"," +
            "\"change-type\",\"service-name\",\"type\",\"protocol\",\"port\",\"members\",\"service-uid\",\"service-comment\"\"TestMgt\",\"05.04.2023 12:00:00\",\"service_added\",\"TestService1\",\"\",\"TCP\",\" (443)\",\"\",\"\",\"\"\"TestMgt\"," +
            "\"05.04.2023 12:00:00\",\"service_modified\",\" deleted: TestService1 added: TestService2\",\"\",\" deleted: TCP added: UDP\",\" deleted:  (443) added:  (6666-7777)\",\"\",\"\",\"\"\"TestMgt\",\"05.04.2023 12:00:00\",\"service_deleted\"," +
            "\"TestService1\",\"\",\"TCP\",\" (443)\",\"\",\"\",\"\"";
            ClassicAssert.AreEqual(expectedCsvResult, RemoveLinebreaks(RemoveGenDate(reportChanges.ExportToCsv())));
        }

        [Test]
        public void ResolvedChangesTechGenerateCsv()
        {
            Log.WriteInfo("Test Log", "starting changes report tech csv generation");
            ReportChanges reportChanges = new(query, userConfig, ReportType.ResolvedChangesTech, new TimeFilter(), false, true)
            {
                ReportData = ConstructChangeReport(true)
            };

            string expectedCsvResult = "# report type: Changes Report (technical)" +
            "# report generation date: Z (UTC)" +
            "# device filter: TestMgt [TestDev]" +
            "# other filters: TestFilter" +
            "# report generator: Firewall Orchestrator - https://fwo.cactus.de/en" +
            "# data protection level: For internal use only#\"Rules\"" +
            "\"management-name\",\"change-time\",\"change-type\",\"rule-name\",\"source-zone\",\"source\",\"destination-zone\",\"destination\",\"service\",\"action\",\"track\",\"rule-enabled\",\"enforcing_device\",\"rule-uid\",\"rule-comment\"" +
            "\"TestMgt\",\"05.04.2023 12:00:00\",\"Rule added\",\"TestRule1\",\"\"srczn1\",\"srczn2\",\"srczn3\"\",\"1.2.3.4/32,127.0.0.1/32\",\"\"dstzn1\",\"dstzn2\",\"dstzn3\"\",\"1.2.3.4-1.2.3.5\",\"443/TCP\",\"accept\",\"none\",\"enabled\",\"\",\"uid1\",\"comment1\"" +
            "\"TestMgt\",\"05.04.2023 12:00:00\",\"Rule modified\",\"TestRule1\",\"\"srczn1\",\"srczn2\",\"srczn3\"\",\"127.0.0.1/32 deleted: 1.2.3.4/32 added: 2.3.4.5\",\"\"dstzn1\",\"dstzn2\",\"dstzn3\"\",\"1.2.3.4-1.2.3.5 added: 10.0.6.0/24\",\" deleted: 443/TCP added: not(443/TCP)\",\"accept\",\"none\",\"enabled\",\"\",\" deleted: uid1\",\" deleted: comment1 added: new comment\"" +
            "\"TestMgt\",\"05.04.2023 12:00:00\",\"Rule modified\",\"TestRule2\",\"\",\"not(TestUser1@1.2.3.4/32,TestUser1@127.0.0.1/32)\",\"\",\" deleted: not(TestUser2@1.2.3.4-1.2.3.5) added: TestUser2@1.2.3.4-1.2.3.5\",\" deleted: not(6666-7777/UDP) added: 6666-7777/UDP\",\"deny\",\"none\",\" deleted: enabled added: disabled\",\"\",\"uid2:123\",\"comment2\"" +
            "\"TestMgt\",\"05.04.2023 12:00:00\",\"Rule deleted\",\"TestRule2\",\"\",\"not(TestUser1@1.2.3.4/32,TestUser1@127.0.0.1/32)\",\"\",\"not(TestUser2@1.2.3.4-1.2.3.5)\",\"not(6666-7777/UDP)\",\"deny\",\"none\",\"enabled\",\"\",\"uid2:123\",\"comment2\"";
            ClassicAssert.AreEqual(expectedCsvResult, RemoveLinebreaks(RemoveGenDate(reportChanges.ExportToCsv())));
        }
    }
}
