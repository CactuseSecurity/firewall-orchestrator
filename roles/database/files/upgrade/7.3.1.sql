--------------------- make sure dedicated managements and devices are not tenant filtered ------------------------

-- tename existing tenant_id columns
DO $$
BEGIN
  IF EXISTS(SELECT *
    FROM information_schema.columns
    WHERE table_name='device' and column_name='tenant_id')
  THEN
      ALTER TABLE "public"."device" RENAME COLUMN "tenant_id" TO "unfiltered_tenant_id";
  END IF;
  IF EXISTS(SELECT *
    FROM information_schema.columns
    WHERE table_name='management' and column_name='tenant_id')
  THEN
      ALTER TABLE "public"."management" RENAME COLUMN "tenant_id" TO "unfiltered_tenant_id";
  END IF;
END $$;


-- TODO: provide UI (settings) for editing unfiltered_tenant for both managements and gateways

CREATE OR REPLACE FUNCTION rule_relevant_for_tenant(rule rule, hasura_session json)
RETURNS boolean AS $$
    DECLARE 
        t_id integer;
        show boolean DEFAULT false;
        mgm_unfiltered_tenant_id integer;
        gw_unfiltered_tenant_id integer;
    
    BEGIN
        t_id := (hasura_session ->> 'x-hasura-tenant-id')::integer;

        IF t_id IS NULL THEN
            RAISE EXCEPTION 'No tenant id found in hasura session'; --> only happens when using auth via x-hasura-admin-secret (no tenant id is set)
        ELSIF t_id = 1 THEN
            show := true;
        ELSE
            SELECT INTO mgm_unfiltered_tenant_id unfiltered_tenant_id FROM rule LEFT JOIN management USING (mgm_id);
			SELECT INTO gw_unfiltered_tenant_id unfiltered_tenant_id FROM rule LEFT JOIN device USING (dev_id);
            IF mgm_unfiltered_tenant_id IS NOT NULL AND mgm_unfiltered_tenant_id=t_id OR gw_unfiltered_tenant_id IS NOT NULL AND gw_unfiltered_tenant_id=t_id THEN
                show := true;
			ELSE
				IF EXISTS (
					SELECT rf.obj_id FROM rule_from rf
						LEFT JOIN rule r ON (rf.rule_id=r.rule_id)
						LEFT JOIN objgrp_flat ON (rf.obj_id=objgrp_flat.objgrp_flat_id)
						LEFT JOIN object ON (objgrp_flat.objgrp_flat_member_id=object.obj_id)
						LEFT JOIN tenant_network ON
							(ip_ranges_overlap(obj_ip, obj_ip_end, tenant_net_ip, tenant_net_ip_end, rf.negated != r.rule_src_neg))
					WHERE rf.rule_id = rule.rule_id AND tenant_id = t_id
				) THEN
					show := true;
				ELSIF EXISTS (
					SELECT rt.obj_id FROM rule_to rt
						LEFT JOIN rule r ON (rt.rule_id=r.rule_id)
						LEFT JOIN objgrp_flat ON (rt.obj_id=objgrp_flat.objgrp_flat_id)
						LEFT JOIN object ON (objgrp_flat.objgrp_flat_member_id=object.obj_id)
						LEFT JOIN tenant_network ON
							(ip_ranges_overlap(obj_ip, obj_ip_end, tenant_net_ip, tenant_net_ip_end, rt.negated != r.rule_dst_neg))
					WHERE rt.rule_id = rule.rule_id AND tenant_id = t_id
				) THEN
					show := true;
				END IF;
			END IF;
        END IF;

        RETURN show;
    END;
$$ LANGUAGE 'plpgsql' STABLE;

