
ALTER TABLE "ldap_connection" ADD column IF NOT EXISTS "active" Boolean NOT NULL Default TRUE;

ALTER TABLE "report" ADD column IF NOT EXISTS "report_type" Integer;
ALTER TABLE "report" ADD column IF NOT EXISTS "description" varchar;

ALTER TABLE "report_schedule" ADD column IF NOT EXISTS "report_schedule_counter" Integer Not NULL Default 0;

ALTER TABLE import_config ADD COLUMN IF NOT EXISTS "debug_mode" Boolean Default FALSE;


CREATE OR REPLACE FUNCTION import_config_from_json ()
    RETURNS TRIGGER
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
$BODY$
LANGUAGE plpgsql
VOLATILE
COST 100;


CREATE OR REPLACE FUNCTION debug_show_time (VARCHAR, TIMESTAMP)
    RETURNS TIMESTAMP
    AS $BODY$
DECLARE
	v_event ALIAS FOR $1; -- description of the processed time
	t_import_start ALIAS FOR $2; -- start time of the import
BEGIN

    RAISE NOTICE '% duration: %s', v_event, now()- t_import_start;
--    RAISE NOTICE '% duration: %s', v_event, CAST((now()- t_import_start) AS VARCHAR);
--    RAISE NOTICE 'duration of last step: %s', CAST(now()- t_import_start AS VARCHAR);
    RETURN now();
END;
$BODY$
LANGUAGE plpgsql
VOLATILE
COST 100;

DROP FUNCTION IF EXISTS public.import_all_main(BIGINT);
DROP FUNCTION IF EXISTS public.import_all_main(BIGINT, BOOLEAN);
CREATE OR REPLACE FUNCTION public.import_all_main(BIGINT, BOOLEAN)
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
				v_err_pos := 'import_rules of device ' || r_dev.dev_name || ' (Management: ' || CAST (i_mgm_id AS VARCHAR) || ')';
				IF (import_rules(r_dev.dev_id, i_current_import_id)) THEN  				-- returns true if rule order needs to be changed
																						-- currently always returns true as each import needs a rule reordering
					v_err_pos := 'import_rules_set_rule_num_numeric of device ' || r_dev.dev_name || ' (Management: ' || CAST (i_mgm_id AS VARCHAR) || ')';
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
$BODY$
  LANGUAGE plpgsql VOLATILE
  COST 100;
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

-- Alter table "rule_review" DROP foreign key ("rule_metadata_id"); -- references "rule_metadata" ("rule_metadata_id") on update restrict on delete cascade;
-- Alter table "rule_review" add  foreign key ("tenant_id") references "tenant" ("tenant_id") on update restrict on delete cascade;
-- DROP index IF EXISTS "IX_relationship32"; -- on "rule_review" ("tenant_id");
-- DROP index IF EXISTS "rule_review_rule_metadata_id"; -- on "rule_review" ("rule_metadata_id");
DROP TABLE IF EXISTS rule_review; 

-- Alter table "object" DELETE  foreign key ("nattyp_id") references "stm_nattyp" ("nattyp_id") on update restrict on delete cascade;
-- DROP index "stm_nattypes_akey"; -- on "stm_nattyp" using btree ("nattyp_name");
DROP table IF EXISTS "stm_nattyp";

DROP table IF EXISTS "tenant_user";

DROP table IF EXISTS "tenant_username";
