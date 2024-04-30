-- $Id: iso-import.sql,v 1.1.2.3 2011-05-07 08:46:47 tim Exp $
-- $Source: /home/cvs/iso/package/install/database/Attic/iso-import.sql,v $

----------------------------------------------------
-- FUNCTION:  undocumented_rule_changes_exist
-- Zweck:     pruefen, ob noch nicht dokumentierte Aenderungen existieren
-- Parameter: KEINE
-- RETURNS:   INTEGER (Anzahl der undok. Aenderungen)
--
CREATE OR REPLACE FUNCTION undocumented_rule_changes_exist() RETURNS INTEGER AS $$
DECLARE
	i_anz_rule_ch INTEGER;
BEGIN
	SELECT INTO i_anz_rule_ch COUNT(*) FROM changelog_rule WHERE NOT documented;
	RETURN (i_anz_rule_ch);
END;
$$ LANGUAGE plpgsql;

----------------------------------------------------
-- FUNCTION:  undocumented_svc_changes_exist
-- Zweck:     pruefen, ob noch nicht dokumentierte Aenderungen existieren
-- Parameter: KEINE
-- RETURNS:   INTEGER
--
CREATE OR REPLACE FUNCTION undocumented_svc_changes_exist() RETURNS INTEGER AS $$
DECLARE
	i_anz_svc_ch INTEGER;
BEGIN
	SELECT INTO i_anz_svc_ch COUNT(*) FROM changelog_service WHERE NOT documented;
	RETURN (i_anz_svc_ch);
END;
$$ LANGUAGE plpgsql;

----------------------------------------------------
-- FUNCTION:  undocumented_usr_changes_exist
-- Zweck:     pruefen, ob noch nicht dokumentierte Aenderungen existieren
-- Parameter: KEINE
-- RETURNS:   INTEGER
--
CREATE OR REPLACE FUNCTION undocumented_usr_changes_exist() RETURNS INTEGER AS $$
DECLARE
	i_anz_usr_ch INTEGER;
BEGIN
	SELECT INTO i_anz_usr_ch COUNT(*) FROM changelog_user WHERE NOT documented;
	RETURN (i_anz_usr_ch);
END;
$$ LANGUAGE plpgsql;

----------------------------------------------------
-- FUNCTION:  undocumented_obj_changes_exist
-- Zweck:     pruefen, ob noch nicht dokumentierte Aenderungen existieren
-- Parameter: KEINE
-- RETURNS:   INTEGER
--
CREATE OR REPLACE FUNCTION undocumented_obj_changes_exist() RETURNS INTEGER AS $$
DECLARE
	i_anz_obj_ch INTEGER;
BEGIN
	SELECT INTO i_anz_obj_ch COUNT(*) FROM changelog_object WHERE NOT documented;
	RETURN (i_anz_obj_ch);
END;
$$ LANGUAGE plpgsql;

----------------------------------------------------
-- FUNCTION:  undocumented_changes_exist
-- Zweck:     pruefen, ob noch nicht dokumentierte Aenderungen existieren
-- Parameter: KEINE
-- RETURNS:   INTEGER
--
CREATE OR REPLACE FUNCTION undocumented_changes_exist() RETURNS INTEGER AS $$
DECLARE
	i_anz_obj_ch INTEGER;
	i_anz_usr_ch INTEGER;
	i_anz_svc_ch INTEGER;
	i_anz_rule_ch INTEGER;
BEGIN
	SELECT INTO i_anz_obj_ch COUNT(*) FROM changelog_object WHERE NOT documented;
	SELECT INTO i_anz_usr_ch COUNT(*) FROM changelog_user WHERE NOT documented;
	SELECT INTO i_anz_svc_ch COUNT(*) FROM changelog_service WHERE NOT documented;
	SELECT INTO i_anz_rule_ch COUNT(*) FROM changelog_rule WHERE NOT documented;
	RETURN (i_anz_obj_ch + i_anz_usr_ch + i_anz_svc_ch + i_anz_rule_ch);
