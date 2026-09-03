
INSERT INTO language ("name", "culture_info") VALUES('German', 'de-DE');
INSERT INTO language ("name", "culture_info") VALUES('English', 'en-US');

INSERT INTO path_analysis_algorithm ("id", "name") VALUES
    (1,'None'),
    (2, 'Network Zone Tree');

insert into uiuser (uiuser_id, uiuser_username, uuid) VALUES (0,'default', 'default');

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
insert into stm_obj_typ (obj_typ_id,obj_typ_name) VALUES (19,'external-gateway');
insert into stm_obj_typ (obj_typ_id,obj_typ_name) VALUES (20,'voip');   -- general voip object replacing old specific ones and including CpmiVoipSipDomain
insert into stm_obj_typ (obj_typ_id,obj_typ_name) VALUES (21,'access-role');

insert into stm_action (action_id,action_name) VALUES (1,'accept'); -- cp, fortinet
insert into stm_action (action_id,action_name, allowed) VALUES (2,'drop', FALSE); -- cp
insert into stm_action (action_id,action_name, allowed) VALUES (3,'deny', FALSE); -- netscreen, fortinet
insert into stm_action (action_id,action_name) VALUES (4,'access'); -- netscreen
insert into stm_action (action_id,action_name) VALUES (5,'client encrypt'); -- cp
insert into stm_action (action_id,action_name) VALUES (6,'client auth'); -- cp
insert into stm_action (action_id,action_name, allowed) VALUES (7,'reject', FALSE); -- cp
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
insert into stm_action (action_id,action_name) VALUES (29,'inform'); -- cp DLP
insert into stm_action (action_id,action_name) VALUES (30,'ask'); -- cp DLP

-- checkpoint old:
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
-- mixed (continuous):
insert into stm_track (track_id,track_name) VALUES (21,'network log'); -- check point R8x
insert into stm_track (track_id,track_name) VALUES (22,'utm start'); -- fortinet
insert into stm_track (track_id,track_name) VALUES (23,'detailed log'); -- check point R8x
insert into stm_track (track_id,track_name) VALUES (24,'extended log'); -- check point R8x

-- insert into stm_dev_typ (dev_typ_id,dev_typ_name,dev_typ_version,dev_typ_manufacturer,dev_typ_predef_svc,dev_typ_is_mgmt,is_pure_routing_device)
--     VALUES (2,'Netscreen','5.x-6.x','Netscreen', '', true,false);
-- insert into stm_dev_typ (dev_typ_id,dev_typ_name,dev_typ_version,dev_typ_manufacturer,dev_typ_predef_svc,dev_typ_is_mgmt,is_pure_routing_device)
--     VALUES (4,'FortiGateStandalone','5ff','Fortinet','', true,false);
insert into stm_dev_typ (dev_typ_id,dev_typ_name,dev_typ_version,dev_typ_manufacturer,dev_typ_predef_svc,dev_typ_is_multi_mgmt,dev_typ_is_mgmt,is_pure_routing_device)
    VALUES (5,'Barracuda Firewall Control Center','Vx','phion','',false,true,false);
insert into stm_dev_typ (dev_typ_id,dev_typ_name,dev_typ_version,dev_typ_manufacturer,dev_typ_predef_svc,dev_typ_is_multi_mgmt,dev_typ_is_mgmt,is_pure_routing_device)
    VALUES (6,'phion netfence','3.x','phion','',false,false,false);
-- insert into stm_dev_typ (dev_typ_id,dev_typ_name,dev_typ_version,dev_typ_manufacturer,dev_typ_predef_svc,dev_typ_is_mgmt,is_pure_routing_device)
--     VALUES (7,'Check Point','R5x-R7x','Check Point','', true,false);
insert into stm_dev_typ (dev_typ_id,dev_typ_name,dev_typ_version,dev_typ_manufacturer,dev_typ_predef_svc,dev_typ_is_multi_mgmt,dev_typ_is_mgmt,is_pure_routing_device)
    VALUES (8,'JUNOS','10-21','Juniper','any;0;0;65535;;junos-predefined-service;simple;', false,true,false);
insert into stm_dev_typ (dev_typ_id,dev_typ_name,dev_typ_version,dev_typ_manufacturer,dev_typ_predef_svc,dev_typ_is_multi_mgmt,dev_typ_is_mgmt,is_pure_routing_device)
    VALUES (9,'Check Point','R8x','Check Point','', false,true,false);
insert into stm_dev_typ (dev_typ_id,dev_typ_name,dev_typ_version,dev_typ_manufacturer,dev_typ_predef_svc,dev_typ_is_multi_mgmt,dev_typ_is_mgmt,is_pure_routing_device)
    VALUES (10,'FortiGate','5ff','Fortinet','', false,false,false);
insert into stm_dev_typ (dev_typ_id,dev_typ_name,dev_typ_version,dev_typ_manufacturer,dev_typ_predef_svc,dev_typ_is_multi_mgmt,dev_typ_is_mgmt,is_pure_routing_device)
    VALUES (11,'FortiADOM','5ff','Fortinet','', false,true,false);
