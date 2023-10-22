

---------------------------------------------------------------------------------------------
-- tenant views
---------------------------------------------------------------------------------------------

-- examples for tenant filtering:	
-- select rule_id from view_tenant_rules where tenant_network.tenant_id=1 and rule.mgm_id=4
-- select rule_id,rule_create from view_tenant_rules where mgm_id=4 group by rule_id,rule_create

-- DROP MATERIALIZED VIEW IF EXISTS view_tenant_rules;
-- CREATE MATERIALIZED VIEW IF NOT EXISTS view_tenant_rules AS
--     select tenant_rules.* from (
--         SELECT rule.*, tenant_network.tenant_id
--             FROM rule
--                 LEFT JOIN rule_to ON (rule.rule_id=rule_to.rule_id)
--                 LEFT JOIN objgrp_flat ON (rule_to.obj_id=objgrp_flat_id)
--                 LEFT JOIN object ON (objgrp_flat_member_id=object.obj_id)
--                 LEFT JOIN tenant_network ON
--                     ( NOT rule_dst_neg AND (obj_ip_end >= tenant_net_ip AND obj_ip <= tenant_net_ip_end))
--                 WHERE rule_head_text IS NULL
--             UNION
--         SELECT rule.*, tenant_network.tenant_id
--             FROM rule
--                 LEFT JOIN rule_from ON (rule.rule_id=rule_from.rule_id)
--                 LEFT JOIN objgrp_flat ON (rule_from.obj_id=objgrp_flat.objgrp_flat_id)
--                 LEFT JOIN object ON (objgrp_flat.objgrp_flat_member_id=object.obj_id)
--                 LEFT JOIN tenant_network ON
--                     ( NOT rule_src_neg AND (obj_ip_end >= tenant_net_ip AND obj_ip <= tenant_net_ip_end) )
--                 WHERE rule_head_text IS NULL
--     ) AS tenant_rules;

-- -- adding indexes for view
-- Create index IF NOT EXISTS idx_view_tenant_rules_tenant_id on view_tenant_rules(tenant_id);
-- Create index IF NOT EXISTS idx_view_tenant_rules_mgm_id on view_tenant_rules(mgm_id);

-- REFRESH MATERIALIZED VIEW view_tenant_rules;
-- GRANT SELECT ON TABLE view_tenant_rules TO GROUP secuadmins, reporters;

-- example tenant_network data:
-- insert into tenant_network (tenant_id, tenant_net_ip, tenant_net_ip_end) values (123, '10.9.8.0/32', '10.9.8.255/32');

-- test query: 
-- select dev_id, rule_num_numeric, view_tenant_rules.rule_id, rule_src,rule_dst
-- from view_tenant_rules 
-- where access_rule, tenant_id=123 and mgm_id=8 and rule_last_seen>=28520 
-- order by dev_id asc, rule_num_numeric asc