END;
$$ LANGUAGE plpgsql;

----------------------------------------------------
-- FUNCTION:  is_import_running
-- Zweck:     pruefen, ob gerade anderer Import aktiv laeuft
-- Parameter: KEINE
-- RETURNS:   BOOLEAN (TRUE: import aktiv, FALSE: kein Import aktiv)
--
CREATE OR REPLACE FUNCTION is_import_running() RETURNS BOOLEAN AS $$
DECLARE
	last_import_time RECORD;
	last_control_id RECORD;
BEGIN
	SELECT INTO last_import_time MAX(start_time) AS last_import FROM import_control WHERE successful_import;
	SELECT INTO last_control_id control_id FROM import_control WHERE stop_time IS NULL AND successful_import AND start_time = last_import_time.last_import;
	IF NOT FOUND THEN  -- OK, kein Import aktiv
		RETURN FALSE;
	ELSE
		RETURN TRUE;
	END IF;
END;
$$ LANGUAGE plpgsql;

----------------------------------------------------
-- FUNCTION:  is_import_running
-- Zweck:     pruefen, ob gerade anderer Import des Managements aktiv laeuft
-- Parameter: Management-System
-- RETURNS:   BOOLEAN (TRUE: import aktiv, FALSE: kein Import aktiv)
--
CREATE OR REPLACE FUNCTION is_import_running(INTEGER) RETURNS BOOLEAN AS $$
DECLARE
	i_mgm_id ALIAS FOR $1;
	last_control_id RECORD;
BEGIN
	SELECT INTO last_control_id control_id FROM import_control
		WHERE stop_time IS NULL AND mgm_id=i_mgm_id;
	IF NOT FOUND THEN  -- OK, kein Import aktiv
		RETURN FALSE;
	ELSE
		RETURN TRUE;
	END IF;
END;
$$ LANGUAGE plpgsql;

----------------------------------------------------
-- FUNCTION:  get_previous_import_id_for_mgmt
-- Zweck:     liefert die ID des direkt vor $2 liegenden Imports des Managements $1 zurueck
-- Parameter1: Management-ID
-- Parameter2: Bezugspunkt (normalerweise = current_import_id)
-- RETURNS:   BIGINT Import-ID
--
CREATE OR REPLACE FUNCTION get_previous_import_id_for_mgmt (INTEGER,BIGINT) RETURNS BIGINT AS $$
DECLARE
	i_mgm_id ALIAS FOR $1; -- ID des Managements
	i_import_id ALIAS FOR $2; -- ID des Imports
	i_prev_import_id BIGINT; -- temp. Record
BEGIN
	IF i_import_id IS NULL THEN
		RETURN NULL;
	ELSE
		SELECT INTO i_prev_import_id MAX(control_id) FROM import_control WHERE mgm_id=i_mgm_id AND control_id<i_import_id AND successful_import;
		IF NOT FOUND THEN
			RETURN NULL;
		END IF;
		RETURN i_prev_import_id;
	END IF;
END;
$$ LANGUAGE plpgsql;


----------------------------------------------------
-- FUNCTION:  get_last_import_id_for_mgmt
-- Zweck:     liefert die ID des letzten Imports des Managements $1 zurueck
-- Parameter1: Management-ID
-- RETURNS:   BIGINT Import-ID
--
CREATE OR REPLACE FUNCTION get_last_import_id_for_mgmt (INTEGER) RETURNS BIGINT AS $$
DECLARE
	i_mgm_id ALIAS FOR $1; -- ID des Managements
	i_prev_import_id BIGINT; -- temp. Record
BEGIN
	SELECT INTO i_prev_import_id MAX(control_id) FROM import_control WHERE mgm_id=i_mgm_id AND successful_import;
	IF NOT FOUND THEN
		RETURN NULL;
	END IF;
	RETURN i_prev_import_id;
