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
-- FUNCTION:  get_dev_typ_id
-- Zweck:     liefert die dev_typ_id zu einem device-name zurueck
-- Parameter: device-name VARCHAR
-- RETURNS:   INTEGER dev_typ_id des uebergebenen devices
--
CREATE OR REPLACE FUNCTION get_dev_typ_id (varchar)
    RETURNS integer
    AS $$
DECLARE
    devicename ALIAS FOR $1;
    dev RECORD;
BEGIN
    SELECT
        INTO dev dev_typ_id
    FROM
        device
    WHERE
        dev_name = devicename;
    IF NOT FOUND THEN
        -- TODO: Fehlerbehandlung
        PERFORM
            error_handling ('ERR_DEV_NOT_FOUND', devicename);
    END IF;
    RETURN dev.dev_typ_id;
END;
$$
LANGUAGE plpgsql;

----------------------------------------------------
-- FUNCTION:  error_handling (einmal mit und einmal ohne variablen Anteil)
-- Zweck:     gibt Fehlermeldung aus und traegt Fehler in error_log_Tabelle ein
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
    INSERT INTO error_log (error_id, error_txt)
        VALUES (errid, err_txt);
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

---------------------------------------------------------------------------------------
-- instr functions that mimic Oracle's counterpart
-- Syntax: instr(string1, string2, [n], [m]) where [] denotes optional parameters.
--
-- Searches string1 beginning at the nth character for the mth occurrence
-- of string2.  If n is negative, search backwards.  If m is not passed,
-- assume 1 (search starts at first character).
--
CREATE OR REPLACE FUNCTION instr (varchar, varchar)
    RETURNS integer
    AS $$
DECLARE
    pos integer;
BEGIN
    pos := instr ($1, $2, 1);
    RETURN pos;
END;
$$
LANGUAGE plpgsql;

CREATE OR REPLACE FUNCTION instr (varchar, varchar, integer)
    RETURNS integer
    AS $$
DECLARE
    string ALIAS FOR $1;
    string_to_search ALIAS FOR $2;
    beg_index ALIAS FOR $3;
    pos integer NOT NULL DEFAULT 0;
    temp_str varchar;
    beg integer;
    length integer;
    ss_length integer;
BEGIN
    IF beg_index > 0 THEN
        temp_str := substring(string FROM beg_index);
        pos := position(string_to_search IN temp_str);
        IF pos = 0 THEN
            RETURN 0;
        ELSE
            RETURN pos + beg_index - 1;
        END IF;
    ELSE
        ss_length := char_length(string_to_search);
        length := char_length(string);
        beg := length + beg_index - ss_length + 2;
        WHILE beg > 0 LOOP
            temp_str := substring(string FROM beg FOR ss_length);
            pos := position(string_to_search IN temp_str);
            IF pos > 0 THEN
                RETURN beg;
            END IF;
            beg := beg - 1;
        END LOOP;
        RETURN 0;
    END IF;
END;
$$
LANGUAGE plpgsql;

CREATE OR REPLACE FUNCTION instr (varchar, varchar, integer, integer)
    RETURNS integer
    AS $$
DECLARE
    string ALIAS FOR $1;
    string_to_search ALIAS FOR $2;
    beg_index ALIAS FOR $3;
    occur_index ALIAS FOR $4;
    pos integer NOT NULL DEFAULT 0;
    occur_number integer NOT NULL DEFAULT 0;
    temp_str varchar;
    beg integer;
    i integer;
    length integer;
    ss_length integer;
BEGIN
    IF beg_index > 0 THEN
        beg := beg_index;
        temp_str := substring(string FROM beg_index);
        FOR i IN 1..occur_index LOOP
            pos := position(string_to_search IN temp_str);
            IF i = 1 THEN
                beg := beg + pos - 1;
            ELSE
                beg := beg + pos;
            END IF;
            temp_str := substring(string FROM beg + 1);
        END LOOP;
        IF pos = 0 THEN
            RETURN 0;
        ELSE
            RETURN beg;
        END IF;
    ELSE
        ss_length := char_length(string_to_search);
        length := char_length(string);
        beg := length + beg_index - ss_length + 2;
        WHILE beg > 0 LOOP
            temp_str := substring(string FROM beg FOR ss_length);
            pos := position(string_to_search IN temp_str);
            IF pos > 0 THEN
                occur_number := occur_number + 1;
                IF occur_number = occur_index THEN
                    RETURN beg;
                END IF;
            END IF;
            beg := beg - 1;
        END LOOP;
        RETURN 0;
    END IF;
END;
$$
LANGUAGE plpgsql;


-- CREATE OR REPLACE FUNCTION add_data_issue(varchar,int,timestamp,BIGINT,varchar,varchar,varchar,bigint,int,int,varchar,varchar,varchar) RETURNS VOID AS $$
-- DECLARE
--     v_source ALIAS FOR $1;
--     i_severity ALIAS FOR $2;
--     t_timestamp ALIAS FOR $3;
-- 	i_current_import_id ALIAS FOR $4;
-- 	v_obj_name ALIAS FOR $5;
-- 	v_obj_uid ALIAS FOR $6;
-- 	v_rule_uid ALIAS FOR $7;
--     i_rule_id  ALIAS FOR $8;
--     i_mgm_id ALIAS FOR $9;
--     i_dev_id   ALIAS FOR $10;
-- 	v_obj_type ALIAS FOR $11;
-- 	v_suspected_cause ALIAS FOR $12;
-- 	v_description ALIAS FOR $13;
--     v_log_string VARCHAR;
-- BEGIN
-- 	INSERT INTO log_data_issue (
--         source, severity, issue_timestamp, import_id, object_name, object_uid, rule_uid, 
--         rule_id, issue_mgm_id, issue_dev_id, object_type, suspected_cause, description ) 
-- 	VALUES ( 
--         v_source, i_severity, t_timestamp, i_current_import_id, v_obj_name, v_obj_uid, v_rule_uid,
--         i_rule_id, i_mgm_id, i_dev_id, v_obj_type, v_suspected_cause, v_description);
-- 	RETURN;
--     v_log_string := 'src=' || v_source || ', sev=' || v_severity;
--     IF t_timestamp IS NOT NULL  THEN
--         v_log_string := v_log_string || ', time=' || t_timestamp; 
--     END IF;
--     IF i_current_import_id IS NOT NULL  THEN
--         v_log_string := v_log_string || ', import_id=' || CAST(i_current_import_id AS VARCHAR); 
--     END IF;
--     IF v_obj_name IS NOT NULL  THEN
--         v_log_string := v_log_string || ', object_name=' || v_obj_name; 
--     END IF;
--     -- todo: add more issue information
--     RAISE INFO '%', v_log_string; -- send the log to syslog as well
-- END;
-- $$ LANGUAGE plpgsql;
