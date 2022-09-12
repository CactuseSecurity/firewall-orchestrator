
INSERT INTO language ("name", "culture_info") VALUES('German', 'de-DE');
INSERT INTO language ("name", "culture_info") VALUES('English', 'en-US');

insert into uiuser (uiuser_id, uiuser_username, uuid) VALUES (0,'default', 'default');

insert into config (config_key, config_value, config_user) VALUES ('DefaultLanguage', 'English', 0);
insert into config (config_key, config_value, config_user) VALUES ('sessionTimeout', '720', 0);
insert into config (config_key, config_value, config_user) VALUES ('sessionTimeoutNoticePeriod', '60', 0); -- in minutes before expiry
-- insert into config (config_key, config_value, config_user) VALUES ('maxMessages', '3', 0);
insert into config (config_key, config_value, config_user) VALUES ('elementsPerFetch', '100', 0);
insert into config (config_key, config_value, config_user) VALUES ('maxInitialFetchesRightSidebar', '10', 0);
insert into config (config_key, config_value, config_user) VALUES ('autoFillRightSidebar', 'True', 0);
insert into config (config_key, config_value, config_user) VALUES ('dataRetentionTime', '731', 0);
insert into config (config_key, config_value, config_user) VALUES ('importSleepTime', '40', 0);
insert into config (config_key, config_value, config_user) VALUES ('importCheckCertificates', 'False', 0);
insert into config (config_key, config_value, config_user) VALUES ('importSuppressCertificateWarnings', 'True', 0);
insert into config (config_key, config_value, config_user) VALUES ('fwApiElementsPerFetch', '150', 0);
insert into config (config_key, config_value, config_user) VALUES ('recertificationPeriod', '365', 0);
insert into config (config_key, config_value, config_user) VALUES ('recertificationNoticePeriod', '30', 0);
insert into config (config_key, config_value, config_user) VALUES ('recertificationDisplayPeriod', '30', 0);
insert into config (config_key, config_value, config_user) VALUES ('ruleRemovalGracePeriod', '60', 0);
insert into config (config_key, config_value, config_user) VALUES ('commentRequired', 'False', 0);
insert into config (config_key, config_value, config_user) VALUES ('messageViewTime', '7', 0);
insert into config (config_key, config_value, config_user) VALUES ('dailyCheckStartAt', '00:00:00', 0);
insert into config (config_key, config_value, config_user) VALUES ('autoDiscoverStartAt', '00:00:00', 0);
insert into config (config_key, config_value, config_user) VALUES ('autoDiscoverSleepTime', '24', 0);
insert into config (config_key, config_value, config_user) VALUES ('minCollapseAllDevices', '15', 0);
insert into config (config_key, config_value, config_user) VALUES ('pwMinLength', '10', 0);
insert into config (config_key, config_value, config_user) VALUES ('pwUpperCaseRequired', 'False', 0);
insert into config (config_key, config_value, config_user) VALUES ('pwLowerCaseRequired', 'False', 0);
insert into config (config_key, config_value, config_user) VALUES ('pwNumberRequired', 'False', 0);
insert into config (config_key, config_value, config_user) VALUES ('pwSpecialCharactersRequired', 'False', 0);
insert into config (config_key, config_value, config_user) VALUES ('maxImportDuration', '4', 0);
insert into config (config_key, config_value, config_user) VALUES ('maxImportInterval', '12', 0);
insert into config (config_key, config_value, config_user) VALUES ('reqMasterStateMatrix', '{"config_value":{"request":{"matrix":{"0":[0,49,620],"49":[49,620],"620":[620]},"derived_states":{"0":0,"49":49,"620":620},"lowest_input_state":0,"lowest_start_state":49,"lowest_end_state":49,"active":true},"approval":{"matrix":{"49":[60],"60":[60,99,610],"99":[99],"610":[610]},"derived_states":{"49":49,"60":60,"99":99,"610":610},"lowest_input_state":49,"lowest_start_state":60,"lowest_end_state":99,"active":true},"planning":{"matrix":{"99":[110],"110":[110,120,130,149],"120":[120,110,130,149],"130":[130,110,120,149,610],"149":[149],"610":[610]},"derived_states":{"99":99,"110":110,"120":110,"130":110,"149":149,"610":610},"lowest_input_state":99,"lowest_start_state":110,"lowest_end_state":149,"active":false},"verification":{"matrix":{"149":[160],"160":[160,199,610],"199":[199],"610":[610]},"derived_states":{"149":149,"160":160,"199":199,"610":610},"lowest_input_state":149,"lowest_start_state":160,"lowest_end_state":199,"active":false},"implementation":{"matrix":{"99":[210],"210":[210,220,249],"220":[220,210,249,610],"249":[249],"610":[610]},"derived_states":{"99":99,"210":210,"220":210,"249":249,"610":610},"lowest_input_state":99,"lowest_start_state":210,"lowest_end_state":249,"active":true},"review":{"matrix":{"249":[260],"260":[260,270,299],"270":[210,270,260,299,610],"299":[299],"610":[610]},"derived_states":{"249":249,"260":260,"270":260,"299":299,"610":610},"lowest_input_state":249,"lowest_start_state":260,"lowest_end_state":299,"active":false},"recertification":{"matrix":{"299":[310],"310":[310,349,400],"349":[349],"400":[400]},"derived_states":{"299":299,"310":310,"349":349,"400":400},"lowest_input_state":299,"lowest_start_state":310,"lowest_end_state":349,"active":false}}}', 0);
insert into config (config_key, config_value, config_user) VALUES ('reqGenStateMatrix', '{"config_value":{"request":{"matrix":{"0":[0,49,620],"49":[49,620],"620":[620]},"derived_states":{"0":0,"49":49,"620":620},"lowest_input_state":0,"lowest_start_state":49,"lowest_end_state":49,"active":true},"approval":{"matrix":{"49":[60],"60":[60,99,610],"99":[99],"610":[610]},"derived_states":{"49":49,"60":60,"99":99,"610":610},"lowest_input_state":49,"lowest_start_state":60,"lowest_end_state":99,"active":true},"planning":{"matrix":{"99":[110],"110":[110,120,130,149],"120":[120,110,130,149],"130":[130,110,120,149,610],"149":[149],"610":[610]},"derived_states":{"99":99,"110":110,"120":110,"130":110,"149":149,"610":610},"lowest_input_state":99,"lowest_start_state":110,"lowest_end_state":149,"active":false},"verification":{"matrix":{"149":[160],"160":[160,199,610],"199":[199],"610":[610]},"derived_states":{"149":149,"160":160,"199":199,"610":610},"lowest_input_state":149,"lowest_start_state":160,"lowest_end_state":199,"active":false},"implementation":{"matrix":{"99":[210],"210":[210,220,249],"220":[220,210,249,610],"249":[249],"610":[610]},"derived_states":{"99":99,"210":210,"220":210,"249":249,"610":610},"lowest_input_state":99,"lowest_start_state":210,"lowest_end_state":249,"active":true},"review":{"matrix":{"249":[260],"260":[260,270,299],"270":[210,270,260,299,610],"299":[299],"610":[610]},"derived_states":{"249":249,"260":260,"270":260,"299":299,"610":610},"lowest_input_state":249,"lowest_start_state":260,"lowest_end_state":299,"active":false},"recertification":{"matrix":{"299":[310],"310":[310,349,400],"349":[349],"400":[400]},"derived_states":{"299":299,"310":310,"349":349,"400":400},"lowest_input_state":299,"lowest_start_state":310,"lowest_end_state":349,"active":false}}}', 0);
insert into config (config_key, config_value, config_user) VALUES ('reqAccStateMatrix', '{"config_value":{"request":{"matrix":{"0":[0,49,620],"49":[49,620],"620":[620]},"derived_states":{"0":0,"49":49,"620":620},"lowest_input_state":0,"lowest_start_state":49,"lowest_end_state":49,"active":true},"approval":{"matrix":{"49":[60],"60":[60,99,610],"99":[99],"610":[610]},"derived_states":{"49":49,"60":60,"99":99,"610":610},"lowest_input_state":49,"lowest_start_state":60,"lowest_end_state":99,"active":true},"planning":{"matrix":{"99":[110],"110":[110,120,130,149],"120":[120,110,130,149],"130":[130,110,120,149,610],"149":[149],"610":[610]},"derived_states":{"99":99,"110":110,"120":110,"130":110,"149":149,"610":610},"lowest_input_state":99,"lowest_start_state":110,"lowest_end_state":149,"active":false},"verification":{"matrix":{"149":[160],"160":[160,199,610],"199":[199],"610":[610]},"derived_states":{"149":149,"160":160,"199":199,"610":610},"lowest_input_state":149,"lowest_start_state":160,"lowest_end_state":199,"active":false},"implementation":{"matrix":{"99":[210],"210":[210,220,249],"220":[220,210,249,610],"249":[249],"610":[610]},"derived_states":{"99":99,"210":210,"220":210,"249":249,"610":610},"lowest_input_state":99,"lowest_start_state":210,"lowest_end_state":249,"active":true},"review":{"matrix":{"249":[260],"260":[260,270,299],"270":[210,270,260,299,610],"299":[299],"610":[610]},"derived_states":{"249":249,"260":260,"270":260,"299":299,"610":610},"lowest_input_state":249,"lowest_start_state":260,"lowest_end_state":299,"active":false},"recertification":{"matrix":{"299":[310],"310":[310,349,400],"349":[349],"400":[400]},"derived_states":{"299":299,"310":310,"349":349,"400":400},"lowest_input_state":299,"lowest_start_state":310,"lowest_end_state":349,"active":false}}}', 0);
insert into config (config_key, config_value, config_user) VALUES ('reqAvailableTaskTypes', '[0,1,2]', 0);
insert into config (config_key, config_value, config_user) VALUES ('reqPriorities', '[{"numeric_prio":1,"name":"Highest","ticket_deadline":1,"approval_deadline":1},{"numeric_prio":2,"name":"High","ticket_deadline":3,"approval_deadline":2},{"numeric_prio":3,"name":"Medium","ticket_deadline":7,"approval_deadline":3},{"numeric_prio":4,"name":"Low","ticket_deadline":14,"approval_deadline":7},{"numeric_prio":5,"name":"Lowest","ticket_deadline":30,"approval_deadline":14}]', 0);
insert into config (config_key, config_value, config_user) VALUES ('reqAutoCreateImplTasks', 'enterInReqTask', 0);
insert into config (config_key, config_value, config_user) VALUES ('reqAllowObjectSearch', 'False', 0);