END;
$$ LANGUAGE plpgsql;


----------------------------------------------------
-- FUNCTION:  get_previous_import_id
-- Zweck:     liefert die ID des direkt vor $2 liegenden Imports des selben Managements zurueck
-- Parameter1: Import_id (normalerweise = current_import_id)
-- RETURNS:   BIGINT Import-ID
--
CREATE OR REPLACE FUNCTION get_previous_import_id (BIGINT) RETURNS BIGINT AS $$
DECLARE
	i_import_id ALIAS FOR $1; 
	i_mgm_id INTEGER; -- ID des Managements
BEGIN
	SELECT INTO i_mgm_id mgm_id FROM import_control WHERE control_id=i_import_id;
	RETURN get_previous_import_id_for_mgmt(i_mgm_id, i_import_id);
END;
$$ LANGUAGE plpgsql;

----------------------------------------------------
-- FUNCTION:  get_import_id_for_dev_at_time
-- Zweck:     liefert die ID des direkt vor $2 liegenden Imports des Devices $1 zurueck
-- Parameter1: Device-ID
-- Parameter2: Zeitpunkt
-- RETURNS:   BIGINT Import-ID
--
CREATE OR REPLACE FUNCTION get_import_id_for_dev_at_time (INTEGER,TIMESTAMP) RETURNS BIGINT AS $$
DECLARE
	i_dev_id ALIAS FOR $1; -- ID des Devices
	t_time ALIAS FOR $2; -- Report-Zeitpunkt
	i_mgm_id INTEGER; -- ID des Managements
	i_import_id BIGINT; -- Result
BEGIN
	SELECT INTO i_mgm_id mgm_id FROM device WHERE dev_id=i_dev_id;
	RETURN get_import_id_for_mgmt_at_time(i_mgm_id, t_time);
END;
$$ LANGUAGE plpgsql;

----------------------------------------------------
-- FUNCTION:  get_import_id_for_mgmt_at_time
-- Zweck:     liefert die ID des direkt vor $2 liegenden Imports des Managements $1 zurueck
-- Parameter1: Management-ID
-- Parameter2: Zeitpunkt
-- RETURNS:   BIGINT Import-ID
--
CREATE OR REPLACE FUNCTION get_import_id_for_mgmt_at_time (INTEGER,TIMESTAMP) RETURNS BIGINT AS $$
DECLARE
	i_mgm_id ALIAS FOR $1; -- ID des Managements
	t_time ALIAS FOR $2; -- Report-Zeitpunkt
	i_import_id BIGINT; -- Result
BEGIN
	SELECT INTO i_import_id MAX(control_id) FROM import_control
		WHERE mgm_id=i_mgm_id AND start_time<t_time AND NOT stop_time IS NULL AND successful_import;
	IF NOT FOUND THEN
		RETURN NULL;
	END IF;
	RETURN i_import_id;
END;
$$ LANGUAGE plpgsql;

----------------------------------------------------
-- FUNCTION:  rollback_current_import
-- Zweck:     entfernt alle Daten des aktuell laufenden Imports
-- Parameter: KEINE
-- RETURNS:   NIX
--
CREATE OR REPLACE FUNCTION rollback_current_import() RETURNS VOID AS $$
DECLARE
	last_import_time RECORD;
	i_cur_ctrl_id BIGINT;
BEGIN
	SELECT INTO last_import_time MAX(start_time) AS last_import FROM import_control;
	SELECT INTO i_cur_ctrl_id control_id FROM import_control WHERE stop_time IS NULL AND start_time = last_import_time.last_import AND successful_import;
	IF FOUND THEN
		DELETE FROM import_service    WHERE control_id = i_cur_ctrl_id;
		DELETE FROM import_object     WHERE control_id = i_cur_ctrl_id;
		DELETE FROM import_rule       WHERE control_id = i_cur_ctrl_id;
		DELETE FROM import_zone       WHERE control_id = i_cur_ctrl_id;
		DELETE FROM import_user       WHERE control_id = i_cur_ctrl_id;
