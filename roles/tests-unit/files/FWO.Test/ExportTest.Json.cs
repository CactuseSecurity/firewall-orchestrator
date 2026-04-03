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
        public void RulesGenerateJson()
        {
            Log.WriteInfo("Test Log", "starting rules report json generation");
            ReportRules reportRules = ConstructReportRules(query, userConfig, ReportType.Rules, ConstructRuleReportRules(false));
            int id = reportRules.ReportData.ManagementData.First().Rulebases.First().Id;

            string expectedJsonResult = "[{\"id\": 0,\"uid\": \"\",\"name\": \"TestMgt\"," +
                                        "\"devices\": [{\"uid\": \"device-1\",\"id\": 1,\"name\": \"Mock Device 1\"," +
                                        "\"rulebase_links\": [{\"gw_id\": 0,\"from_rule_id\": null,\"rule\": null,\"rulebaseByFromRulebaseId\": null,\"from_rulebase_id\": null,\"rulebase\": null,\"link_type\": 0,\"stm_link_type\": null,\"is_initial\": true,\"is_global\": false,\"is_section\": false}]," +
                                        "\"changelog_rules\": null,\"rules_aggregate\": {\"aggregate\": {\"count\": 0}}," +
                                        "\"unusedRules_Count\": {\"aggregate\": {\"count\": 0}}}]," +
                                        "\"rulebases\": [{\"id\": " + id + ",\"name\": \"Mock Rulebase " + id + "\",\"changelog_rules\": null," +
                                        "\"rules_aggregate\": {\"aggregate\": {\"count\": 0}}," +
                                        "\"rules\": [" +
                                        "{\"rule_id\": 0,\"rule_uid\": \"uid1\",\"mgm_id\": 0,\"rule_num_numeric\": 1,\"rule_name\": \"TestRule1\",\"rule_comment\": \"comment1\",\"rule_disabled\": false," +
                                        "\"rule_services\": [{\"service\": {\"svc_id\": 1,\"svc_name\": \"TestService1\",\"svc_uid\": \"\",\"svc_port\": 443,\"svc_port_end\": 443,\"svc_source_port\": null,\"svc_source_port_end\": null,\"svc_code\": \"\",\"svc_timeout\": null,\"svc_typ_id\": null,\"active\": false,\"svc_create\": 0,\"svc_create_time\": {\"time\": \"0001-01-01T00:00:00\"},\"svc_last_seen\": 0,\"service_type\": {\"name\": \"\"},\"svc_comment\": \"\",\"svc_color_id\": null,\"stm_color\": null,\"ip_proto_id\": null,\"protocol_name\": {\"id\": 6,\"name\": \"TCP\"},\"svc_member_names\": \"\",\"svc_member_refs\": \"\",\"svcgrps\": [],\"svcgrp_flats\": [],\"svc_rpcnr\": null}}]," +
                                        "\"rule_svc_neg\": false,\"rule_svc\": \"\",\"rule_svc_refs\": \"\",\"rule_src_neg\": false,\"rule_src\": \"\",\"rule_src_refs\": \"\",\"rule_from_zones\": [{\"zone\": {\"zone_id\": 0,\"zone_name\": \"srczn1\"}},{\"zone\": {\"zone_id\": 0,\"zone_name\": \"srczn2\"}},{\"zone\": {\"zone_id\": 0,\"zone_name\": \"srczn3\"}}]," +
                                        "\"rule_froms\": [" +
                                        "{\"object\": {\"obj_id\": 1,\"obj_name\": \"TestIp1\",\"obj_ip\": \"1.2.3.4/32\",\"obj_ip_end\": \"1.2.3.4/32\",\"obj_uid\": \"\",\"zone\": null,\"active\": false,\"obj_create\": 0,\"obj_create_time\": {\"time\": \"0001-01-01T00:00:00\"},\"obj_last_seen\": 0,\"type\": {\"id\": 0,\"name\": \"network\"},\"obj_color\": null,\"obj_comment\": \"\",\"obj_member_names\": \"\",\"obj_member_refs\": \"\",\"objgrps\": [],\"objgrp_flats\": []},\"usr\": {\"user_id\": 0,\"user_uid\": \"\",\"user_name\": \"\",\"user_comment\": \"\",\"user_lastname\": \"\",\"user_firstname\": \"\",\"usr_typ_id\": 0,\"type\": {\"usr_typ_name\": \"\"},\"user_create\": 0,\"user_create_time\": {\"time\": \"0001-01-01T00:00:00\"},\"user_last_seen\": 0,\"user_member_names\": \"\",\"user_member_refs\": \"\",\"usergrps\": [],\"usergrp_flats\": []}}," +
                                        "{\"object\": {\"obj_id\": 2,\"obj_name\": \"TestIp2\",\"obj_ip\": \"127.0.0.1/32\",\"obj_ip_end\": \"127.0.0.1/32\",\"obj_uid\": \"\",\"zone\": null,\"active\": false,\"obj_create\": 0,\"obj_create_time\": {\"time\": \"0001-01-01T00:00:00\"},\"obj_last_seen\": 0,\"type\": {\"id\": 0,\"name\": \"network\"},\"obj_color\": null,\"obj_comment\": \"\",\"obj_member_names\": \"\",\"obj_member_refs\": \"\",\"objgrps\": [],\"objgrp_flats\": []},\"usr\": {\"user_id\": 0,\"user_uid\": \"\",\"user_name\": \"\",\"user_comment\": \"\",\"user_lastname\": \"\",\"user_firstname\": \"\",\"usr_typ_id\": 0,\"type\": {\"usr_typ_name\": \"\"},\"user_create\": 0,\"user_create_time\": {\"time\": \"0001-01-01T00:00:00\"},\"user_last_seen\": 0,\"user_member_names\": \"\",\"user_member_refs\": \"\",\"usergrps\": [],\"usergrp_flats\": []}}]," +
                                        "\"rule_dst_neg\": false,\"rule_dst\": \"\",\"rule_dst_refs\": \"\",\"rule_to_zones\": [{\"zone\": {\"zone_id\": 0,\"zone_name\": \"dstzn1\"}},{\"zone\": {\"zone_id\": 0,\"zone_name\": \"dstzn2\"}},{\"zone\": {\"zone_id\": 0,\"zone_name\": \"dstzn3\"}}]," +
                                        "\"rule_tos\": [" +
                                        "{\"object\": {\"obj_id\": 3,\"obj_name\": \"TestIpRange\",\"obj_ip\": \"1.2.3.4/32\",\"obj_ip_end\": \"1.2.3.5/32\",\"obj_uid\": \"\",\"zone\": null,\"active\": false,\"obj_create\": 0,\"obj_create_time\": {\"time\": \"0001-01-01T00:00:00\"},\"obj_last_seen\": 0,\"type\": {\"id\": 0,\"name\": \"ip_range\"},\"obj_color\": null,\"obj_comment\": \"\",\"obj_member_names\": \"\",\"obj_member_refs\": \"\",\"objgrps\": [],\"objgrp_flats\": []},\"usr\": {\"user_id\": 0,\"user_uid\": \"\",\"user_name\": \"\",\"user_comment\": \"\",\"user_lastname\": \"\",\"user_firstname\": \"\",\"usr_typ_id\": 0,\"type\": {\"usr_typ_name\": \"\"},\"user_create\": 0,\"user_create_time\": {\"time\": \"0001-01-01T00:00:00\"},\"user_last_seen\": 0,\"user_member_names\": \"\",\"user_member_refs\": \"\",\"usergrps\": [],\"usergrp_flats\": []}}]," +
                                        "\"rule_action\": \"accept\",\"rule_track\": \"none\",\"section_header\": \"\"," +
                                        "\"rule_metadatum\": {\"rule_metadata_id\": 0,\"rule_created\": null,\"created_import\": null,\"removed\": null,\"removed_import\": null,\"rule_first_hit\": null,\"rule_last_hit\": \"2022-04-19T00:00:00\",\"recertification\": [],\"recert_history\": [],\"rule_uid\": \"\",\"rules\": [],\"Recert\": false}," +
                                        "\"translate\": {\"rule_svc_neg\": false,\"rule_svc\": \"\",\"rule_services\": [],\"rule_src_neg\": false,\"rule_src\": \"\",\"rule_froms\": [],\"rule_dst_neg\": false,\"rule_dst\": \"\",\"rule_tos\": []}," +
                                        "\"owner_name\": \"\",\"owner_id\": null,\"matches\": \"\",\"rule_custom_fields\": \"\",\"rule_implied\": false,\"nat_rule\": false,\"rulebase_id\": 0,\"rule_num\": 0,\"rule_enforced_on_gateways\": [],\"rule_installon\": null,\"rule_time\": null,\"rule_times\": [],\"violations\": [],\"rulebase\": {\"id\": 0,\"name\": \"\",\"uid\": \"\",\"mgm_id\": 0,\"is_global\": false,\"created\": 0,\"removed\": 0,\"rules\": []},\"uiuser\": null,\"rule\": null,\"rule_owners\": [],\"ChangeID\": \"\",\"AdoITID\": \"\",\"Compliance\": 0,\"ViolationDetails\": \"\",\"DisplayOrderNumberString\": \"1\",\"DisplayOrderNumber\": 1,\"Certified\": false,\"DeviceName\": \"\",\"RulebaseName\": \"\",\"DisregardedFroms\": [],\"DisregardedTos\": [],\"DisregardedServices\": [],\"ShowDisregarded\": false}," +
                                        "{\"rule_id\": 0,\"rule_uid\": \"uid2:123\",\"mgm_id\": 0,\"rule_num_numeric\": 0,\"rule_name\": \"TestRule2\",\"rule_comment\": \"comment2\",\"rule_disabled\": false," +
                                        "\"rule_services\": [{\"service\": {\"svc_id\": 2,\"svc_name\": \"TestService2\",\"svc_uid\": \"\",\"svc_port\": 6666,\"svc_port_end\": 7777,\"svc_source_port\": null,\"svc_source_port_end\": null,\"svc_code\": \"\",\"svc_timeout\": null,\"svc_typ_id\": null,\"active\": false,\"svc_create\": 0,\"svc_create_time\": {\"time\": \"0001-01-01T00:00:00\"},\"svc_last_seen\": 0,\"service_type\": {\"name\": \"\"},\"svc_comment\": \"\",\"svc_color_id\": null,\"stm_color\": null,\"ip_proto_id\": null,\"protocol_name\": {\"id\": 17,\"name\": \"UDP\"},\"svc_member_names\": \"\",\"svc_member_refs\": \"\",\"svcgrps\": [],\"svcgrp_flats\": [],\"svc_rpcnr\": null}}]," +
                                        "\"rule_svc_neg\": true,\"rule_svc\": \"\",\"rule_svc_refs\": \"\",\"rule_src_neg\": true,\"rule_src\": \"\",\"rule_src_refs\": \"\",\"rule_from_zones\": []," +
                                        "\"rule_froms\": [" +
                                        "{\"object\": {\"obj_id\": 1,\"obj_name\": \"TestIp1\",\"obj_ip\": \"1.2.3.4/32\",\"obj_ip_end\": \"1.2.3.4/32\",\"obj_uid\": \"\",\"zone\": null,\"active\": false,\"obj_create\": 0,\"obj_create_time\": {\"time\": \"0001-01-01T00:00:00\"},\"obj_last_seen\": 0,\"type\": {\"id\": 0,\"name\": \"network\"},\"obj_color\": null,\"obj_comment\": \"\",\"obj_member_names\": \"\",\"obj_member_refs\": \"\",\"objgrps\": [],\"objgrp_flats\": []},\"usr\": {\"user_id\": 1,\"user_uid\": \"\",\"user_name\": \"TestUser1\",\"user_comment\": \"\",\"user_lastname\": \"\",\"user_firstname\": \"\",\"usr_typ_id\": 0,\"type\": {\"usr_typ_name\": \"\"},\"user_create\": 0,\"user_create_time\": {\"time\": \"0001-01-01T00:00:00\"},\"user_last_seen\": 0,\"user_member_names\": \"\",\"user_member_refs\": \"\",\"usergrps\": [],\"usergrp_flats\": []}}," +
                                        "{\"object\": {\"obj_id\": 2,\"obj_name\": \"TestIp2\",\"obj_ip\": \"127.0.0.1/32\",\"obj_ip_end\": \"127.0.0.1/32\",\"obj_uid\": \"\",\"zone\": null,\"active\": false,\"obj_create\": 0,\"obj_create_time\": {\"time\": \"0001-01-01T00:00:00\"},\"obj_last_seen\": 0,\"type\": {\"id\": 0,\"name\": \"network\"},\"obj_color\": null,\"obj_comment\": \"\",\"obj_member_names\": \"\",\"obj_member_refs\": \"\",\"objgrps\": [],\"objgrp_flats\": []},\"usr\": {\"user_id\": 1,\"user_uid\": \"\",\"user_name\": \"TestUser1\",\"user_comment\": \"\",\"user_lastname\": \"\",\"user_firstname\": \"\",\"usr_typ_id\": 0,\"type\": {\"usr_typ_name\": \"\"},\"user_create\": 0,\"user_create_time\": {\"time\": \"0001-01-01T00:00:00\"},\"user_last_seen\": 0,\"user_member_names\": \"\",\"user_member_refs\": \"\",\"usergrps\": [],\"usergrp_flats\": []}}]," +
                                        "\"rule_dst_neg\": true,\"rule_dst\": \"\",\"rule_dst_refs\": \"\",\"rule_to_zones\": []," +
                                        "\"rule_tos\": [" +
                                        "{\"object\": {\"obj_id\": 3,\"obj_name\": \"TestIpRange\",\"obj_ip\": \"1.2.3.4/32\",\"obj_ip_end\": \"1.2.3.5/32\",\"obj_uid\": \"\",\"zone\": null,\"active\": false,\"obj_create\": 0,\"obj_create_time\": {\"time\": \"0001-01-01T00:00:00\"},\"obj_last_seen\": 0,\"type\": {\"id\": 0,\"name\": \"ip_range\"},\"obj_color\": null,\"obj_comment\": \"\",\"obj_member_names\": \"\",\"obj_member_refs\": \"\",\"objgrps\": [],\"objgrp_flats\": []},\"usr\": {\"user_id\": 2,\"user_uid\": \"\",\"user_name\": \"TestUser2\",\"user_comment\": \"\",\"user_lastname\": \"\",\"user_firstname\": \"\",\"usr_typ_id\": 0,\"type\": {\"usr_typ_name\": \"group\"},\"user_create\": 0,\"user_create_time\": {\"time\": \"0001-01-01T00:00:00\"},\"user_last_seen\": 0,\"user_member_names\": \"\",\"user_member_refs\": \"\",\"usergrps\": [],\"usergrp_flats\": []}}]," +
                                        "\"rule_action\": \"deny\",\"rule_track\": \"none\",\"section_header\": \"\"," +
                                        "\"rule_metadatum\": {\"rule_metadata_id\": 0,\"rule_created\": null,\"created_import\": null,\"removed\": null,\"removed_import\": null,\"rule_first_hit\": null,\"rule_last_hit\": null,\"recertification\": [],\"recert_history\": [],\"rule_uid\": \"\",\"rules\": [],\"Recert\": false}," +
                                        "\"translate\": {\"rule_svc_neg\": false,\"rule_svc\": \"\",\"rule_services\": [],\"rule_src_neg\": false,\"rule_src\": \"\",\"rule_froms\": [],\"rule_dst_neg\": false,\"rule_dst\": \"\",\"rule_tos\": []}," +
                                        "\"owner_name\": \"\",\"owner_id\": null,\"matches\": \"\",\"rule_custom_fields\": \"\",\"rule_implied\": false,\"nat_rule\": false,\"rulebase_id\": 0,\"rule_num\": 0,\"rule_enforced_on_gateways\": [],\"rule_installon\": null,\"rule_time\": null,\"rule_times\": [],\"violations\": [],\"rulebase\": {\"id\": 0,\"name\": \"\",\"uid\": \"\",\"mgm_id\": 0,\"is_global\": false,\"created\": 0,\"removed\": 0,\"rules\": []},\"uiuser\": null,\"rule\": null,\"rule_owners\": [],\"ChangeID\": \"\",\"AdoITID\": \"\",\"Compliance\": 0,\"ViolationDetails\": \"\",\"DisplayOrderNumberString\": \"\",\"DisplayOrderNumber\": 2,\"Certified\": false,\"DeviceName\": \"\",\"RulebaseName\": \"\",\"DisregardedFroms\": [],\"DisregardedTos\": [],\"DisregardedServices\": [],\"ShowDisregarded\": false}]}]," +
                                        "\"changelog_rules\": null,\"changelog_objects\": null,\"changelog_services\": null,\"changelog_users\": null,\"import\": {\"aggregate\": {\"max\": {\"id\": null}}},\"import_controls\": [],\"RelevantImportId\": null,\"is_super_manager\": false,\"multi_device_manager_id\": null,\"management\": null,\"managementByMultiDeviceManagerId\": [],\"networkObjects\": [],\"serviceObjects\": [],\"userObjects\": [],\"zoneObjects\": []," +
                                        "\"reportNetworkObjects\": [{\"obj_id\": 1,\"obj_name\": \"TestIp1\",\"obj_ip\": \"1.2.3.4/32\",\"obj_ip_end\": \"1.2.3.4/32\",\"obj_uid\": \"\",\"zone\": null,\"active\": false,\"obj_create\": 0," +
                                        "\"obj_create_time\": {\"time\": \"0001-01-01T00:00:00\"},\"obj_last_seen\": 0,\"type\": {\"id\": 0,\"name\": \"network\"},\"obj_color\": null,\"obj_comment\": \"\",\"obj_member_names\": \"\",\"obj_member_refs\": \"\"," +
                                        "\"objgrps\": [],\"objgrp_flats\": []},{\"obj_id\": 2,\"obj_name\": \"TestIp2\",\"obj_ip\": \"127.0.0.1/32\",\"obj_ip_end\": \"127.0.0.1/32\",\"obj_uid\": \"\",\"zone\": null,\"active\": false,\"obj_create\": 0," +
                                        "\"obj_create_time\": {\"time\": \"0001-01-01T00:00:00\"},\"obj_last_seen\": 0,\"type\": {\"id\": 0,\"name\": \"network\"},\"obj_color\": null,\"obj_comment\": \"\",\"obj_member_names\": \"\",\"obj_member_refs\": \"\",\"objgrps\": []," +
                                        "\"objgrp_flats\": []},{\"obj_id\": 3,\"obj_name\": \"TestIpRange\",\"obj_ip\": \"1.2.3.4/32\",\"obj_ip_end\": \"1.2.3.5/32\",\"obj_uid\": \"\",\"zone\": null,\"active\": false,\"obj_create\": 0," +
                                        "\"obj_create_time\": {\"time\": \"0001-01-01T00:00:00\"},\"obj_last_seen\": 0,\"type\": {\"id\": 0,\"name\": \"ip_range\"},\"obj_color\": null,\"obj_comment\": \"\",\"obj_member_names\": \"\",\"obj_member_refs\": \"\"," +
                                        "\"objgrps\": [],\"objgrp_flats\": []}],\"reportServiceObjects\": [{\"svc_id\": 1,\"svc_name\": \"TestService1\",\"svc_uid\": \"\",\"svc_port\": 443,\"svc_port_end\": 443,\"svc_source_port\": null,\"svc_source_port_end\": null," +
                                        "\"svc_code\": \"\",\"svc_timeout\": null,\"svc_typ_id\": null,\"active\": false,\"svc_create\": 0,\"svc_create_time\": {\"time\": \"0001-01-01T00:00:00\"},\"svc_last_seen\": 0,\"service_type\": {\"name\": \"\"},\"svc_comment\": \"\"," +
                                        "\"svc_color_id\": null,\"stm_color\": null,\"ip_proto_id\": null,\"protocol_name\": {\"id\": 6,\"name\": \"TCP\"},\"svc_member_names\": \"\",\"svc_member_refs\": \"\",\"svcgrps\": [],\"svcgrp_flats\": []," +
                                        "\"svc_rpcnr\": null},{\"svc_id\": 2,\"svc_name\": \"TestService2\",\"svc_uid\": \"\",\"svc_port\": 6666,\"svc_port_end\": 7777,\"svc_source_port\": null,\"svc_source_port_end\": null,\"svc_code\": \"\"," +
                                        "\"svc_timeout\": null,\"svc_typ_id\": null,\"active\": false,\"svc_create\": 0,\"svc_create_time\": {\"time\": \"0001-01-01T00:00:00\"},\"svc_last_seen\": 0,\"service_type\": {\"name\": \"\"},\"svc_comment\": \"\"," +
                                        "\"svc_color_id\": null,\"stm_color\": null,\"ip_proto_id\": null,\"protocol_name\": {\"id\": 17,\"name\": \"UDP\"},\"svc_member_names\": \"\",\"svc_member_refs\": \"\",\"svcgrps\": [],\"svcgrp_flats\": [],\"svc_rpcnr\": null}]," +
                                        "\"reportUserObjects\": [{\"user_id\": 1,\"user_uid\": \"\",\"user_name\": \"TestUser1\",\"user_comment\": \"\",\"user_lastname\": \"\",\"user_firstname\": \"\",\"usr_typ_id\": 0,\"type\": {\"usr_typ_name\": \"\"},\"user_create\": 0," +
                                        "\"user_create_time\": {\"time\": \"0001-01-01T00:00:00\"},\"user_last_seen\": 0,\"user_member_names\": \"\",\"user_member_refs\": \"\",\"usergrps\": [],\"usergrp_flats\": []},{\"user_id\": 2,\"user_uid\": \"\"," +
                                        "\"user_name\": \"TestUser2\",\"user_comment\": \"\",\"user_lastname\": \"\",\"user_firstname\": \"\",\"usr_typ_id\": 0,\"type\": {\"usr_typ_name\": \"group\"},\"user_create\": 0,\"user_create_time\": {\"time\": \"0001-01-01T00:00:00\"}," +
                                        "\"user_last_seen\": 0,\"user_member_names\": \"\",\"user_member_refs\": \"\",\"usergrps\": [],\"usergrp_flats\": []}],\"ReportedRuleIds\": [0],\"ReportedNetworkServiceIds\": [],\"objects_aggregate\": {\"aggregate\": {\"count\": 0}}," +
                                        "\"services_aggregate\": {\"aggregate\": {\"count\": 0}},\"usrs_aggregate\": {\"aggregate\": {\"count\": 0}},\"rules_aggregate\": {\"aggregate\": {\"count\": 0}},\"unusedRules_Count\": {\"aggregate\": {\"count\": 0}},\"Ignore\": false}]";
            string jsonExport = RemoveLinebreaks(RemoveGenDate(reportRules.ExportToJson(), false, true));
            ClassicAssert.AreEqual(expectedJsonResult, jsonExport);
        }

        [Test]
        public void ResolvedRulesGenerateJson()
        {
            Log.WriteInfo("Test Log", "starting resolved rules report json generation");
            ReportRules reportRules = ConstructReportRules(query, userConfig, ReportType.ResolvedRules, ConstructRuleReportRules(true));

            string expectedJsonResult =
            "{\"report type\": \"Rules Report (resolved)\",\"report generation date\": \"Z (UTC)\"," +
            "\"date of configuration shown\": \"2023-04-20T15:50:04Z (UTC)\",\"device filter\": \"TestMgt [Mock Device 1]\",\"other filters\": \"TestFilter\"," +
            "\"report generator\": \"Firewall Orchestrator - https://fwo.cactus.de/en\",\"data protection level\": \"For internal use only\"," +
            "\"managements\": [{\"TestMgt\": {\"gateways\": [{\"Mock Device 1\": {" +
            "\"rules\": [{\"number\": 1,\"name\": \"TestRule1\",\"source zones\": [\"srczn1\",\"srczn2\",\"srczn3\"],\"source negated\": false," +
            "\"source\": [\"TestIp1 (1.2.3.4/32)\",\"TestIp2 (127.0.0.1/32)\"],\"destination zones\": [\"dstzn1\",\"dstzn2\",\"dstzn3\"],\"destination negated\": false," +
            "\"destination\": [\"TestIpRange (1.2.3.4-1.2.3.5)\"],\"service negated\": false," +
            "\"service\": [\"TestService1 (443/TCP)\"],\"action\": \"accept\",\"tracking\": \"none\",\"disabled\": false,\"rule uid\": \"uid1\",\"comment\": \"comment1\"}," +
            "{\"number\": 2,\"name\": \"TestRule2\",\"source zones\": [],\"source negated\": true," +
            "\"source\": [\"TestUser1@TestIp1 (1.2.3.4/32)\",\"TestUser1@TestIp2 (127.0.0.1/32)\"],\"destination zones\": [],\"destination negated\": true," +
            "\"destination\": [\"TestUser2@TestIpRange (1.2.3.4-1.2.3.5)\"],\"service negated\": true," +
            "\"service\": [\"TestService2 (6666-7777/UDP)\"],\"action\": \"deny\",\"tracking\": \"none\",\"disabled\": false,\"rule uid\": \"uid2:123\",\"comment\": \"comment2\"}]}}]}}]}";
            string jsonExport = RemoveLinebreaks(RemoveGenDate(reportRules.ExportToJson(), false, true));
            ClassicAssert.AreEqual(expectedJsonResult, jsonExport);
        }

        [Test]
        public void ResolvedRulesGenerateJsonWithoutRules()
        {
            Log.WriteInfo("Test Log", "starting resolved rules report json generation");
            ReportRules reportRules = ConstructReportRulesWithoutRules(true, query, userConfig, ReportType.ResolvedRules);

            string expectedJsonResult =
            "{\"report type\": \"Rules Report (resolved)\",\"report generation date\": \"Z (UTC)\"," +
            "\"date of configuration shown\": \"2023-04-20T15:50:04Z (UTC)\",\"device filter\": \"TestMgt [Mock Device 1]\",\"other filters\": \"TestFilter\"," +
            "\"report generator\": \"Firewall Orchestrator - https://fwo.cactus.de/en\",\"data protection level\": \"For internal use only\"," +
            "\"managements\": [{\"TestMgt\": {\"gateways\": [{\"Mock Device 1\": {" +
            "\"rules\": []}}]}}]}";
            string jsonExport = RemoveLinebreaks(RemoveGenDate(reportRules.ExportToJson(), false, true));
            ClassicAssert.AreEqual(expectedJsonResult, jsonExport);
        }

        [Test]
        public void ResolvedRulesTechGenerateJson()
        {
            Log.WriteInfo("Test Log", "starting resolved rules report tech json generation");
            ReportRules reportRules = ConstructReportRules(query, userConfig, ReportType.ResolvedRulesTech, ConstructRuleReportRules(true));
            string expectedJsonResult =
            "{\"report type\": \"Rules Report (technical)\",\"report generation date\": \"Z (UTC)\"," +
            "\"date of configuration shown\": \"2023-04-20T15:50:04Z (UTC)\"," +
            "\"device filter\": \"TestMgt [Mock Device 1]\",\"other filters\": \"TestFilter\"," +
            "\"report generator\": \"Firewall Orchestrator - https://fwo.cactus.de/en\",\"data protection level\": \"For internal use only\"," +
            "\"managements\": [{\"TestMgt\": {\"gateways\": [{\"Mock Device 1\": {" +
            "\"rules\": [{\"number\": 1,\"name\": \"TestRule1\",\"source zones\": [\"srczn1\",\"srczn2\",\"srczn3\"],\"source negated\": false," +
            "\"source\": [\"1.2.3.4/32\",\"127.0.0.1/32\"],\"destination zones\": [\"dstzn1\",\"dstzn2\",\"dstzn3\"],\"destination negated\": false," +
            "\"destination\": [\"1.2.3.4-1.2.3.5\"],\"service negated\": false," +
            "\"service\": [\"443/TCP\"],\"action\": \"accept\",\"tracking\": \"none\",\"disabled\": false,\"rule uid\": \"uid1\",\"comment\": \"comment1\"}," +
            "{\"number\": 2,\"name\": \"TestRule2\",\"source zones\": [],\"source negated\": true," +
            "\"source\": [\"TestUser1@1.2.3.4/32\",\"TestUser1@127.0.0.1/32\"],\"destination zones\": []," +
            "\"destination negated\": true,\"destination\": [\"TestUser2@1.2.3.4-1.2.3.5\"],\"service negated\": true," +
            "\"service\": [\"6666-7777/UDP\"],\"action\": \"deny\",\"tracking\": \"none\",\"disabled\": false,\"rule uid\": \"uid2:123\",\"comment\": \"comment2\"}]}}]}}]}";
            string exportJson = RemoveLinebreaks(RemoveGenDate(reportRules.ExportToJson(), false, true));
            ClassicAssert.AreEqual(expectedJsonResult, exportJson);
        }

        [Test]
        public void ChangesGenerateJson()
        {
            Log.WriteInfo("Test Log", "starting changes report json generation");
            ReportChanges reportChanges = new(query, userConfig, ReportType.Changes, new TimeFilter(), false, true)
            {
                ReportData = ConstructChangeReport(false)
            };

            string expectedJsonResult =
            "{\"report type\": \"Changes Report\",\"report generation date\": \"Z (UTC)\",\"device filter\": \"TestMgt [TestDev]\",\"other filters\": \"TestFilter\",\"report generator\": \"Firewall Orchestrator - https://fwo.cactus.de/en\",\"data protection level\": \"For internal use only\"," +
            "\"managements\": [{\"TestMgt\": {\"rule changes\": [" +
            "{\"change time\": \"05.04.2023 12:00:00\",\"change action\": \"Rule added\",\"name\": \"TestRule1\"," +
            "\"source zones\": [\"srczn1\",\"srczn2\",\"srczn3\"],\"source negated\": false,\"source\": []," +
            "\"destination zones\": [\"dstzn1\",\"dstzn2\",\"dstzn3\"],\"destination negated\": false,\"destination\": []," +
            "\"service negated\": false,\"service\": [],\"action\": \"accept\",\"tracking\": \"none\",\"disabled\": false,\"Enforcing Gateway\": [],\"rule uid\": \"uid1\",\"comment\": \"comment1\"}," +
            "{\"change time\": \"05.04.2023 12:00:00\",\"change action\": \"Rule modified\",\"name\": \"TestRule1\"," +
            "\"source zones\": [\"srczn1\",\"srczn2\",\"srczn3\"],\"source negated\": false,\"source\": []," +
            "\"destination zones\": [\"dstzn1\",\"dstzn2\",\"dstzn3\"],\"destination negated\": false,\"destination\": []," +
            "\"service negated\": \"deleted: false, added: true\",\"service\": [],\"action\": \"accept\",\"tracking\": \"none\",\"disabled\": false,\"EnforcingGateways\": [],\"rule uid\": \"deleted: uid1\",\"comment\": \"deleted: comment1, added: new comment\"}," +
            "{\"change time\": \"05.04.2023 12:00:00\",\"change action\": \"Rule modified\",\"name\": \"TestRule2\"," +
            "\"source zones\": [],\"source negated\": true,\"source\": []," +
            "\"destination zones\": [],\"destination negated\": \"deleted: true, added: false\",\"destination\": []," +
            "\"service negated\": \"deleted: true, added: false\",\"service\": [],\"action\": \"deny\",\"tracking\": \"none\",\"disabled\": \"deleted: false, added: true\",\"EnforcingGateways\": [],\"rule uid\": \"uid2:123\",\"comment\": \"comment2\"}," +
            "{\"change time\": \"05.04.2023 12:00:00\",\"change action\": \"Rule deleted\",\"name\": \"TestRule2\"," +
            "\"source zones\": [],\"source negated\": true,\"source\": []," +
            "\"destination zones\": [],\"destination negated\": true,\"destination\": []," +
            "\"service negated\": true,\"service\": [],\"action\": \"deny\",\"tracking\": \"none\",\"disabled\": false,\"Enforcing Gateway\": [],\"rule uid\": \"uid2:123\",\"comment\": \"comment2\"}]}}]}";
            ClassicAssert.AreEqual(expectedJsonResult, RemoveLinebreaks(RemoveGenDate(reportChanges.ExportToJson(), false, true)));
        }

        [Test]
        public void ResolvedChangesGenerateJson()
        {
            Log.WriteInfo("Test Log", "starting resolved changes report json generation");
            ReportChanges reportChanges = new(query, userConfig, ReportType.ResolvedChanges, new TimeFilter(), false, true)
            {
                ReportData = ConstructChangeReport(true)
            };

            string expectedJsonResult =
            "{\"report type\": \"Changes Report (resolved)\",\"report generation date\": \"Z (UTC)\",\"device filter\": \"TestMgt [TestDev]\",\"other filters\": \"TestFilter\",\"report generator\": \"Firewall Orchestrator - https://fwo.cactus.de/en\",\"data protection level\": \"For internal use only\"," +
            "\"managements\": [{\"TestMgt\": {\"rule changes\": [" +
            "{\"change time\": \"05.04.2023 12:00:00\",\"change action\": \"Rule added\",\"name\": \"TestRule1\"," +
            "\"source zones\": [\"srczn1\",\"srczn2\",\"srczn3\"],\"source negated\": false,\"source\": [\"TestIp1 (1.2.3.4/32)\",\"TestIp2 (127.0.0.1/32)\"]," +
            "\"destination zones\": [\"dstzn1\",\"dstzn2\",\"dstzn3\"],\"destination negated\": false,\"destination\": [\"TestIpRange (1.2.3.4-1.2.3.5)\"]," +
            "\"service negated\": false,\"service\": [\"TestService1 (443/TCP)\"],\"action\": \"accept\",\"tracking\": \"none\",\"disabled\": false,\"Enforcing Gateway\": [],\"rule uid\": \"uid1\",\"comment\": \"comment1\"}," +
            "{\"change time\": \"05.04.2023 12:00:00\",\"change action\": \"Rule modified\",\"name\": \"TestRule1\"," +
            "\"source zones\": [\"srczn1\",\"srczn2\",\"srczn3\"],\"source negated\": false,\"source\": [\"TestIp2 (127.0.0.1/32)\",\"deleted: TestIp1 (1.2.3.4/32)\",\"added: TestIp1Changed (2.3.4.5)\"]," +
            "\"destination zones\": [\"dstzn1\",\"dstzn2\",\"dstzn3\"],\"destination negated\": false,\"destination\": [\"TestIpRange (1.2.3.4-1.2.3.5)\",\"added: TestIpNew (10.0.6.0/24)\"]," +
            "\"service negated\": \"deleted: false, added: true\",\"service\": [\"TestService1 (443/TCP)\"],\"action\": \"accept\",\"tracking\": \"none\",\"disabled\": false,\"EnforcingGateways\": [],\"rule uid\": \"deleted: uid1\",\"comment\": \"deleted: comment1, added: new comment\"}," +
            "{\"change time\": \"05.04.2023 12:00:00\",\"change action\": \"Rule modified\",\"name\": \"TestRule2\"," +
            "\"source zones\": [],\"source negated\": true,\"source\": [\"TestUser1@TestIp1 (1.2.3.4/32)\",\"TestUser1@TestIp2 (127.0.0.1/32)\"]," +
            "\"destination zones\": [],\"destination negated\": \"deleted: true, added: false\",\"destination\": [\"TestUser2@TestIpRange (1.2.3.4-1.2.3.5)\"]," +
            "\"service negated\": \"deleted: true, added: false\",\"service\": [\"TestService2 (6666-7777/UDP)\"],\"action\": \"deny\",\"tracking\": \"none\",\"disabled\": \"deleted: false, added: true\",\"EnforcingGateways\": [],\"rule uid\": \"uid2:123\",\"comment\": \"comment2\"}," +
            "{\"change time\": \"05.04.2023 12:00:00\",\"change action\": \"Rule deleted\",\"name\": \"TestRule2\"," +
            "\"source zones\": [],\"source negated\": true,\"source\": [\"TestUser1@TestIp1 (1.2.3.4/32)\",\"TestUser1@TestIp2 (127.0.0.1/32)\"]," +
            "\"destination zones\": [],\"destination negated\": true,\"destination\": [\"TestUser2@TestIpRange (1.2.3.4-1.2.3.5)\"]," +
            "\"service negated\": true,\"service\": [\"TestService2 (6666-7777/UDP)\"],\"action\": \"deny\",\"tracking\": \"none\",\"disabled\": false,\"Enforcing Gateway\": [],\"rule uid\": \"uid2:123\",\"comment\": \"comment2\"}]}}]}";
            // Log.WriteInfo("Test Log", removeLinebreaks((removeGenDate(reportChanges.ExportToJson(), false, true))));
            ClassicAssert.AreEqual(expectedJsonResult, RemoveLinebreaks(RemoveGenDate(reportChanges.ExportToJson(), false, true)));
        }

        [Test]
        public void ResolvedChangesTechGenerateJson()
        {
            Log.WriteInfo("Test Log", "starting resolved changes report json generation");
            ReportChanges reportChanges = new(query, userConfig, ReportType.ResolvedChangesTech, new TimeFilter(), false, true)
            {
                ReportData = ConstructChangeReport(true)
            };

            string expectedJsonResult =
            "{\"report type\": \"Changes Report (technical)\",\"report generation date\": \"Z (UTC)\",\"device filter\": \"TestMgt [TestDev]\",\"other filters\": \"TestFilter\",\"report generator\": \"Firewall Orchestrator - https://fwo.cactus.de/en\",\"data protection level\": \"For internal use only\"," +
            "\"managements\": [{\"TestMgt\": {\"rule changes\": [" +
            "{\"change time\": \"05.04.2023 12:00:00\",\"change action\": \"Rule added\",\"name\": \"TestRule1\"," +
            "\"source zones\": [\"srczn1\",\"srczn2\",\"srczn3\"],\"source negated\": false,\"source\": [\"1.2.3.4/32\",\"127.0.0.1/32\"]," +
            "\"destination zones\": [\"dstzn1\",\"dstzn2\",\"dstzn3\"],\"destination negated\": false,\"destination\": [\"1.2.3.4-1.2.3.5\"]," +
            "\"service negated\": false,\"service\": [\"443/TCP\"],\"action\": \"accept\",\"tracking\": \"none\",\"disabled\": false,\"Enforcing Gateway\": [],\"rule uid\": \"uid1\",\"comment\": \"comment1\"}," +
            "{\"change time\": \"05.04.2023 12:00:00\",\"change action\": \"Rule modified\",\"name\": \"TestRule1\"," +
            "\"source zones\": [\"srczn1\",\"srczn2\",\"srczn3\"],\"source negated\": false,\"source\": [\"127.0.0.1/32\",\"deleted: 1.2.3.4/32\",\"added: 2.3.4.5\"]," +
            "\"destination zones\": [\"dstzn1\",\"dstzn2\",\"dstzn3\"],\"destination negated\": false,\"destination\": [\"1.2.3.4-1.2.3.5\",\"added: 10.0.6.0/24\"]," +
            "\"service negated\": \"deleted: false, added: true\",\"service\": [\"443/TCP\"],\"action\": \"accept\",\"tracking\": \"none\",\"disabled\": false,\"EnforcingGateways\": [],\"rule uid\": \"deleted: uid1\",\"comment\": \"deleted: comment1, added: new comment\"}," +
            "{\"change time\": \"05.04.2023 12:00:00\",\"change action\": \"Rule modified\",\"name\": \"TestRule2\"," +
            "\"source zones\": [],\"source negated\": true,\"source\": [\"TestUser1@1.2.3.4/32\",\"TestUser1@127.0.0.1/32\"]," +
            "\"destination zones\": [],\"destination negated\": \"deleted: true, added: false\",\"destination\": [\"TestUser2@1.2.3.4-1.2.3.5\"]," +
            "\"service negated\": \"deleted: true, added: false\",\"service\": [\"6666-7777/UDP\"],\"action\": \"deny\",\"tracking\": \"none\",\"disabled\": \"deleted: false, added: true\",\"EnforcingGateways\": [],\"rule uid\": \"uid2:123\",\"comment\": \"comment2\"}," +
            "{\"change time\": \"05.04.2023 12:00:00\",\"change action\": \"Rule deleted\",\"name\": \"TestRule2\"," +
            "\"source zones\": [],\"source negated\": true,\"source\": [\"TestUser1@1.2.3.4/32\",\"TestUser1@127.0.0.1/32\"]," +
            "\"destination zones\": [],\"destination negated\": true,\"destination\": [\"TestUser2@1.2.3.4-1.2.3.5\"]," +
            "\"service negated\": true,\"service\": [\"6666-7777/UDP\"],\"action\": \"deny\",\"tracking\": \"none\",\"disabled\": false,\"Enforcing Gateway\": [],\"rule uid\": \"uid2:123\",\"comment\": \"comment2\"}]}}]}";
            ClassicAssert.AreEqual(expectedJsonResult, RemoveLinebreaks(RemoveGenDate(reportChanges.ExportToJson(), false, true)));
        }
        [Test]
        public void ResolvedChangesTechGenerateJsonIncludeObjects()
        {
            Log.WriteInfo("Test Log", "starting resolved changes report json generation");
            ReportChanges reportChanges = new(query, userConfig, ReportType.ResolvedChangesTech, new TimeFilter(), true, true)
            {
                ReportData = ConstructChangeReport(true)
            };

            string expectedJsonResult =
            "{\"report type\": \"Changes Report (technical)\",\"report generation date\": \"Z (UTC)\",\"device filter\": \"TestMgt [TestDev]\",\"other filters\": \"TestFilter\",\"report generator\": \"Firewall Orchestrator - https://fwo.cactus.de/en\",\"data protection level\": \"For internal use only\"," +
            "\"managements\": [{\"TestMgt\": {\"rule changes\": [" +
            "{\"change time\": \"05.04.2023 12:00:00\",\"change action\": \"Rule added\",\"name\": \"TestRule1\"," +
            "\"source zones\": [\"srczn1\",\"srczn2\",\"srczn3\"],\"source negated\": false,\"source\": [\"1.2.3.4/32\",\"127.0.0.1/32\"]," +
            "\"destination zones\": [\"dstzn1\",\"dstzn2\",\"dstzn3\"],\"destination negated\": false,\"destination\": [\"1.2.3.4-1.2.3.5\"]," +
            "\"service negated\": false,\"service\": [\"443/TCP\"],\"action\": \"accept\",\"tracking\": \"none\",\"disabled\": false,\"Enforcing Gateway\": [],\"rule uid\": \"uid1\",\"comment\": \"comment1\"}," +
            "{\"change time\": \"05.04.2023 12:00:00\",\"change action\": \"Rule modified\",\"name\": \"TestRule1\"," +
            "\"source zones\": [\"srczn1\",\"srczn2\",\"srczn3\"],\"source negated\": false,\"source\": [\"127.0.0.1/32\",\"deleted: 1.2.3.4/32\",\"added: 2.3.4.5\"]," +
            "\"destination zones\": [\"dstzn1\",\"dstzn2\",\"dstzn3\"],\"destination negated\": false,\"destination\": [\"1.2.3.4-1.2.3.5\",\"added: 10.0.6.0/24\"]," +
            "\"service negated\": \"deleted: false, added: true\",\"service\": [\"443/TCP\"],\"action\": \"accept\",\"tracking\": \"none\",\"disabled\": false,\"EnforcingGateways\": [],\"rule uid\": \"deleted: uid1\",\"comment\": \"deleted: comment1, added: new comment\"}," +
            "{\"change time\": \"05.04.2023 12:00:00\",\"change action\": \"Rule modified\",\"name\": \"TestRule2\"," +
            "\"source zones\": [],\"source negated\": true,\"source\": [\"TestUser1@1.2.3.4/32\",\"TestUser1@127.0.0.1/32\"]," +
            "\"destination zones\": [],\"destination negated\": \"deleted: true, added: false\",\"destination\": [\"TestUser2@1.2.3.4-1.2.3.5\"]," +
            "\"service negated\": \"deleted: true, added: false\",\"service\": [\"6666-7777/UDP\"],\"action\": \"deny\",\"tracking\": \"none\",\"disabled\": \"deleted: false, added: true\",\"EnforcingGateways\": [],\"rule uid\": \"uid2:123\",\"comment\": \"comment2\"}," +
            "{\"change time\": \"05.04.2023 12:00:00\",\"change action\": \"Rule deleted\",\"name\": \"TestRule2\"," +
            "\"source zones\": [],\"source negated\": true,\"source\": [\"TestUser1@1.2.3.4/32\",\"TestUser1@127.0.0.1/32\"]," +
            "\"destination zones\": [],\"destination negated\": true,\"destination\": [\"TestUser2@1.2.3.4-1.2.3.5\"]," +
            "\"service negated\": true,\"service\": [\"6666-7777/UDP\"],\"action\": \"deny\",\"tracking\": \"none\",\"disabled\": false,\"Enforcing Gateway\": [],\"rule uid\": \"uid2:123\",\"comment\": \"comment2\"}]," +
            "\"Network Object changes\": [{\"change time\": \"05.04.2023 12:00:00\",\"change action\": \"network_object_added\",\"name\": \"TestIp1\",\"ObjectType\": \"network\",\"Object Type\": \" (1.2.3.4)\"," +
            "\"Member Names\": \"[]\",\"rule uid\": \"\",\"comment\": \"\"},{\"change time\": \"05.04.2023 12:00:00\",\"change action\": \"network_object_modified\",\"name\": \"deleted: TestIp1, added: TestIp1Changed\"," +
            "\"ObjectType\": \"deleted: network, added: host\",\"Object Type\": \"deleted:  (1.2.3.4), added:  (2.3.4.5)\",\"Member Names\": \"[]\",\"rule uid\": \"\",\"comment\": \"\"},{\"change time\": \"05.04.2023 12:00:00\"," +
            "\"change action\": \"network_object_deleted\",\"name\": \"TestIp2\",\"ObjectType\": \"network\",\"Object Type\": \" (127.0.0.1)\",\"Member Names\": \"[]\",\"rule uid\": \"\",\"comment\": \"\"}]," +
            "\"Service Object changes\": [{\"change time\": \"05.04.2023 12:00:00\",\"change action\": \"service_added\",\"name\": \"TestService1\",\"ObjectType\": \"\",\"Protocol\": \"TCP\",\"Port\": \" (443)\",\"Member Names\": \"[]\"," +
            "\"rule uid\": \"\",\"comment\": \"\"},{\"change time\": \"05.04.2023 12:00:00\",\"change action\": \"service_modified\",\"name\": \"deleted: TestService1, added: TestService2\",\"ObjectType\": \"\"," +
            "\"Protocol\": \"deleted: TCP, added: UDP\",\"Port\": \"deleted:  (443), added:  (6666-7777)\",\"Member Names\": \"[]\",\"rule uid\": \"\",\"comment\": \"\"},{\"change time\": \"05.04.2023 12:00:00\"," +
            "\"change action\": \"service_deleted\",\"name\": \"TestService1\",\"ObjectType\": \"\",\"Protocol\": \"TCP\",\"Port\": \" (443)\",\"Member Names\": \"[]\",\"rule uid\": \"\",\"comment\": \"\"}]}}]}";
            ClassicAssert.AreEqual(expectedJsonResult, RemoveLinebreaks(RemoveGenDate(reportChanges.ExportToJson(), false, true)));
        }
    }
}
