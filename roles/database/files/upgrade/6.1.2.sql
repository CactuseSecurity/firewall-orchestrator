insert into stm_dev_typ (dev_typ_id,dev_typ_name,dev_typ_version,dev_typ_manufacturer,dev_typ_predef_svc,dev_typ_is_multi_mgmt,dev_typ_is_mgmt,is_pure_routing_device)
     VALUES (21,'Palo Alto Firewall','2023ff','Palo Alto','',false,true,false) ON CONFLICT DO NOTHING;
insert into stm_dev_typ (dev_typ_id,dev_typ_name,dev_typ_version,dev_typ_manufacturer,dev_typ_predef_svc,dev_typ_is_multi_mgmt,dev_typ_is_mgmt,is_pure_routing_device)
    VALUES (22,'Palo Alto Panorama','2023ff','Palo Alto','',true,true,false) ON CONFLICT DO NOTHING;
insert into stm_dev_typ (dev_typ_id,dev_typ_name,dev_typ_version,dev_typ_manufacturer,dev_typ_predef_svc,dev_typ_is_multi_mgmt,dev_typ_is_mgmt,is_pure_routing_device)
    VALUES (23,'Palo Alto Management','2023ff','Palo Alto','',false,true,false) ON CONFLICT DO NOTHING;

drop view if exists v_rule_with_src_owner cascade;
drop view if exists v_rule_with_dst_owner cascade;


CREATE OR REPLACE VIEW v_rule_with_src_owner AS 
	SELECT r.rule_id, owner.id as owner_id, owner_network.ip as matching_ip, 'source' AS match_in, owner.name as owner_name, 
		recert_interval, rule_metadata.rule_last_certified, rule_last_certifier
	FROM v_active_access_allow_rules r
	LEFT JOIN rule_from ON (r.rule_id=rule_from.rule_id)
	LEFT JOIN objgrp_flat of ON (rule_from.obj_id=of.objgrp_flat_id)
	LEFT JOIN object o ON (of.objgrp_flat_member_id=o.obj_id)
	LEFT JOIN owner_network ON (o.obj_ip>>=owner_network.ip OR o.obj_ip<<=owner_network.ip)
	LEFT JOIN owner ON (owner_network.owner_id=owner.id)
	LEFT JOIN rule_metadata ON (r.rule_uid=rule_metadata.rule_uid AND r.dev_id=rule_metadata.dev_id)
	WHERE NOT o.obj_ip IS NULL
	GROUP BY r.rule_id, matching_ip, owner.id, owner.name, rule_metadata.rule_last_certified, rule_last_certifier;
	
CREATE OR REPLACE VIEW v_rule_with_dst_owner AS 
	SELECT r.rule_id, owner.id as owner_id, owner_network.ip as matching_ip, 'destination' AS match_in, owner.name as owner_name, 
		recert_interval, rule_metadata.rule_last_certified, rule_last_certifier
	FROM v_active_access_allow_rules r
	LEFT JOIN rule_to ON (r.rule_id=rule_to.rule_id)
	LEFT JOIN objgrp_flat of ON (rule_to.obj_id=of.objgrp_flat_id)
	LEFT JOIN object o ON (of.objgrp_flat_member_id=o.obj_id)
	LEFT JOIN owner_network ON (o.obj_ip>>=owner_network.ip OR o.obj_ip<<=owner_network.ip)
	LEFT JOIN owner ON (owner_network.owner_id=owner.id)
	LEFT JOIN rule_metadata ON (r.rule_uid=rule_metadata.rule_uid AND r.dev_id=rule_metadata.dev_id)
	WHERE NOT o.obj_ip IS NULL
	GROUP BY r.rule_id, matching_ip, owner.id, owner.name, rule_metadata.rule_last_certified, rule_last_certifier;

--drop view view_rule_with_owner;
CREATE OR REPLACE VIEW view_rule_with_owner AS 
	SELECT DISTINCT r.rule_num_numeric, r.track_id, r.action_id, r.rule_from_zone, r.rule_to_zone, r.dev_id, r.mgm_id, r.rule_uid, uno.rule_id, uno.owner_id, uno.owner_name, uno.rule_last_certified, uno.rule_last_certifier, 
	rule_action, rule_name, rule_comment, rule_track, rule_src_neg, rule_dst_neg, rule_svc_neg,
	rule_head_text, rule_disabled, access_rule, xlate_rule, nat_rule,
	string_agg(DISTINCT match_in || ':' || matching_ip::VARCHAR, '; ' order by match_in || ':' || matching_ip::VARCHAR desc) as matches,
	recert_interval
	FROM ( SELECT DISTINCT * FROM v_rule_with_src_owner UNION SELECT DISTINCT * FROM v_rule_with_dst_owner ) AS uno
	LEFT JOIN rule AS r USING (rule_id)
	GROUP BY rule_id, owner_id, owner_name, rule_last_certified, rule_last_certifier, r.rule_from_zone, r.rule_to_zone,  recert_interval,
		r.dev_id, r.mgm_id, r.rule_uid, rule_num_numeric, track_id, action_id, 	rule_action, rule_name, rule_comment, rule_track, rule_src_neg, rule_dst_neg, rule_svc_neg,
		rule_head_text, rule_disabled, access_rule, xlate_rule, nat_rule;

CREATE OR REPLACE VIEW view_recert_overdue_rules AS 
	SELECT * FROM view_rule_with_owner as rules
	WHERE now()::DATE -recert_interval> (select max(recert_date) from recertification where recertified and owner_id=rules.owner_id);