--      TODO: hier fehlt noch der Rollback in den eigentlichen Daten
	-- neu angelegte Objekte koennen gleich wieder geloescht werden
		DELETE FROM changelog_service	WHERE control_id = i_cur_ctrl_id AND change_action='N';
		DELETE FROM changelog_object	WHERE control_id = i_cur_ctrl_id AND change_action='N';
		DELETE FROM changelog_user		WHERE control_id = i_cur_ctrl_id AND change_action='N';
		DELETE FROM changelog_rule		WHERE control_id = i_cur_ctrl_id AND change_action='N';
		DELETE FROM service				WHERE svc_create=i_cur_ctrl_id;
		DELETE FROM object				WHERE obj_create=i_cur_ctrl_id;
		DELETE FROM usr					WHERE user_create=i_cur_ctrl_id;
		DELETE FROM rule				WHERE rule_create=i_cur_ctrl_id;
		-- Loeschen der Regelreihenfolge
		-- DELETE FROM rule_order			WHERE control_id=i_cur_ctrl_id;
		-- abschliessend Loeschen des Control-Eintrags
		DELETE FROM import_control		WHERE control_id = i_cur_ctrl_id;
--	ELSE  -- TODO: Fehlerbehandlung - was, wenn kein Import laeuft?
--		PERFORM error_handling('ERR_NO_IMP_RUNNING', CAST (last_import_time.last_import AS VARCHAR));
    END IF;
    RETURN;
END;
$$ LANGUAGE plpgsql;


----------------------------------------------------
-- FUNCTION:  rollback_import_of_mgm
-- Zweck:     entfernt alle Daten des aktuell laufenden Imports eines Managementsystems
-- Parameter: mgm_id
-- RETURNS:   NIX
--
CREATE OR REPLACE FUNCTION rollback_import_of_mgm(INTEGER) RETURNS VOID AS $$
DECLARE
	i_mgm_id ALIAS FOR $1; -- ID des zurueckzuruderndes Managements
	last_import_time RECORD;
	i_cur_ctrl_id BIGINT;
BEGIN
	SELECT INTO i_cur_ctrl_id MAX(control_id) FROM import_control WHERE mgm_id=i_mgm_id AND stop_time is NULL;
	IF FOUND THEN
		DELETE FROM import_service	WHERE control_id = i_cur_ctrl_id;
		DELETE FROM import_object	WHERE control_id = i_cur_ctrl_id;
		DELETE FROM import_rule		WHERE control_id = i_cur_ctrl_id;
		DELETE FROM import_zone		WHERE control_id = i_cur_ctrl_id;
		DELETE FROM import_user		WHERE control_id = i_cur_ctrl_id;
		DELETE FROM changelog_service	WHERE control_id = i_cur_ctrl_id AND change_action='N';
		DELETE FROM changelog_object	WHERE control_id = i_cur_ctrl_id AND change_action='N';
		DELETE FROM changelog_user	WHERE control_id = i_cur_ctrl_id AND change_action='N';
		DELETE FROM changelog_rule	WHERE control_id = i_cur_ctrl_id AND change_action='N';
		DELETE FROM service		WHERE svc_create=i_cur_ctrl_id;
		DELETE FROM object		WHERE obj_create=i_cur_ctrl_id;
		DELETE FROM usr			WHERE user_create=i_cur_ctrl_id;
		DELETE FROM rule		WHERE rule_create=i_cur_ctrl_id;
		-- DELETE FROM rule_order		WHERE control_id=i_cur_ctrl_id; 		-- Loeschen der Regelreihenfolge
		DELETE FROM import_control	WHERE control_id = i_cur_ctrl_id; 		-- abschliessend Loeschen des Control-Eintrags
	ELSE  
		RAISE NOTICE 'No running import found for management id %', i_mgm_id;
	END IF;
	RETURN;
