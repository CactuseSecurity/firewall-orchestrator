--------------------------------------------------------------------------------------
-- BASIC FUNCTIONS
----------------------------------------------------
-- FUNCTION:  is_numeric
-- Zweck:     ist ein String eine reine Zahl?
-- Parameter: VARCHAR
-- RETURNS:   BOOLEAN
--
CREATE OR REPLACE FUNCTION is_numeric (varchar)
    RETURNS boolean
    AS $$
DECLARE
    input ALIAS FOR $1;
BEGIN
    RETURN (input ~ '[0-9]');
END;
$$
LANGUAGE plpgsql;

----------------------------------------------------
-- FUNCTION:  are_equal
-- Zweck:     sind zwei Werte gleich (oder beide NULL)?
-- Parameter: 2x Boolean oder 2x varchar oder ...
-- RETURNS:   BOOLEAN
--
CREATE OR REPLACE FUNCTION are_equal (boolean, boolean)
    RETURNS boolean
    AS $$
BEGIN
    IF (($1 IS NULL AND $2 IS NULL) OR $1 = $2) THEN
        RETURN TRUE;
    ELSE
        RETURN FALSE;
    END IF;
END;
$$
LANGUAGE plpgsql;

CREATE OR REPLACE FUNCTION are_equal (varchar, varchar)
    RETURNS boolean
    AS $$
DECLARE
    v_str varchar;
BEGIN
    IF (($1 IS NULL AND $2 IS NULL) OR $1 = $2) THEN
        RETURN TRUE;
    ELSE
        RETURN FALSE;
    END IF;
END;
$$
LANGUAGE plpgsql;

CREATE OR REPLACE FUNCTION are_equal (text, text)
    RETURNS boolean
    AS $$
DECLARE
    v_str varchar;
BEGIN
    IF (($1 IS NULL AND $2 IS NULL) OR $1 = $2) THEN
        RETURN TRUE;
    ELSE
        RETURN FALSE;
    END IF;
END;
$$
LANGUAGE plpgsql;

CREATE OR REPLACE FUNCTION are_equal (cidr, cidr)
    RETURNS boolean
    AS $$
DECLARE
    v_str varchar;
BEGIN
    IF (($1 IS NULL AND $2 IS NULL) OR $1 = $2) THEN
        RETURN TRUE;
    ELSE
        RETURN FALSE;
    END IF;
END;
$$
LANGUAGE plpgsql;

CREATE OR REPLACE FUNCTION are_equal (integer, integer)
    RETURNS boolean
    AS $$
BEGIN
    IF (($1 IS NULL AND $2 IS NULL) OR $1 = $2) THEN
        RETURN TRUE;
    ELSE
        RETURN FALSE;
    END IF;
END;
$$
LANGUAGE plpgsql;

CREATE OR REPLACE FUNCTION are_equal (bigint, bigint)
    RETURNS boolean
    AS $$
BEGIN
    IF (($1 IS NULL AND $2 IS NULL) OR ((NOT $1 IS NULL AND NOT $2 IS NULL) AND $1 = $2)) THEN
        RETURN TRUE;
    ELSE
        RETURN FALSE;
    END IF;
END;
$$
LANGUAGE plpgsql;

CREATE OR REPLACE FUNCTION are_equal (smallint, smallint)
    RETURNS boolean
    AS $$
BEGIN
    -- RAISE DEBUG 'are_equal_smallint 1, p1=%, p2=%', $1, $1;
    IF (($1 IS NULL AND $2 IS NULL) OR ((NOT $1 IS NULL AND NOT $2 IS NULL) AND $1 = $2)) THEN
        RETURN TRUE;
    ELSE
        RETURN FALSE;
    END IF;
END;
$$
LANGUAGE plpgsql;

----------------------------------------------------
-- FUNCTION:    is_svc_group
-- Zweck:       liefert TRUE, wenn service eine Gruppe ist
-- Parameter1:  svc_id
-- RETURNS:     BOOLEAN
--
CREATE OR REPLACE FUNCTION is_svc_group (bigint)
    RETURNS boolean
    AS $$
DECLARE
    i_svc_id ALIAS FOR $1;
    r_svc RECORD;
    -- zu pruefendes Objekt