CREATE OR REPLACE FUNCTION get_rules_for_tenant(device_row device, tenant integer, hasura_session json)
RETURNS SETOF rule AS $$
    DECLARE
        t_id integer;
        mgm_unfiltered_tenant_id integer;
        gw_unfiltered_tenant_id integer;
    BEGIN
        t_id := (hasura_session ->> 'x-hasura-tenant-id')::integer;
        IF t_id IS NULL THEN
            RAISE EXCEPTION 'No tenant id found in hasura session'; --> only happens when using auth via x-hasura-admin-secret (no tenant id is set)
        ELSIF t_id != 1  AND t_id != tenant THEN
            RAISE EXCEPTION 'A non-tenant-0 user was trying to generate a report for another tenant.';
        ELSIF tenant = 1 THEN
            RAISE EXCEPTION 'Tenant0 cannot be simulated.';
        ELSE
            SELECT INTO mgm_unfiltered_tenant_id management.unfiltered_tenant_id FROM device LEFT JOIN management USING (mgm_id) WHERE device.dev_id=device_row.dev_id;
            SELECT INTO gw_unfiltered_tenant_id device.unfiltered_tenant_id FROM device WHERE dev_id=device_row.dev_id;

            IF mgm_unfiltered_tenant_id IS NOT NULL AND mgm_unfiltered_tenant_id=tenant OR
				gw_unfiltered_tenant_id IS NOT NULL AND gw_unfiltered_tenant_id=tenant
			THEN
				RETURN QUERY SELECT * FROM rule WHERE dev_id=device_row.dev_id;
            ELSE
				RETURN QUERY
					SELECT r.* FROM rule r
						LEFT JOIN rule_from rf ON (r.rule_id=rf.rule_id)
						LEFT JOIN objgrp_flat rf_of ON (rf.obj_id=rf_of.objgrp_flat_id)
						LEFT JOIN object rf_o ON (rf_of.objgrp_flat_member_id=rf_o.obj_id)
						LEFT JOIN tenant_network ON
							(ip_ranges_overlap(rf_o.obj_ip, rf_o.obj_ip_end, tenant_net_ip, tenant_net_ip_end, rf.negated != r.rule_src_neg))
					WHERE r.dev_id = device_row.dev_id AND tenant_id = tenant AND rule_head_text IS NULL
					UNION
					SELECT r.* FROM rule r
						LEFT JOIN rule_to rt ON (r.rule_id=rt.rule_id)
						LEFT JOIN objgrp_flat rt_of ON (rt.obj_id=rt_of.objgrp_flat_id)
						LEFT JOIN object rt_o ON (rt_of.objgrp_flat_member_id=rt_o.obj_id)
						LEFT JOIN tenant_network ON
							(ip_ranges_overlap(rt_o.obj_ip, rt_o.obj_ip_end, tenant_net_ip, tenant_net_ip_end, rt.negated != r.rule_dst_neg))
					WHERE r.dev_id = device_row.dev_id AND tenant_id = tenant AND rule_head_text IS NULL
					ORDER BY rule_name;
			END IF;
        END IF;
    END;
$$ LANGUAGE 'plpgsql' STABLE;

CREATE OR REPLACE FUNCTION get_rule_froms_for_tenant(rule rule, tenant integer, hasura_session json)
RETURNS SETOF rule_from AS $$
    DECLARE
        t_id integer;
        mgm_unfiltered_tenant_id integer;
        gw_unfiltered_tenant_id integer;
    BEGIN
        t_id := (hasura_session ->> 'x-hasura-tenant-id')::integer;

        IF t_id IS NULL THEN
            RAISE EXCEPTION 'No tenant id found in hasura session'; --> only happens when using auth via x-hasura-admin-secret (no tenant id is set)
        ELSIF t_id != 1  AND t_id != tenant THEN
            RAISE EXCEPTION 'A non-tenant-0 user was trying to generate a report for another tenant.';
        ELSIF tenant = 1 THEN
            RAISE EXCEPTION 'Tenant0 cannot be simulated.';
        ELSE
            SELECT INTO mgm_unfiltered_tenant_id management.unfiltered_tenant_id FROM device LEFT JOIN management USING (mgm_id) WHERE device.dev_id=rule.dev_id;
            SELECT INTO gw_unfiltered_tenant_id device.unfiltered_tenant_id FROM device WHERE dev_id=rule.dev_id;

            IF mgm_unfiltered_tenant_id IS NOT NULL AND mgm_unfiltered_tenant_id=tenant OR
				gw_unfiltered_tenant_id IS NOT NULL AND gw_unfiltered_tenant_id=tenant
			THEN
                RETURN QUERY SELECT rf.* FROM rule_from rf WHERE rule_id = rule.rule_id;
            ELSIF EXISTS (
                    SELECT rt.obj_id FROM rule_to rt
                        LEFT JOIN objgrp_flat ON (rt.obj_id=objgrp_flat.objgrp_flat_id)
                        LEFT JOIN object ON (objgrp_flat.objgrp_flat_member_id=object.obj_id)
                        LEFT JOIN tenant_network ON
                            (ip_ranges_overlap(obj_ip, obj_ip_end, tenant_net_ip, tenant_net_ip_end, rt.negated != rule.rule_dst_neg))
                    WHERE rt.rule_id = rule.rule_id AND tenant_id = tenant
                ) THEN
                    RETURN QUERY
                        SELECT rf.* FROM rule_from rf WHERE rule_id = rule.rule_id;
            ELSE
                RETURN QUERY
                    SELECT DISTINCT rf.* FROM rule_from rf
                        LEFT JOIN objgrp_flat ON (rf.obj_id=objgrp_flat.objgrp_flat_id)
                        LEFT JOIN object ON (objgrp_flat.objgrp_flat_member_id=object.obj_id)
                        LEFT JOIN tenant_network ON
                            (ip_ranges_overlap(obj_ip, obj_ip_end, tenant_net_ip, tenant_net_ip_end, rf.negated != rule.rule_src_neg))
                    WHERE rule_id = rule.rule_id AND tenant_id = tenant;
            END IF;
        END IF;
    END;
