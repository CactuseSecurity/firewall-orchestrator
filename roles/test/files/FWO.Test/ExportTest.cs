using NUnit.Framework;
using FWO.Logging;
using FWO.Report;
using FWO.Report.Filter;
using FWO.Api.Data;


namespace FWO.Test
{
    [TestFixture]
    internal class ExportTest
    {
        static NetworkObject dummyIp1 = new NetworkObject(){ Id = 1, Name = "dummyIp1", IP = "1.2.3.4/32", IpEnd = "", Type = new NetworkObjectType(){ Name = "network" }};
        static NetworkObject dummyIp2 = new NetworkObject(){ Id = 2, Name = "dummyIp2", IP = "127.0.0.1/32", IpEnd = "", Type = new NetworkObjectType(){ Name = "network" }};
        static NetworkObject dummyIpRange = new NetworkObject(){ Id = 3, Name = "dummyIpRange", IP = "1.2.3.4/32", IpEnd = "1.2.3.5/32", Type = new NetworkObjectType(){ Name = "ip_range" }};
        static NetworkObject dummyIpNew = new NetworkObject(){ Id = 4, Name = "dummyIpNew", IP = "10.0.6.1/32", Type = new NetworkObjectType(){ Name = "network" }};
        static NetworkObject dummyIp1Changed = new NetworkObject(){ Id = 5, Name = "dummyIp1Changed", IP = "2.3.4.5/32", IpEnd = "", Type = new NetworkObjectType(){ Name = "network" }};

        static NetworkService dummyservice1 = new NetworkService(){  Id = 1, DestinationPort = 443, DestinationPortEnd = 443, Name = "dummyservice1", Protocol = new NetworkProtocol { Name = "TCP" }};
        static NetworkService dummyservice2 = new NetworkService(){  Id = 2, DestinationPort = 6666, DestinationPortEnd = 7777, Name = "dummyservice2", Protocol = new NetworkProtocol { Name = "UDP" }};

        static NetworkUser dummyuser1 = new NetworkUser(){ Id = 1, Name = "dummyuser1" };
        static NetworkUser dummyuser2 = new NetworkUser(){ Id = 2, Name = "dummyuser2" };

        static Rule Rule1 = new Rule();
        static Rule Rule1Changed = new Rule();
        static Rule Rule2 = new Rule();
        static Rule Rule2Changed = new Rule();

        SimulatedUserConfig userConfig = new SimulatedUserConfig();
        DynGraphqlQuery query = new DynGraphqlQuery("TestFilter"){ ReportTimeString = "2023-04-20T17:50:04" };

        [SetUp]
        public void Initialize()
        {
        }

        [Test]
        public void RulesGenerateHtml()
        {
            Log.WriteInfo("Test Log", "starting rules report html generation");
            ReportRules reportRules = new ReportRules(query, userConfig, ReportType.Rules);
            reportRules.Managements = ConstructRuleReport(false);

            string expectedHtmlResult = "<!DOCTYPE html><html><head><meta charset=\"utf-8\"/><title>Rules Report</title>" +
            "<style>table {font-family: arial, sans-serif;font-size: 10px;border-collapse: collapse;width: 100 %;}td {border: 1px solid #000000;text-align: left;padding: 3px;}th {border: 1px solid #000000;text-align: left;padding: 3px;background-color: #dddddd;}</style></head>" +
            "<body>" +
            "<h2>Rules Report</h2>" +
            "<p>Filter: TestFilter</p>" +
            "<p>Time of configuration: 2023-04-20T15:50:04Z (UTC)</p>" +
            "<p>Generated on: Z (UTC)</p>" +
            "<p>Devices: TestMgt [TestDev]</p><hr>" +
            "<h3>TestMgt</h3><hr>" +
            "<h4>TestDev</h4><hr>" +
            "<table><tr><th>No.</th><th>Name</th><th>Source Zone</th><th>Source</th><th>Destination Zone</th><th>Destination</th><th>Services</th><th>Action</th><th>Track</th><th>Enabled</th><th>Uid</th><th>Comment</th></tr>" +
            "<tr><td>1</td><td>TestRule1</td><td>srczn</td>" +
            "<td><p><span class=\"oi oi-rss\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"#nwobj1\" target=\"_top\" style=\"\">dummyIp1</a> (1.2.3.4/32)<br><span class=\"oi oi-rss\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"#nwobj2\" target=\"_top\" style=\"\">dummyIp2</a> (127.0.0.1/32)</p></td>" +
            "<td>dstzn</td>" +
            "<td><p><span class=\"oi oi-resize-width\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"#nwobj3\" target=\"_top\" style=\"\">dummyIpRange</a> (1.2.3.4/32-1.2.3.5/32)</p></td>" +
            "<td><p><span class=\"oi oi-wrench\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"#svc1\" target=\"_top\" style=\"\">dummyservice1</a> (443/TCP)</p></td>" +
            "<td>accept</td><td>none</td><td><b>Y</b></td><td>uid1</td><td>comment1</td></tr>" +
            "<tr><td>2</td><td>TestRule2</td><td></td>" +
            "<td><p>not<br><span class=\"oi oi-people\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"#user1\" target=\"_top\" style=\"\">dummyuser1</a>@<span class=\"oi oi-rss\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"#nwobj1\" target=\"_top\" style=\"\">dummyIp1</a> (1.2.3.4/32)<br><span class=\"oi oi-people\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"#user1\" target=\"_top\" style=\"\">dummyuser1</a>@<span class=\"oi oi-rss\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"#nwobj2\" target=\"_top\" style=\"\">dummyIp2</a> (127.0.0.1/32)</p></td>" +
            "<td></td>" +
            "<td><p>not<br><span class=\"oi oi-people\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"#user2\" target=\"_top\" style=\"\">dummyuser2</a>@<span class=\"oi oi-resize-width\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"#nwobj3\" target=\"_top\" style=\"\">dummyIpRange</a> (1.2.3.4/32-1.2.3.5/32)</p></td>" +
            "<td><p>not<br><span class=\"oi oi-wrench\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"#svc2\" target=\"_top\" style=\"\">dummyservice2</a> (6666-7777/UDP)</p></td>" +
            "<td>deny</td><td>none</td><td><b>Y</b></td><td>uid2:123</td><td>comment2</td></tr></table>" +
            "<h4>Network Objects</h4><hr>" +
            "<table><tr><th>No.</th><th>Name</th><th>Type</th><th>IP Address</th><th>Members</th><th>Uid</th><th>Comment</th></tr>" +
            "<tr><td>1</td><td><a name=nwobj1>dummyIp1</a></td><td>network</td><td>1.2.3.4/32</td><td></td><td></td><td></td></tr>" +
            "<tr><td>2</td><td><a name=nwobj2>dummyIp2</a></td><td>network</td><td>127.0.0.1/32</td><td></td><td></td><td></td></tr>" +
            "<tr><td>3</td><td><a name=nwobj3>dummyIpRange</a></td><td>ip_range</td><td>1.2.3.4/32-1.2.3.5/32</td><td></td><td></td><td></td></tr>" +
            "</table>" +
            "<h4>Network Services</h4><hr>" +
            "<table><tr><th>No.</th><th>Name</th><th>Type</th><th>Protocol</th><th>Port</th><th>Members</th><th>Uid</th><th>Comment</th></tr>" +
            "<tr><td>1</td><td>dummyservice1</td><td><a name=svc1>dummyservice1</a></td><td>TCP</td><td>443</td><td></td><td></td><td></td></tr>" +
            "<tr><td>2</td><td>dummyservice2</td><td><a name=svc2>dummyservice2</a></td><td>UDP</td><td>6666-7777</td><td></td><td></td><td></td></tr>" +
            "</table>" +
            "<h4>Users</h4><hr>" +
            "<table><tr><th>No.</th><th>Name</th><th>Type</th><th>Members</th><th>Uid</th><th>Comment</th></tr>" +
            "<tr><td>1</td><td>dummyuser1</td><td><a name=user1>dummyuser1</a></td><td></td><td></td><td></td></tr>" +
            "<tr><td>2</td><td>dummyuser2</td><td><a name=user2>dummyuser2</a></td><td></td><td></td><td></td></tr>" +
            "</table></body></html>";
            Assert.AreEqual(expectedHtmlResult, removeLinebreaks((removeGenDate(reportRules.ExportToHtml(), true))));
        }