insert into stm_dev_typ (dev_typ_id,dev_typ_name,dev_typ_version,dev_typ_manufacturer,dev_typ_predef_svc,dev_typ_is_multi_mgmt,dev_typ_is_mgmt,is_pure_routing_device)
    VALUES (12,'FortiManager','5ff','Fortinet','',true,true,false);
insert into stm_dev_typ (dev_typ_id,dev_typ_name,dev_typ_version,dev_typ_manufacturer,dev_typ_predef_svc,dev_typ_is_multi_mgmt,dev_typ_is_mgmt,is_pure_routing_device)
    VALUES (13,'Check Point','MDS R8x','Check Point','',true,true,false);
insert into stm_dev_typ (dev_typ_id,dev_typ_name,dev_typ_version,dev_typ_manufacturer,dev_typ_predef_svc,dev_typ_is_multi_mgmt,dev_typ_is_mgmt,is_pure_routing_device)
    VALUES (14,'Cisco Firepower Management Center','7ff','Cisco','',true,true,false);
insert into stm_dev_typ (dev_typ_id,dev_typ_name,dev_typ_version,dev_typ_manufacturer,dev_typ_predef_svc,dev_typ_is_multi_mgmt,dev_typ_is_mgmt,is_pure_routing_device)
    VALUES (15,'Cisco Firepower Domain','7ff','Cisco','',false,true,false);
insert into stm_dev_typ (dev_typ_id,dev_typ_name,dev_typ_version,dev_typ_manufacturer,dev_typ_predef_svc,dev_typ_is_multi_mgmt,dev_typ_is_mgmt,is_pure_routing_device)
    VALUES (16,'Cisco Firepower Gateway','7ff','Cisco','',false,false,false);
insert into stm_dev_typ (dev_typ_id,dev_typ_name,dev_typ_version,dev_typ_manufacturer,dev_typ_predef_svc,dev_typ_is_multi_mgmt,dev_typ_is_mgmt,is_pure_routing_device)
     VALUES (17,'DummyRouter Management','1','DummyRouter','',false,true,true) ON CONFLICT DO NOTHING;
insert into stm_dev_typ (dev_typ_id,dev_typ_name,dev_typ_version,dev_typ_manufacturer,dev_typ_predef_svc,dev_typ_is_multi_mgmt,dev_typ_is_mgmt,is_pure_routing_device)
    VALUES (18,'DummyRouter Gateway','1','DummyRouter','',false,false,true) ON CONFLICT DO NOTHING;
insert into stm_dev_typ (dev_typ_id,dev_typ_name,dev_typ_version,dev_typ_manufacturer,dev_typ_predef_svc,dev_typ_is_multi_mgmt,dev_typ_is_mgmt,is_pure_routing_device)
     VALUES (19,'Azure','2022ff','Microsoft','',false,true,false) ON CONFLICT DO NOTHING;
insert into stm_dev_typ (dev_typ_id,dev_typ_name,dev_typ_version,dev_typ_manufacturer,dev_typ_predef_svc,dev_typ_is_multi_mgmt,dev_typ_is_mgmt,is_pure_routing_device)
    VALUES (20,'Azure Firewall','2022ff','Microsoft','',false,false,false) ON CONFLICT DO NOTHING;
insert into stm_dev_typ (dev_typ_id,dev_typ_name,dev_typ_version,dev_typ_manufacturer,dev_typ_predef_svc,dev_typ_is_multi_mgmt,dev_typ_is_mgmt,is_pure_routing_device)
     VALUES (21,'Palo Alto Firewall','2023ff','Palo Alto','',false,true,false) ON CONFLICT DO NOTHING;
insert into stm_dev_typ (dev_typ_id,dev_typ_name,dev_typ_version,dev_typ_manufacturer,dev_typ_predef_svc,dev_typ_is_multi_mgmt,dev_typ_is_mgmt,is_pure_routing_device)
    VALUES (22,'Palo Alto Panorama','2023ff','Palo Alto','',true,true,false) ON CONFLICT DO NOTHING;
insert into stm_dev_typ (dev_typ_id,dev_typ_name,dev_typ_version,dev_typ_manufacturer,dev_typ_predef_svc,dev_typ_is_multi_mgmt,dev_typ_is_mgmt,is_pure_routing_device)
    VALUES (23,'Palo Alto Management','2023ff','Palo Alto','',false,true,false) ON CONFLICT DO NOTHING;
insert into stm_dev_typ (dev_typ_id,dev_typ_name,dev_typ_version,dev_typ_manufacturer,dev_typ_predef_svc,dev_typ_is_multi_mgmt,dev_typ_is_mgmt,is_pure_routing_device)
    VALUES (24,'FortiOS Management','REST','Fortinet','',false,true,false) ON CONFLICT DO NOTHING;
