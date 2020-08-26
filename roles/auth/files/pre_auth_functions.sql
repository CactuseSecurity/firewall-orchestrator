/*
CREATE TYPE role_type AS (
    id      int,
    name    VARCHAR
);


Create table "device_data_table"
(
    "id"      int,
    "name"    VARCHAR
);
CREATE TYPE device_type AS (
    id      int,
    name    VARCHAR
);

CREATE OR REPLACE FUNCTION public.get_role_visible_device_types(
	integer)
    RETURNS SETOF device_data_table 
    LANGUAGE 'plpgsql'

    COST 100
    STABLE 
    ROWS 1000
    
AS $BODY$
DECLARE
	i_role_id ALIAS FOR $1;
	i_dev_id integer;
    v_dev_name VARCHAR;
	b_can_view_all_devices boolean;
BEGIN
    SELECT INTO b_can_view_all_devices role_can_view_all_devices FROM role WHERE role_id=i_user_id;
    IF b_can_view_all_devices THEN
        FOR i_dev_id, v_dev_name IN SELECT dev_id, dev_name FROM device
        LOOP
            RETURN NEXT ROW (i_dev_id, i_dev_name);
        END LOOP;
    ELSE
        FOR i_dev_id, v_dev_name IN SELECT device_id, dev_name FROM role JOIN role_to_device USING (role_id) LEFT JOIN device ON (role_to_device.dev_id=device.dev_id) WHERE role_id=i_role_id
        LOOP
            RETURN NEXT ROW (i_dev_id, i_dev_name);
        END LOOP;
    END IF;
    RETURN;
END;
$BODY$;

*/

CREATE OR REPLACE FUNCTION public.get_role_visible_devices(
	integer)
    RETURNS SETOF integer 
    LANGUAGE 'plpgsql'

    COST 100
    STABLE 
    ROWS 1000
    
AS $BODY$
DECLARE
	i_role_id ALIAS FOR $1;
	i_dev_id integer;
	b_can_view_all_devices boolean;
BEGIN
    SELECT INTO b_can_view_all_devices role_can_view_all_devices FROM role WHERE role_id=i_user_id;
    IF b_can_view_all_devices THEN
        FOR i_dev_id IN SELECT dev_id FROM device
        LOOP
            RETURN NEXT i_dev_id;
        END LOOP;
    ELSE
        FOR i_dev_id IN SELECT device_id FROM role JOIN role_to_device USING (role_id) WHERE role_id=i_role_id
        LOOP
            RETURN NEXT i_dev_id;
        END LOOP;
    END IF;
    RETURN;
END;
$BODY$;


CREATE OR REPLACE FUNCTION public.get_role_visible_managements(integer)
    RETURNS SETOF integer
    LANGUAGE 'plpgsql'
AS $BODY$
DECLARE
	i_role_id ALIAS FOR $1;
	i_mgm_id integer;
	b_can_view_all_devices boolean;
BEGIN
--    SELECT INTO b_can_view_all_devices bool_or(role_can_view_all_devices) FROM role_to_user JOIN role USING (role_id) WHERE role_to_user.user_id=i_user_id;
    SELECT INTO b_can_view_all_devices role_can_view_all_devices FROM role WHERE role_id=i_role_id;
    IF b_can_view_all_devices THEN
        FOR i_mgm_id IN SELECT mgm_id FROM management
        LOOP
            RETURN NEXT i_mgm_id;
        END LOOP;
    ELSE
        FOR i_mgm_id IN SELECT mgm_id FROM role JOIN role_to_device USING (role_id) JOIN device ON (role_to_device.device_id=device.dev_id) WHERE role.role_id=i_role_id
        LOOP
            RETURN NEXT i_mgm_id;
        END LOOP;
    END IF;
	RETURN;
END;
$BODY$;