        [Test]
        public void ResolvedRulesGenerateCsv()
        {
            Log.WriteInfo("Test Log", "starting rules report resolved csv generation");
            ReportRules reportRules = new ReportRules(query, userConfig, ReportType.ResolvedRules);
            reportRules.Managements = ConstructRuleReport(true);

            string expectedCsvResult = "# report type: Rules Report (resolved)\r\n" +
            "# report generation date: Z (UTC)\r\n" +
            "# date of configuration shown: 2023-04-20T15:50:04Z (UTC)\r\n" +
            "# device filter: TestMgt [TestDev]\r\n" +
            "# other filters: TestFilter\r\n" +
            "# report generator: Firewall Orchestrator - https://fwo.cactus.de/en\r\n" +
            "# data protection level: For internal use only\r\n#\r\n" +
            "\"management-name\",\"device-name\",\"rule-number\",\"rule-name\",\"source-zone\",\"source\",\"destination-zone\",\"destination\",\"service\",\"action\",\"track\",\"rule-enabled\",\"rule-uid\",\"rule-comment\"\r\n" +
            "\"TestMgt\",\"TestDev\",\"1\",\"TestRule1\",\"srczn\",\"dummyIp1 (1.2.3.4/32),dummyIp2 (127.0.0.1/32)\",\"dstzn\",\"dummyIpRange (1.2.3.4/32-1.2.3.5/32)\",\"dummyservice1 (443/TCP)\",\"accept\",\"none\",\"enabled\",\"uid1\",\"comment1\"\r\n" +
            "\"TestMgt\",\"TestDev\",\"2\",\"TestRule2\",\"\",\"not(dummyuser1@dummyIp1 (1.2.3.4/32),dummyuser1@dummyIp2 (127.0.0.1/32))\",\"\",\"not(dummyuser2@dummyIpRange (1.2.3.4/32-1.2.3.5/32))\",\"not(dummyservice2 (6666-7777/UDP))\",\"deny\",\"none\",\"enabled\",\"uid2:123\",\"comment2\"\r\n";
            Assert.AreEqual(expectedCsvResult, removeGenDate(reportRules.ExportToCsv()));
        }

        [Test]
        public void ResolvedRulesGenerateHtml()
        {
            Log.WriteInfo("Test Log", "starting rules report resolved html generation");
            ReportRules reportRules = new ReportRules(query, userConfig, ReportType.ResolvedRules);
            reportRules.Managements = ConstructRuleReport(true);

            string expectedHtmlResult = "<!DOCTYPE html><html><head><meta charset=\"utf-8\"/><title>Rules Report (resolved)</title>" +
            "<style>table {font-family: arial, sans-serif;font-size: 10px;border-collapse: collapse;width: 100 %;}td {border: 1px solid #000000;text-align: left;padding: 3px;}th {border: 1px solid #000000;text-align: left;padding: 3px;background-color: #dddddd;}</style></head>" +
            "<body>" +
            "<h2>Rules Report (resolved)</h2>" +
            "<p>Filter: TestFilter</p>" +
            "<p>Time of configuration: 2023-04-20T15:50:04Z (UTC)</p>" +
            "<p>Generated on: Z (UTC)</p>" +
            "<p>Devices: TestMgt [TestDev]</p><hr>" +
            "<h3>TestMgt</h3><hr>" +
            "<h4>TestDev</h4><hr>" +
            "<table><tr><th>No.</th><th>Name</th><th>Source Zone</th><th>Source</th><th>Destination Zone</th><th>Destination</th><th>Services</th><th>Action</th><th>Track</th><th>Enabled</th><th>Uid</th><th>Comment</th></tr>" +
            "<tr><td>1</td><td>TestRule1</td><td>srczn</td>" +
            "<td><p>dummyIp1 (1.2.3.4/32)<br>dummyIp2 (127.0.0.1/32)</p></td>" +
            "<td>dstzn</td>" +
            "<td><p>dummyIpRange (1.2.3.4/32-1.2.3.5/32)</p></td>" +
            "<td><p>dummyservice1 (443/TCP)</p></td>" +
            "<td>accept</td><td>none</td><td><b>Y</b></td><td>uid1</td><td>comment1</td></tr>" +
            "<tr><td>2</td><td>TestRule2</td><td></td>" +
            "<td><p>not<br>dummyuser1@dummyIp1 (1.2.3.4/32)<br>dummyuser1@dummyIp2 (127.0.0.1/32)</p></td>" +
            "<td></td>" +
            "<td><p>not<br>dummyuser2@dummyIpRange (1.2.3.4/32-1.2.3.5/32)</p></td>" +
            "<td><p>not<br>dummyservice2 (6666-7777/UDP)</p></td>" +
            "<td>deny</td><td>none</td><td><b>Y</b></td><td>uid2:123</td><td>comment2</td></tr></table>" +
            "</body></html>";
            Assert.AreEqual(expectedHtmlResult, removeLinebreaks((removeGenDate(reportRules.ExportToHtml(), true))));
        }