INSERT INTO "report_format" ("report_format_name") VALUES ('json');
INSERT INTO "report_format" ("report_format_name") VALUES ('pdf');
INSERT INTO "report_format" ("report_format_name") VALUES ('csv');
INSERT INTO "report_format" ("report_format_name") VALUES ('html');

-- default report templates belong to user 0 
INSERT INTO "report_template" ("report_filter","report_template_name","report_template_comment","report_template_owner", "report_parameters") 
    VALUES ('','Current Rules','T0101', 0,
        '{"report_type":1,"device_filter":{"management":[]},
            "time_filter": {
                "is_shortcut": true,
                "shortcut": "now",
                "report_time": "2022-01-01T00:00:00.0000000+01:00",
                "timerange_type": "SHORTCUT",
                "shortcut_range": "this year",
                "offset": 0,
                "interval": "DAYS",
                "start_time": "2022-01-01T00:00:00.0000000+01:00",
                "end_time": "2022-01-01T00:00:00.0000000+01:00",
                "open_start": false,
                "open_end": false}}');
INSERT INTO "report_template" ("report_filter","report_template_name","report_template_comment","report_template_owner", "report_parameters") 
    VALUES ('','This year''s Rule Changes','T0102', 0, 
        '{"report_type":2,"device_filter":{"management":[]},
            "time_filter": {
                "is_shortcut": true,
                "shortcut": "now",
                "report_time": "2022-01-01T00:00:00.0000000+01:00",
                "timerange_type": "SHORTCUT",
                "shortcut_range": "this year",
                "offset": 0,
                "interval": "DAYS",
                "start_time": "2022-01-01T00:00:00.0000000+01:00",
                "end_time": "2022-01-01T00:00:00.0000000+01:00",
                "open_start": false,
                "open_end": false}}');