BEGIN
    SELECT
        INTO r_svc svc_typ_name
    FROM
        service
    LEFT JOIN stm_svc_typ ON service.svc_typ_id = stm_svc_typ.svc_typ_id
WHERE
    service.svc_id = i_svc_id;
    IF r_svc.svc_typ_name = 'group' THEN
        -- Gruppe
        RETURN TRUE;
    ELSE
        -- keine Gruppe
        RETURN FALSE;
    END IF;
END;
$$
LANGUAGE plpgsql;

----------------------------------------------------
-- FUNCTION:    is_obj_group
-- Zweck:       liefert TRUE, wenn objekt eine Gruppe ist
-- Parameter1:  obj_id
-- RETURNS:     BOOLEAN
--
CREATE OR REPLACE FUNCTION is_obj_group (bigint)
    RETURNS boolean
    AS $$
DECLARE
    i_obj_id ALIAS FOR $1;
    r_obj RECORD;
    -- zu pruefendes Objekt
BEGIN
    SELECT
        INTO r_obj obj_typ_name
    FROM
        object
    LEFT JOIN stm_obj_typ ON object.obj_typ_id = stm_obj_typ.obj_typ_id
WHERE
    object.obj_id = i_obj_id;
    IF r_obj.obj_typ_name = 'group' THEN
        -- Gruppe
        RETURN TRUE;
    ELSE
        -- keine Gruppe
        RETURN FALSE;
    END IF;
END;
$$
LANGUAGE plpgsql;

----------------------------------------------------
-- FUNCTION:    is_user_group
-- Zweck:       liefert TRUE, wenn user eine Gruppe ist
-- Parameter1:  user_id
-- RETURNS:     BOOLEAN
--
CREATE OR REPLACE FUNCTION is_user_group (bigint)
    RETURNS boolean
    AS $$
DECLARE
    i_user_id ALIAS FOR $1;
    r_user RECORD;
    -- zu pruefendes Objekt
BEGIN
    SELECT
        INTO r_user usr_typ_name
    FROM
        usr
    LEFT JOIN stm_usr_typ ON usr.usr_typ_id = stm_usr_typ.usr_typ_id
WHERE
    usr.user_id = i_user_id;
    IF r_user.usr_typ_name = 'group' THEN
        -- Gruppe
        RETURN TRUE;
    ELSE
        -- keine Gruppe
        RETURN FALSE;
    END IF;
END;
$$
LANGUAGE plpgsql;

----------------------------------------------------
-- FUNCTION:  get_admin_id_from_name
-- Zweck:     liefert zu einem admin-namen die zugehoerige uiuser_id zurueck
-- Parameter: name des admins
-- RETURNS:   INTEGER uiuser_id
--
CREATE OR REPLACE FUNCTION get_admin_id_from_name (varchar)
    RETURNS integer
    AS $$
DECLARE
    v_admin_name ALIAS FOR $1;
    r_admin RECORD;
BEGIN
    IF v_admin_name IS NULL OR v_admin_name = '' THEN
        RETURN NULL;
    END IF;
    SELECT
        INTO r_admin *
    FROM
        uiuser
    WHERE
        uiuser_username = v_admin_name;
    IF NOT FOUND THEN
        IF v_admin_name <> 'CheckPoint' AND v_admin_name <> 'Upgrade Process' AND v_admin_name <> 'Check Point SmartCenter Server Update Process' THEN
            PERFORM
                error_handling ('INFO_ADMIN_NOT_FOUND', v_admin_name);
        END IF;
        RETURN NULL;
    END IF;
    RETURN r_admin.uiuser_id;
END;
$$
LANGUAGE plpgsql;

----------------------------------------------------
-- FUNCTION:  error_handling (einmal mit und einmal ohne variablen Anteil)
-- Zweck:     gibt Fehlermeldung aus
-- Parameter: error-string (id), [wert einer variablen]
-- RETURNS:   error string
--
CREATE OR REPLACE FUNCTION error_handling (varchar)
    RETURNS varchar
    AS $$
DECLARE
    errid ALIAS FOR $1;
BEGIN
    RETURN error_handling (errid, '');
END;
$$
LANGUAGE plpgsql;

CREATE OR REPLACE FUNCTION error_handling (varchar, varchar)
    RETURNS varchar
    AS $$
DECLARE
    errid ALIAS FOR $1;
    var_output_string ALIAS FOR $2;
    err RECORD;
    lang RECORD;
    err_txt text;
    err_prefix varchar;