        [Test]
        public void ResolvedRulesTechGenerateCsv()
        {
            Log.WriteInfo("Test Log", "starting rules report tech csv generation");
            ReportRules reportRules = new ReportRules(query, userConfig, ReportType.ResolvedRulesTech);
            reportRules.Managements = ConstructRuleReport(true);

            string expectedCsvResult = "# report type: Rules Report (technical)\r\n" +
            "# report generation date: Z (UTC)\r\n" +
            "# date of configuration shown: 2023-04-20T15:50:04Z (UTC)\r\n" +
            "# device filter: TestMgt [TestDev]\r\n" +
            "# other filters: TestFilter\r\n" +
            "# report generator: Firewall Orchestrator - https://fwo.cactus.de/en\r\n" +
            "# data protection level: For internal use only\r\n#\r\n" +
            "\"management-name\",\"device-name\",\"rule-number\",\"rule-name\",\"source-zone\",\"source\",\"destination-zone\",\"destination\",\"service\",\"action\",\"track\",\"rule-enabled\",\"rule-uid\",\"rule-comment\"\r\n" +
            "\"TestMgt\",\"TestDev\",\"1\",\"TestRule1\",\"srczn\",\"1.2.3.4/32,127.0.0.1/32\",\"dstzn\",\"1.2.3.4/32-1.2.3.5/32\",\"443/TCP\",\"accept\",\"none\",\"enabled\",\"uid1\",\"comment1\"\r\n" +
            "\"TestMgt\",\"TestDev\",\"2\",\"TestRule2\",\"\",\"not(dummyuser1@1.2.3.4/32,dummyuser1@127.0.0.1/32)\",\"\",\"not(dummyuser2@1.2.3.4/32-1.2.3.5/32)\",\"not(6666-7777/UDP)\",\"deny\",\"none\",\"enabled\",\"uid2:123\",\"comment2\"\r\n";
            Assert.AreEqual(expectedCsvResult, removeGenDate(reportRules.ExportToCsv()));
        }

        [Test]
        public void ResolvedRulesTechGenerateHtml()
        {
            Log.WriteInfo("Test Log", "starting rules report resolved html generation");
            ReportRules reportRules = new ReportRules(query, userConfig, ReportType.ResolvedRulesTech);
            reportRules.Managements = ConstructRuleReport(true);

            string expectedHtmlResult = "<!DOCTYPE html><html><head><meta charset=\"utf-8\"/><title>Rules Report (technical)</title>" +
            "<style>table {font-family: arial, sans-serif;font-size: 10px;border-collapse: collapse;width: 100 %;}td {border: 1px solid #000000;text-align: left;padding: 3px;}th {border: 1px solid #000000;text-align: left;padding: 3px;background-color: #dddddd;}</style></head>" +
            "<body>" +
            "<h2>Rules Report (technical)</h2>" +
            "<p>Filter: TestFilter</p>" +
            "<p>Time of configuration: 2023-04-20T15:50:04Z (UTC)</p>" +
            "<p>Generated on: Z (UTC)</p>" +
            "<p>Devices: TestMgt [TestDev]</p><hr>" +
            "<h3>TestMgt</h3><hr>" +
            "<h4>TestDev</h4><hr>" +
            "<table><tr><th>No.</th><th>Name</th><th>Source Zone</th><th>Source</th><th>Destination Zone</th><th>Destination</th><th>Services</th><th>Action</th><th>Track</th><th>Enabled</th><th>Uid</th><th>Comment</th></tr>" +
            "<tr><td>1</td><td>TestRule1</td><td>srczn</td>" +
            "<td><p>1.2.3.4/32<br>127.0.0.1/32</p></td>" +
            "<td>dstzn</td>" +
            "<td><p>1.2.3.4/32-1.2.3.5/32</p></td>" +
            "<td><p>443/TCP</p></td>" +
            "<td>accept</td><td>none</td><td><b>Y</b></td><td>uid1</td><td>comment1</td></tr>" +
            "<tr><td>2</td><td>TestRule2</td><td></td>" +
            "<td><p>not<br>dummyuser1@1.2.3.4/32<br>dummyuser1@127.0.0.1/32</p></td>" +
            "<td></td>" +
            "<td><p>not<br>dummyuser2@1.2.3.4/32-1.2.3.5/32</p></td>" +
            "<td><p>not<br>6666-7777/UDP</p></td>" +
            "<td>deny</td><td>none</td><td><b>Y</b></td><td>uid2:123</td><td>comment2</td></tr></table>" +
            "</body></html>";
            Assert.AreEqual(expectedHtmlResult, removeLinebreaks((removeGenDate(reportRules.ExportToHtml(), true))));
        }

