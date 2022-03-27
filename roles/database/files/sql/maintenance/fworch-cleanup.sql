----------------------------------------------------
-- Maintenance functions for db cleanup
----------------------------------------------------

CREATE OR REPLACE FUNCTION delete_import(BIGINT) RETURNS VOID AS $$
DECLARE
	i_import_id ALIAS FOR $1; -- ID des zurueckzuruderndes Managements
BEGIN
	DELETE FROM import_service	WHERE control_id = i_import_id;
	DELETE FROM import_object	WHERE control_id = i_import_id;
	DELETE FROM import_rule		WHERE control_id = i_import_id;
	DELETE FROM import_zone		WHERE control_id = i_import_id;
	DELETE FROM import_user		WHERE control_id = i_import_id;
	DELETE FROM changelog_service	WHERE control_id = i_import_id;
	DELETE FROM changelog_object	WHERE control_id = i_import_id;
	DELETE FROM changelog_user	WHERE control_id = i_import_id;
	DELETE FROM changelog_rule	WHERE control_id = i_import_id;
	DELETE FROM service		WHERE svc_create=i_import_id;
	DELETE FROM object		WHERE obj_create=i_import_id;
	DELETE FROM usr			WHERE user_create=i_import_id;
	DELETE FROM rule		WHERE rule_create=i_import_id;
	DELETE FROM rule_order		WHERE control_id=i_import_id; 		-- Loeschen der Regelreihenfolge
	DELETE FROM import_control	WHERE control_id = i_import_id; 		-- abschliessend Loeschen des Control-Eintrags
	RETURN;
END;
$$ LANGUAGE plpgsql;

CREATE OR REPLACE FUNCTION get_stale_imports(BIGINT) RETURNS VOID AS $$
-- returns all import ids that are unneccessary
DECLARE
	i_import_id ALIAS FOR $1; -- ID des zurueckzuruderndes Managements
BEGIN
	-- to be filled
	RETURN;
END;
$$ LANGUAGE plpgsql;

--  loesche alle rule_order Eintraege, die nicht referenziert werden
-- DELETE FROM rule_order WHERE control_id IN (
--   SELECT control_id FROM import_control WHERE NOT control_id IN (
--      SELECT control_id AS id FROM import_control WHERE control_id IN
-- 	(
-- 		SELECT control_id AS id FROM changelog_service UNION
-- 		SELECT control_id AS id FROM changelog_rule UNION
-- 		SELECT control_id AS id FROM changelog_object UNION
-- 		SELECT control_id AS id FROM changelog_user UNION
-- 		SELECT rule_create AS id FROM rule UNION
-- 		SELECT rule_last_seen AS id FROM rule UNION
-- 		SELECT rt_create AS id FROM rule_to UNION
-- 		SELECT rt_last_seen AS id FROM rule_to UNION
-- 		SELECT rf_create AS id FROM rule_from UNION
-- 		SELECT rf_last_seen AS id FROM rule_from UNION
-- 		SELECT rs_create AS id FROM rule_service UNION
-- 		SELECT rs_last_seen AS id FROM rule_service UNION
-- 		SELECT import_created AS id FROM usergrp_flat UNION
-- 		SELECT import_last_seen AS id FROM usergrp_flat UNION
-- 		SELECT import_created AS id FROM usergrp UNION
-- 		SELECT import_last_seen AS id FROM usergrp UNION
-- 		SELECT import_created AS id FROM objgrp_flat UNION
-- 		SELECT import_last_seen AS id FROM objgrp_flat UNION
-- 		SELECT import_created AS id FROM objgrp UNION
-- 		SELECT import_last_seen AS id FROM objgrp UNION
-- 		SELECT import_created AS id FROM svcgrp_flat UNION
-- 		SELECT import_last_seen AS id FROM svcgrp_flat UNION
-- 		SELECT import_created AS id FROM svcgrp UNION
-- 		SELECT import_last_seen AS id FROM svcgrp UNION
-- 		SELECT obj_create AS id FROM object UNION
-- 		SELECT obj_last_seen AS id FROM object UNION
-- 		SELECT svc_create AS id FROM service UNION
-- 		SELECT svc_last_seen AS id FROM service

-- 	)
--     )
-- );


--  loesche alle import_control Eintraege, die nicht referenziert werden
DELETE FROM import_control WHERE control_id IN (
  SELECT control_id FROM import_control WHERE NOT control_id IN (
     SELECT control_id AS id FROM import_control WHERE control_id IN
	(
		SELECT control_id AS id FROM changelog_service UNION
		SELECT control_id AS id FROM changelog_rule UNION
		SELECT control_id AS id FROM changelog_object UNION
		SELECT control_id AS id FROM changelog_user UNION
		SELECT rule_create AS id FROM rule UNION
		SELECT rule_last_seen AS id FROM rule UNION
		SELECT rt_create AS id FROM rule_to UNION
		SELECT rt_last_seen AS id FROM rule_to UNION
		SELECT rf_create AS id FROM rule_from UNION
		SELECT rf_last_seen AS id FROM rule_from UNION
		SELECT rs_create AS id FROM rule_service UNION
		SELECT rs_last_seen AS id FROM rule_service UNION
		SELECT import_created AS id FROM usergrp_flat UNION
		SELECT import_last_seen AS id FROM usergrp_flat UNION
		SELECT import_created AS id FROM usergrp UNION
		SELECT import_last_seen AS id FROM usergrp UNION
		SELECT import_created AS id FROM objgrp_flat UNION
		SELECT import_last_seen AS id FROM objgrp_flat UNION
		SELECT import_created AS id FROM objgrp UNION
		SELECT import_last_seen AS id FROM objgrp UNION
		SELECT import_created AS id FROM svcgrp_flat UNION
		SELECT import_last_seen AS id FROM svcgrp_flat UNION
		SELECT import_created AS id FROM svcgrp UNION
		SELECT import_last_seen AS id FROM svcgrp UNION
		SELECT obj_create AS id FROM object UNION
		SELECT obj_last_seen AS id FROM object UNION
		SELECT svc_create AS id FROM service UNION
		SELECT svc_last_seen AS id FROM service

	)
    )
);