$$ LANGUAGE 'plpgsql' STABLE;


CREATE OR REPLACE FUNCTION get_rule_tos_for_tenant(rule rule, tenant integer, hasura_session json)
RETURNS SETOF rule_to AS $$
    DECLARE
        t_id integer;
        mgm_unfiltered_tenant_id integer;
        gw_unfiltered_tenant_id integer;
    BEGIN
        t_id := (hasura_session ->> 'x-hasura-tenant-id')::integer;

        IF t_id IS NULL THEN
            RAISE EXCEPTION 'No tenant id found in hasura session'; --> only happens when using auth via x-hasura-admin-secret (no tenant id is set)
        ELSIF t_id != 1  AND t_id != tenant THEN
            RAISE EXCEPTION 'A non-tenant-0 user was trying to generate a report for another tenant.';
        ELSIF tenant = 1 THEN
            RAISE EXCEPTION 'Tenant0 cannot be simulated.';
        ELSE
            SELECT INTO mgm_unfiltered_tenant_id management.unfiltered_tenant_id FROM device LEFT JOIN management USING (mgm_id) WHERE device.dev_id=rule.dev_id;
            SELECT INTO gw_unfiltered_tenant_id device.unfiltered_tenant_id FROM device WHERE dev_id=rule.dev_id;

            IF mgm_unfiltered_tenant_id IS NOT NULL AND mgm_unfiltered_tenant_id=tenant OR
				gw_unfiltered_tenant_id IS NOT NULL AND gw_unfiltered_tenant_id=tenant
			THEN
                RETURN QUERY SELECT rt.* FROM rule_to rt WHERE rule_id = rule.rule_id;
            ELSIF EXISTS (
                SELECT rf.obj_id FROM rule_from rf
                    LEFT JOIN objgrp_flat ON (rf.obj_id=objgrp_flat.objgrp_flat_id)
                    LEFT JOIN object ON (objgrp_flat.objgrp_flat_member_id=object.obj_id)
                    LEFT JOIN tenant_network ON
                        (ip_ranges_overlap(obj_ip, obj_ip_end, tenant_net_ip, tenant_net_ip_end, rf.negated != rule.rule_src_neg))
                WHERE rf.rule_id = rule.rule_id AND tenant_id = tenant
                ) THEN
                RETURN QUERY
                    SELECT rt.* FROM rule_to rt WHERE rule_id = rule.rule_id;
            ELSE
                RETURN QUERY
                    SELECT DISTINCT rt.* FROM rule_to rt
                        LEFT JOIN objgrp_flat ON (rt.obj_id=objgrp_flat.objgrp_flat_id)
                        LEFT JOIN object ON (objgrp_flat.objgrp_flat_member_id=object.obj_id)
                        LEFT JOIN tenant_network ON
                            (ip_ranges_overlap(obj_ip, obj_ip_end, tenant_net_ip, tenant_net_ip_end, rt.negated != rule.rule_dst_neg))
                    WHERE rule_id = rule.rule_id AND tenant_id = tenant;
            END IF;
        END IF;
    END;
$$ LANGUAGE 'plpgsql' STABLE;