        [Test]
        public void ChangesGenerateHtml()
        {
            Log.WriteInfo("Test Log", "starting changes report html generation");
            ReportChanges reportChanges = new ReportChanges(query, userConfig, ReportType.Changes);
            reportChanges.Managements = ConstructChangeReport(false);

            string expectedHtmlResult = "<!DOCTYPE html><html><head><meta charset=\"utf-8\"/><title>Changes Report</title>" +
            "<style>table {font-family: arial, sans-serif;font-size: 10px;border-collapse: collapse;width: 100 %;}td {border: 1px solid #000000;text-align: left;padding: 3px;}th {border: 1px solid #000000;text-align: left;padding: 3px;background-color: #dddddd;}</style></head>" +
            "<body>" +
            "<h2>Changes Report</h2>" +
            "<p>Filter: TestFilter</p>" +
            "<p>Generated on: Z (UTC)</p>" +
            "<p>Devices: TestMgt [TestDev]</p><hr>" +
            "<h3>TestMgt</h3><hr>" +
            "<h4>TestDev</h4><hr>" +
            "<table><tr><th>Change Time</th><th>Change Type</th><th>Name</th><th>Source Zone</th><th>Source</th><th>Destination Zone</th><th>Destination</th><th>Services</th><th>Action</th><th>Track</th><th>Enabled</th><th>Uid</th><th>Comment</th></tr>" +
            "<tr><td>05.04.2023 12:00:00</td><td>Rule modified</td><td>TestRule1</td><td>srczn</td>" +
            "<td><p><span class=\"oi oi-rss\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"#nwobj2\" target=\"_top\" style=\"\">dummyIp2</a> (127.0.0.1/32)<br></p>" +
            "deleted: <p style=\"color: red; text-decoration: line-through red;\"><span class=\"oi oi-rss\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"#nwobj1\" target=\"_top\" style=\"color: red\">dummyIp1</a> (1.2.3.4/32)<br></p>" +
            "added: <p style=\"color: green; text-decoration: bold;\"><span class=\"oi oi-rss\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"#nwobj5\" target=\"_top\" style=\"color: green\">dummyIp1Changed</a> (2.3.4.5/32)</p></td>" +
            "<td>dstzn</td>" +
            "<td><p><span class=\"oi oi-resize-width\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"#nwobj3\" target=\"_top\" style=\"\">dummyIpRange</a> (1.2.3.4/32-1.2.3.5/32)<br></p>" +
            "added: <p style=\"color: green; text-decoration: bold;\"><span class=\"oi oi-rss\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"#nwobj4\" target=\"_top\" style=\"color: green\">dummyIpNew</a> (10.0.6.1/32)</p></td>" +
            "<td>deleted: <p style=\"color: red; text-decoration: line-through red;\"><span class=\"oi oi-wrench\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"#svc1\" target=\"_top\" style=\"color: red\">dummyservice1</a> (443/TCP)<br></p>" +
            "added: <p style=\"color: green; text-decoration: bold;\">not<br><span class=\"oi oi-wrench\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"#svc1\" target=\"_top\" style=\"color: green\">dummyservice1</a> (443/TCP)</p></td>" +
            "<td>accept</td><td>none</td><td><b>Y</b></td><td>deleted: <p style=\"color: red; text-decoration: line-through red;\">uid1<br></p></td>" +
            "<td>deleted: <p style=\"color: red; text-decoration: line-through red;\">comment1<br></p>added: <p style=\"color: green; text-decoration: bold;\">new comment</p></td></tr>" +
            "<tr><td>05.04.2023 12:00:00</td><td>Rule modified</td><td>TestRule2</td><td></td>" +
            "<td><p>not<br><span class=\"oi oi-people\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"#user1\" target=\"_top\" style=\"\">dummyuser1</a>@<span class=\"oi oi-rss\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"#nwobj1\" target=\"_top\" style=\"\">dummyIp1</a> (1.2.3.4/32)<br>" +
            "<span class=\"oi oi-people\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"#user1\" target=\"_top\" style=\"\">dummyuser1</a>@<span class=\"oi oi-rss\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"#nwobj2\" target=\"_top\" style=\"\">dummyIp2</a> (127.0.0.1/32)</p></td>" +
            "<td></td>" +
            "<td>deleted: <p style=\"color: red; text-decoration: line-through red;\">not<br><span class=\"oi oi-people\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"#user2\" target=\"_top\" style=\"color: red\">dummyuser2</a>@<span class=\"oi oi-resize-width\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"#nwobj3\" target=\"_top\" style=\"color: red\">dummyIpRange</a> (1.2.3.4/32-1.2.3.5/32)<br></p>" +
            "added: <p style=\"color: green; text-decoration: bold;\"><span class=\"oi oi-people\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"#user2\" target=\"_top\" style=\"color: green\">dummyuser2</a>@<span class=\"oi oi-resize-width\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"#nwobj3\" target=\"_top\" style=\"color: green\">dummyIpRange</a> (1.2.3.4/32-1.2.3.5/32)</p></td>" +
            "<td>deleted: <p style=\"color: red; text-decoration: line-through red;\">not<br><span class=\"oi oi-wrench\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"#svc2\" target=\"_top\" style=\"color: red\">dummyservice2</a> (6666-7777/UDP)<br></p>" +
            "added: <p style=\"color: green; text-decoration: bold;\"><span class=\"oi oi-wrench\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"#svc2\" target=\"_top\" style=\"color: green\">dummyservice2</a> (6666-7777/UDP)</p></td>" +
            "<td>deny</td><td>none</td><td>deleted: <p style=\"color: red; text-decoration: line-through red;\"><b>Y</b><br></p>added: <p style=\"color: green; text-decoration: bold;\"><b>N</b></p></td><td>uid2:123</td><td>comment2</td></tr>" +
            "<tr><td>05.04.2023 12:00:00</td><td>Rule added</td><td>TestRule1</td><td>srczn</td>" +
            "<td><p><span class=\"oi oi-rss\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"#nwobj1\" target=\"_top\" style=\"color: green\">dummyIp1</a> (1.2.3.4/32)<br><span class=\"oi oi-rss\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"#nwobj2\" target=\"_top\" style=\"color: green\">dummyIp2</a> (127.0.0.1/32)</p></td>" +
            "<td>dstzn</td>" +
            "<td><p><span class=\"oi oi-resize-width\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"#nwobj3\" target=\"_top\" style=\"color: green\">dummyIpRange</a> (1.2.3.4/32-1.2.3.5/32)</p></td>" +
            "<td><p><span class=\"oi oi-wrench\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"#svc1\" target=\"_top\" style=\"color: green\">dummyservice1</a> (443/TCP)</p></td>" +
            "<td>accept</td><td>none</td><td><b>Y</b></td><td>uid1</td><td>comment1</td></tr>" +
            "<tr><td>05.04.2023 12:00:00</td><td>Rule deleted</td><td>TestRule2</td><td></td>" +
            "<td><p>not<br><span class=\"oi oi-people\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"#user1\" target=\"_top\" style=\"color: red\">dummyuser1</a>@<span class=\"oi oi-rss\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"#nwobj1\" target=\"_top\" style=\"color: red\">dummyIp1</a> (1.2.3.4/32)<br>" +
            "<span class=\"oi oi-people\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"#user1\" target=\"_top\" style=\"color: red\">dummyuser1</a>@<span class=\"oi oi-rss\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"#nwobj2\" target=\"_top\" style=\"color: red\">dummyIp2</a> (127.0.0.1/32)</p></td>" +
            "<td></td>" +
            "<td><p>not<br><span class=\"oi oi-people\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"#user2\" target=\"_top\" style=\"color: red\">dummyuser2</a>@<span class=\"oi oi-resize-width\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"#nwobj3\" target=\"_top\" style=\"color: red\">dummyIpRange</a> (1.2.3.4/32-1.2.3.5/32)</p></td>" +
            "<td><p>not<br><span class=\"oi oi-wrench\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"#svc2\" target=\"_top\" style=\"color: red\">dummyservice2</a> (6666-7777/UDP)</p></td>" +
            "<td>deny</td><td>none</td><td><b>Y</b></td><td>uid2:123</td><td>comment2</td></tr></table>" +
            // "<h4>Network Objects</h4><hr>" +
            // "<table><tr><th>No.</th><th>Name</th><th>Type</th><th>IP Address</th><th>Members</th><th>Uid</th><th>Comment</th></tr>" +
            // "<tr><td>1</td><td><a name=nwobj1>dummyIp1</a></td><td>network</td><td>1.2.3.4/32</td><td></td><td></td><td></td></tr>" +
            // "<tr><td>2</td><td><a name=nwobj2>dummyIp2</a></td><td>network</td><td>127.0.0.1/32</td><td></td><td></td><td></td></tr>" +
            // "<tr><td>3</td><td><a name=nwobj3>dummyIpRange</a></td><td>ip_range</td><td>1.2.3.4/32-1.2.3.5/32</td><td></td><td></td><td></td></tr>" +
            // "<tr><td>3</td><td><a name=nwobj4>dummyIpNew</a></td><td>network</td><td>10.0.6.1/32</td><td></td><td></td><td></td></tr>" +
            // "<tr><td>3</td><td><a name=nwobj5>dummyIp1Changed</a></td><td>network</td><td>2.3.4.5/32</td><td></td><td></td><td></td></tr>" +
            // "</table>" +
            // "<h4>Network Services</h4><hr>" +
            // "<table><tr><th>No.</th><th>Name</th><th>Type</th><th>Protocol</th><th>Port</th><th>Members</th><th>Uid</th><th>Comment</th></tr>" +
            // "<tr><td>1</td><td>dummyservice1</td><td><a name=svc1>dummyservice1</a></td><td>TCP</td><td>443</td><td></td><td></td><td></td></tr>" +
            // "<tr><td>2</td><td>dummyservice2</td><td><a name=svc2>dummyservice2</a></td><td>UDP</td><td>6666-7777</td><td></td><td></td><td></td></tr>" +
            // "</table>" +
            // "<h4>Users</h4><hr>" +
            // "<table><tr><th>No.</th><th>Name</th><th>Type</th><th>Members</th><th>Uid</th><th>Comment</th></tr>" +
            // "<tr><td>1</td><td>dummyuser1</td><td><a name=user1>dummyuser1</a></td><td></td><td></td><td></td></tr>" +
            // "<tr><td>2</td><td>dummyuser2</td><td><a name=user2>dummyuser2</a></td><td></td><td></td><td></td></tr>" +
            // "</table>"+
            "</body></html>";
            Assert.AreEqual(expectedHtmlResult, removeLinebreaks((removeGenDate(reportChanges.ExportToHtml(), true))));
        }

