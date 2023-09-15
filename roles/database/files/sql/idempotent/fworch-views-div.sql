

---------------------------------------------------------------------------------------------
-- tenant views
---------------------------------------------------------------------------------------------

/*
-- get all rules of a tenant
CREATE OR REPLACE VIEW view_tenant_rules AS 
	select x.rule_id, x.rule_create, x.rule_last_seen, x.tenant_id, x.mgm_id from (
		SELECT rule.rule_id, rule.rule_create, rule.rule_last_seen, tenant_network.tenant_id, rule.mgm_id, rule_order.dev_id
			FROM rule
				LEFT JOIN rule_order ON (rule.rule_id=rule_order.rule_id)
				LEFT JOIN rule_to ON (rule.rule_id=rule_to.rule_id)
				LEFT JOIN objgrp_flat ON (rule_to.obj_id=objgrp_flat_id)
				LEFT JOIN object ON (objgrp_flat_member_id=object.obj_id)
				LEFT JOIN tenant_network ON
					(
						(NOT rule_dst_neg AND (obj_ip<<tenant_net_ip OR obj_ip>>tenant_net_ip OR obj_ip=tenant_net_ip))
						 OR (rule_dst_neg AND (NOT obj_ip<<tenant_net_ip AND NOT obj_ip>>tenant_net_ip AND NOT obj_ip=tenant_net_ip))
					)
				WHERE rule_head_text IS NULL
			UNION
		SELECT rule.rule_id, rule.rule_create, rule.rule_last_seen, tenant_network.tenant_id, rule.mgm_id, rule_order.dev_id
			FROM rule
				LEFT JOIN rule_order ON (rule.rule_id=rule_order.rule_id)
				LEFT JOIN rule_from ON (rule.rule_id=rule_from.rule_id)
				LEFT JOIN objgrp_flat ON (rule_from.obj_id=objgrp_flat.objgrp_flat_id)
				LEFT JOIN object ON (objgrp_flat.objgrp_flat_member_id=object.obj_id)
				LEFT JOIN tenant_network ON
					(
						(NOT rule_src_neg AND (obj_ip<<tenant_net_ip OR obj_ip>>tenant_net_ip OR obj_ip=tenant_net_ip))
						 OR (rule_src_neg AND (NOT obj_ip<<tenant_net_ip AND NOT obj_ip>>tenant_net_ip AND NOT obj_ip=tenant_net_ip))
					)
				WHERE rule_head_text IS NULL
	) AS x; 	-- GROUP BY rule_id,tenant_id,mgm_id,rule_create, rule_last_seen
	
-- examples for tenant filtering:	
-- select rule_id from view_tenant_rules where tenant_network.tenant_id=1 and rule.mgm_id=4
-- select rule_id,rule_create from view_tenant_rules where mgm_id=4 group by rule_id,rule_create
*/


CREATE OR REPLACE VIEW view_device_names AS
	SELECT 'Management: ' || mgm_name || ', Device: ' || dev_name AS dev_string, dev_id, mgm_id, dev_name, mgm_name FROM device LEFT JOIN management USING (mgm_id);

-- view for ip address filtering
DROP MATERIALIZED VIEW IF EXISTS nw_object_limits;
-- CREATE MATERIALIZED VIEW nw_object_limits AS
-- 	select obj_id, mgm_id,
-- 		host ( object.obj_ip )::cidr as first_ip,
-- 		CASE 
-- 			WHEN object.obj_ip_end IS NULL
-- 			THEN host(broadcast(object.obj_ip))::cidr 
-- 			ELSE host(broadcast(object.obj_ip_end))::cidr 
-- 		END last_ip
-- 	from object;

-- -- adding indexes for view
-- Create index IF NOT EXISTS idx_nw_object_limits_obj_id on nw_object_limits (obj_id);
-- Create index IF NOT EXISTS idx_nw_object_limits_mgm_id on nw_object_limits (mgm_id);

DROP MATERIALIZED VIEW IF EXISTS view_tenant_rules;
CREATE MATERIALIZED VIEW IF NOT EXISTS view_tenant_rules AS
    select tenant_rules.* from (
        SELECT rule.*, tenant_network.tenant_id
            FROM rule
                LEFT JOIN rule_to ON (rule.rule_id=rule_to.rule_id)
                LEFT JOIN objgrp_flat ON (rule_to.obj_id=objgrp_flat_id)
                LEFT JOIN object ON (objgrp_flat_member_id=object.obj_id)
                LEFT JOIN tenant_network ON
                    ( NOT rule_dst_neg AND (obj_ip>>=tenant_net_ip OR obj_ip<<=tenant_net_ip))
                WHERE rule_head_text IS NULL
            UNION
        SELECT rule.*, tenant_network.tenant_id
            FROM rule
                LEFT JOIN rule_from ON (rule.rule_id=rule_from.rule_id)
                LEFT JOIN objgrp_flat ON (rule_from.obj_id=objgrp_flat.objgrp_flat_id)
                LEFT JOIN object ON (objgrp_flat.objgrp_flat_member_id=object.obj_id)
                LEFT JOIN tenant_network ON
                    ( NOT rule_src_neg AND (obj_ip>>=tenant_net_ip OR obj_ip<<=tenant_net_ip) )
                WHERE rule_head_text IS NULL
    ) AS tenant_rules;

-- adding indexes for view
Create index IF NOT EXISTS idx_view_tenant_rules_tenant_id on view_tenant_rules(tenant_id);
Create index IF NOT EXISTS idx_view_tenant_rules_mgm_id on view_tenant_rules(mgm_id);

REFRESH MATERIALIZED VIEW view_tenant_rules;
GRANT SELECT ON TABLE view_tenant_rules TO GROUP secuadmins, reporters;
/*

	query filterRulesByTenant($importId: bigint) {
	view_tenant_rules(where: {access_rule: {_eq: true}, rule_last_seen: {_gte: $importId},  rule_create: {_lte: $importId}}) {
		rule_id
		rule_src
		rule_dst
		rule_create
		rule_last_seen
		tenant_id
	}
	}

*/

-- example tenant_network data:
-- insert into tenant_network (tenant_id, tenant_net_ip) values (123, '10.9.8.0/24');

-- test query: 
-- select dev_id, rule_num_numeric, view_tenant_rules.rule_id, rule_src,rule_dst
-- from view_tenant_rules 
-- where access_rule, tenant_id=123 and mgm_id=8 and rule_last_seen>=28520 
-- order by dev_id asc, rule_num_numeric asc


---------------------------------------------------------------------------------------------
-- GRANTS on exportable Views
---------------------------------------------------------------------------------------------

GRANT SELECT ON TABLE view_device_names TO GROUP secuadmins, reporters;
GRANT SELECT ON TABLE view_rule_source_or_destination TO GROUP secuadmins, reporters;
