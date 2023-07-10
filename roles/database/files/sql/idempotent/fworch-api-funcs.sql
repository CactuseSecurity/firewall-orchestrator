

CREATE OR REPLACE FUNCTION public.get_visible_devices_per_tenant(integer)
    RETURNS SETOF device_type 
    LANGUAGE 'plpgsql'
    COST 100
    STABLE 
    ROWS 1000
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
        FOR i_dev_id, v_dev_name IN SELECT device_id, dev_name FROM tenant JOIN tenant_to_device USING (tenant_id) LEFT JOIN device ON (tenant_to_device.device_id=device.dev_id) WHERE tenant.tenant_id=i_tenant_id
        LOOP
            RETURN NEXT ROW (i_dev_id, v_dev_name);
        END LOOP;
    END IF;
    RETURN;
END;
$BODY$;

CREATE OR REPLACE FUNCTION public.get_visible_managements_per_tenant(integer)
    RETURNS SETOF device_type 
    LANGUAGE 'plpgsql'
    COST 100
    STABLE 
    ROWS 1000
AS $BODY$
DECLARE
	i_tenant_id ALIAS FOR $1;
	i_mgm_id integer;
    v_mgm_name VARCHAR;
	b_can_view_all_devices boolean;
    i_dev_id integer;
BEGIN
    SELECT INTO b_can_view_all_devices tenant_can_view_all_devices FROM tenant WHERE tenant_id=i_tenant_id;
    IF b_can_view_all_devices THEN
        FOR i_mgm_id, v_mgm_name IN SELECT mgm_id, mgm_name FROM management
        LOOP
            RETURN NEXT ROW (i_mgm_id, v_mgm_name);
        END LOOP;
    ELSE
        -- return all managements belonging to devices the tenant can view - derive it from get_visible_devices_per_tenant:
        FOR i_mgm_id, v_mgm_name IN SELECT DISTINCT mgm_id, mgm_name FROM management WHERE mgm_id IN (SELECT mgm_id FROM device WHERE dev_id IN (SELECT id FROM get_visible_devices_per_tenant(i_tenant_id)))
        LOOP
            RETURN NEXT ROW (i_mgm_id, v_mgm_name);
        END LOOP;
    END IF;
	RETURN;
END;
$BODY$;

CREATE OR REPLACE FUNCTION public.filter_rule_nwobj_resolveds(management_row management, rule_ids bigint[], import_id bigint)
 RETURNS SETOF object
 LANGUAGE sql
 STABLE
AS $function$
  SELECT o.*
  FROM rule_nwobj_resolved r JOIN object o ON (r.obj_id=o.obj_id)
  WHERE r.mgm_id = management_row.mgm_id AND rule_id = any (rule_ids) AND r.created <= import_id AND (r.removed IS NULL OR r.removed >= import_id)
  GROUP BY o.obj_id
  ORDER BY MAX(obj_name), o.obj_id
$function$;

CREATE OR REPLACE FUNCTION public.filter_rule_svc_resolveds(management_row management, rule_ids bigint[], import_id bigint)
 RETURNS SETOF service
 LANGUAGE sql
 STABLE
AS $function$
  SELECT s.*
  FROM rule_svc_resolved r JOIN service s ON (r.svc_id=s.svc_id)
  WHERE r.mgm_id = management_row.mgm_id AND rule_id = any (rule_ids) AND r.created <= import_id AND (r.removed IS NULL OR r.removed >= import_id)
  GROUP BY s.svc_id
  ORDER BY MAX(svc_name), s.svc_id
$function$;

CREATE OR REPLACE FUNCTION public.filter_rule_user_resolveds(management_row management, rule_ids bigint[], import_id bigint)
 RETURNS SETOF usr
 LANGUAGE sql
 STABLE