        [Test]
        public void ResolvedChangesGenerateCsv()
        {
            Log.WriteInfo("Test Log", "starting changes report resolved csv generation");
            ReportChanges reportChanges = new ReportChanges(query, userConfig, ReportType.ResolvedChanges);
            reportChanges.Managements = ConstructChangeReport(true);

            string expectedCsvResult = "# report type: Changes Report (resolved)\r\n" +
            "# report generation date: Z (UTC)\r\n" +
            "# device filter: TestMgt [TestDev]\r\n" +
            "# other filters: TestFilter\r\n" +
            "# report generator: Firewall Orchestrator - https://fwo.cactus.de/en\r\n" +
            "# data protection level: For internal use only\r\n#\r\n" +
            "\"management-name\",\"device-name\",\"change-time\",\"change-type\",\"rule-name\",\"source-zone\",\"source\",\"destination-zone\",\"destination\",\"service\",\"action\",\"track\",\"rule-enabled\",\"rule-uid\",\"rule-comment\"\r\n" +
            "\"TestMgt\",\"TestDev\",\"05.04.2023 12:00:00\",\"Rule modified\",\"TestRule1\",\"srczn\",\"dummyIp2 (127.0.0.1/32) deleted: dummyIp1 (1.2.3.4/32) added: dummyIp1Changed (2.3.4.5/32)\"," +
            "\"dstzn\",\"dummyIpRange (1.2.3.4/32-1.2.3.5/32) added: dummyIpNew (10.0.6.1/32)\"," +
            "\" deleted: dummyservice1 (443/TCP) added: not(dummyservice1 (443/TCP))\",\"accept\",\"none\",\"enabled\",\" deleted: uid1\",\" deleted: comment1 added: new comment\"\r\n" +
            "\"TestMgt\",\"TestDev\",\"05.04.2023 12:00:00\",\"Rule modified\",\"TestRule2\",\"\",\"not(dummyuser1@dummyIp1 (1.2.3.4/32),dummyuser1@dummyIp2 (127.0.0.1/32))\"," +
            "\"\",\" deleted: not(dummyuser2@dummyIpRange (1.2.3.4/32-1.2.3.5/32)) added: dummyuser2@dummyIpRange (1.2.3.4/32-1.2.3.5/32)\"," +
            "\" deleted: not(dummyservice2 (6666-7777/UDP)) added: dummyservice2 (6666-7777/UDP)\",\"deny\",\"none\",\" deleted: enabled added: disabled\",\"uid2:123\",\"comment2\"\r\n" +
            "\"TestMgt\",\"TestDev\",\"05.04.2023 12:00:00\",\"Rule added\",\"TestRule1\",\"srczn\",\"dummyIp1 (1.2.3.4/32),dummyIp2 (127.0.0.1/32)\"," +
            "\"dstzn\",\"dummyIpRange (1.2.3.4/32-1.2.3.5/32)\",\"dummyservice1 (443/TCP)\",\"accept\",\"none\",\"enabled\",\"uid1\",\"comment1\"\r\n" +
            "\"TestMgt\",\"TestDev\",\"05.04.2023 12:00:00\",\"Rule deleted\",\"TestRule2\",\"\",\"not(dummyuser1@dummyIp1 (1.2.3.4/32),dummyuser1@dummyIp2 (127.0.0.1/32))\"," +
            "\"\",\"not(dummyuser2@dummyIpRange (1.2.3.4/32-1.2.3.5/32))\",\"not(dummyservice2 (6666-7777/UDP))\",\"deny\",\"none\",\"enabled\",\"uid2:123\",\"comment2\"\r\n";
            Assert.AreEqual(expectedCsvResult, removeGenDate(reportChanges.ExportToCsv()));
        }

