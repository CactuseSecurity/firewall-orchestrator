-- ensure the firewall schema is resolvable for the unqualified references below, even on a
-- connection opened before the ALTER DATABASE ... SET search_path took effect (issue #4793).
SET search_path TO "$user", public, firewall;

CREATE OR REPLACE FUNCTION public.get_visible_devices_per_tenant(integer)
    RETURNS SETOF device_type 
    LANGUAGE 'plpgsql'
    STABLE 
AS $BODY$
DECLARE
    i_tenant_id ALIAS FOR $1;
    i_dev_id integer;
    v_dev_name VARCHAR;
    b_can_view_all_devices boolean;
BEGIN
    SELECT INTO b_can_view_all_devices tenant_can_view_all_devices FROM tenant WHERE tenant_id=i_tenant_id;
    IF b_can_view_all_devices THEN
        FOR i_dev_id, v_dev_name IN SELECT dev_id, dev_name FROM device
        LOOP
            RETURN NEXT ROW (i_dev_id, v_dev_name);
        END LOOP;
    ELSE
        FOR i_dev_id, v_dev_name IN 
            SELECT device_id, dev_name FROM tenant 
                            RIGHT JOIN tenant_to_device USING (tenant_id) 
                            LEFT JOIN device ON (tenant_to_device.device_id=device.dev_id) 
                            WHERE tenant.tenant_id=i_tenant_id
            UNION
            SELECT dev_id, dev_name FROM tenant
                            RIGHT JOIN tenant_to_management USING (tenant_id) 
                            LEFT JOIN device ON (NOT tenant_to_management.shared AND tenant_to_management.management_id=device.mgm_id)
                            WHERE tenant.tenant_id=i_tenant_id and dev_id is not null
        LOOP
            RETURN NEXT ROW (i_dev_id, v_dev_name);
        END LOOP;
        -- also add devices that belong to unfiltered managements
    END IF;
    RETURN;
END;
$BODY$;

CREATE OR REPLACE FUNCTION public.get_visible_managements_per_tenant(integer)
    RETURNS SETOF device_type 
    LANGUAGE 'plpgsql'
    STABLE 
AS $BODY$
DECLARE
    i_tenant_id ALIAS FOR $1;
    i_mgm_id integer;
    v_mgm_name VARCHAR;
    b_can_view_all_devices boolean;
BEGIN
    SELECT INTO b_can_view_all_devices tenant_can_view_all_devices FROM tenant WHERE tenant_id=i_tenant_id;
    IF b_can_view_all_devices THEN
        FOR i_mgm_id, v_mgm_name IN SELECT mgm_id, mgm_name FROM management
        LOOP
            RETURN NEXT ROW (i_mgm_id, v_mgm_name);
        END LOOP;
    ELSE
        FOR i_mgm_id, v_mgm_name IN 
            SELECT mgm_id, mgm_name FROM tenant
                RIGHT JOIN tenant_to_management USING (tenant_id)
                LEFT JOIN management ON (management_id=mgm_id)
                WHERE tenant.tenant_id=i_tenant_id and mgm_id is not null
        LOOP
            RETURN NEXT ROW (i_mgm_id, v_mgm_name);
        END LOOP;
    END IF;
    RETURN;
END;
$BODY$;

CREATE OR REPLACE FUNCTION public.filter_rule_nwobj_resolveds(management_row management, rule_ids bigint[], import_id bigint)
 RETURNS SETOF firewall.nw_object
 LANGUAGE sql
 STABLE
AS $function$
  SELECT o.*
  FROM firewall.rule_nw_object_resolved r JOIN firewall.nw_object o ON (r.obj_id=o.obj_id)
  WHERE r.mgm_id = management_row.mgm_id AND rule_id = any (rule_ids) AND r.created <= import_id AND (r.removed IS NULL OR r.removed > import_id)
  GROUP BY o.obj_id
  ORDER BY MAX(obj_name), o.obj_id
$function$;

CREATE OR REPLACE FUNCTION public.filter_rule_svc_resolveds(management_row management, rule_ids bigint[], import_id bigint)
 RETURNS SETOF firewall.nw_service
 LANGUAGE sql
 STABLE
AS $function$
  SELECT s.*
  FROM firewall.rule_nw_service_resolved r JOIN firewall.nw_service s ON (r.svc_id=s.svc_id)
  WHERE r.mgm_id = management_row.mgm_id AND rule_id = any (rule_ids) AND r.created <= import_id AND (r.removed IS NULL OR r.removed > import_id)
  GROUP BY s.svc_id
  ORDER BY MAX(svc_name), s.svc_id
$function$;

CREATE OR REPLACE FUNCTION public.filter_rule_user_resolveds(management_row management, rule_ids bigint[], import_id bigint)
 RETURNS SETOF firewall.nw_user
 LANGUAGE sql
 STABLE
AS $function$
  SELECT u.*
  FROM firewall.rule_nw_user_resolved r JOIN firewall.nw_user u ON (r.user_id=u.user_id)
  WHERE r.mgm_id = management_row.mgm_id AND rule_id = any (rule_ids) AND r.created <= import_id AND (r.removed IS NULL OR r.removed > import_id)
  GROUP BY u.user_id
  ORDER BY MAX(user_name), u.user_id
$function$;


CREATE OR REPLACE FUNCTION ip_ranges_overlap(ip1_start cidr, ip1_end cidr, ip2_start cidr, ip2_end cidr, inverted boolean DEFAULT FALSE)
    RETURNS boolean AS $$
    BEGIN
        IF ip1_start IS NULL OR ip1_end IS NULL OR ip2_start IS NULL OR ip2_end IS NULL THEN
            RETURN FALSE;
        END IF;

        IF inverted THEN                                            -- []: cidr1 ~> invert (): cidr2
            IF ip1_start <= ip2_start AND ip2_end <= ip1_end THEN   --[-*(--)-*]--  ~>  --]-*(--)-*[--
                RETURN FALSE;
            ELSE
                RETURN TRUE;
            END IF;
        END IF;

        RETURN ip1_start <= ip2_end AND ip2_start <= ip1_end;
    END;
$$ LANGUAGE 'plpgsql' STABLE;


-- true if the changed sources or destinations of a rule change are relevant for the tenant: an object added to or
-- removed from the rule overlaps with a network of the tenant (see nw_obj_in_tenant_network)
CREATE OR REPLACE FUNCTION has_relevant_change(cl_rule changelog_rule, tenant integer)
RETURNS boolean AS $$
    BEGIN
        IF tenant IS NULL THEN
            RAISE EXCEPTION 'Given tenant is NULL';
        ELSIF tenant = 1 THEN
            RETURN true;
        END IF;

        PERFORM 1 FROM ( -- set of difference between rule_from of old and new rule
            SELECT obj_id, negated FROM rule_from WHERE rule_id = cl_rule.old_rule_id EXCEPT SELECT obj_id, negated FROM rule_from WHERE rule_id = cl_rule.new_rule_id
            UNION
            (SELECT obj_id, negated FROM rule_from WHERE rule_id = cl_rule.new_rule_id EXCEPT SELECT obj_id, negated FROM rule_from WHERE rule_id = cl_rule.old_rule_id)
        ) AS diff
        WHERE nw_obj_in_tenant_network(diff.obj_id, diff.negated, tenant)
        LIMIT 1;
        IF FOUND THEN
            RETURN true;
        END IF;

        PERFORM 1 FROM ( -- set of difference between rule_to of old and new rule
            SELECT obj_id, negated FROM rule_to WHERE rule_id = cl_rule.old_rule_id EXCEPT SELECT obj_id, negated FROM rule_to WHERE rule_id = cl_rule.new_rule_id
            UNION
            (SELECT obj_id, negated FROM rule_to WHERE rule_id = cl_rule.new_rule_id EXCEPT SELECT obj_id, negated FROM rule_to WHERE rule_id = cl_rule.old_rule_id)
        ) AS diff
        WHERE nw_obj_in_tenant_network(diff.obj_id, diff.negated, tenant)
        LIMIT 1;
        RETURN FOUND;
    END;
$$ LANGUAGE 'plpgsql' STABLE;


CREATE OR REPLACE FUNCTION cl_rule_relevant_for_tenant(cl_rule changelog_rule, hasura_session json)
RETURNS boolean AS $$
    DECLARE t_id integer;
    show boolean DEFAULT false;
    
    BEGIN
        t_id := (hasura_session ->> 'x-hasura-tenant-id')::integer;

        IF t_id IS NULL THEN
            RAISE EXCEPTION 'No tenant id found in hasura session'; --> only happens when using auth via x-hasura-admin-secret (no tenant id is set)
        ELSIF t_id = 1 THEN
            show := true;
        ELSE
            show := has_relevant_change(cl_rule, t_id);
        END IF;

        RETURN show;
    END;
$$ LANGUAGE 'plpgsql' STABLE;



-- The row parameters must not be named like the rule_from / rule_to tables: inside a query a table of the same
-- name takes precedence, so the device of an arbitrary rule was used for the full rulebase visibility check
-- (GHSA-v8hx-cx2q-j75v). Renaming an input parameter needs the old function to be dropped first.
DROP FUNCTION IF EXISTS public.rule_from_relevant_for_tenant(rule_from, json);
DROP FUNCTION IF EXISTS public.rule_to_relevant_for_tenant(rule_to, json);

-- true if the network object or a member of it (if it is a group) overlaps with a network of the tenant
-- b_negated: the object is negated (either by itself or by the rule side it is used in)
-- The importer writes objgrp_flat rows (the group itself and its members) for groups only, so a plain object is
-- checked by its own address; a group has no address of its own and only matches through its members.
-- plpgsql instead of sql: a sql function querying tables cannot be inlined and would be planned on every call
CREATE OR REPLACE FUNCTION nw_obj_in_tenant_network(i_obj_id bigint, b_negated boolean, i_tenant_id integer)
RETURNS boolean AS $$
    BEGIN
        PERFORM 1 FROM firewall.nw_object o
            JOIN tenant_network tn ON (ip_ranges_overlap(o.obj_ip, o.obj_ip_end, tn.tenant_net_ip, tn.tenant_net_ip_end, b_negated))
        WHERE o.obj_id = i_obj_id AND tn.tenant_id = i_tenant_id
        LIMIT 1;
        IF FOUND THEN
            RETURN true;
        END IF;

        PERFORM 1 FROM objgrp_flat og
            JOIN firewall.nw_object o ON (o.obj_id = og.objgrp_flat_member_id)
            JOIN tenant_network tn ON (ip_ranges_overlap(o.obj_ip, o.obj_ip_end, tn.tenant_net_ip, tn.tenant_net_ip_end, b_negated))
        WHERE og.objgrp_flat_id = i_obj_id AND tn.tenant_id = i_tenant_id
        LIMIT 1;
        RETURN FOUND;
    END;
$$ LANGUAGE 'plpgsql' STABLE;

-- true if a source object of the rule overlaps with a network of the tenant
CREATE OR REPLACE FUNCTION rule_froms_in_tenant_network(i_rule_id bigint, b_src_neg boolean, i_tenant_id integer)
RETURNS boolean AS $$
    BEGIN
        PERFORM 1 FROM rule_from rf
        WHERE rf.rule_id = i_rule_id AND nw_obj_in_tenant_network(rf.obj_id, rf.negated != b_src_neg, i_tenant_id)
        LIMIT 1;
        RETURN FOUND;
    END;
$$ LANGUAGE 'plpgsql' STABLE;

-- true if a destination object of the rule overlaps with a network of the tenant
CREATE OR REPLACE FUNCTION rule_tos_in_tenant_network(i_rule_id bigint, b_dst_neg boolean, i_tenant_id integer)
RETURNS boolean AS $$
    BEGIN
        PERFORM 1 FROM rule_to rt
        WHERE rt.rule_id = i_rule_id AND nw_obj_in_tenant_network(rt.obj_id, rt.negated != b_dst_neg, i_tenant_id)
        LIMIT 1;
        RETURN FOUND;
    END;
$$ LANGUAGE 'plpgsql' STABLE;

-- true if a gateway link (rule_enforced_on_gateway or rulebase_link row) applies to a rule version:
-- a current rule only counts the current links, a removed rule version the links that existed during its lifetime.
-- So a rule moved to another gateway, or a rulebase linked to another gateway, stops being visible through the old
-- gateway, while historic rule versions stay linked to the gateways they were enforced on.
-- All arguments are import ids (control_id); sql instead of plpgsql, so the expression is inlined into the queries.
CREATE OR REPLACE FUNCTION link_valid_for_rule_version(i_link_created bigint, i_link_removed bigint, i_rule_created bigint, i_rule_removed bigint)
RETURNS boolean AS $$
    SELECT CASE
        WHEN i_rule_removed IS NULL THEN i_link_removed IS NULL
        ELSE COALESCE(i_link_created, 0) < i_rule_removed AND (i_link_removed IS NULL OR i_link_removed > i_rule_created)
    END
$$ LANGUAGE sql IMMUTABLE;

-- the rule row replaces the single columns, as the lifetime of the rule version is needed as well
DROP FUNCTION IF EXISTS public.rule_fully_visible_to_tenant(bigint, integer, integer, integer);

-- true if the tenant may see the rule completely: the tenant has an unshared mapping to the management of the rule
-- or to a gateway the rule applies to. These are the gateways in rule_enforced_on_gateway; a rule with only an
-- all-gateways entry there (dev_id NULL), or a non-access rule without entries (nat rules), applies to all gateways
-- linking its rulebase. The importer gives every access rule explicit entries, so an access rule without any entry
-- is installed on targets that are no gateways of the management (e.g. a gateway group): it is not fully visible.
-- Only links valid for the rule version count, see link_valid_for_rule_version.
-- rule.dev_id is not used: it is no longer written by the importer (v9 rulebase model).
CREATE OR REPLACE FUNCTION rule_fully_visible_to_tenant(p_rule rule, i_tenant_id integer)
RETURNS boolean AS $$
    BEGIN
        PERFORM 1 FROM tenant_to_management ttm
        WHERE ttm.management_id = p_rule.mgm_id AND ttm.tenant_id = i_tenant_id AND NOT ttm.shared;
        IF FOUND THEN
            RETURN true;
        END IF;

        PERFORM 1 FROM rule_enforced_on_gateway reg
            JOIN tenant_to_device ttd ON (ttd.device_id = reg.dev_id)
        WHERE reg.rule_id = p_rule.rule_id AND ttd.tenant_id = i_tenant_id AND NOT ttd.shared
            AND link_valid_for_rule_version(reg.created, reg.removed, p_rule.rule_create, p_rule.removed)
        LIMIT 1;
        IF FOUND THEN
            RETURN true;
        END IF;

        PERFORM 1 FROM rule_enforced_on_gateway reg
        WHERE reg.rule_id = p_rule.rule_id AND reg.dev_id IS NOT NULL
            AND link_valid_for_rule_version(reg.created, reg.removed, p_rule.rule_create, p_rule.removed)
        LIMIT 1;
        IF FOUND THEN -- the rule is restricted to explicit gateways, none of them is fully visible
            RETURN false;
        END IF;

        IF COALESCE(p_rule.access_rule, true) THEN
            PERFORM 1 FROM rule_enforced_on_gateway reg
            WHERE reg.rule_id = p_rule.rule_id AND reg.dev_id IS NULL
                AND link_valid_for_rule_version(reg.created, reg.removed, p_rule.rule_create, p_rule.removed)
            LIMIT 1;
            IF NOT FOUND THEN -- install-on targets unknown to the management: do not guess from the rulebase links
                RETURN false;
            END IF;
        END IF;

        PERFORM 1 FROM rulebase_link rl
            JOIN tenant_to_device ttd ON (ttd.device_id = rl.gw_id)
        WHERE rl.to_rulebase_id = p_rule.rulebase_id AND ttd.tenant_id = i_tenant_id AND NOT ttd.shared
            AND link_valid_for_rule_version(rl.created, rl.removed, p_rule.rule_create, p_rule.removed)
        LIMIT 1;
        RETURN FOUND;
    END;
$$ LANGUAGE 'plpgsql' STABLE;

-- a rule_from row is visible to a tenant if its rule is fully visible to the tenant,
-- if the source object itself overlaps with a tenant network
-- or if any destination object of the same rule overlaps with a tenant network
CREATE OR REPLACE FUNCTION rule_from_relevant_for_tenant(p_rule_from rule_from, hasura_session json)
RETURNS boolean AS $$
    DECLARE
        i_tenant_id integer;
        r_rule rule;
    BEGIN
        i_tenant_id := (hasura_session ->> 'x-hasura-tenant-id')::integer;

        IF i_tenant_id IS NULL THEN
            RAISE EXCEPTION 'No tenant id found in hasura session'; --> only happens when using auth via x-hasura-admin-secret (no tenant id is set)
        ELSIF i_tenant_id = 1 THEN
            RETURN true;
        END IF;

        SELECT r.* INTO r_rule FROM rule r WHERE r.rule_id = p_rule_from.rule_id;

        IF NOT FOUND THEN
            RETURN false;
        ELSIF rule_fully_visible_to_tenant(r_rule, i_tenant_id) THEN
            RETURN true;
        ELSIF nw_obj_in_tenant_network(p_rule_from.obj_id, p_rule_from.negated != r_rule.rule_src_neg, i_tenant_id) THEN
            RETURN true;
        END IF;

        -- all rule_from objects are visible if a rule_to object of the same rule is in a tenant network
        RETURN rule_tos_in_tenant_network(p_rule_from.rule_id, r_rule.rule_dst_neg, i_tenant_id);
    END;
$$ LANGUAGE 'plpgsql' STABLE;

-- a rule_to row is visible to a tenant if its rule is fully visible to the tenant,
-- if the destination object itself overlaps with a tenant network
-- or if any source object of the same rule overlaps with a tenant network
CREATE OR REPLACE FUNCTION rule_to_relevant_for_tenant(p_rule_to rule_to, hasura_session json)
RETURNS boolean AS $$
    DECLARE
        i_tenant_id integer;
        r_rule rule;
    BEGIN
        i_tenant_id := (hasura_session ->> 'x-hasura-tenant-id')::integer;

        IF i_tenant_id IS NULL THEN
            RAISE EXCEPTION 'No tenant id found in hasura session'; --> only happens when using auth via x-hasura-admin-secret (no tenant id is set)
        ELSIF i_tenant_id = 1 THEN
            RETURN true;
        END IF;

        SELECT r.* INTO r_rule FROM rule r WHERE r.rule_id = p_rule_to.rule_id;

        IF NOT FOUND THEN
            RETURN false;
        ELSIF rule_fully_visible_to_tenant(r_rule, i_tenant_id) THEN
            RETURN true;
        ELSIF nw_obj_in_tenant_network(p_rule_to.obj_id, p_rule_to.negated != r_rule.rule_dst_neg, i_tenant_id) THEN
            RETURN true;
        END IF;

        -- all rule_to objects are visible if a rule_from object of the same rule is in a tenant network
        RETURN rule_froms_in_tenant_network(p_rule_to.rule_id, r_rule.rule_src_neg, i_tenant_id);
    END;
$$ LANGUAGE 'plpgsql' STABLE;

CREATE OR REPLACE FUNCTION get_changelog_rules_for_tenant(device_row device, tenant integer, hasura_session json)
RETURNS SETOF changelog_rule AS $$
    DECLARE t_id integer;
    
    BEGIN
        t_id := (hasura_session ->> 'x-hasura-tenant-id')::integer;

        IF t_id IS NULL THEN
            RAISE EXCEPTION 'No tenant id found in hasura session';
        -- ELSIF t_id != 1 THEN
        --     RAISE EXCEPTION 'Tenant id in hasura session is not 1 (admin). Tenant simulation not allowed.';
        ELSIF tenant = 1 THEN
            RAISE EXCEPTION 'Tenant 1 (admin) cannot be simulated.';
        ELSE
            RETURN QUERY
                SELECT cl_rule.* FROM changelog_rule cl_rule
                WHERE cl_rule.dev_id = device_row.dev_id AND has_relevant_change(cl_rule, tenant) = true;
        END IF;
    END;
$$ LANGUAGE 'plpgsql' STABLE;

CREATE OR REPLACE FUNCTION get_objects_for_tenant(management_row management, tenant integer, hasura_session json)
RETURNS SETOF firewall.nw_object AS $$
    DECLARE t_id integer;
    
    BEGIN
        t_id := (hasura_session ->> 'x-hasura-tenant-id')::integer;

        IF t_id IS NULL THEN
            RAISE EXCEPTION 'No tenant id found in hasura session'; --> only happens when using auth via x-hasura-admin-secret (no tenant id is set)
        -- ELSIF t_id != 1 THEN
        --     RAISE EXCEPTION 'Tenant id in hasura session is not 1 (admin). Tenant simulation not allowed.';
        ELSIF tenant = 1 THEN
            RAISE EXCEPTION 'Tenant 1 (admin) cannot be simulated.';
        ELSE
            -- the objects used in a rule that is visible to the tenant by its ip addresses (an object or a member of it
            -- in a tenant network, or the other side of the rule in a tenant network), see nw_obj_in_tenant_network
            RETURN QUERY
                SELECT o.* FROM firewall.nw_object o
                WHERE o.mgm_id = management_row.mgm_id
                    AND (o.obj_id IN (
                            SELECT rf.obj_id FROM rule_from rf JOIN rule r ON (r.rule_id = rf.rule_id)
                            WHERE r.mgm_id = management_row.mgm_id AND r.rule_head_text IS NULL
                                AND (nw_obj_in_tenant_network(rf.obj_id, rf.negated != r.rule_src_neg, tenant)
                                    OR rule_tos_in_tenant_network(r.rule_id, r.rule_dst_neg, tenant)))
                        OR o.obj_id IN (
                            SELECT rt.obj_id FROM rule_to rt JOIN rule r ON (r.rule_id = rt.rule_id)
                            WHERE r.mgm_id = management_row.mgm_id AND r.rule_head_text IS NULL
                                AND (nw_obj_in_tenant_network(rt.obj_id, rt.negated != r.rule_dst_neg, tenant)
                                    OR rule_froms_in_tenant_network(r.rule_id, r.rule_src_neg, tenant))))
                ORDER BY o.obj_name;
        END IF;
    END;
$$ LANGUAGE 'plpgsql' STABLE;


------------------------------------------------------------------------------------------------------------------------
-- rule_relevant complexity: O(rf + rt)
-- rule_from_relevant complexity: O(rt)
-- rule_to_relevant complexity: O(rf)
-- total for single rule: O(rf + rt + 2*rf*rt)
--  theoretical min needed complexity: O(2(rf+rt))
-- obj_relevant complexity: O(r * rf * rt)
-- with material view: all O(1) but additional O(ten * r * (rf + rt)) for each import / tenant change

-- replaced by rule_fully_visible_to_tenant, which does not depend on the obsolete rule.dev_id
DROP FUNCTION IF EXISTS public.rulebase_fully_visible_to_tenant(integer, integer);


CREATE OR REPLACE FUNCTION rule_relevant_for_tenant(rule rule, hasura_session json)
RETURNS boolean AS $$
    DECLARE
        t_id integer;
    BEGIN
        t_id := (hasura_session ->> 'x-hasura-tenant-id')::integer;

        IF t_id IS NULL THEN
            RAISE EXCEPTION 'No tenant id found in hasura session'; --> only happens when using auth via x-hasura-admin-secret (no tenant id is set)
        ELSIF t_id = 1 THEN
            RETURN true;
        END IF;

        RETURN rule_fully_visible_to_tenant(rule, t_id)
            OR rule_froms_in_tenant_network(rule.rule_id, rule.rule_src_neg, t_id)
            OR rule_tos_in_tenant_network(rule.rule_id, rule.rule_dst_neg, t_id);
    END;
$$ LANGUAGE 'plpgsql' STABLE;

-- the rules of a device are the rules of the rulebases linked to it while the rule version was valid
-- (rule.dev_id is no longer written by the importer)
CREATE OR REPLACE FUNCTION get_rules_for_tenant(device_row device, tenant integer, hasura_session json)
RETURNS SETOF rule AS $$
    DECLARE
        t_id integer;
    BEGIN
        t_id := (hasura_session ->> 'x-hasura-tenant-id')::integer;

        IF t_id IS NULL THEN
            RAISE EXCEPTION 'No tenant id found in hasura session'; --> only happens when using auth via x-hasura-admin-secret (no tenant id is set)
        ELSIF t_id != 1  AND t_id != tenant THEN
            RAISE EXCEPTION 'A non-tenant-0 user was trying to generate a report for another tenant.';
        ELSIF tenant = 1 THEN
            RAISE EXCEPTION 'Tenant0 cannot be simulated.';
        ELSE -- same visibility as rule_relevant_for_tenant, section headers only for fully visible rules
            RETURN QUERY
                SELECT r.* FROM rule r
                WHERE r.rulebase_id IN (SELECT rl.to_rulebase_id FROM rulebase_link rl WHERE rl.gw_id = device_row.dev_id
                        AND link_valid_for_rule_version(rl.created, rl.removed, r.rule_create, r.removed))
                    AND (rule_fully_visible_to_tenant(r, tenant)
                        OR (r.rule_head_text IS NULL AND (rule_froms_in_tenant_network(r.rule_id, r.rule_src_neg, tenant)
                            OR rule_tos_in_tenant_network(r.rule_id, r.rule_dst_neg, tenant))))
                ORDER BY r.rule_name;
        END IF;
    END;
$$ LANGUAGE 'plpgsql' STABLE;

CREATE OR REPLACE FUNCTION get_rule_froms_for_tenant(rule rule, tenant integer, hasura_session json)
RETURNS SETOF rule_from AS $$
    DECLARE
        t_id integer;
    BEGIN
        t_id := (hasura_session ->> 'x-hasura-tenant-id')::integer;

        IF t_id IS NULL THEN
            RAISE EXCEPTION 'No tenant id found in hasura session'; --> only happens when using auth via x-hasura-admin-secret (no tenant id is set)
        ELSIF t_id != 1  AND t_id != tenant THEN
            RAISE EXCEPTION 'A non-tenant-0 user was trying to generate a report for another tenant.';
        ELSIF tenant = 1 THEN
            RAISE EXCEPTION 'Tenant0 cannot be simulated.';
        ELSIF rule_fully_visible_to_tenant(rule, tenant)
            OR rule_tos_in_tenant_network(rule.rule_id, rule.rule_dst_neg, tenant) THEN
            RETURN QUERY SELECT rf.* FROM rule_from rf WHERE rf.rule_id = rule.rule_id;
        ELSE
            RETURN QUERY
                SELECT rf.* FROM rule_from rf
                WHERE rf.rule_id = rule.rule_id AND nw_obj_in_tenant_network(rf.obj_id, rf.negated != rule.rule_src_neg, tenant);
        END IF;
    END;
$$ LANGUAGE 'plpgsql' STABLE;

CREATE OR REPLACE FUNCTION public.get_rule_tos_for_tenant(rule rule, tenant integer, hasura_session json)
RETURNS SETOF rule_to AS $$
    DECLARE
        t_id integer;
    BEGIN
        t_id := (hasura_session ->> 'x-hasura-tenant-id')::integer;

        IF t_id IS NULL THEN
            RAISE EXCEPTION 'No tenant id found in hasura session'; --> only happens when using auth via x-hasura-admin-secret (no tenant id is set)
        ELSIF t_id != 1  AND t_id != tenant THEN
            RAISE EXCEPTION 'A non-tenant-0 user was trying to generate a report for another tenant.';
        ELSIF tenant = 1 THEN
            RAISE EXCEPTION 'Tenant0 cannot be simulated.';
        ELSIF rule_fully_visible_to_tenant(rule, tenant)
            OR rule_froms_in_tenant_network(rule.rule_id, rule.rule_src_neg, tenant) THEN
            RETURN QUERY SELECT rt.* FROM rule_to rt WHERE rt.rule_id = rule.rule_id;
        ELSE
            RETURN QUERY
                SELECT rt.* FROM rule_to rt
                WHERE rt.rule_id = rule.rule_id AND nw_obj_in_tenant_network(rt.obj_id, rt.negated != rule.rule_dst_neg, tenant);
        END IF;
    END;
$$ LANGUAGE 'plpgsql' STABLE;


CREATE OR REPLACE FUNCTION get_rules_for_owner(device_row device, ownerid integer)
RETURNS SETOF rule AS $$
    BEGIN
        RETURN QUERY
        SELECT r.* FROM rule r
            LEFT JOIN rule_from rf ON (r.rule_id=rf.rule_id)
            LEFT JOIN objgrp_flat rf_of ON (rf.obj_id=rf_of.objgrp_flat_id)
            LEFT JOIN firewall.nw_object rf_o ON (rf_of.objgrp_flat_member_id=rf_o.obj_id)
            LEFT JOIN owner_network ON
            (ip_ranges_overlap(rf_o.obj_ip, rf_o.obj_ip_end, ip, ip_end, rf.negated != r.rule_src_neg))
        WHERE r.dev_id = device_row.dev_id AND owner_id = ownerid AND rule_head_text IS NULL
        UNION
        SELECT r.* FROM rule r
            LEFT JOIN rule_to rt ON (r.rule_id=rt.rule_id)
            LEFT JOIN objgrp_flat rt_of ON (rt.obj_id=rt_of.objgrp_flat_id)
            LEFT JOIN firewall.nw_object rt_o ON (rt_of.objgrp_flat_member_id=rt_o.obj_id)
            LEFT JOIN owner_network ON
            (ip_ranges_overlap(rt_o.obj_ip, rt_o.obj_ip_end, ip, ip_end, rt.negated != r.rule_dst_neg))
        WHERE r.dev_id = device_row.dev_id AND owner_id = ownerid AND rule_head_text IS NULL
        ORDER BY rule_name;
    END;
$$ LANGUAGE 'plpgsql' STABLE;

DROP FUNCTION IF EXISTS public.get_rulebase_for_owner;
DROP VIEW IF EXISTS public.rule_api;
CREATE OR REPLACE VIEW public.rule_api AS
    SELECT
        rule_id, last_change_admin, rule_name, mgm_id, parent_rule_id, parent_rule_type, active, rule_num_numeric,
        rule_ruleid, rule_uid, rule_disabled, rule_src_neg, rule_dst_neg, rule_svc_neg, action_id, track_id,
        rule_src, rule_dst, rule_svc, rule_src_refs, rule_dst_refs, rule_svc_refs,
        rule_action, rule_track, rule_installon, rule_time, rule_comment, rule_head_text, rule_implied, rule_create,
        dev_id, rule_custom_fields, access_rule, nat_rule, xlate_rule, is_global, rulebase_id, removed
    FROM rule;

CREATE OR REPLACE FUNCTION public.get_rulebase_for_owner(
    rulebase_row rulebase,
    ownerid integer
)
RETURNS SETOF rule_api
LANGUAGE plpgsql
STABLE
AS $function$
BEGIN
    RETURN QUERY
    SELECT *
    FROM (
        WITH src_rules AS (
            SELECT r.rule_id, r.rule_src_neg, r.rulebase_id, rf_o.obj_ip, rf_o.obj_ip_end, rf.negated
            FROM rule_api r
            LEFT JOIN rule_from rf ON r.rule_id = rf.rule_id
            LEFT JOIN objgrp_flat rf_of ON rf.obj_id = rf_of.objgrp_flat_id
            LEFT JOIN firewall.nw_object rf_o ON rf_of.objgrp_flat_member_id = rf_o.obj_id
            WHERE r.rulebase_id = rulebase_row.id
              AND r.active = true
              AND rule_head_text IS NULL
        ),
        dst_rules AS (
            SELECT r.rule_id, r.rule_dst_neg, r.rulebase_id, rt_o.obj_ip, rt_o.obj_ip_end, rt.negated
            FROM rule_api r
            LEFT JOIN rule_to rt ON r.rule_id = rt.rule_id
            LEFT JOIN objgrp_flat rt_of ON rt.obj_id = rt_of.objgrp_flat_id
            LEFT JOIN firewall.nw_object rt_o ON rt_of.objgrp_flat_member_id = rt_o.obj_id
            WHERE r.rulebase_id = rulebase_row.id
              AND r.active = true
              AND rule_head_text IS NULL
        )
        SELECT r.*
        FROM src_rules s
        LEFT JOIN owner_network onw ON ip_ranges_overlap(s.obj_ip, s.obj_ip_end, ip, ip_end, s.negated != s.rule_src_neg)
        JOIN rule_api r ON r.rule_id = s.rule_id
        WHERE onw.owner_id = ownerid
        UNION
        SELECT r.*
        FROM dst_rules d
        LEFT JOIN owner_network onw ON ip_ranges_overlap(d.obj_ip, d.obj_ip_end, ip, ip_end, d.negated != d.rule_dst_neg)
        JOIN rule_api r ON r.rule_id = d.rule_id
        WHERE onw.owner_id = ownerid
    ) AS combined
    ORDER BY rule_name ASC;
END;
$function$;

-- SEC-19: the security state of a local user (last login, last password change, password change
-- flag) is shown to that user only. Roles reachable from a UI session other than admin and auditor
-- have no column permission on these uiuser columns; the Hasura computed fields below return them
-- for the row of the session user and NULL for every other row.
CREATE OR REPLACE FUNCTION public.uiuser_own_last_login(uiuser_row uiuser, hasura_session json)
RETURNS timestamp with time zone
LANGUAGE sql
STABLE
AS $function$
    SELECT CASE WHEN uiuser_row.uiuser_id::text = hasura_session ->> 'x-hasura-user-id'
        THEN uiuser_row.uiuser_last_login END;
$function$;

CREATE OR REPLACE FUNCTION public.uiuser_own_last_password_change(uiuser_row uiuser, hasura_session json)
RETURNS timestamp with time zone
LANGUAGE sql
STABLE
AS $function$
    SELECT CASE WHEN uiuser_row.uiuser_id::text = hasura_session ->> 'x-hasura-user-id'
        THEN uiuser_row.uiuser_last_password_change END;
$function$;

CREATE OR REPLACE FUNCTION public.uiuser_own_password_must_be_changed(uiuser_row uiuser, hasura_session json)
RETURNS boolean
LANGUAGE sql
STABLE
AS $function$
    SELECT CASE WHEN uiuser_row.uiuser_id::text = hasura_session ->> 'x-hasura-user-id'
        THEN uiuser_row.uiuser_password_must_be_changed END;
$function$;
