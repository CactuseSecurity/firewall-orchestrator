
-- adding azure devices
insert into stm_dev_typ (dev_typ_id,dev_typ_name,dev_typ_version,dev_typ_manufacturer,dev_typ_predef_svc,dev_typ_is_multi_mgmt,dev_typ_is_mgmt,is_pure_routing_device)
     VALUES (19,'Azure','2022ff','Microsoft','',false,true,false) ON CONFLICT DO NOTHING;
insert into stm_dev_typ (dev_typ_id,dev_typ_name,dev_typ_version,dev_typ_manufacturer,dev_typ_predef_svc,dev_typ_is_multi_mgmt,dev_typ_is_mgmt,is_pure_routing_device)
    VALUES (20,'Azure Firewall','2022ff','Microsoft','',false,false,false) ON CONFLICT DO NOTHING;

ALTER TABLE management ADD COLUMN IF NOT EXISTS cloud_tenant_id VARCHAR;
ALTER TABLE management ADD COLUMN IF NOT EXISTS cloud_subscription_id VARCHAR;

ALTER TABLE import_credential ADD COLUMN IF NOT EXISTS cloud_client_id VARCHAR;
ALTER TABLE import_credential ADD COLUMN IF NOT EXISTS cloud_client_secret VARCHAR;

--------------------------------------------------------------
-- sample data - adding owner data

INSERT INTO owner (name, dn, group_dn, is_default, tenant_id, recert_interval, next_recert_date, app_id_external) 
		VALUES    ('owner F', 'ad-single-owner-f', 'ad-group-owner-f', false, 1, 30, '2022-12-01T00:00:00', 123)
		ON CONFLICT DO NOTHING; 
INSERT INTO owner (name, dn, group_dn, is_default, tenant_id, recert_interval, next_recert_date, app_id_external) 
		VALUES    ('owner D', 'ad-single-owner-d', 'ad-group-owner-d', false, 1, 30, '2022-12-01T00:00:00', 234)
		ON CONFLICT DO NOTHING; 
INSERT INTO owner (name, dn, group_dn, is_default, tenant_id, recert_interval, next_recert_date, app_id_external) 
		VALUES    ('default owner', 'ad-single-owner-default', 'ad-group-owner-default', true, 1, 30, '2022-12-01T00:00:00', 111)
		ON CONFLICT DO NOTHING; 

INSERT INTO owner_network (owner_id, ip) 
		VALUES    ((SELECT id FROM owner WHERE name='owner F'), '10.222.0.0/27')
		ON CONFLICT DO NOTHING; 

INSERT INTO owner_network (owner_id, ip) 
		VALUES    ((SELECT id FROM owner WHERE name='owner D'), '10.222.0.32/27')
		ON CONFLICT DO NOTHING; 

INSERT INTO owner_network (owner_id, ip) 
		VALUES    ((SELECT id FROM owner WHERE name='owner F'), '10.0.0.0/27')
		ON CONFLICT DO NOTHING; 

INSERT INTO owner_network (owner_id, ip) 
		VALUES    ((SELECT id FROM owner WHERE name='owner D'), '10.0.0.32/27')
		ON CONFLICT DO NOTHING; 

-- drop view v_rule_with_src_owner cascade;
-- drop view v_rule_with_dst_owner cascade;

CREATE OR REPLACE VIEW v_active_access_rules AS 
	SELECT * FROM rule r
	WHERE r.active AND r.access_rule AND NOT r.rule_disabled AND r.rule_head_text IS NULL;

CREATE OR REPLACE VIEW v_active_access_allow_rules AS 
	SELECT * FROM rule r
	WHERE r.active AND r.access_rule AND r.rule_head_text IS NULL AND NOT r.action_id IN (2,3,7);
	-- do not deal with deny rules

CREATE OR REPLACE VIEW v_rule_with_src_owner AS 
	SELECT r.rule_id, owner.id as owner_id, owner_network.ip as matching_ip, 'source' AS match_in, owner.name as owner_name, 
		rule_metadata.rule_last_certified, rule_last_certifier
	FROM v_active_access_allow_rules r
	LEFT JOIN rule_from ON (r.rule_id=rule_from.rule_id)
	LEFT JOIN objgrp_flat of ON (rule_from.obj_id=of.objgrp_flat_id)
	LEFT JOIN object o ON (o.obj_typ_id<>2 AND of.objgrp_flat_member_id=o.obj_id)
	LEFT JOIN owner_network ON (o.obj_ip>>=owner_network.ip OR o.obj_ip<<=owner_network.ip)
	LEFT JOIN owner ON (owner_network.owner_id=owner.id)
	LEFT JOIN rule_metadata ON (r.rule_uid=rule_metadata.rule_uid AND r.dev_id=rule_metadata.dev_id)
	GROUP BY r.rule_id, matching_ip, owner.id, owner.name, rule_metadata.rule_last_certified, rule_last_certifier;
	