        [Test]
        public void ResolvedChangesGenerateHtml()
        {
            Log.WriteInfo("Test Log", "starting changes report resolved html generation");
            ReportChanges reportChanges = new ReportChanges(query, userConfig, ReportType.ResolvedChanges);
            reportChanges.Managements = ConstructChangeReport(true);

            string expectedHtmlResult = "<!DOCTYPE html><html><head><meta charset=\"utf-8\"/><title>Changes Report (resolved)</title>" +
            "<style>table {font-family: arial, sans-serif;font-size: 10px;border-collapse: collapse;width: 100 %;}td {border: 1px solid #000000;text-align: left;padding: 3px;}th {border: 1px solid #000000;text-align: left;padding: 3px;background-color: #dddddd;}</style></head>" +
            "<body>" +
            "<h2>Changes Report (resolved)</h2>" +
            "<p>Filter: TestFilter</p>" +
            "<p>Generated on: Z (UTC)</p>" +
            "<p>Devices: TestMgt [TestDev]</p><hr>" +
            "<h3>TestMgt</h3><hr>" +
            "<h4>TestDev</h4><hr>" +
            "<table><tr><th>Change Time</th><th>Change Type</th><th>Name</th><th>Source Zone</th><th>Source</th><th>Destination Zone</th><th>Destination</th><th>Services</th><th>Action</th><th>Track</th><th>Enabled</th><th>Uid</th><th>Comment</th></tr>" +
            "<tr><td>05.04.2023 12:00:00</td><td>Rule modified</td><td>TestRule1</td><td>srczn</td>" +
            "<td><p>dummyIp2 (127.0.0.1/32)<br></p>deleted: <p style=\"color: red; text-decoration: line-through red;\">dummyIp1 (1.2.3.4/32)<br></p>" +
            "added: <p style=\"color: green; text-decoration: bold;\">dummyIp1Changed (2.3.4.5/32)</p></td>" +
            "<td>dstzn</td>" +
            "<td><p>dummyIpRange (1.2.3.4/32-1.2.3.5/32)<br></p>added: <p style=\"color: green; text-decoration: bold;\">dummyIpNew (10.0.6.1/32)</p></td>" +
            "<td>deleted: <p style=\"color: red; text-decoration: line-through red;\">dummyservice1 (443/TCP)<br></p>" +
            "added: <p style=\"color: green; text-decoration: bold;\">not<br>dummyservice1 (443/TCP)</p></td>" +
            "<td>accept</td><td>none</td><td><b>Y</b></td><td>deleted: <p style=\"color: red; text-decoration: line-through red;\">uid1<br></p></td>" +
            "<td>deleted: <p style=\"color: red; text-decoration: line-through red;\">comment1<br></p>added: <p style=\"color: green; text-decoration: bold;\">new comment</p></td></tr>" +
            "<tr><td>05.04.2023 12:00:00</td><td>Rule modified</td><td>TestRule2</td><td></td>" +
            "<td><p>not<br>dummyuser1@dummyIp1 (1.2.3.4/32)<br>dummyuser1@dummyIp2 (127.0.0.1/32)</p></td>" +
            "<td></td>" +
            "<td>deleted: <p style=\"color: red; text-decoration: line-through red;\">not<br>dummyuser2@dummyIpRange (1.2.3.4/32-1.2.3.5/32)<br></p>added: <p style=\"color: green; text-decoration: bold;\">dummyuser2@dummyIpRange (1.2.3.4/32-1.2.3.5/32)</p></td>" +
            "<td>deleted: <p style=\"color: red; text-decoration: line-through red;\">not<br>dummyservice2 (6666-7777/UDP)<br></p>added: <p style=\"color: green; text-decoration: bold;\">dummyservice2 (6666-7777/UDP)</p></td>" +
            "<td>deny</td><td>none</td><td>deleted: <p style=\"color: red; text-decoration: line-through red;\"><b>Y</b><br></p>added: <p style=\"color: green; text-decoration: bold;\"><b>N</b></p></td><td>uid2:123</td><td>comment2</td></tr>" +
            "<tr><td>05.04.2023 12:00:00</td><td>Rule added</td><td>TestRule1</td><td>srczn</td>" +
            "<td><p>dummyIp1 (1.2.3.4/32)<br>dummyIp2 (127.0.0.1/32)</p></td>" +
            "<td>dstzn</td>" +
            "<td><p>dummyIpRange (1.2.3.4/32-1.2.3.5/32)</p></td>" +
            "<td><p>dummyservice1 (443/TCP)</p></td>" +
            "<td>accept</td><td>none</td><td><b>Y</b></td><td>uid1</td><td>comment1</td></tr>" +
            "<tr><td>05.04.2023 12:00:00</td><td>Rule deleted</td><td>TestRule2</td><td></td>" +
            "<td><p>not<br>dummyuser1@dummyIp1 (1.2.3.4/32)<br>dummyuser1@dummyIp2 (127.0.0.1/32)</p></td>" +
            "<td></td>" +
            "<td><p>not<br>dummyuser2@dummyIpRange (1.2.3.4/32-1.2.3.5/32)</p></td>" +
            "<td><p>not<br>dummyservice2 (6666-7777/UDP)</p></td>" +
            "<td>deny</td><td>none</td><td><b>Y</b></td><td>uid2:123</td><td>comment2</td></tr></table>" +
            "</body></html>";
            Assert.AreEqual(expectedHtmlResult, removeLinebreaks((removeGenDate(reportChanges.ExportToHtml(), true))));
        }

        [Test]
        public void ResolvedChangesTechGenerateCsv()
        {
            Log.WriteInfo("Test Log", "starting changes report tech csv generation");
            ReportChanges reportChanges = new ReportChanges(query, userConfig, ReportType.ResolvedChangesTech);
            reportChanges.Managements = ConstructChangeReport(true);

            string expectedCsvResult = "# report type: Changes Report (technical)\r\n" +
            "# report generation date: Z (UTC)\r\n" +
            "# device filter: TestMgt [TestDev]\r\n" +
            "# other filters: TestFilter\r\n" +
            "# report generator: Firewall Orchestrator - https://fwo.cactus.de/en\r\n" +
            "# data protection level: For internal use only\r\n#\r\n" +
            "\"management-name\",\"device-name\",\"change-time\",\"change-type\",\"rule-name\",\"source-zone\",\"source\",\"destination-zone\",\"destination\",\"service\",\"action\",\"track\",\"rule-enabled\",\"rule-uid\",\"rule-comment\"\r\n" +
            "\"TestMgt\",\"TestDev\",\"05.04.2023 12:00:00\",\"Rule modified\",\"TestRule1\",\"srczn\",\"127.0.0.1/32 deleted: 1.2.3.4/32 added: 2.3.4.5/32\",\"dstzn\",\"1.2.3.4/32-1.2.3.5/32 added: 10.0.6.1/32\",\" deleted: 443/TCP added: not(443/TCP)\",\"accept\",\"none\",\"enabled\",\" deleted: uid1\",\" deleted: comment1 added: new comment\"\r\n" +
            "\"TestMgt\",\"TestDev\",\"05.04.2023 12:00:00\",\"Rule modified\",\"TestRule2\",\"\",\"not(dummyuser1@1.2.3.4/32,dummyuser1@127.0.0.1/32)\",\"\",\" deleted: not(dummyuser2@1.2.3.4/32-1.2.3.5/32) added: dummyuser2@1.2.3.4/32-1.2.3.5/32\",\" deleted: not(6666-7777/UDP) added: 6666-7777/UDP\",\"deny\",\"none\",\" deleted: enabled added: disabled\",\"uid2:123\",\"comment2\"\r\n" +
            "\"TestMgt\",\"TestDev\",\"05.04.2023 12:00:00\",\"Rule added\",\"TestRule1\",\"srczn\",\"1.2.3.4/32,127.0.0.1/32\",\"dstzn\",\"1.2.3.4/32-1.2.3.5/32\",\"443/TCP\",\"accept\",\"none\",\"enabled\",\"uid1\",\"comment1\"\r\n" +
            "\"TestMgt\",\"TestDev\",\"05.04.2023 12:00:00\",\"Rule deleted\",\"TestRule2\",\"\",\"not(dummyuser1@1.2.3.4/32,dummyuser1@127.0.0.1/32)\",\"\",\"not(dummyuser2@1.2.3.4/32-1.2.3.5/32)\",\"not(6666-7777/UDP)\",\"deny\",\"none\",\"enabled\",\"uid2:123\",\"comment2\"\r\n";
            Assert.AreEqual(expectedCsvResult, removeGenDate(reportChanges.ExportToCsv()));
        }