insert into stm_dev_typ (dev_typ_id,dev_typ_name,dev_typ_version,dev_typ_manufacturer,dev_typ_predef_svc,dev_typ_is_multi_mgmt,dev_typ_is_mgmt,is_pure_routing_device)
    VALUES (25,'Fortinet FortiOS Gateway','REST','Fortinet','',false,false,false) ON CONFLICT DO NOTHING;
insert into stm_dev_typ (dev_typ_id,dev_typ_name,dev_typ_version,dev_typ_manufacturer,dev_typ_predef_svc,dev_typ_is_multi_mgmt,dev_typ_is_mgmt,is_pure_routing_device)
    VALUES (26,'NSX','REST','VMWare','',false,true,false) ON CONFLICT DO NOTHING;
insert into stm_dev_typ (dev_typ_id,dev_typ_name,dev_typ_version,dev_typ_manufacturer,dev_typ_predef_svc,dev_typ_is_multi_mgmt,dev_typ_is_mgmt,is_pure_routing_device)
    VALUES (27,'NSX DFW Gateway','REST','VMWare','',false,false,false) ON CONFLICT DO NOTHING;
insert into stm_dev_typ (dev_typ_id,dev_typ_name,dev_typ_version,dev_typ_manufacturer,dev_typ_predef_svc,dev_typ_is_multi_mgmt,dev_typ_is_mgmt,is_pure_routing_device)
    VALUES (28,'Cisco Asa','9','Cisco','',false,true,false);
insert into stm_dev_typ (dev_typ_id,dev_typ_name,dev_typ_version,dev_typ_manufacturer,dev_typ_predef_svc,dev_typ_is_multi_mgmt,dev_typ_is_mgmt,is_pure_routing_device)
    VALUES (29,'Cisco Asa on FirePower','9','Cisco','',false,true,false);
insert into stm_dev_typ (dev_typ_id,dev_typ_name,dev_typ_version,dev_typ_manufacturer,dev_typ_predef_svc,dev_typ_is_multi_mgmt,dev_typ_is_mgmt,is_pure_routing_device)
    VALUES (30, 'Generic Firewall Management', '1.0', null, '', false, true, false);
insert into stm_dev_typ (dev_typ_id,dev_typ_name,dev_typ_version,dev_typ_manufacturer,dev_typ_predef_svc,dev_typ_is_multi_mgmt,dev_typ_is_mgmt,is_pure_routing_device)
    VALUES (31, 'Generic Firewall Gateway', '1.0', null, '', false, false, false);
insert into stm_dev_typ (dev_typ_id,dev_typ_name,dev_typ_version,dev_typ_manufacturer,dev_typ_predef_svc,dev_typ_is_multi_mgmt,dev_typ_is_mgmt,is_pure_routing_device)
    VALUES (32,'OPNsense standalone','25ff','Deciso','',false,true,false) ON CONFLICT DO NOTHING;

-- SET statement_timeout = 0;
-- SET client_encoding = 'UTF8';
-- SET standard_conforming_strings = on;
-- SET check_function_bodies = false;
-- SET client_min_messages = warning;
-- SET search_path = public, pg_catalog;

INSERT INTO owner (id, name, is_default, recert_interval, app_id_external) 
VALUES    (0, 'super-owner', true, 365, 'NONE');
INSERT INTO owner_responsible_type (id, name, active, sort_order, allow_modelling, allow_recertification)
VALUES
    (1, 'Main responsible', true, 10, true, true),
    (2, 'Supporting responsible', true, 20, true, true),
    (3, 'Optional escalation responsible', true, 30, false, false)
ON CONFLICT DO NOTHING;
INSERT INTO owner_responsible (owner_id, dn, responsible_type)
VALUES
    (0, 'uid=admin,ou=tenant0,ou=operator,ou=user,dc=fworch,dc=internal', 1),
    (0, 'group-dn-for-super-owner', 2)
ON CONFLICT DO NOTHING;

insert into stm_link_type (id, name) VALUES (2, 'ordered');
insert into stm_link_type (id, name) VALUES (3, 'inline');
insert into stm_link_type (id, name) VALUES (4, 'concatenated');
insert into stm_link_type (id, name) VALUES (5, 'domain');
insert into stm_link_type (id, name) VALUES (6, 'nat');
insert into stm_link_type (id, name) VALUES (7, 'policy');

-- insert into compliance.assessability_issue_type (type_id, type_name) VALUES (1, 'empty group');
-- insert into compliance.assessability_issue_type (type_id, type_name) VALUES (2, 'broadcast address');
-- insert into compliance.assessability_issue_type (type_id, type_name) VALUES (3, 'DHCP IP undefined address');
-- insert into compliance.assessability_issue_type (type_id, type_name) VALUES (4, 'dynamic internet address');

INSERT INTO stm_import (import_type_id, import_type_name) VALUES (1, 'rule');
INSERT INTO stm_import (import_type_id, import_type_name) VALUES (2, 'owner');
INSERT INTO stm_import (import_type_id, import_type_name) VALUES (3, 'admin via reinitialize button');
INSERT INTO stm_import (import_type_id, import_type_name) VALUES (4, 'log');