INSERT INTO "report_template" ("report_filter","report_template_name","report_template_comment","report_template_owner", "report_parameters") 
    VALUES ('','Basic Statistics','T0103', 0,
        '{"report_type":3,"device_filter":{"management":[]},
            "time_filter": {
                "is_shortcut": true,
                "shortcut": "now",
                "report_time": "2022-01-01T00:00:00.0000000+01:00",
                "timerange_type": "SHORTCUT",
                "shortcut_range": "this year",
                "offset": 0,
                "interval": "DAYS",
                "start_time": "2022-01-01T00:00:00.0000000+01:00",
                "end_time": "2022-01-01T00:00:00.0000000+01:00",
                "open_start": false,
                "open_end": false}}');
INSERT INTO "report_template" ("report_filter","report_template_name","report_template_comment","report_template_owner", "report_parameters") 
    VALUES ('(src=any or dst=any or svc=any or src=all or dst=all or svc=all) and not(action=drop or action=reject or action=deny) ',
        'Compliance: Pass rules with ANY','T0104', 0, 
        '{"report_type":1,"device_filter":{"management":[]},
            "time_filter": {
                "is_shortcut": true,
                "shortcut": "now",
                "report_time": "2022-01-01T00:00:00.0000000+01:00",
                "timerange_type": "SHORTCUT",
                "shortcut_range": "this year",
                "offset": 0,
                "interval": "DAYS",
                "start_time": "2022-01-01T00:00:00.0000000+01:00",
                "end_time": "2022-01-01T00:00:00.0000000+01:00",
                "open_start": false,
                "open_end": false}}');