BEGIN
    err_txt := '';
    SELECT
        INTO err *
    FROM
        error
    WHERE
        error_id = errid;
    IF NOT FOUND THEN
        RAISE EXCEPTION 'errorid not found %', errid;
    END IF;
    SELECT
        INTO lang config_value
    FROM
        config
    WHERE
        config_key = 'DefaultLanguage';
    IF NOT FOUND THEN
        RAISE EXCEPTION 'config not found, %', errid;
    END IF;
    IF lang.config_value = 'German' THEN
        err_txt := err.error_txt_ger;
        IF err.error_lvl = 1 THEN
            err_prefix := 'FEHLER: ';
        ELSIF err.error_lvl = 2 THEN
            err_prefix := 'WARNUNG: ';
        ELSIF err.error_lvl = 3 THEN
            err_prefix := 'WARNUNG: ';
        ELSIF err.error_lvl = 4 THEN
            err_prefix := 'INFO: ';
        ELSE
            RAISE EXCEPTION 'Unbekannte Fehlerstufe %', err.error_lvl;
        END IF;
    ELSE
        err_txt := err.error_txt_eng;
        IF err.error_lvl = 1 THEN
            err_prefix := 'ERROR: ';
        ELSIF err.error_lvl = 2 THEN
            err_prefix := 'WARNING: ';
        ELSIF err.error_lvl = 3 THEN
            err_prefix := 'WARNING: ';
        ELSIF err.error_lvl = 4 THEN
            err_prefix := 'INFO: ';
        ELSE
            RAISE EXCEPTION 'Unbekannte Fehlerstufe %', err.error_lvl;
        END IF;
    END IF;
    err_prefix := err_prefix || errid || ': ';
    IF var_output_string <> '' THEN
        err_txt := err_txt || ': ' || var_output_string;
    END IF;
    err_txt := err_prefix || err_txt;
    -- INSERT INTO error_log (error_id, error_txt)
    --     VALUES (errid, err_txt);
    IF err.error_lvl = 1 THEN
        RAISE DEBUG 'sorry, encountered fatal error: %', err_txt;
        RAISE EXCEPTION '%', err_txt;
    ELSIF err.error_lvl = 2 THEN
        RAISE NOTICE '%', err_txt;
    ELSIF err.error_lvl = 3 THEN
        RAISE NOTICE '%', err_txt;
    ELSIF err.error_lvl = 4 THEN
        RAISE DEBUG '%', err_txt;
        --		NULL;
    ELSE
        RAISE EXCEPTION 'unknown errorlevel %', err.error_lvl;
    END IF;
    RETURN err_txt;
END;
$$
LANGUAGE plpgsql;

---------------------------------------------------------------------------------------
-- entfernt alle Whitespaces vom Anfang u. Ende eine Strings
CREATE OR REPLACE FUNCTION remove_spaces (varchar)
    RETURNS varchar
    AS $$
DECLARE
    s ALIAS FOR $1;
    --    res		VARCHAR;
    --    test	VARCHAR;
    --    left	VARCHAR;
    --    right	VARCHAR;
    --    pos		integer;
BEGIN
    --	res := s;
    --	test := substring(s, '^.*?([' || E'\t' || ' ]).*?$');
    --	if test IS NOT NULL AND char_length(test)>0 THEN
    --		left := substring(s, '^(.*?)[ ' || E'\t' || '].*?$');
    --		right := substring(s, '^.*?[ ' || E'\t' || '](.*?)$');
    --		res := left || remove_spaces(right);
    --	END IF;
    RETURN btrim(s);
END;
$$
LANGUAGE plpgsql;

---------------------------------------------------------------------------------------
-- Entfernt Tabs und Leerzeichen am Anfang und Ende des Strings
CREATE OR REPLACE FUNCTION del_surrounding_spaces (varchar)
    RETURNS varchar
    AS $$
DECLARE
    s ALIAS FOR $1;
BEGIN
    --    return substring(s, '[ \t]*(.*?)[ \t]*');
    RETURN substring(s, '[ ' || E'\t' || ']*(.*?)[ ' || E'\t' || ']*');
END;
$$
LANGUAGE plpgsql;

