
ALTER TABLE "ldap_connection" ADD column IF NOT EXISTS "active" Boolean NOT NULL Default TRUE;

ALTER TABLE "report" ADD column IF NOT EXISTS "report_type" Integer;
ALTER TABLE "report" ADD column IF NOT EXISTS "description" varchar;

ALTER TABLE "report_schedule" ADD column IF NOT EXISTS "report_schedule_counter" Integer Not NULL Default 0;

ALTER TABLE import_config ADD COLUMN IF NOT EXISTS "debug_mode" Boolean Default FALSE;


CREATE OR REPLACE FUNCTION import_config_from_json ()
    RETURNS TRIGGER
    LANGUAGE plpgsql
    AS $BODY$
DECLARE
    import_id BIGINT;
    r_import_result RECORD;
BEGIN
    INSERT INTO import_object
    SELECT
        *
    FROM
        jsonb_populate_recordset(NULL::import_object, NEW.config -> 'network_objects');

    INSERT INTO import_service
    SELECT
        *
    FROM
        jsonb_populate_recordset(NULL::import_service, NEW.config -> 'service_objects');

    INSERT INTO import_user
    SELECT
        *
    FROM
        jsonb_populate_recordset(NULL::import_user, NEW.config -> 'user_objects');

    INSERT INTO import_zone
    SELECT
        *
    FROM
        jsonb_populate_recordset(NULL::import_zone, NEW.config -> 'zone_objects');

    INSERT INTO import_rule
    SELECT
        *
    FROM
        jsonb_populate_recordset(NULL::import_rule, NEW.config -> 'rules');

    IF NEW.start_import_flag THEN
        -- finally start the stored procedure import
        PERFORM import_all_main(NEW.import_id, NEW.debug_mode);        
    END IF;
    RETURN NEW;
END;
$BODY$;


CREATE OR REPLACE FUNCTION debug_show_time (VARCHAR, TIMESTAMP)
    RETURNS TIMESTAMP
    LANGUAGE plpgsql
    AS $BODY$
DECLARE
	v_event ALIAS FOR $1; -- description of the processed time
	t_import_start ALIAS FOR $2; -- start time of the import
BEGIN

    RAISE NOTICE '% duration: %s', v_event, now()- t_import_start;
    RETURN now();
END;
$BODY$;

DROP FUNCTION IF EXISTS public.import_all_main(BIGINT);
DROP FUNCTION IF EXISTS public.import_all_main(BIGINT, BOOLEAN);
CREATE OR REPLACE FUNCTION public.import_all_main(BIGINT, BOOLEAN)
    LANGUAGE plpgsql  
    RETURNS VARCHAR AS
$BODY$
DECLARE
	i_current_import_id ALIAS FOR $1; -- ID of the current import
	b_debug_mode ALIAS FOR $2; -- should we output debug info?
	i_mgm_id INTEGER;
	r_dev RECORD;
	b_force_initial_import BOOLEAN;
	b_is_initial_import BOOLEAN;
	b_do_not_import BOOLEAN;
	v_err_pos VARCHAR;
	v_err_str VARCHAR;
	v_err_str_refs VARCHAR;
	b_result BOOLEAN;
	r_obj RECORD;
	v_exception_message VARCHAR;
	v_exception_details VARCHAR;
	v_exception_hint VARCHAR;
	v_exception VARCHAR;
	t_import_start TIMESTAMP;
	t_last_measured_timestamp TIMESTAMP;	