AS $function$
  SELECT u.*
  FROM rule_user_resolved r JOIN usr u ON (r.user_id=u.user_id)
  WHERE r.mgm_id = management_row.mgm_id AND rule_id = any (rule_ids) AND r.created <= import_id AND (r.removed IS NULL OR r.removed >= import_id)
  GROUP BY u.user_id
  ORDER BY MAX(user_name), u.user_id
$function$;

CREATE OR REPLACE FUNCTION get_objects_for_tenant(management_row management, hasura_session json)
RETURNS SETOF object AS $$
    DECLARE t_id integer;
    
    BEGIN
        t_id := (hasura_session ->> 'x-hasura-tenant-id')::integer;

        IF t_id IS NULL THEN
            RAISE EXCEPTION 'No tenant id found in hasura session'; --> only happens when using auth via x-hasura-admin-secret (no tenant id is set)
        ELSIF t_id = 1 THEN
            RETURN QUERY SELECT o.*
                FROM object o
                WHERE o.mgm_id = management_row.mgm_id
                GROUP BY o.obj_id
                ORDER BY MAX(obj_name), o.obj_id;
        ELSE
            RETURN QUERY SELECT o.*
                FROM object o
                    LEFT JOIN tenant_network ON
                        (o.obj_ip>>=tenant_net_ip OR o.obj_ip<<=tenant_net_ip)
                WHERE o.mgm_id = management_row.mgm_id AND tenant_id = t_id
                GROUP BY o.obj_id
                ORDER BY MAX(obj_name), o.obj_id;
        END IF;
    END;
$$ LANGUAGE 'plpgsql' STABLE;


-- does not use any views
CREATE OR REPLACE FUNCTION get_rule_froms_for_tenant(rule rule, hasura_session json)
RETURNS SETOF rule_from AS $$
    DECLARE t_id integer;
    rule_to_obj RECORD;
    show_all boolean DEFAULT false;
    
    BEGIN
        t_id := (hasura_session ->> 'x-hasura-tenant-id')::integer;

        IF t_id IS NULL THEN
            RAISE EXCEPTION 'No tenant id found in hasura session'; --> only happens when using auth via x-hasura-admin-secret (no tenant id is set)
        ELSIF t_id = 1 THEN
            show_all := true;
        ELSE
            FOR rule_to_obj IN
                SELECT rt.*, tenant_network.tenant_id
                FROM rule_to rt
                    LEFT JOIN objgrp_flat ON (rt.obj_id=objgrp_flat_id)
                    LEFT JOIN object ON (objgrp_flat_member_id=object.obj_id)
                    LEFT JOIN tenant_network ON
                        (obj_ip>>=tenant_net_ip OR obj_ip<<=tenant_net_ip)
                WHERE rule_id = rule.rule_id
            LOOP
                IF rule_to_obj.tenant_id = t_id THEN
                    show_all := true;
                    EXIT;
                END IF;
            END LOOP;
        END IF;


        IF show_all THEN
            RETURN QUERY SELECT *
                FROM rule_from
                WHERE rule_id = rule.rule_id;
        ELSE
            RETURN QUERY SELECT rule_from.*
                FROM rule_from
                    LEFT JOIN objgrp_flat ON (rule_from.obj_id=objgrp_flat.objgrp_flat_id)
                    LEFT JOIN object ON (objgrp_flat.objgrp_flat_member_id=object.obj_id)
                    LEFT JOIN tenant_network ON
                        (obj_ip>>=tenant_net_ip OR obj_ip<<=tenant_net_ip)
                WHERE rule_id = rule.rule_id AND tenant_id = t_id; --OR tenant_id IS NULL ?
        END IF;
    END;
$$ LANGUAGE 'plpgsql' STABLE;


