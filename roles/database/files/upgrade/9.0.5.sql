CREATE INDEX IF NOT EXISTS idx_rule_dev_active_access
    ON rule (dev_id, rule_num_numeric)
    WHERE active = true AND access_rule = true AND rule_disabled = false AND rule_head_text IS NULL;

CREATE INDEX IF NOT EXISTS idx_rule_from_rule_obj
    ON rule_from (rule_id, obj_id);

CREATE INDEX IF NOT EXISTS idx_rule_to_rule_obj
    ON rule_to (rule_id, obj_id);

CREATE INDEX IF NOT EXISTS idx_objgrp_flat_flat_member
    ON objgrp_flat (objgrp_flat_id, objgrp_flat_member_id);

CREATE INDEX IF NOT EXISTS idx_tenant_network_tenant
    ON tenant_network (tenant_id);

CREATE INDEX IF NOT EXISTS idx_tenant_to_management_lookup
    ON tenant_to_management (management_id, tenant_id, shared);

CREATE INDEX IF NOT EXISTS idx_tenant_to_device_lookup
    ON tenant_to_device (device_id, tenant_id, shared);

CREATE EXTENSION IF NOT EXISTS pg_trgm;

CREATE INDEX IF NOT EXISTS idx_rule_name_trgm_active_access
    ON rule USING gin (rule_name gin_trgm_ops)
    WHERE active = true AND access_rule = true AND rule_disabled = false AND rule_head_text IS NULL;

CREATE INDEX IF NOT EXISTS idx_rule_comment_trgm_active_access
    ON rule USING gin (rule_comment gin_trgm_ops)
    WHERE active = true AND access_rule = true AND rule_disabled = false AND rule_head_text IS NULL;

CREATE OR REPLACE FUNCTION get_rules_for_tenant(device_row device, tenant integer, hasura_session json)
RETURNS SETOF rule AS $$
    DECLARE
        t_id integer;
    BEGIN
        t_id := (hasura_session ->> 'x-hasura-tenant-id')::integer;
        IF t_id IS NULL THEN
            RAISE EXCEPTION 'No tenant id found in hasura session';
        ELSIF t_id != 1 AND t_id != tenant THEN
            RAISE EXCEPTION 'A non-tenant-0 user was trying to generate a report for another tenant.';
        ELSIF tenant = 1 THEN
            RAISE EXCEPTION 'Tenant0 cannot be simulated.';
        ELSE
            IF rulebase_fully_visible_to_tenant(device_row.dev_id, tenant)
            THEN
                RETURN QUERY
                    SELECT *
                    FROM rule
                    WHERE dev_id = device_row.dev_id;
            ELSE
                RETURN QUERY
                    WITH visible_from AS (
                        SELECT DISTINCT rf.rule_id
                        FROM rule_from rf
                        JOIN rule r ON r.rule_id = rf.rule_id
                        LEFT JOIN objgrp_flat rf_of ON rf.obj_id = rf_of.objgrp_flat_id
                        LEFT JOIN object rf_o ON rf_of.objgrp_flat_member_id = rf_o.obj_id
                        JOIN tenant_network tn ON
                            tn.tenant_id = tenant
                            AND ip_ranges_overlap(rf_o.obj_ip, rf_o.obj_ip_end, tn.tenant_net_ip, tn.tenant_net_ip_end, rf.negated != r.rule_src_neg)
                        WHERE r.dev_id = device_row.dev_id
                            AND r.rule_head_text IS NULL
                    ),
                    visible_to AS (
                        SELECT DISTINCT rt.rule_id
                        FROM rule_to rt
                        JOIN rule r ON r.rule_id = rt.rule_id
                        LEFT JOIN objgrp_flat rt_of ON rt.obj_id = rt_of.objgrp_flat_id
                        LEFT JOIN object rt_o ON rt_of.objgrp_flat_member_id = rt_o.obj_id
                        JOIN tenant_network tn ON
                            tn.tenant_id = tenant
                            AND ip_ranges_overlap(rt_o.obj_ip, rt_o.obj_ip_end, tn.tenant_net_ip, tn.tenant_net_ip_end, rt.negated != r.rule_dst_neg)
                        WHERE r.dev_id = device_row.dev_id
                            AND r.rule_head_text IS NULL
                    ),
                    visible_rules AS (
                        SELECT rule_id FROM visible_from
                        UNION
                        SELECT rule_id FROM visible_to
                    )
                    SELECT r.*
                    FROM rule r
                    JOIN visible_rules vr ON vr.rule_id = r.rule_id
                    WHERE r.dev_id = device_row.dev_id
                        AND r.rule_head_text IS NULL
                    ORDER BY r.rule_name;
            END IF;
        END IF;
    END;
$$ LANGUAGE 'plpgsql' STABLE;