INSERT INTO "report_template" ("report_filter","report_template_name","report_template_comment","report_template_owner", "report_parameters") 
    VALUES ('','Current NAT Rules','T0105', 0,
        '{"report_type":4,"device_filter":{"management":[]},
            "time_filter": {
                "is_shortcut": true,
                "shortcut": "now",
                "report_time": "2022-01-01T00:00:00.0000000+01:00",
                "timerange_type": "SHORTCUT",
                "shortcut_range": "this year",
                "offset": 0,
                "interval": "DAYS",
                "start_time": "2022-01-01T00:00:00.0000000+01:00",
                "end_time": "2022-01-01T00:00:00.0000000+01:00",
                "open_start": false,
                "open_end": false}}');

insert into parent_rule_type (id, name) VALUES (1, 'section');          -- do not restart numbering
insert into parent_rule_type (id, name) VALUES (2, 'guarded-layer');    -- restart numbering, rule restrictions are ANDed to all rules below it, layer is not entered if guard does not apply
insert into parent_rule_type (id, name) VALUES (3, 'unguarded-layer');  -- restart numbering, no further restrictions

insert into stm_change_type (change_type_id,change_type_name) VALUES (1,'factory settings');
insert into stm_change_type (change_type_id,change_type_name) VALUES (2,'initial import');
insert into stm_change_type (change_type_id,change_type_name) VALUES (3,'in operation');

insert into stm_usr_typ (usr_typ_id,usr_typ_name) VALUES (1,'group');
insert into stm_usr_typ (usr_typ_id,usr_typ_name) VALUES (2,'simple');

insert into stm_svc_typ (svc_typ_id,svc_typ_name,svc_typ_comment) VALUES (1,'simple','standard services');
insert into stm_svc_typ (svc_typ_id,svc_typ_name,svc_typ_comment) VALUES (2,'group','groups of services');
insert into stm_svc_typ (svc_typ_id,svc_typ_name,svc_typ_comment) VALUES (3,'rpc','special services, here: RPC');

insert into stm_obj_typ (obj_typ_id,obj_typ_name) VALUES (1,'network');
insert into stm_obj_typ (obj_typ_id,obj_typ_name) VALUES (2,'group');
insert into stm_obj_typ (obj_typ_id,obj_typ_name) VALUES (3,'host');
insert into stm_obj_typ (obj_typ_id,obj_typ_name) VALUES (4,'machines_range');
insert into stm_obj_typ (obj_typ_id,obj_typ_name) VALUES (5,'dynamic_net_obj');
insert into stm_obj_typ (obj_typ_id,obj_typ_name) VALUES (6,'sofaware_profiles_security_level');
insert into stm_obj_typ (obj_typ_id,obj_typ_name) VALUES (7,'gateway');
insert into stm_obj_typ (obj_typ_id,obj_typ_name) VALUES (8,'cluster_member');
insert into stm_obj_typ (obj_typ_id,obj_typ_name) VALUES (9,'gateway_cluster');
insert into stm_obj_typ (obj_typ_id,obj_typ_name) VALUES (10,'domain');
insert into stm_obj_typ (obj_typ_id,obj_typ_name) VALUES (11,'group_with_exclusion');
insert into stm_obj_typ (obj_typ_id,obj_typ_name) VALUES (12,'ip_range');
insert into stm_obj_typ (obj_typ_id,obj_typ_name) VALUES (13,'uas_collection');
insert into stm_obj_typ (obj_typ_id,obj_typ_name) VALUES (14,'sofaware_gateway');
insert into stm_obj_typ (obj_typ_id,obj_typ_name) VALUES (15,'voip_gk');
insert into stm_obj_typ (obj_typ_id,obj_typ_name) VALUES (16,'gsn_handover_group');
insert into stm_obj_typ (obj_typ_id,obj_typ_name) VALUES (17,'voip_sip');
insert into stm_obj_typ (obj_typ_id,obj_typ_name) VALUES (18,'simple-gateway');