BEGIN
	BEGIN -- catch exception block
		t_import_start := now();
		v_err_pos := 'start';
		SELECT INTO i_mgm_id mgm_id FROM import_control WHERE control_id=i_current_import_id;
		SELECT INTO b_is_initial_import is_initial_import FROM import_control WHERE control_id=i_current_import_id;
		IF NOT b_is_initial_import THEN -- pruefen, ob force_flag des Mangements gesetzt ist
			SELECT INTO b_force_initial_import force_initial_import FROM management WHERE mgm_id=i_mgm_id;
			IF b_force_initial_import THEN b_is_initial_import := TRUE; END IF;
		END IF;
	
		-- import base objects
		v_err_pos := 'import_zone_main';
		PERFORM import_zone_main	(i_current_import_id, b_is_initial_import);
		v_err_pos := 'import_nwobj_main';
		PERFORM import_nwobj_main	(i_current_import_id, b_is_initial_import);	
		v_err_pos := 'import_svc_main';
		PERFORM import_svc_main		(i_current_import_id, b_is_initial_import);
		v_err_pos := 'import_usr_main';
		PERFORM import_usr_main		(i_current_import_id, b_is_initial_import);
		RAISE  DEBUG 'after usr_import';
		v_err_pos := 'rulebase_import_start';

		t_last_measured_timestamp := debug_show_time('import of base objects', t_import_start);	
		-- import rulebases
		FOR r_dev IN
			SELECT * FROM device WHERE mgm_id=i_mgm_id AND NOT do_not_import
		LOOP
			SELECT INTO b_do_not_import do_not_import FROM device WHERE dev_id=r_dev.dev_id;
			IF NOT b_do_not_import THEN		--	RAISE NOTICE 'importing %', r_dev.dev_name;
				v_err_pos := 'import_rules of device ' || r_dev.dev_name || ' (Management: ' || CAST (i_mgm_id AS VARCHAR) || ') ';
				IF (import_rules(r_dev.dev_id, i_current_import_id)) THEN  				-- returns true if rule order needs to be changed
																						-- currently always returns true as each import needs a rule reordering
					v_err_pos := 'import_rules_set_rule_num_numeric of device ' || r_dev.dev_name || ' (Management: ' || CAST (i_mgm_id AS VARCHAR) || ') ';
					-- in case of any changes - adjust rule_num values in rulebase
					PERFORM import_rules_set_rule_num_numeric (i_current_import_id,r_dev.dev_id);
				END IF;
			END IF;
		END LOOP;

		t_last_measured_timestamp := debug_show_time('import of rules', t_last_measured_timestamp);	

		v_err_pos := 'ImpGlobRef';
		SELECT INTO b_result * FROM import_global_refhandler_main(i_current_import_id);
		IF NOT b_result THEN --  alle Referenzen aendern (basiert nur auf Eintraegen in changelog_xxx	
			SELECT INTO v_err_str_refs import_errors FROM import_control WHERE control_id=i_current_import_id;
			RAISE NOTICE 'notice. error in import_global_refhandler_main';
			RAISE EXCEPTION 'error in import_global_refhandler_main';
		ELSE  -- no error so far
			v_err_pos := 'get_active_rules_with_broken_refs_per_mgm';
			SELECT INTO v_err_str_refs * FROM get_active_rules_with_broken_refs_per_mgm ('|', FALSE, i_mgm_id);
			IF NOT are_equal(v_err_str_refs, '') THEN
				RAISE EXCEPTION 'error in get_active_rules_with_broken_refs_per_mgm: %', v_err_str_refs;
--				RAISE NOTICE 'found broken references in get_active_rules_with_broken_refs_per_mgm: %', v_err_str_refs;
			END IF;
		END IF;
		IF b_force_initial_import THEN UPDATE management SET force_initial_import=FALSE WHERE mgm_id=i_mgm_id; END IF; 	-- evtl. gesetztes management.force_initial_import-Flag loeschen	
		v_err_pos := 'import_changelog_sync';
		PERFORM import_changelog_sync (i_current_import_id, i_mgm_id); -- Abgleich zwischen import_changelog und changelog_xxx	
	EXCEPTION
		WHEN OTHERS THEN -- read error from import_control and rollback
			GET STACKED DIAGNOSTICS v_exception_message = MESSAGE_TEXT,
                          v_exception_details = PG_EXCEPTION_DETAIL,
                          v_exception_hint = PG_EXCEPTION_HINT;
			v_exception := v_exception_message || v_exception_details || v_exception_hint;
			v_err_pos := 'ERR-ImpMain@' || v_err_pos;
			RAISE DEBUG 'import_all_main - Exception block entered with v_err_pos=%', v_err_pos;
			SELECT INTO v_err_str import_errors FROM import_control WHERE control_id=i_current_import_id;
			IF v_err_str IS NULL THEN
				UPDATE import_control SET import_errors = v_err_pos || v_exception WHERE control_id=i_current_import_id;
			ELSE 
				UPDATE import_control SET import_errors = v_err_str || v_err_pos || v_exception WHERE control_id=i_current_import_id;				
			END IF;
			IF NOT v_err_str_refs IS NULL THEN
				SELECT INTO v_err_str import_errors FROM import_control WHERE control_id=i_current_import_id;
				UPDATE import_control SET import_errors = v_err_str || ';' || v_err_str_refs WHERE control_id=i_current_import_id;
			END IF;
			RAISE NOTICE 'ERROR: import_all_main failed';
			RETURN v_err_str;
			-- RETURN FALSE;
	END;
	RETURN '';
END;
$BODY$;
ALTER FUNCTION public.import_all_main(BIGINT, BOOLEAN) OWNER TO fworch;

DO $$
BEGIN
IF EXISTS(SELECT *
    FROM information_schema.columns
    WHERE table_name='management' and column_name='ssh_private_key')
THEN
    ALTER TABLE "management" RENAME COLUMN "ssh_private_key" TO "secret";
END IF;
END $$;

--------- remove unused tables --------------- 

CREATE OR REPLACE FUNCTION get_request_str(VARCHAR,BIGINT) RETURNS VARCHAR AS $$
DECLARE
	v_table	ALIAS FOR $1;
	i_id	ALIAS FOR $2;
	v_result VARCHAR;
BEGIN
	v_result := '';
	RETURN 'v_result';
END;
$$ LANGUAGE plpgsql;

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


DROP TABLE IF EXISTS rule_review; 

Alter table "object" drop constraint if exists "object_nattyp_id_fkey"; 
DROP table IF EXISTS "stm_nattyp";

DROP table IF EXISTS "tenant_user";

DROP table IF EXISTS "tenant_username";

DO $$
BEGIN
IF EXISTS(SELECT *
    FROM information_schema.columns
    WHERE table_name='request_object_change')
THEN
	Alter table "request_object_change" drop constraint if exists "log_obj_id_changelog_object_log_obj_id";
	Alter table "request_object_change" drop constraint if exists "request_object_change_request_id_fkey";
	DROP table request_object_change;
END IF;
END $$;

DO $$
BEGIN
IF EXISTS(SELECT *
    FROM information_schema.columns
    WHERE table_name='request_rule_change')
THEN
	Alter table "request_rule_change" drop constraint if exists "log_rule_id_changelog_rule_log_rule_id";
	Alter table "request_rule_change" drop constraint if exists "request_rule_change_request_id_fkey";
	DROP table request_rule_change;
END IF;
END $$;

DO $$
BEGIN
IF EXISTS(SELECT *
    FROM information_schema.columns
    WHERE table_name='request_service_change')
THEN
	Alter table "request_service_change" drop constraint if exists "log_svc_id_changelog_service_log_svc_id";
	Alter table "request_service_change" drop constraint if exists "request_service_change_request_id_fkey";
	DROP table request_service_change;
END IF;
END $$;

DO $$
BEGIN
IF EXISTS(SELECT *
    FROM information_schema.columns
    WHERE table_name='request_user_change')
THEN
	Alter table "request_user_change" drop constraint if exists "log_usr_id_changelog_user_log_usr_id";
	Alter table "request_user_change" drop constraint if exists "request_user_change_request_id_fkey";
	DROP table request_user_change;
END IF;
END $$;


DO $$
BEGIN
IF EXISTS(SELECT *
    FROM information_schema.columns
    WHERE table_name='request')
THEN
	Alter table "request" drop constraint if exists "request_type_id_request_type_id";
	Alter table "request" drop constraint if exists "tenant_id_tenant_tenant_id";
	DROP table request;
END IF;
END $$;

DROP table IF EXISTS request_type;

DROP table if exists "tenant_object";

DROP table if exists "report_template_viewable_by_tenant";

drop table if exists "error_log";

-- index optimization
Create index IF NOT EXISTS idx_import_rule01 on import_rule (rule_id);
Create index IF NOT EXISTS idx_zone01 on zone (zone_name,mgm_id);
Create index IF NOT EXISTS idx_rule01 on rule (rule_uid,mgm_id,dev_id,active,nat_rule,xlate_rule);
drop index if exists "firewall_akey";
drop index if exists "kunden_akey";
drop index if exists "management_akey";
drop index if exists "stm_color_akey";
drop index if exists "stm_fw_typ_a2key"; 
drop index if exists "stm_fw_typ_akey";
drop index if exists "stm_obj_typ_akey";
drop index if exists "IX_relationship4";
drop index if exists "IX_relationship6";
drop index if exists "IX_Relationship93";
drop index if exists "IX_relationship11";
drop index if exists "IX_relationship7";
drop index if exists "IX_Relationship165";
drop index if exists "IX_Relationship188";
drop index if exists "IX_relationship10";
drop index if exists "IX_Relationship52";
drop index if exists "IX_Relationship63";
drop index if exists "IX_Relationship69";
drop index if exists "IX_Relationship70";
drop index if exists "IX_Relationship71";
drop index if exists "IX_Relationship109";
drop index if exists "IX_Relationship110";
drop index if exists "IX_Relationship111";
drop index if exists "IX_Relationship112";
drop index if exists "IX_Relationship159";
drop index if exists "IX_Relationship161";
drop index if exists "IX_Relationship162";
drop index if exists "IX_Relationship163";
drop index if exists IX_relationship19;
drop index if exists IX_relationship13;
drop index if exists IX_Relationship118;
drop index if exists IX_Relationship155;
Create index IF NOT EXISTS idx_changelog_object01 on changelog_object (change_type_id);
drop index if exists IX_Relationship130;
Create index IF NOT EXISTS idx_changelog_object02 on changelog_object (mgm_id);
drop index if exists IX_Relationship158;
Create index IF NOT EXISTS idx_changelog_rule01 on changelog_rule (change_type_id);
drop index if exists IX_Relationship127;
Create index IF NOT EXISTS idx_changelog_rule02 on changelog_rule (mgm_id);
drop index if exists IX_Relationship128;
Create index IF NOT EXISTS idx_changelog_rule03 on changelog_rule (dev_id);
drop index if exists IX_Relationship156;
Create index IF NOT EXISTS idx_changelog_service01 on changelog_service (change_type_id);
drop index if exists IX_Relationship131;
Create index IF NOT EXISTS idx_changelog_service02 on changelog_service (mgm_id);
drop index if exists IX_Relationship157;
Create index IF NOT EXISTS idx_changelog_user01 on changelog_user (change_type_id);
drop index if exists IX_Relationship129;
Create index IF NOT EXISTS idx_changelog_user02 on changelog_user (mgm_id);
drop index if exists IX_relationship5;
Create index IF NOT EXISTS idx_device01 on device (mgm_id);
DROP index if exists IX_relationship21;
Create index IF NOT EXISTS idx_import_rule01 on import_rule (rule_id);
DROP index if exists IX_relationship8;
Create index IF NOT EXISTS idx_object01 on object (mgm_id);
Create index IF NOT EXISTS idx_rule01 on rule (rule_uid,mgm_id,dev_id,active,nat_rule,xlate_rule);
DROP index if exists rule_index;
Create index IF NOT EXISTS idx_rule02 on rule (mgm_id,rule_id,rule_uid,dev_id);
DROP index if exists IX_Relationship186;
Create index IF NOT EXISTS idx_rule03 on rule (dev_id);
DROP index if exists IX_relationship25;
Create index IF NOT EXISTS idx_rule_from01 on rule_from (rule_id);
DROP index if exists IX_relationship29;
Create index IF NOT EXISTS idx_rule_service01 on rule_service (rule_id);
DROP index if exists IX_relationship30;
Create index IF NOT EXISTS idx_rule_service02 on rule_service (svc_id);
DROP index if exists IX_relationship27;
Create index IF NOT EXISTS idx_rule_to01 on rule_to (rule_id);
DROP index if exists IX_relationship17;
Create index IF NOT EXISTS idx_service01 on service (mgm_id);
DROP index if exists IX_Relationship43;
Create index IF NOT EXISTS idx_usr01 on usr (mgm_id);
Create index IF NOT EXISTS idx_zone01 on zone (zone_name,mgm_id);
DROP index if exists IX_Relationship38;
Create index IF NOT EXISTS idx_zone02 on zone (mgm_id); -- needed as mgm_id is not first column on above composite index
DROP index if exists IX_Relationship185;
DROP index if exists IX_Relationship149;
DROP index if exists IX_relationship12;
DROP index if exists IX_relationship18;
DROP index if exists IX_Relationship83;

DROP index if exists import_control_only_one_null_stop_time_per_mgm_when_null;
CREATE UNIQUE INDEX IF NOT EXISTS uidx_import_control_only_one_null_stop_time_per_mgm_when_null ON import_control (mgm_id) WHERE stop_time IS NULL;