END;
$$ LANGUAGE plpgsql;

----------------------------------------------------
-- FUNCTION:  remove_import_lock
-- Zweck:     setzt die stop_time des aktuellen import_control-Datensatzes nach Beendigung des Imports
-- Parameter: control_id
-- RETURNS:   TRUE (dummy)
--
CREATE OR REPLACE FUNCTION remove_import_lock(BIGINT) RETURNS BOOLEAN AS $$
DECLARE
    i_last_control_id ALIAS FOR $1; -- ID des aktiven Imports
BEGIN    -- setzen der stop_time
    UPDATE import_control SET stop_time = now() WHERE control_id = i_last_control_id;
    RETURN TRUE;
END;
$$ LANGUAGE plpgsql;

----------------------------------------------------
-- FUNCTION:  show_change_summary
-- Zweck:     gibt einen String mit allen Aenderungen zurueck
-- Parameter: import_id (BIGINT)
-- RETURNS:   VARCHAR
--
CREATE OR REPLACE FUNCTION show_change_summary(BIGINT) RETURNS VARCHAR AS $$
DECLARE
    ctrl_id ALIAS FOR $1;
    r_change RECORD; -- Record mit Anzahl Aenderungen
    v_changed VARCHAR; -- String mit allen Aenderungen
BEGIN
    v_changed := '';
    SELECT INTO r_change COUNT(*) AS anzahl FROM changelog_object WHERE control_id = ctrl_id AND change_action = 'I';
    IF (r_change.anzahl>0) THEN
	v_changed := v_changed || E'\n' || error_handling('MSG_NUMBER_CHANGES_OBJ_INS', CAST (r_change.anzahl AS VARCHAR));
    END IF;
    SELECT INTO r_change COUNT(*) AS anzahl FROM changelog_object WHERE control_id = ctrl_id AND change_action = 'C';
    IF (r_change.anzahl>0) THEN
	v_changed := v_changed || E'\n' || error_handling('MSG_NUMBER_CHANGES_OBJ_CHG', CAST (r_change.anzahl AS VARCHAR));
    END IF;
    SELECT INTO r_change COUNT(*) AS anzahl FROM changelog_object WHERE control_id = ctrl_id AND change_action = 'D';
    IF (r_change.anzahl>0) THEN
	v_changed := v_changed || E'\n' || error_handling('MSG_NUMBER_CHANGES_OBJ_DEL', CAST (r_change.anzahl AS VARCHAR));
    END IF;
----------------------------
    SELECT INTO r_change COUNT(*) AS anzahl FROM changelog_user WHERE control_id = ctrl_id AND change_action = 'I';
    IF (r_change.anzahl>0) THEN
	v_changed := v_changed || E'\n' || error_handling('MSG_NUMBER_CHANGES_USR_INS', CAST (r_change.anzahl AS VARCHAR));
    END IF;
    SELECT INTO r_change COUNT(*) AS anzahl FROM changelog_user WHERE control_id = ctrl_id AND change_action = 'C';
    IF (r_change.anzahl>0) THEN
	v_changed := v_changed || E'\n' || error_handling('MSG_NUMBER_CHANGES_USR_CHG', CAST (r_change.anzahl AS VARCHAR));
    END IF;
    SELECT INTO r_change COUNT(*) AS anzahl FROM changelog_user WHERE control_id = ctrl_id AND change_action = 'D';
    IF (r_change.anzahl>0) THEN
	v_changed := v_changed || E'\n' || error_handling('MSG_NUMBER_CHANGES_USR_DEL', CAST (r_change.anzahl AS VARCHAR));
    END IF;