insert into stm_action (action_id,action_name) VALUES (1,'accept'); -- cp, fortinet
insert into stm_action (action_id,action_name) VALUES (2,'drop'); -- cp
insert into stm_action (action_id,action_name) VALUES (3,'deny'); -- netscreen, fortinet
insert into stm_action (action_id,action_name) VALUES (4,'access'); -- netscreen
insert into stm_action (action_id,action_name) VALUES (5,'client encrypt'); -- cp
insert into stm_action (action_id,action_name) VALUES (6,'client auth'); -- cp
insert into stm_action (action_id,action_name) VALUES (7,'reject'); -- cp
insert into stm_action (action_id,action_name) VALUES (8,'encrypt'); -- cp
insert into stm_action (action_id,action_name) VALUES (9,'user auth'); -- cp
insert into stm_action (action_id,action_name) VALUES (10,'session auth'); -- cp
insert into stm_action (action_id,action_name) VALUES (11,'permit'); -- netscreen
insert into stm_action (action_id,action_name) VALUES (12,'permit webauth'); -- netscreen
insert into stm_action (action_id,action_name) VALUES (13,'redirect'); -- phion
insert into stm_action (action_id,action_name) VALUES (14,'map'); -- phion
insert into stm_action (action_id,action_name) VALUES (15,'permit auth'); -- netscreen
insert into stm_action (action_id,action_name) VALUES (16,'tunnel l2tp'); -- netscreen vpn
insert into stm_action (action_id,action_name) VALUES (17,'tunnel vpn-group'); -- netscreen vpn
insert into stm_action (action_id,action_name) VALUES (18,'tunnel vpn'); -- netscreen vpn
insert into stm_action (action_id,action_name) VALUES (19,'actionlocalredirect'); -- phion
insert into stm_action (action_id,action_name) VALUES (20,'inner layer'); -- check point r8x
-- adding new nat actions for nat rules (xlate_rule only)
insert into stm_action (action_id,action_name) VALUES (21,'NAT src') ON CONFLICT DO NOTHING; -- source ip nat
insert into stm_action (action_id,action_name) VALUES (22,'NAT src, dst') ON CONFLICT DO NOTHING; -- source and destination ip nat
insert into stm_action (action_id,action_name) VALUES (23,'NAT src, dst, svc') ON CONFLICT DO NOTHING; -- source and destination ip nat plus port nat
insert into stm_action (action_id,action_name) VALUES (24,'NAT dst') ON CONFLICT DO NOTHING; -- destination ip nat
insert into stm_action (action_id,action_name) VALUES (25,'NAT dst, svc') ON CONFLICT DO NOTHING; -- destination ip nat plus port nat
insert into stm_action (action_id,action_name) VALUES (26,'NAT svc') ON CONFLICT DO NOTHING; -- port nat
insert into stm_action (action_id,action_name) VALUES (27,'NAT src, svc') ON CONFLICT DO NOTHING; -- source ip nat plus port nat
insert into stm_action (action_id,action_name) VALUES (28,'NAT') ON CONFLICT DO NOTHING; -- generic NAT

insert into stm_track (track_id,track_name) VALUES (1,'log');
insert into stm_track (track_id,track_name) VALUES (2,'none');
insert into stm_track (track_id,track_name) VALUES (3,'alert');
insert into stm_track (track_id,track_name) VALUES (4,'userdefined');
insert into stm_track (track_id,track_name) VALUES (5,'mail');
insert into stm_track (track_id,track_name) VALUES (6,'account');
insert into stm_track (track_id,track_name) VALUES (7,'userdefined 1');
insert into stm_track (track_id,track_name) VALUES (8,'userdefined 2');
insert into stm_track (track_id,track_name) VALUES (9,'userdefined 3');
insert into stm_track (track_id,track_name) VALUES (10,'snmptrap');
-- junos
insert into stm_track (track_id,track_name) VALUES (11,'log count');
insert into stm_track (track_id,track_name) VALUES (12,'count');
insert into stm_track (track_id,track_name) VALUES (13,'log alert');
insert into stm_track (track_id,track_name) VALUES (14,'log alert count');
insert into stm_track (track_id,track_name) VALUES (15,'log alert count alarm');
insert into stm_track (track_id,track_name) VALUES (16,'log count alarm');
insert into stm_track (track_id,track_name) VALUES (17,'count alarm');
-- fortinet:
insert into stm_track (track_id,track_name) VALUES (18,'all');
insert into stm_track (track_id,track_name) VALUES (19,'all start');
insert into stm_track (track_id,track_name) VALUES (20,'utm');
insert into stm_track (track_id,track_name) VALUES (22,'utm start');
insert into stm_track (track_id,track_name) VALUES (21,'network log'); -- check point R8x:

insert into stm_dev_typ (dev_typ_id,dev_typ_name,dev_typ_version,dev_typ_manufacturer,dev_typ_predef_svc) VALUES (2,'Netscreen','5.x-6.x','Netscreen', '');
insert into stm_dev_typ (dev_typ_id,dev_typ_name,dev_typ_version,dev_typ_manufacturer,dev_typ_predef_svc) VALUES (4,'FortiGateStandalone','5ff','Fortinet','');
insert into stm_dev_typ (dev_typ_id,dev_typ_name,dev_typ_version,dev_typ_manufacturer,dev_typ_predef_svc) VALUES (5,'Barracuda Firewall Control Center','Vx','phion','');
insert into stm_dev_typ (dev_typ_id,dev_typ_name,dev_typ_version,dev_typ_manufacturer,dev_typ_predef_svc) VALUES (6,'phion netfence','3.x','phion','');
insert into stm_dev_typ (dev_typ_id,dev_typ_name,dev_typ_version,dev_typ_manufacturer,dev_typ_predef_svc) VALUES (7,'Check Point','R5x-R7x','Check Point','');
insert into stm_dev_typ (dev_typ_id,dev_typ_name,dev_typ_version,dev_typ_manufacturer,dev_typ_predef_svc) VALUES (8,'JUNOS','10-21','Juniper','any;0;0;65535;;junos-predefined-service;simple;');
insert into stm_dev_typ (dev_typ_id,dev_typ_name,dev_typ_version,dev_typ_manufacturer,dev_typ_predef_svc) VALUES (9,'Check Point','R8x','Check Point','');
insert into stm_dev_typ (dev_typ_id,dev_typ_name,dev_typ_version,dev_typ_manufacturer,dev_typ_predef_svc) VALUES (10,'FortiGate','5ff','Fortinet','');
insert into stm_dev_typ (dev_typ_id,dev_typ_name,dev_typ_version,dev_typ_manufacturer,dev_typ_predef_svc) VALUES (11,'FortiADOM','5ff','Fortinet','');
insert into stm_dev_typ (dev_typ_id,dev_typ_name,dev_typ_version,dev_typ_manufacturer,dev_typ_predef_svc,dev_typ_is_multi_mgmt) VALUES (12,'FortiManager','5ff','Fortinet','',true);
insert into stm_dev_typ (dev_typ_id,dev_typ_name,dev_typ_version,dev_typ_manufacturer,dev_typ_predef_svc,dev_typ_is_multi_mgmt) VALUES (13,'Check Point','MDS R8x','Check Point','',true);