        [Test]
        public void ResolvedChangesTechGenerateHtml()
        {
            Log.WriteInfo("Test Log", "starting changes report tech html generation");
            ReportChanges reportChanges = new ReportChanges(query, userConfig, ReportType.ResolvedChangesTech);
            reportChanges.Managements = ConstructChangeReport(true);

            string expectedHtmlResult = "<!DOCTYPE html><html><head><meta charset=\"utf-8\"/><title>Changes Report (technical)</title>" +
            "<style>table {font-family: arial, sans-serif;font-size: 10px;border-collapse: collapse;width: 100 %;}td {border: 1px solid #000000;text-align: left;padding: 3px;}th {border: 1px solid #000000;text-align: left;padding: 3px;background-color: #dddddd;}</style></head>" +
            "<body>" +
            "<h2>Changes Report (technical)</h2>" +
            "<p>Filter: TestFilter</p>" +
            "<p>Generated on: Z (UTC)</p>" +
            "<p>Devices: TestMgt [TestDev]</p><hr>" +
            "<h3>TestMgt</h3><hr>" +
            "<h4>TestDev</h4><hr>" +
            "<table><tr><th>Change Time</th><th>Change Type</th><th>Name</th><th>Source Zone</th><th>Source</th><th>Destination Zone</th><th>Destination</th><th>Services</th><th>Action</th><th>Track</th><th>Enabled</th><th>Uid</th><th>Comment</th></tr>" +
            "<tr><td>05.04.2023 12:00:00</td><td>Rule modified</td><td>TestRule1</td><td>srczn</td>" +
            "<td><p>127.0.0.1/32<br></p>" +
            "deleted: <p style=\"color: red; text-decoration: line-through red;\">1.2.3.4/32<br></p>" +
            "added: <p style=\"color: green; text-decoration: bold;\">2.3.4.5/32</p></td>" +
            "<td>dstzn</td>" +
            "<td><p>1.2.3.4/32-1.2.3.5/32<br></p>added: <p style=\"color: green; text-decoration: bold;\">10.0.6.1/32</p></td>" +
            "<td>deleted: <p style=\"color: red; text-decoration: line-through red;\">443/TCP<br></p>" +
            "added: <p style=\"color: green; text-decoration: bold;\">not<br>443/TCP</p></td>" +
            "<td>accept</td><td>none</td><td><b>Y</b></td><td>deleted: <p style=\"color: red; text-decoration: line-through red;\">uid1<br></p></td>" +
            "<td>deleted: <p style=\"color: red; text-decoration: line-through red;\">comment1<br></p>added: <p style=\"color: green; text-decoration: bold;\">new comment</p></td></tr>" +
            "<tr><td>05.04.2023 12:00:00</td><td>Rule modified</td><td>TestRule2</td><td></td>" +
            "<td><p>not<br>dummyuser1@1.2.3.4/32<br>dummyuser1@127.0.0.1/32</p></td>" +
            "<td></td>" +
            "<td>deleted: <p style=\"color: red; text-decoration: line-through red;\">not<br>dummyuser2@1.2.3.4/32-1.2.3.5/32<br></p>added: <p style=\"color: green; text-decoration: bold;\">dummyuser2@1.2.3.4/32-1.2.3.5/32</p></td>" +
            "<td>deleted: <p style=\"color: red; text-decoration: line-through red;\">not<br>6666-7777/UDP<br></p>added: <p style=\"color: green; text-decoration: bold;\">6666-7777/UDP</p></td>" +
            "<td>deny</td><td>none</td><td>deleted: <p style=\"color: red; text-decoration: line-through red;\"><b>Y</b><br></p>added: <p style=\"color: green; text-decoration: bold;\"><b>N</b></p></td><td>uid2:123</td><td>comment2</td></tr>" +
            "<tr><td>05.04.2023 12:00:00</td><td>Rule added</td><td>TestRule1</td><td>srczn</td>" +
            "<td><p>1.2.3.4/32<br>127.0.0.1/32</p></td>" +
            "<td>dstzn</td>" +
            "<td><p>1.2.3.4/32-1.2.3.5/32</p></td>" +
            "<td><p>443/TCP</p></td>" +
            "<td>accept</td><td>none</td><td><b>Y</b></td><td>uid1</td><td>comment1</td></tr>" +
            "<tr><td>05.04.2023 12:00:00</td><td>Rule deleted</td><td>TestRule2</td><td></td>" +
            "<td><p>not<br>dummyuser1@1.2.3.4/32<br>dummyuser1@127.0.0.1/32</p></td>" +
            "<td></td>" +
            "<td><p>not<br>dummyuser2@1.2.3.4/32-1.2.3.5/32</p></td>" +
            "<td><p>not<br>6666-7777/UDP</p></td>" +
            "<td>deny</td><td>none</td><td><b>Y</b></td><td>uid2:123</td><td>comment2</td></tr></table>" +
            "</body></html>";
            Assert.AreEqual(expectedHtmlResult, removeLinebreaks((removeGenDate(reportChanges.ExportToHtml(), true))));
        }


        private NetworkLocation[] InitFroms(bool resolved, bool user = false)
        {
            if(resolved)
            {
                return new NetworkLocation[]{ new NetworkLocation(user ? dummyuser1 : new NetworkUser(), new NetworkObject(){ ObjectGroupFlats = new GroupFlat<NetworkObject>[]
                {
                    new GroupFlat<NetworkObject>(){ Object = dummyIp1 },
                    new GroupFlat<NetworkObject>(){ Object = dummyIp2 }
                }})};
            }
            else
            {
                return new NetworkLocation[]
                {
                    new NetworkLocation(user ? dummyuser1 : new NetworkUser(), dummyIp1),
                    new NetworkLocation(user ? dummyuser1 : new NetworkUser(), dummyIp2)
                };
            }
        }

