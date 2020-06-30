
CREATE OR REPLACE FUNCTION public.get_user_visible_devices(integer)
    RETURNS SETOF integer
    LANGUAGE 'plpgsql'
AS $BODY$
DECLARE
	i_user_id ALIAS FOR $1;
	i_dev_id integer;
	b_can_view_all_devices boolean;
BEGIN
    SELECT INTO b_can_view_all_devices bool_or(role_can_view_all_devices) FROM role_to_user JOIN role USING (role_id) WHERE role_to_user.user_id=i_user_id;
    IF b_can_view_all_devices THEN
        FOR i_dev_id IN SELECT dev_id FROM device
        LOOP
            RETURN NEXT i_dev_id;
        END LOOP;
    ELSE
        FOR i_dev_id IN SELECT device_id FROM role_to_user JOIN role USING (role_id) JOIN role_to_device USING (role_id) WHERE role_to_user.user_id=i_user_id
        LOOP
            RETURN NEXT i_dev_id;
        END LOOP;
    END IF;
	RETURN;
END;
$BODY$;

CREATE OR REPLACE FUNCTION public.get_user_visible_managements(integer)
    RETURNS SETOF integer
    LANGUAGE 'plpgsql'
AS $BODY$
DECLARE
	i_user_id ALIAS FOR $1;
	i_mgm_id integer;
	b_can_view_all_devices boolean;
BEGIN
    SELECT INTO b_can_view_all_devices bool_or(role_can_view_all_devices) FROM role_to_user JOIN role USING (role_id) WHERE role_to_user.user_id=i_user_id;
    IF b_can_view_all_devices THEN
        FOR i_mgm_id IN SELECT mgm_id FROM management
        LOOP
            RETURN NEXT i_mgm_id;
        END LOOP;
    ELSE
        FOR i_mgm_id IN SELECT mgm_id FROM role_to_user JOIN role USING (role_id) JOIN role_to_device USING (role_id) JOIN device ON (role_to_device.device_id=device.dev_id) WHERE role_to_user.user_id=i_user_id
        LOOP
            RETURN NEXT i_mgm_id;
        END LOOP;
    END IF;
	RETURN;
END;
$BODY$;