update stm_dev_typ set dev_typ_predef_svc=
'ANY;0;0;65535;1;other;simple
AOL;6;5190;5194;30;remote;simple
APPLE-ICHAT-SNATMAP;17;5678;5678;1;other;simple
BGP;6;179;179;30;other;simple
CHARGEN;17;19;19;1;other;simple
DHCP-Relay;17;67;67;1;info seeking;simple
DISCARD;17;9;9;1;other;simple
DNS;17;53;53;1;info seeking;simple
ECHO;17;7;7;1;other;simple
FINGER;6;79;79;30;info seeking;simple
FTP;6;21;21;30;remote;simple
FTP-Get;6;21;21;30;remote;simple
FTP-Put;6;21;21;30;remote;simple
GNUTELLA;17;6346;6347;1;remote;simple
GOPHER;6;70;70;30;info seeking;simple
GRE;47;0;65535;60;remote;simple
GTP;6;3386;3386;30;remote;simple
H.323;6;1720;1720;30;remote;simple
HTTP;6;80;80;5;info seeking;simple
HTTP-EXT;6;8000;8001;5;info seeking;simple
HTTPS;6;443;443;30;security;simple
ICMP Address Mask;1;0;65535;1;other;simple
ICMP Dest Unreachable;1;0;65535;1;other;simple
ICMP Fragment Needed;1;0;65535;1;other;simple
ICMP Fragment Reassembly;1;0;65535;1;other;simple
ICMP Host Unreachable;1;0;65535;1;other;simple
ICMP Parameter Problem;1;0;65535;1;other;simple
ICMP Port Unreachable;1;0;65535;1;other;simple
ICMP Protocol Unreach;1;0;65535;1;other;simple
ICMP Redirect;1;0;65535;1;other;simple
ICMP Redirect Host;1;0;65535;1;other;simple
ICMP Redirect TOS & Host;1;0;65535;1;other;simple
ICMP Redirect TOS & Net;1;0;65535;1;other;simple
ICMP Source Quench;1;0;65535;1;other;simple
ICMP Source Route Fail;1;0;65535;1;other;simple
ICMP Time Exceeded;1;0;65535;1;other;simple
ICMP-ANY;1;0;65535;1;other;simple
ICMP-INFO;1;0;65535;1;other;simple
ICMP-TIMESTAMP;1;0;65535;1;other;simple
ICMP6 Dst Unreach addr;58;0;65535;30;other;simple
ICMP6 Dst Unreach admin;58;0;65535;30;other;simple
ICMP6 Dst Unreach beyond;58;0;65535;30;other;simple
ICMP6 Dst Unreach port;58;0;65535;30;other;simple
ICMP6 Dst Unreach route;58;0;65535;30;other;simple
ICMP6 Echo Reply;58;0;65535;30;other;simple
ICMP6 Echo Request;58;0;65535;30;other;simple
ICMP6 HAAD Reply;58;0;65535;30;other;simple
ICMP6 HAAD Request;58;0;65535;30;other;simple
ICMP6 MP Advertisement;58;0;65535;30;other;simple
ICMP6 MP Solicitation;58;0;65535;30;other;simple
ICMP6 Packet Too Big;58;0;65535;30;other;simple
ICMP6 Param Prob header;58;0;65535;30;other;simple
ICMP6 Param Prob nexthdr;58;0;65535;30;other;simple
ICMP6 Param Prob option;58;0;65535;30;other;simple
ICMP6 Time Exceed reasse;58;0;65535;30;other;simple
ICMP6 Time Exceed transi;58;0;65535;30;other;simple
ICMP6-ANY;58;0;65535;30;other;simple
IDENT;6;113;113;30;other;simple
IKE;17;500;500;1;security;simple
IKE-NAT;17;500;500;3;security;simple
IMAP;6;143;143;30;email;simple
Internet Locator Service;6;389;389;30;info seeking;simple
IRC;6;6660;6669;30;remote;simple
L2TP;17;1701;1701;1;remote;simple
LDAP;6;389;389;30;info seeking;simple
LPR;6;515;515;30;other;simple
MAIL;6;25;25;30;email;simple
MGCP-CA;17;2727;2727;120;other;simple
MGCP-UA;17;2427;2427;120;other;simple
MS-AD-BR;;;;1;other;rpc
MS-AD-DRSUAPI;;;;1;other;rpc
MS-AD-DSROLE;;;;1;other;rpc
MS-AD-DSSETUP;;;;1;other;rpc
MS-DTC;;;;1;other;rpc
MS-EXCHANGE-DATABASE;;;;30;other;rpc
MS-EXCHANGE-DIRECTORY;;;;30;other;rpc
MS-EXCHANGE-INFO-STORE;;;;30;other;rpc
MS-EXCHANGE-MTA;;;;30;other;rpc
MS-EXCHANGE-STORE;;;;30;other;rpc
MS-EXCHANGE-SYSATD;;;;30;other;rpc
MS-FRS;;;;1;other;rpc
MS-IIS-COM;;;;30;other;rpc
MS-IIS-IMAP4;;;;1;other;rpc
MS-IIS-INETINFO;;;;1;other;rpc
MS-IIS-NNTP;;;;1;other;rpc
MS-IIS-POP3;;;;1;other;rpc
MS-IIS-SMTP;;;;1;other;rpc
MS-ISMSERV;;;;1;other;rpc
MS-MESSENGER;;;;30;other;rpc
MS-MQQM;;;;1;other;rpc
MS-NETLOGON;;;;1;other;rpc
MS-RPC-ANY;;;;1;other;rpc
MS-RPC-EPM;17;135;135;30;remote;simple
MS-SCHEDULER;;;;1;other;rpc
MS-SQL;6;1433;1433;30;other;simple
MS-WIN-DNS;;;;1;other;rpc
MS-WINS;;;;1;other;rpc
MS-WMIC;;;;30;other;rpc
MSN;6;1863;1863;30;remote;simple
NBDS;17;138;138;1;remote;simple
NBNAME;17;137;137;1;remote;simple
NetMeeting;6;1720;1720;30;remote;simple
NFS;17;111;111;40;remote;simple
NNTP;6;119;119;30;info seeking;simple
NS Global;6;15397;15397;30;remote;simple
NS Global PRO;6;15397;15397;30;remote;simple
NSM;17;69;69;1;other;simple
NTP;17;123;123;1;other;simple
OSPF;89;0;65535;1;other;simple
PC-Anywhere;17;5632;5632;1;remote;simple
PING;1;0;65535;1;other;simple
PINGv6;58;0;65535;30;other;simple
POP3;6;110;110;30;email;simple
PPTP;6;1723;1723;30;security;simple
RADIUS;17;1812;1813;1;other;simple
Real Media;6;7070;7070;30;info seeking;simple
REXEC;6;512;512;30;remote;simple
RIP;17;520;520;1;other;simple
RLOGIN;6;513;513;30;remote;simple
RSH;6;514;514;30;remote;simple
RTSP;6;554;554;30;info seeking;simple
SCCP;6;2000;2000;30;other;simple
SCTP-ANY;132;0;65535;1;other;simple
SIP;17;5060;5060;1;other;simple
SMB;6;139;139;30;remote;simple
SMTP;6;25;25;30;email;simple
SNMP;17;161;161;1;other;simple
SQL Monitor;17;1434;1434;1;other;simple
SQL*Net V1;6;1525;1525;30;other;simple
SQL*Net V2;6;1521;1521;30;other;simple
SSH;6;22;22;30;security;simple
SUN-RPC;;;;1;other;rpc
SUN-RPC-ANY;;;;1;other;rpc
SUN-RPC-MOUNTD;;;;30;other;rpc
SUN-RPC-NFS;;;;40;other;rpc
SUN-RPC-NLOCKMGR;;;;1;other;rpc
SUN-RPC-PORTMAPPER;17;111;111;40;remote;simple
SUN-RPC-RQUOTAD;;;;30;other;rpc
SUN-RPC-RSTATD;;;;30;other;rpc
SUN-RPC-RUSERD;;;;30;other;rpc
SUN-RPC-SADMIND;;;;30;other;rpc
SUN-RPC-SPRAYD;;;;30;other;rpc
SUN-RPC-STATUS;;;;30;other;rpc
SUN-RPC-WALLD;;;;30;other;rpc
SUN-RPC-YPBIND;;;;30;other;rpc
SYSLOG;17;514;514;1;other;simple
TALK;17;517;518;1;other;simple
TCP-ANY;6;0;65535;30;other;simple
TELNET;6;23;23;30;remote;simple
TFTP;17;69;69;1;remote;simple
TRACEROUTE;1;0;65535;1;other;simple
UDP-ANY;17;0;65535;1;other;simple
UUCP;17;540;540;1;remote;simple
VDO Live;6;7000;7010;30;info seeking;simple
VNC;6;5800;5800;30;other;simple
WAIS;6;210;210;30;info seeking;simple
WHOIS;6;43;43;30;info seeking;simple
WINFRAME;6;1494;1494;30;remote;simple
X-WINDOWS;6;6000;6063;30;remote;simple
YMSG;6;5050;5050;30;remote;simple
APPLE-ICHAT;;;;;Apple iChat Services Group;group;AOL|APPLE-ICHAT-SNATMAP|DNS|HTTP|HTTPS|SIP
MGCP;;;;;Media Gateway Control Protoc;group;MGCP-CA|MGCP-UA
MS-AD;;;;;Microsoft Active Directory;group;MS-AD-BR|MS-AD-DRSUAPI|MS-AD-DSROLE|MS-AD-DSSETUP
MS-EXCHANGE;;;;;Microsoft Exchange;group;MS-EXCHANGE-DIRECTORY|MS-EXCHANGE-INFO-STORE|MS-EXCHANGE-MTA|MS-EXCHANGE-STORE|MS-EXCHANGE-SYSATD
MS-IIS;;;;;Microsoft IIS Server;group;MS-IIS-COM|MS-IIS-IMAP4|MS-IIS-INETINFO|MS-IIS-NNTP|MS-IIS-POP3|MS-IIS-SMTP
VOIP;;;;;VOIP Service Group;group;H.323|MGCP-CA|MGCP-UA|SCCP|SIP'
where dev_typ_id=2;