CREATE OR REPLACE VIEW v_rule_with_dst_owner AS 
	SELECT r.rule_id, owner.id as owner_id, owner_network.ip as matching_ip, 'destination' AS match_in, owner.name as owner_name, 
		rule_metadata.rule_last_certified, rule_last_certifier
	FROM v_active_access_allow_rules r
	LEFT JOIN rule_to ON (r.rule_id=rule_to.rule_id)
	LEFT JOIN objgrp_flat of ON (rule_to.obj_id=of.objgrp_flat_id)
	LEFT JOIN object o ON (o.obj_typ_id<>2 AND of.objgrp_flat_member_id=o.obj_id)
	LEFT JOIN owner_network ON (o.obj_ip>>=owner_network.ip OR o.obj_ip<<=owner_network.ip)
	LEFT JOIN owner ON (owner_network.owner_id=owner.id)
	LEFT JOIN rule_metadata ON (r.rule_uid=rule_metadata.rule_uid AND r.dev_id=rule_metadata.dev_id)
	GROUP BY r.rule_id, matching_ip, owner.id, owner.name, rule_metadata.rule_last_certified, rule_last_certifier;

--drop view view_rule_with_owner;

CREATE OR REPLACE VIEW view_rule_with_owner AS 
	SELECT DISTINCT r.rule_num_numeric, r.track_id, r.action_id, r.rule_from_zone, r.rule_to_zone, r.dev_id, r.mgm_id, r.rule_uid, uno.rule_id, uno.owner_id, uno.owner_name, uno.rule_last_certified, uno.rule_last_certifier, 
	rule_action, rule_name, rule_comment, rule_track, rule_src_neg, rule_dst_neg, rule_svc_neg,
	rule_head_text, rule_disabled, access_rule, xlate_rule, nat_rule,
	array_agg(DISTINCT match_in || ':' || matching_ip::VARCHAR order by match_in || ':' || matching_ip::VARCHAR desc) as matches
	FROM ( SELECT DISTINCT * FROM v_rule_with_src_owner UNION SELECT DISTINCT * FROM v_rule_with_dst_owner ) AS uno
	LEFT JOIN rule AS r USING (rule_id)
	GROUP BY rule_id, owner_id, owner_name, rule_last_certified, rule_last_certifier, r.rule_from_zone, r.rule_to_zone, 
		r.dev_id, r.mgm_id, r.rule_uid, rule_num_numeric, track_id, action_id, 	rule_action, rule_name, rule_comment, rule_track, rule_src_neg, rule_dst_neg, rule_svc_neg,
		rule_head_text, rule_disabled, access_rule, xlate_rule, nat_rule;

-- all rules for a specific owner without "any rules":
-- select * from view_rule_with_owner
-- left join rule using (rule_id)
-- left join rule_metadata using (rule_uid)
-- where owner_name='owner F'
-- AND (NOT rule_src like '%Any' AND NOT rule_dst like  '%Any' AND NOT rule_src='all' AND NOT rule_dst='all')
-- order by rule_id;

-- all rules without owner without "any rules":
-- select * from view_rule_with_owner
-- left join rule using (rule_id)
-- left join rule_metadata using (rule_uid)
-- where owner_name IS NULL
-- AND rule_id NOT IN (
-- 	select DISTINCT rule_id from view_rule_with_owner
-- 	where NOT owner_name IS NULL
-- )
-- AND (NOT rule_src like '%Any' AND NOT rule_dst like  '%Any' AND NOT rule_src='all' AND NOT rule_dst='all')
-- order by rule_id;

-- select rules for an owner without "any rules":
-- select rule_id, rule.rule_uid, matches, owner_id, owner_name, rule_metadata.rule_last_certified, rule_metadata.rule_last_certifier, mgm_id, rule.dev_id, rule_src, rule_dst, rule_svc, rule_action
-- from view_rule_with_owner
-- left join rule using (rule_id)
-- left join rule_metadata ON (rule.rule_uid=rule_metadata.rule_uid AND rule.dev_id=rule_metadata.dev_id)
-- where owner_name ='owner F'
-- AND (NOT rule_src like '%Any' AND NOT rule_dst like  '%Any' AND NOT rule_src='all' AND NOT rule_dst='all')
-- AND rule.dev_id=2
-- order by mgm_id, dev_id, rule_id;