CREATE OR REPLACE FUNCTION get_last_change_admin_of_rulebase_change (BIGINT, INTEGER) RETURNS INTEGER AS
$BODY$
DECLARE
    i_import_id			ALIAS FOR $1;
    i_dev_id			ALIAS FOR $2;
    r_rule				RECORD;
    i_admin_counter		INTEGER;
BEGIN 

	SELECT INTO i_admin_counter COUNT(distinct import_admin) FROM changelog_rule
		WHERE control_id=i_import_id AND dev_id=i_dev_id AND NOT import_admin IS NULL GROUP BY import_admin;
	IF (i_admin_counter=1) THEN
		SELECT INTO r_rule import_admin FROM changelog_rule
			WHERE control_id=i_import_id AND dev_id=i_dev_id AND NOT import_admin IS NULL GROUP BY import_admin;
--		RAISE NOTICE 'Found last_change_admin %', r_rule.import_admin;
		IF FOUND THEN
			RETURN r_rule.import_admin;
		ELSE
			RETURN NULL;
		END IF;
	ELSE
		RETURN NULL;
	END IF;
END; 
$BODY$
  LANGUAGE 'plpgsql' VOLATILE;

----------------------------------------------------
-- FUNCTION:    get_last_change_admin_of_obj_delete(import_id, mgm_id)
-- Zweck:       liefert den change_admin fuer einen Import zurueck (fuer svc- nwobj u. usr_deletes
--              benoetigt fuer obj / svc / usr _deletes
--              Annahme: ein Admin hat alle Changes an einem Management zu einem Zeitpunkt gemacht
--						 wenn nicht, dann wird NULL zurueckgeliefert
-- Parameter1:  import id
-- RETURNS:     id des change_admins
--

-- DROP FUNCTION get_last_change_admin_of_obj_delete (BIGINT);
CREATE OR REPLACE FUNCTION get_last_change_admin_of_obj_delete (BIGINT) RETURNS INTEGER AS
$BODY$
DECLARE
    i_import_id			ALIAS FOR $1;
    r_obj				RECORD;
    i_admin_counter		INTEGER;
    i_admin_id			INTEGER;
BEGIN 
	i_admin_counter := 0;
	FOR r_obj IN
		SELECT import_admin FROM changelog_object WHERE control_id=i_import_id AND NOT import_admin IS NULL
		UNION
		SELECT import_admin FROM changelog_service WHERE control_id=i_import_id AND NOT import_admin IS NULL
		UNION		
		SELECT import_admin FROM changelog_user WHERE control_id=i_import_id AND NOT import_admin IS NULL
	LOOP
		i_admin_counter := i_admin_counter + 1;
		i_admin_id := r_obj.import_admin;
	END LOOP;
	IF (i_admin_counter=1) THEN
		RETURN i_admin_id;
	ELSE
		RETURN NULL;
	END IF;
END; 
$BODY$
LANGUAGE 'plpgsql' VOLATILE;

----------------------------------------------------
-- FUNCTION:	get_previous_import_id(devid, zeitpunkt)
-- Zweck:		liefert zu einem Device + Zeitpunkt die Import ID des vorherigen Imports
-- Parameter1:	Device_id (INTEGER)
-- Parameter2:	Time (timestamp)
-- RETURNS:		ID des vorherigen Imports
--
CREATE OR REPLACE FUNCTION get_previous_import_id(INTEGER,TIMESTAMP) RETURNS BIGINT AS $$
DECLARE
	i_device_id ALIAS FOR $1;
	t_report_time_in ALIAS FOR $2;
	t_report_time TIMESTAMP;
	i_mgm_id INTEGER;
	i_prev_import_id BIGINT;
BEGIN
	IF t_report_time_in IS NULL THEN
		t_report_time := now();
	ELSE
		t_report_time := t_report_time_in;
	END IF;
	SELECT INTO i_mgm_id mgm_id FROM device WHERE dev_id=i_device_id;
	SELECT INTO i_prev_import_id max(control_id) FROM import_control WHERE mgm_id=i_mgm_id AND
		start_time<=t_report_time AND NOT stop_time IS NULL AND successful_import;
	IF NOT FOUND THEN
		RETURN NULL;
	ELSE
--		RAISE NOTICE 'found get_previous_import_id: %', i_prev_import_id;
	    RETURN i_prev_import_id;
	END IF;
END;
$$ LANGUAGE plpgsql;