-- SET statement_timeout = 0;
-- SET client_encoding = 'UTF8';
-- SET standard_conforming_strings = on;
-- SET check_function_bodies = false;
-- SET client_min_messages = warning;
-- SET search_path = public, pg_catalog;

insert into request.state (id,name) VALUES (0,'Draft');
insert into request.state (id,name) VALUES (49,'Requested');

insert into request.state (id,name) VALUES (50,'To Approve');
insert into request.state (id,name) VALUES (60,'In Approval');
insert into request.state (id,name) VALUES (99,'Approved');

insert into request.state (id,name) VALUES (100,'To Plan');
insert into request.state (id,name) VALUES (110,'In Planning');
insert into request.state (id,name) VALUES (120,'Wait For Approval');
insert into request.state (id,name) VALUES (130,'Compliance Violation');
insert into request.state (id,name) VALUES (149,'Planned');

insert into request.state (id,name) VALUES (150,'To Verify Plan');
insert into request.state (id,name) VALUES (160,'Plan In Verification');
insert into request.state (id,name) VALUES (199,'Plan Verified');

insert into request.state (id,name) VALUES (200,'To Implement');
insert into request.state (id,name) VALUES (210,'In Implementation');
insert into request.state (id,name) VALUES (220,'Implementation Trouble');
insert into request.state (id,name) VALUES (249,'Implemented');

insert into request.state (id,name) VALUES (250,'To Review');
insert into request.state (id,name) VALUES (260,'In Review');
insert into request.state (id,name) VALUES (270,'Further Work Requested');
insert into request.state (id,name) VALUES (299,'Verified');

insert into request.state (id,name) VALUES (300,'To Recertify');
insert into request.state (id,name) VALUES (310,'In Recertification');
insert into request.state (id,name) VALUES (349,'Recertified');
insert into request.state (id,name) VALUES (400,'Decertified');

insert into request.state (id,name) VALUES (500,'InProgress');

insert into request.state (id,name) VALUES (600,'Done');
insert into request.state (id,name) VALUES (610,'Rejected');
insert into request.state (id,name) VALUES (620,'Discarded');
