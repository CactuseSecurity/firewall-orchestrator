﻿using FWO.Config.Api;

namespace FWO.Test
{
    internal class SimulatedUserConfig : UserConfig
    {
        public Dictionary<string, string> DummyTranslate = new Dictionary<string, string>()
        {
            {"Rules","Rules Report"},
            {"ResolvedRules","Rules Report (resolved)"},
            {"ResolvedRulesTech","Rules Report (technical)"},
            {"UnusedRules","Unused Rules Report"},
            {"Recertification","Recertification Report"},
            {"NatRules","NAT Rules Report"},
            {"Changes","Changes Report"},
            {"ResolvedChanges","Changes Report (resolved)"},
            {"ResolvedChangesTech","Changes Report (technical)"},
            {"Connections","Connections Report"},
            {"date_of_config","Time of configuration"},
            {"generated_on","Generated on"},
            {"negated","not"},
            {"users","Users"},
            {"rule_added","Rule added"},
            {"rule_deleted","Rule deleted"},
            {"rule_modified","Rule modified"},
            {"deleted","deleted"},
            {"added","added"},
            {"change_time","Change Time"},
            {"change_type","Change Type"},
            {"number","No."},
            {"name","Name"},
            {"source_zone","Source Zone"},
            {"source","Source"},
            {"destination_zone","Destination Zone"},
            {"destination","Destination"},
            {"services","Services"},
            {"action","Action"},
            {"track","Track"},
            {"enabled","Enabled"},
            {"uid","Uid"},
            {"comment","Comment"},
            {"type","Type"},
            {"ip_address","IP Address"},
            {"members","Members"},
            {"network_objects","Network Objects"},
            {"network_services","Network Services"},
            {"protocol","Protocol"},
            {"port","Port"},
            {"next_recert","Next Recertification Date"},
            {"owner","Owner"},
            {"ip_matches","IP address match"},
            {"last_hit","Last Hit"},
            {"trans_source","Translated Source"},
            {"trans_destination","Translated Destination"},
            {"trans_services","Translated Services"},
            {"from","from"},
            {"until","until"},
            {"C9001","This object was..."},
            {"C9002","This App Server was..."},
            {"is_in_use","Is in use"},
            {"devices","Devices"},
            {"owners","Owners"},
            {"filter","Filter"},
            {"id","Id"},
            {"ip","Ip"},
            {"group","Group"},
            {"host","Host"},
            {"network","Network"},
            {"ip_range","IP Range"},
            {"connections","Connections"},
            {"interfaces","Interfaces"},
            {"own_common_services","Own Common Services"},
            {"global_common_services","Global Common Services"},
            {"func_reason","Functional Reason"},
            {"interface_description","Interface Description"},
            {"published","Published"}
        };

        public override string GetText(string key)
        {
            return DummyTranslate[key];
        }
    }
}
