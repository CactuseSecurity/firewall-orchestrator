

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

CREATE OR REPLACE FUNCTION public.get_owners_per_task(integer)
    RETURNS SETOF owner 
    LANGUAGE 'plpgsql'
    COST 100
    STABLE 
    ROWS 1000
AS $BODY$
DECLARE
	i_task_id ALIAS FOR $1;
    i_id integer;
    v_name Varchar;
    v_dn Varchar;
    v_group_dn Varchar;
    b_default boolean;
    i_tenant_id integer;
    i_recert_interval integer;
	t_next_recert_date Timestamp;
    v_app_id_external varchar;
BEGIN
    FOR i_id, v_name, v_dn, v_group_dn, b_default, i_tenant_id, i_recert_interval, t_next_recert_date, v_app_id_external
    IN SELECT id, name, dn, group_dn, is_default, tenant_id, recert_interval, next_recert_date, app_id_external FROM request.task JOIN request_owner USING (request_task_id) LEFT JOIN owner ON (request_owner.owner_id=owner.id) WHERE request.task.id=i_task_id
    LOOP
        RETURN NEXT ROW (i_id, v_name, v_dn, v_group_dn, b_default, i_tenant_id, i_recert_interval, t_next_recert_date, v_app_id_external);
    END LOOP;
    RETURN;
END;
$BODY$;
