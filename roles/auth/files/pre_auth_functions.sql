
-- CREATE TYPE tenant_type AS (
--     id      int,
--     name    VARCHAR
-- );

Create table "device_type"
(
    "id"      int,
    "name"    VARCHAR
);

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

/* needs to be completed & tested

CREATE OR REPLACE FUNCTION public.get_visible_managements_per_tenant(integer)
    RETURNS SETOF device_type 
    LANGUAGE 'plpgsql'
AS $BODY$
DECLARE
	i_tenant_id ALIAS FOR $1;
	i_mgm_id integer;
    v_mgm_name VARCHAR;
	b_can_view_all_devices boolean;
    i_dev_id integer;
BEGIN
--    SELECT INTO b_can_view_all_devices bool_or(tenant_can_view_all_devices) FROM tenant_to_user JOIN tenant USING (tenant_id) WHERE tenant_to_user.user_id=i_user_id;
    SELECT INTO b_can_view_all_devices tenant_can_view_all_devices FROM tenant WHERE tenant_id=i_tenant_id;
    IF b_can_view_all_devices THEN
        FOR i_mgm_id, v_mgm_name IN SELECT mgm_id, mgm_name FROM management
        LOOP
            RETURN NEXT ROW (i_mgm_id, v_mgm_name);
        END LOOP;
    ELSE
        -- return all managements belonging to devices the tenant can view - derive it from get_visible_devices_per_tenant:
        -- create a distinct list of all mgm_id, mgm_name tuples the tenant can view 

        FOR i_mgm_id, i_mgm_name IN SELECT id, name FROM UNION get_visible_devices_per_tenant(i_tenant_id)
        LOOP
            SELECT INTO i_mgm_id mgm_id FROM device WHERE dev_id=i_dev;
            RETURN NEXT ROW (SELECT mgm_id, mgm_name FROM management WHERE mgm_id=i_mgm_id);
        END LOOP;

        -- FOR i_mgm_id, v_mgm_name IN SELECT mgm_id, mgm_name FROM tenant JOIN tenant_to_device USING (tenant_id) JOIN management ON (tenant_to_device.device_id=device.dev_id) WHERE tenant.tenant_id=i_tenant_id
        -- LOOP
        --     RETURN NEXT i_mgm_id;
        -- END LOOP;
    END IF;
	RETURN;
END;
$BODY$;
*/

/*
CREATE OR REPLACE FUNCTION public.get_visible_managements_per_tenant(integer)
    RETURNS SETOF device_data_table 
    LANGUAGE 'plpgsql'
AS $BODY$
DECLARE
	i_tenant_id ALIAS FOR $1;
	i_mgm_id integer;
    v_mgm_name VARCHAR;
	b_can_view_all_devices boolean;
BEGIN
--    SELECT INTO b_can_view_all_devices bool_or(tenant_can_view_all_devices) FROM tenant_to_user JOIN tenant USING (tenant_id) WHERE tenant_to_user.user_id=i_user_id;
    SELECT INTO b_can_view_all_devices tenant_can_view_all_devices FROM tenant WHERE tenant_id=i_tenant_id;
    IF b_can_view_all_devices THEN
        FOR i_mgm_id, v_dev_name IN SELECT mgm_id, mgm_name FROM management
        LOOP
            RETURN NEXT ROW (i_mgm_id, v_mgm_name);
        END LOOP;
    ELSE
        FOR i_mgm_id, v_mgm_name IN SELECT mgm_id, mgm_name FROM tenant JOIN tenant_to_device USING (tenant_id) JOIN management ON (tenant_to_device.device_id=device.dev_id) WHERE tenant.tenant_id=i_tenant_id
        LOOP
            RETURN NEXT i_mgm_id;
        END LOOP;
    END IF;
	RETURN;
END;
$BODY$;

*/