CREATE OR REPLACE FUNCTION get_rule_tos_for_tenant(rule rule, hasura_session json)
RETURNS SETOF rule_to AS $$
    DECLARE t_id integer;
    rule_from_obj RECORD;
    show_all boolean DEFAULT false;
    
    BEGIN
        t_id := (hasura_session ->> 'x-hasura-tenant-id')::integer;

        IF t_id IS NULL THEN
            RAISE EXCEPTION 'No tenant id found in hasura session'; --> only happens when using auth via x-hasura-admin-secret (no tenant id is set)
        ELSIF t_id = 1 THEN
            show_all := true;
        ELSE
            FOR rule_from_obj IN
                SELECT rf.*, tenant_network.tenant_id
                FROM rule_from rf
                    LEFT JOIN objgrp_flat ON (rf.obj_id=objgrp_flat_id)
                    LEFT JOIN object ON (objgrp_flat_member_id=object.obj_id)
                    LEFT JOIN tenant_network ON
                        (obj_ip>>=tenant_net_ip OR obj_ip<<=tenant_net_ip)
                WHERE rule_id = rule.rule_id
            LOOP
                IF rule_from_obj.tenant_id = t_id THEN
                    show_all := true;
                    EXIT;
                END IF;
            END LOOP;
        END IF;

        IF show_all THEN
            RETURN QUERY SELECT *
                FROM rule_to
                WHERE rule_id = rule.rule_id;
        ELSE
            RETURN QUERY SELECT rule_to.*
                FROM rule_to
                    LEFT JOIN objgrp_flat ON (rule_to.obj_id=objgrp_flat.objgrp_flat_id)
                    LEFT JOIN object ON (objgrp_flat.objgrp_flat_member_id=object.obj_id)
                    LEFT JOIN tenant_network ON
                        (obj_ip>>=tenant_net_ip OR obj_ip<<=tenant_net_ip)
                WHERE rule_id = rule.rule_id AND tenant_id = t_id; --OR tenant_id IS NULL ?
        END IF;
    END;
$$ LANGUAGE 'plpgsql' STABLE;


CREATE OR REPLACE FUNCTION has_relevant_change(cl_rule changelog_rule, hasura_session json)
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
            IF EXISTS (
                SELECT diff.obj_id FROM ( -- set of difference between rule_from of old and new rule
                    SELECT obj_id FROM rule_from WHERE rule_id = cl_rule.old_rule_id EXCEPT SELECT obj_id FROM rule_from WHERE rule_id = cl_rule.new_rule_id
                    UNION
                    (SELECT obj_id FROM rule_from WHERE rule_id = cl_rule.new_rule_id EXCEPT SELECT obj_id FROM rule_from WHERE rule_id = cl_rule.old_rule_id)
                ) AS diff
                JOIN objgrp_flat ON (obj_id=objgrp_flat_id)
                JOIN object ON (objgrp_flat_member_id=object.obj_id)
                JOIN tenant_network ON
                    (obj_ip>>=tenant_net_ip OR obj_ip<<=tenant_net_ip)
                WHERE tenant_id = t_id
            ) THEN
                show := true;
            END IF;

            IF EXISTS (
                SELECT diff.obj_id FROM ( -- set of difference between rule_to of old and new rule
                    SELECT obj_id FROM rule_to WHERE rule_id = cl_rule.old_rule_id EXCEPT SELECT obj_id FROM rule_to WHERE rule_id = cl_rule.new_rule_id
                    UNION
                    (SELECT obj_id FROM rule_to WHERE rule_id = cl_rule.new_rule_id EXCEPT SELECT obj_id FROM rule_to WHERE rule_id = cl_rule.old_rule_id)
                ) AS diff
                JOIN objgrp_flat ON (obj_id=objgrp_flat_id)
                JOIN object ON (objgrp_flat_member_id=object.obj_id)
                JOIN tenant_network ON
                    (obj_ip>>=tenant_net_ip OR obj_ip<<=tenant_net_ip)
                WHERE tenant_id = t_id
            ) THEN
                show := true;
            END IF;

        END IF;

        RETURN show;
    END;
$$ LANGUAGE 'plpgsql' STABLE;