----------------------------
    SELECT INTO r_change COUNT(*) AS anzahl FROM changelog_service WHERE control_id = ctrl_id AND change_action = 'I';
    IF (r_change.anzahl>0) THEN
        v_changed := v_changed || E'\n' || error_handling('MSG_NUMBER_CHANGES_SVC_INS', CAST (r_change.anzahl AS VARCHAR));
    END IF;
    SELECT INTO r_change COUNT(*) AS anzahl FROM changelog_service WHERE control_id = ctrl_id AND change_action = 'C';
    IF (r_change.anzahl>0) THEN
        v_changed := v_changed || E'\n' || error_handling('MSG_NUMBER_CHANGES_SVC_CHG', CAST (r_change.anzahl AS VARCHAR));
    END IF;
    SELECT INTO r_change COUNT(*) AS anzahl FROM changelog_service WHERE control_id = ctrl_id AND change_action = 'D';
    IF (r_change.anzahl>0) THEN
        v_changed := v_changed || E'\n' || error_handling('MSG_NUMBER_CHANGES_SVC_DEL', CAST (r_change.anzahl AS VARCHAR));
    END IF;
----------------------------
    SELECT INTO r_change COUNT(*) AS anzahl FROM changelog_rule WHERE control_id = ctrl_id AND change_action = 'I';
    IF (r_change.anzahl>0) THEN
        v_changed := v_changed || E'\n' || error_handling('MSG_NUMBER_CHANGES_RULE_INS', CAST (r_change.anzahl AS VARCHAR));
    END IF;
    SELECT INTO r_change COUNT(*) AS anzahl FROM changelog_rule WHERE control_id = ctrl_id AND change_action = 'C';
    IF (r_change.anzahl>0) THEN
        v_changed := v_changed || E'\n' || error_handling('MSG_NUMBER_CHANGES_RULE_CHG', CAST (r_change.anzahl AS VARCHAR));
    END IF;
    SELECT INTO r_change COUNT(*) AS anzahl FROM changelog_rule WHERE control_id = ctrl_id AND change_action = 'D';
    IF (r_change.anzahl>0) THEN
        v_changed := v_changed || E'\n' || error_handling('MSG_NUMBER_CHANGES_RULE_DEL', CAST (r_change.anzahl AS VARCHAR));
    END IF;
----------------------------
    RETURN v_changed;
END;
$$ LANGUAGE plpgsql;

CREATE OR REPLACE FUNCTION found_changes_in_import(BIGINT) RETURNS VARCHAR AS $$
BEGIN
    RETURN (select show_change_summary($1)<>'');
END;
$$ LANGUAGE plpgsql;

CREATE OR REPLACE FUNCTION clean_up_tables (BIGINT) RETURNS VOID AS $$
DECLARE
	i_current_import_id ALIAS FOR $1;
BEGIN
	DELETE FROM import_object WHERE control_id=i_current_import_id;
    VACUUM ANALYZE import_object;
    DELETE FROM import_service WHERE control_id=i_current_import_id;
    VACUUM ANALYZE import_service;
    DELETE FROM import_user WHERE control_id=i_current_import_id;
    VACUUM ANALYZE import_user;
    DELETE FROM import_rule WHERE control_id=i_current_import_id;
    VACUUM ANALYZE import_rule;
    DELETE FROM import_zone WHERE control_id=i_current_import_id;
    VACUUM ANALYZE import_zone;

	VACUUM ANALYZE object; -- neuordnen der Tabellen
	VACUUM ANALYZE objgrp_flat; -- neuordnen der Tabellen
	VACUUM ANALYZE rule_from; -- neuordnen der Tabellen
	VACUUM ANALYZE rule_to; -- neuordnen der Tabellen
	VACUUM ANALYZE service; -- neuordnen der Tabellen
	VACUUM ANALYZE rule; -- neuordnen der Tabellen
	VACUUM ANALYZE usr; -- neuordnen der Tabellen
	RETURN;
END;
$$ LANGUAGE plpgsql;