        private NetworkLocation[] InitTos(bool resolved, bool user = false)
        {
            if(resolved)
            {
                return new NetworkLocation[]{ new NetworkLocation(user ? dummyuser2 : new NetworkUser(), new NetworkObject(){ ObjectGroupFlats = new GroupFlat<NetworkObject>[]
                {
                    new GroupFlat<NetworkObject>(){ Object = dummyIpRange }
                }})};
            }
            else
            {
                return new NetworkLocation[]
                {
                    new NetworkLocation(user ? dummyuser2 : new NetworkUser(), dummyIpRange),
                };
            }
        }

        private ServiceWrapper[] InitServices(NetworkService service, bool resolved)
        {
            if(resolved)
            {
                return new ServiceWrapper[]{new ServiceWrapper(){ Content = new NetworkService(){ServiceGroupFlats = new GroupFlat<NetworkService>[]
                {
                    new GroupFlat<NetworkService>(){ Object = service }
                }}}};
            }
            else
            {
                return new ServiceWrapper[]
                {
                    new ServiceWrapper(){ Content = service },
                };
            }
        }

        private Rule InitRule1(bool resolved)
        {
            return new Rule()
            {
                Name = "TestRule1",
                Action = "accept",
                Comment = "comment1",
                Disabled = false,
                DisplayOrderNumber = 1,
                Track = "none",
                Uid = "uid1",
                SourceZone = new NetworkZone(){ Name = "srczn" },
                SourceNegated = false,
                Froms = InitFroms(resolved),
                DestinationZone = new NetworkZone(){ Name = "dstzn" },
                DestinationNegated = false,
                Tos = InitTos(resolved),
                ServiceNegated = false,
                Services = InitServices(dummyservice1, resolved)
            };
        }

        private Rule InitRule2(bool resolved)
        {
            return new Rule()
            {
                Name = "TestRule2",
                Action = "deny",
                Comment = "comment2",
                Disabled = false,
                DisplayOrderNumber = 2,
                Track = "none",
                Uid = "uid2:123",
                SourceNegated = true,
                Froms = InitFroms(resolved, true),
                DestinationNegated = true,
                Tos = InitTos(resolved, true),
                ServiceNegated = true,
                Services = InitServices(dummyservice2, resolved)
            };
        }

        private Management[] ConstructRuleReport(bool resolved)
        {
            Rule1 = InitRule1(resolved);
            Rule2 = InitRule2(resolved);
            List<Management> report = new List<Management>();
            Management testMgt = new Management()
            { 
                Name = "TestMgt",
                ReportObjects = new NetworkObject[]{ dummyIp1, dummyIp2, dummyIpRange },
                ReportServices = new NetworkService[]{ dummyservice1, dummyservice2 },
                ReportUsers = new NetworkUser[]{ dummyuser1, dummyuser2 }
            };
            Device testDev = new Device(){ Name = "TestDev" };
            testDev.Rules = new Rule[]{ Rule1, Rule2 };
            testMgt.Devices = new Device[]{ testDev };
            report.Add(testMgt);
            return report.ToArray();
        }

        private Management[] ConstructChangeReport(bool resolved)
        {
            Rule1 = InitRule1(resolved);
            Rule1Changed = InitRule1(resolved);
            Rule2 = InitRule2(resolved);
            Rule2Changed = InitRule2(resolved);
            if(resolved)
            {
                Rule1Changed.Froms[0].Object.ObjectGroupFlats[0].Object = dummyIp1Changed;
                Rule1Changed.Tos = new NetworkLocation[]{new NetworkLocation(new NetworkUser(), new NetworkObject(){ObjectGroupFlats = new GroupFlat<NetworkObject>[]
                {
                    new GroupFlat<NetworkObject>(){ Object = dummyIpRange },
                    new GroupFlat<NetworkObject>(){ Object = dummyIpNew }
                }})};  
            }
            else
            {
                Rule1Changed.Froms[0].Object = dummyIp1Changed;
                Rule1Changed.Tos = new NetworkLocation[]
                {
                    new NetworkLocation(new NetworkUser(), dummyIpRange),
                    new NetworkLocation(new NetworkUser(), dummyIpNew)
                };
            }
            Rule1Changed.Uid = "";
            Rule1Changed.ServiceNegated = true;
            Rule1Changed.Comment = "new comment";

            Rule2Changed.DestinationNegated = false;
            Rule2Changed.ServiceNegated = false;
            Rule2Changed.Disabled = true;

            List<Management> report = new List<Management>();
            Management testMgt = new Management(){ Name = "TestMgt" };
            Device testDev = new Device(){ Name = "TestDev" };
            RuleChange ruleChange1 = new RuleChange()
            {
                ChangeAction = 'C',
                ChangeImport = new ChangeImport(){ Time = new DateTime(2023,04,05,12,0,0) },
                OldRule = Rule1,
                NewRule = Rule1Changed
            };

            RuleChange ruleChange2 = new RuleChange()
            {
                ChangeAction = 'C',
                ChangeImport = new ChangeImport(){ Time = new DateTime(2023,04,05,12,0,0) },
                OldRule = Rule2,
                NewRule = Rule2Changed
            };

            RuleChange ruleChange3 = new RuleChange()
            {
                ChangeAction = 'I',
                ChangeImport = new ChangeImport(){ Time = new DateTime(2023,04,05,12,0,0) },
                NewRule = Rule1
            };

            RuleChange ruleChange4 = new RuleChange()
            {
                ChangeAction = 'D',
                ChangeImport = new ChangeImport(){ Time = new DateTime(2023,04,05,12,0,0) },
                OldRule = Rule2
            };

            testDev.RuleChanges = new RuleChange[]{ ruleChange1, ruleChange2, ruleChange3, ruleChange4 };
            testMgt.Devices = new Device[]{ testDev };
            report.Add(testMgt);
            return report.ToArray();
        }

        private string removeGenDate(string exportString, bool html = false)
        {
            string dateText = html ? "<p>Generated on: " : "# report generation date: ";
            int startGenTime = exportString.IndexOf(dateText);
            if(startGenTime > 0)
            {
                return exportString.Remove(startGenTime + dateText.Length, 19);
            }
            return exportString;
        }

        private string removeLinebreaks(string exportString)
        {
            while(exportString.Contains("\r\n "))
            {
                exportString = exportString.Replace("\r\n ","\r\n");
            }
            while(exportString.Contains(" \r\n"))
            {
                exportString = exportString.Replace(" \r\n","\r\n");
            }
            return exportString.Replace("\r\n","");
        }
    }
}
