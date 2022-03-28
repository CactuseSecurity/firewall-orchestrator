----------------------------------------------------
-- FUNCTION:  import_rules (device_id)
-- Purpose:   adds all changed rules of the current import to the rule table
-- Purpose:   using function insert_single_rule
-- Parameter: device_id
-- Parameter2: import_id
-- RETURNS:   has anything been changed?
--
CREATE OR REPLACE FUNCTION import_rules (INTEGER,BIGINT) RETURNS BOOLEAN AS $$
DECLARE
    i_dev_id  ALIAS FOR $1; -- device id
    i_current_import_id ALIAS FOR $2;
    i_mgm_id  INTEGER; -- for fetching the mgm_ID of the device
    r_rule RECORD; -- record with single rule_id from the import_rule table
    v_rulebase_name VARCHAR; -- namve of the rulebase of the current device
    b_is_initial_import BOOLEAN;
    v_rule_head_text VARCHAR;
    b_rule_order_to_be_written BOOLEAN;
    i_change_admin INTEGER;
	v_uid VARCHAR;
	i_added_rule_id BIGINT;
	i_xlate_rule_id BIGINT;
BEGIN
	b_rule_order_to_be_written := FALSE; 
	SELECT INTO i_mgm_id mgm_id FROM import_control WHERE control_id=i_current_import_id;
	SELECT INTO v_rulebase_name local_rulebase_name FROM device WHERE dev_id=i_dev_id;
	SELECT INTO r_rule rule_id FROM rule WHERE dev_id=i_dev_id LIMIT 1;
	IF FOUND THEN
		b_is_initial_import := FALSE;
		SELECT INTO r_rule force_initial_import FROM management WHERE mgm_id=i_mgm_id;
		IF r_rule.force_initial_import THEN
			b_is_initial_import := TRUE;
			b_rule_order_to_be_written := TRUE; 
		END IF;
		SELECT INTO r_rule force_initial_import FROM device WHERE dev_id=i_dev_id;
		IF r_rule.force_initial_import THEN
			b_is_initial_import := TRUE;
			UPDATE device SET force_initial_import=FALSE WHERE dev_id=i_dev_id;	-- reset force_initial_import flag
		END IF;
	ELSE
		b_is_initial_import := TRUE;
		b_rule_order_to_be_written := TRUE; 
	END IF;
	RAISE DEBUG 'import_rules - importing rulebase: %', v_rulebase_name;

	b_rule_order_to_be_written := import_rules_xlate(i_mgm_id, i_dev_id, i_current_import_id, v_rulebase_name, b_is_initial_import, b_rule_order_to_be_written) OR b_rule_order_to_be_written;
	b_rule_order_to_be_written := import_rules_access(i_mgm_id, i_dev_id, i_current_import_id, v_rulebase_name, b_is_initial_import, b_rule_order_to_be_written) OR b_rule_order_to_be_written;
	b_rule_order_to_be_written := import_rules_combined(i_mgm_id, i_dev_id, i_current_import_id, v_rulebase_name, b_is_initial_import, b_rule_order_to_be_written, v_uid) OR b_rule_order_to_be_written;

	RAISE DEBUG 'import_rules - after insert loop';
	IF NOT b_is_initial_import THEN	-- set active=false for the old version of rules that have been changed
		i_change_admin := get_last_change_admin_of_rulebase_change(i_current_import_id,i_dev_id);
		FOR r_rule IN -- every deleted Regel is inserted in changelog_rule
			SELECT rule_id, rule_name, (rule_head_text is NULL) as is_security_relevant FROM rule 
				WHERE active AND dev_id=i_dev_id AND mgm_id=i_mgm_id AND rule_last_seen<i_current_import_id
		LOOP
			RAISE DEBUG 'import_rules - changelog delete %', r_rule.rule_id; 
			INSERT INTO changelog_rule
				(control_id,new_rule_id,old_rule_id,change_action,import_admin,documented,mgm_id,dev_id,security_relevant)
				VALUES (i_current_import_id,NULL,r_rule.rule_id,'D',i_change_admin,FALSE,i_mgm_id,i_dev_id,r_rule.is_security_relevant);
			PERFORM error_handling('INFO_RULE_DELETED', r_rule.rule_name);
		END LOOP;
		RAISE DEBUG 'import_rules - after delete loop';
		UPDATE rule SET active=FALSE WHERE rule_id IN
			( SELECT rule.rule_id FROM rule
			  WHERE active AND dev_id=i_dev_id AND mgm_id=i_mgm_id AND rule_last_seen<i_current_import_id );
		RAISE DEBUG 'import_rules - after active=false update';
	END IF;
	RETURN TRUE;
END; 
$$ LANGUAGE plpgsql;


CREATE OR REPLACE FUNCTION import_rules_xlate (INT,INT,BIGINT,VARCHAR,BOOLEAN,BOOLEAN) RETURNS BOOLEAN AS $$
DECLARE
	i_mgm_id ALIAS FOR $1;
	i_dev_id ALIAS FOR $2;
    i_current_import_id ALIAS FOR $3;
    v_rulebase_name ALIAS FOR $4; -- name of the rulebase of the current device
    b_is_initial_import ALIAS FOR $5;
    b_rule_order_to_be_written ALIAS FOR $6;
    r_rule RECORD; -- record with single rule_id from the import_rule table
	i_added_rule_id BIGINT;
BEGIN
	-- first handle xlate rules defining translation (rule_type = 'xlate')
	-- we need these first to be able to set reference in original packet matching rule later

	FOR r_rule IN -- check each rule if it was changed add add accordingly
		SELECT rule_id FROM import_rule WHERE control_id = i_current_import_id AND rulebase_name = v_rulebase_name AND rule_type = 'xlate'
		LOOP
			i_added_rule_id := insert_single_rule(r_rule.rule_id,i_dev_id,i_mgm_id,i_current_import_id,b_is_initial_import);
			IF NOT i_added_rule_id IS NULL THEN
				b_rule_order_to_be_written := TRUE;
			END IF;
	END LOOP;
	RETURN b_rule_order_to_be_written;
END; 
$$ LANGUAGE plpgsql;


CREATE OR REPLACE FUNCTION import_rules_access (INT,INT,BIGINT,VARCHAR,BOOLEAN,BOOLEAN) RETURNS BOOLEAN AS $$
DECLARE
	i_mgm_id ALIAS FOR $1;
	i_dev_id ALIAS FOR $2;
    i_current_import_id ALIAS FOR $3;
    v_rulebase_name ALIAS FOR $4; -- name of the rulebase of the current device
    b_is_initial_import ALIAS FOR $5;
    b_rule_order_to_be_written ALIAS FOR $6;
    r_rule RECORD; -- record with single rule_id from the import_rule table
	i_added_rule_id BIGINT;
BEGIN
	-- handle pure access rules:
	FOR r_rule IN -- check each rule if it was changed add add accordingly
		SELECT rule_id FROM import_rule WHERE control_id = i_current_import_id AND rulebase_name = v_rulebase_name AND (rule_type = 'access' OR rule_type IS NULL)
	LOOP
		i_added_rule_id := insert_single_rule(r_rule.rule_id,i_dev_id,i_mgm_id,i_current_import_id,b_is_initial_import);
		IF NOT i_added_rule_id IS NULL THEN
			b_rule_order_to_be_written := TRUE;
		END IF;
	END LOOP;
	RETURN b_rule_order_to_be_written;
END; 
$$ LANGUAGE plpgsql;


CREATE OR REPLACE FUNCTION import_rules_combined (INT,INT,BIGINT,VARCHAR,BOOLEAN,BOOLEAN,VARCHAR) RETURNS BOOLEAN AS $$
DECLARE
	i_mgm_id ALIAS FOR $1;
	i_dev_id ALIAS FOR $2;
    i_current_import_id ALIAS FOR $3;
    v_rulebase_name ALIAS FOR $4; -- name of the rulebase of the current device
    b_is_initial_import ALIAS FOR $5;
    b_rule_order_to_be_written ALIAS FOR $6;
	v_uid ALIAS FOR $7;
	i_xlate_rule_id BIGINT;
    r_rule RECORD; -- record with single rule_id from the import_rule table
	i_added_rule_id BIGINT;
BEGIN
	-- handle combined and original rules:
	FOR r_rule IN -- check each rule if it was changed add add accordingly
		SELECT rule_id FROM import_rule WHERE control_id = i_current_import_id AND rulebase_name = v_rulebase_name AND (rule_type = 'combined' OR rule_type = 'original')
		LOOP
			i_added_rule_id := insert_single_rule(r_rule.rule_id,i_dev_id,i_mgm_id,i_current_import_id,b_is_initial_import);
			IF NOT i_added_rule_id IS NULL THEN
				b_rule_order_to_be_written := TRUE;
				-- here we need to set references to the xlate type rule
				SELECT INTO v_uid rule_uid FROM rule WHERE rule_id=i_added_rule_id;
				SELECT INTO i_xlate_rule_id rule_id FROM rule WHERE rule_uid = v_uid AND NOT rule_id=i_added_rule_id and active;
				IF NOT FOUND THEN
					RAISE NOTICE 'import_rules - issue: did not find corresponding xlate rule %', v_uid;
				END IF;
				UPDATE rule SET xlate_rule=i_xlate_rule_id WHERE rule_id=i_added_rule_id;
			END IF;
	END LOOP;
	RETURN b_rule_order_to_be_written;
END; 
$$ LANGUAGE plpgsql;

----------------------------------------------------
-- FUNCTION:   import_rules_set_rule_num_numeric (control_id, device_id)
-- purpose:    sets numeric rule order value in field rule_num_numeric for sorting rules in the correct order
-- Parameter1: import id (control_id)
-- Parameter2: device_id
-- RETURNS:    nothing
/*  function layout:
	for each rule in import_rule
		if rule changed
			get rule_num_numeric of previous and next rule from rule table
			if no prev & no next rule exists: rule_num_numeric = 0
			elsif no next rule exists: rule_num_numeric = prev + 1000
			elsif no prev rule exists: rule_num_numeric = next - 1000
			else set rule_num_numeric = (prev+next)/2
*/
CREATE OR REPLACE FUNCTION import_rules_set_rule_num_numeric (BIGINT,INTEGER) RETURNS VOID AS $$
DECLARE
	i_current_control_id ALIAS FOR $1; -- ID des aktiven Imports
	i_dev_id ALIAS FOR $2; -- ID des zu importierenden Devices
	i_mgm_id INTEGER; -- ID des zugehoerigen Managements
	r_rule RECORD;
	i_prev_numeric_value BIGINT;
	i_next_numeric_value BIGINT;
	i_numeric_value BIGINT;

BEGIN
	RAISE DEBUG 'import_rules_set_rule_num_numeric - start';
	SELECT INTO i_mgm_id mgm_id FROM device WHERE dev_id=i_dev_id;
	RAISE DEBUG 'import_rules_set_rule_num_numeric - mgm_id=%, dev_id=%, before inserting', i_mgm_id, i_dev_id;
	FOR r_rule IN -- set rule_num_numeric for changed (i.e. "new") rules
		SELECT rule.rule_id, rule_num_numeric FROM import_rule LEFT JOIN rule USING (rule_uid) WHERE
			active AND
			import_rule.control_id = i_current_control_id AND
			rule.dev_id=i_dev_id 
			ORDER BY import_rule.rule_num
	LOOP
		RAISE DEBUG 'import_rules_set_rule_num_numeric loop rule %', CAST(r_rule.rule_id AS VARCHAR );
		IF r_rule.rule_num_numeric IS NULL THEN
			RAISE DEBUG 'import_rules_set_rule_num_numeric found new rule %', CAST(r_rule.rule_id AS VARCHAR );
			-- get numeric value of next rule:
			SELECT INTO i_next_numeric_value rule_num_numeric FROM rule 
				WHERE active AND dev_id=i_dev_id AND mgm_id=i_mgm_id AND rule_num_numeric>i_prev_numeric_value ORDER BY rule_num_numeric LIMIT 1;
			RAISE DEBUG 'import_rules_set_rule_num_numeric next rule %', CAST(i_next_numeric_value AS VARCHAR);
			IF i_prev_numeric_value IS NULL AND i_next_numeric_value IS NULL THEN
				i_numeric_value := 0;
			ELSIF i_next_numeric_value IS NULL THEN
				i_numeric_value := i_prev_numeric_value + 1000;
			ELSIF i_prev_numeric_value IS NULL THEN
				i_numeric_value := i_next_numeric_value - 1000;
			ELSE
				i_numeric_value := (i_prev_numeric_value + i_next_numeric_value) / 2;
			END IF; 
			RAISE DEBUG 'import_rules_set_rule_num_numeric determined rule_num_numeric %', CAST(i_numeric_value AS VARCHAR);
			UPDATE rule SET rule_num_numeric = i_numeric_value WHERE rule.rule_id=r_rule.rule_id;
			r_rule.rule_num_numeric := i_numeric_value;
		END IF;
		i_prev_numeric_value := r_rule.rule_num_numeric;
	END LOOP;
	RAISE DEBUG 'import_rules_set_rule_num_numeric - end';
	RETURN;
END;
$$ LANGUAGE plpgsql;

----------------------------------------------------
-- FUNCTION:   insert_single_rule
-- Purpose     adds a single rule of the current import into the rule table
-- Parameter1: import_rule.rule_id - ID of the rule to import
-- Parameter2: dev_id
-- Parameter3: mgm_id
-- Parameter4: control_id
-- Parameter5: b_is_initial_import
-- RETURNS:   new rule id

-- dropping first due to change of return type in v5.5.1:
DROP FUNCTION IF EXISTS insert_single_rule(bigint,integer,integer,bigint,boolean);

CREATE OR REPLACE FUNCTION insert_single_rule(BIGINT,INTEGER,INTEGER,BIGINT,BOOLEAN) RETURNS BIGINT AS $$
DECLARE
    id   ALIAS FOR $1;
    i_dev_id   ALIAS FOR $2;
    i_mgm_id   ALIAS FOR $3;
    i_control_id ALIAS FOR $4;
    b_is_initial_import ALIAS FOR $5;
    r_to_import   RECORD;    -- the record to import from import_rule
    r_meta   RECORD;    	 -- rule meta data record
    i_rule_num    INTEGER;   -- the rule number
    s_track       VARCHAR;   -- track-string
    i_action_id   INTEGER;   -- action_id
    i_track_id    INTEGER;   -- Record for tracking action
    i_fromzone    INTEGER;   -- Record for source zone
    i_tozone      INTEGER;   -- Record for destination zone
    i_admin_id    INTEGER;   -- ID of last_change_admin
    b_implied     BOOLEAN;   -- rule is implicit?
    r_existing	  RECORD;	 -- formerly existing rule (for changes)
    b_insert	  BOOLEAN;	 -- new rule
    b_change	  BOOLEAN;	 -- was the rule changed?
    b_change_sr   BOOLEAN;	 -- non-security-relevant change
    v_change_id	  VARCHAR;	 -- type of change
    i_new_rule_id BIGINT;    -- id of rule just about to be inserted
    i_old_rule_id BIGINT;    -- id of exisiting rule
	b_is_documented BOOLEAN; 
	t_outtext	  TEXT; 
	i_change_type INTEGER;
	v_change_action VARCHAR;    
	i_parent_rule_id BIGINT;
	i_parent_rule_type SMALLINT;
	r_parent_rule RECORD;
	b_access_rule BOOLEAN;
	b_nat_rule BOOLEAN;
	-- v_local_error VARCHAR;
	-- v_error_str VARCHAR;
BEGIN
	RAISE DEBUG 'insert_single_rule start, rule_id: %', id;

    b_insert := FALSE;    b_change := FALSE;    b_change_sr := FALSE;
    SELECT INTO r_to_import * FROM import_rule WHERE rule_id = id; -- get rule record from import_rule
-- fetch zone id ------------------------------------------------------------------------------------------
    IF (r_to_import.rule_from_zone IS NULL) THEN	i_fromzone := NULL;
    ELSE SELECT INTO i_fromzone zone_id FROM zone WHERE zone_name = r_to_import.rule_from_zone AND zone.mgm_id = i_mgm_id; -- AND active;
	END IF;
    IF (r_to_import.rule_to_zone IS NULL) THEN i_tozone := NULL;
	ELSE SELECT INTO i_tozone zone_id FROM zone WHERE zone_name = r_to_import.rule_to_zone AND zone.mgm_id = i_mgm_id; -- AND active;
	END IF;
-- fetch track ID ------------------------------------------------------------------------------------------
	IF char_length(cast(r_to_import.rule_track as varchar))=0 THEN	s_track := 'none';
	ELSE	s_track := lower(r_to_import.rule_track);
	END IF;
    SELECT INTO i_track_id track_id FROM stm_track WHERE track_name = s_track; -- Track-ID holen
    IF NOT FOUND THEN	PERFORM error_handling('ERR_NO_TRACK', s_track);	END IF;
-- fetch action id ------------------------------------------------------------------------------------------
    SELECT INTO i_action_id action_id FROM stm_action WHERE action_name = lower(r_to_import.rule_action); -- Action-ID holen
	IF NOT FOUND THEN	PERFORM error_handling('ERR_NO_ACTION', r_to_import.rule_action);	END IF;
-- fetch rule_num ------------------------------------------------------------------------------------------
    IF (r_to_import.rule_num IS NULL OR CAST(r_to_import.rule_num AS VARCHAR) = '') THEN 	-- if there is no rule number
		i_rule_num := 0;  																  	-- we use 0
    ELSE 
		i_rule_num := CAST(r_to_import.rule_num AS INTEGER); 								-- cast text into integer (todo: add error handling)
    END IF;    
-- which rule was changed? -----------------------------------------------------------------
	RAISE DEBUG 'insert_single_rule 1, rule_id: %', id;
	IF (r_to_import.rule_uid IS NULL) THEN -- removed char_length-check due to utf-8 problems
		PERFORM error_handling('ERR_RULE_NOT_IDENTIFYABLE');
	END IF;

	-- setting rule type vars
	IF r_to_import.rule_type = 'access' OR r_to_import.rule_type = 'combined' OR r_to_import.rule_type IS NULL THEN
		b_access_rule := TRUE;
	ELSE 
		b_access_rule := FALSE;
	END IF;
	IF NOT (r_to_import.rule_type = 'access' OR r_to_import.rule_type IS NULL) THEN
		b_nat_rule := TRUE;
	ELSE 
		b_nat_rule := FALSE;
	END IF;

	-- SELECT INTO r_existing * FROM rule WHERE
	-- 	rule_uid=r_to_import.rule_uid AND rule.mgm_id=i_mgm_id AND rule.dev_id=i_dev_id AND rule.active AND rule.access_rule=b_access_rule AND rule.nat_rule=b_nat_rule;

	IF r_to_import.rule_type = 'original' THEN
		SELECT INTO r_existing * FROM rule WHERE
			rule_uid=r_to_import.rule_uid AND rule.mgm_id=i_mgm_id AND rule.dev_id=i_dev_id AND rule.active AND rule.nat_rule AND NOT rule.xlate_rule IS NULL;
	ELSE 
		IF r_to_import.rule_type = 'xlate' THEN
			SELECT INTO r_existing * FROM rule WHERE
				rule_uid=r_to_import.rule_uid AND rule.mgm_id=i_mgm_id AND rule.dev_id=i_dev_id AND rule.active AND rule.nat_rule AND rule.xlate_rule IS NULL;

			ELSE -- standard access rule or combined original rule 
				SELECT INTO r_existing * FROM rule WHERE
	--				rule_uid=r_to_import.rule_uid AND rule.mgm_id=i_mgm_id AND rule.dev_id=i_dev_id AND rule.active AND NOT rule.nat_rule AND rule.access_rule;
					rule_uid=r_to_import.rule_uid AND rule.mgm_id=i_mgm_id AND rule.dev_id=i_dev_id AND rule.active AND rule.access_rule AND 
					((rule.nat_rule AND rule.xlate_rule IS NOT NULL) OR NOT rule.nat_rule AND rule.xlate_rule IS NULL);
			END IF;
		END IF;


	-- IF NOT SELECT COUNT(r_existing) == 1
	RAISE DEBUG 'insert_single_rule 2, rule_id: %', id;
	IF FOUND THEN  -- rule already exists
		RAISE DEBUG 'insert_single_rule 3, rule_id: %', id;
		IF ( NOT (
			are_equal(r_existing.rule_uid, r_to_import.rule_uid) AND
			are_equal(r_existing.rule_ruleid,r_to_import.rule_ruleid) AND
			are_equal(r_existing.rule_from_zone,i_fromzone) AND
			are_equal(r_existing.rule_to_zone,i_tozone) AND
			are_equal(r_existing.rule_disabled, r_to_import.rule_disabled) AND
			are_equal(r_existing.rule_src, r_to_import.rule_src) AND
			are_equal(r_existing.rule_dst, r_to_import.rule_dst) AND
			are_equal(r_existing.rule_svc, r_to_import.rule_svc) AND
			are_equal(r_existing.rule_src_refs,r_to_import.rule_src_refs) AND
			are_equal(r_existing.rule_dst_refs, r_to_import.rule_dst_refs) AND
			are_equal(r_existing.rule_svc_refs, r_to_import.rule_svc_refs) AND
			are_equal(r_existing.rule_src_neg, r_to_import.rule_src_neg) AND
			are_equal(r_existing.rule_dst_neg, r_to_import.rule_dst_neg) AND
			are_equal(r_existing.rule_svc_neg, r_to_import.rule_svc_neg) AND
			are_equal(r_existing.action_id, i_action_id) AND
			are_equal(r_existing.track_id, i_track_id) AND
			are_equal(r_existing.rule_installon, r_to_import.rule_installon) AND
			-- are_equal(r_existing.access_rule, r_to_import.access_rule) AND
			-- cannot compare the following two as they are not part of import_rule:
--			are_equal(r_existing.parent_rule_id, r_to_import.parent_rule_id) AND
--			are_equal(r_existing.parent_rule_type, r_to_import.parent_rule_type) AND
			are_equal(r_existing.rule_time, r_to_import.rule_time) ))
		THEN
			RAISE DEBUG 'insert_single_rule 4, rule_id: %', id;
			b_change := TRUE;
			b_change_sr := TRUE;
		END IF;
		IF ( NOT (		--	from here: non-security-relevant changes
			are_equal(r_existing.rule_name,r_to_import.rule_name) AND 
			are_equal(r_existing.rule_head_text, r_to_import.rule_head_text) AND
			-- are_equal(r_existing.nat_rule, r_to_import.nat_rule) AND
			are_equal(r_existing.rule_comment, r_to_import.rule_comment) ))
		THEN
			b_change := TRUE;
		END IF;
		IF (b_change)
		THEN	
			v_change_id := 'INFO_RULE_CHANGED';
		ELSE
			UPDATE rule SET rule_last_seen = i_control_id WHERE rule_id = r_existing.rule_id;
		END IF;
	ELSE -- rule was changed
		b_insert := TRUE;
		v_change_id := 'INFO_RULE_INSERTED'; 
	END IF;
	IF (b_change OR b_insert) THEN
		PERFORM error_handling(v_change_id, r_to_import.rule_uid);
		i_admin_id := get_admin_id_from_name(r_to_import.last_change_admin);   
		RAISE DEBUG 'rule_change_or_insert: %', r_to_import.rule_uid;
		-- execute INSERT statement
		IF r_to_import.rule_svc IS NULL OR r_to_import.rule_src IS NULL OR r_to_import.rule_dst IS NULL THEN
			RAISE NOTICE 'rule_change with svc, dst or svc = NULL: %', r_to_import.rule_uid;			
		ELSE
			RAISE DEBUG 'rule_change_or_insert_before_insert: %', r_to_import.rule_uid;
			SELECT INTO r_meta rule_metadata_id FROM rule_metadata WHERE dev_id=i_dev_id AND rule_uid=r_to_import.rule_uid;
			IF FOUND THEN
				UPDATE rule_metadata SET rule_last_modified=now() WHERE dev_id=i_dev_id AND rule_uid=CAST(r_to_import.rule_uid AS TEXT);
			ELSE
				INSERT INTO rule_metadata (rule_uid, dev_id) VALUES(r_to_import.rule_uid, i_dev_id);
			END IF;
			RAISE DEBUG 'rule_change_after_rule_metadata change: %', r_to_import.rule_uid;

			i_parent_rule_id := NULL;
			i_parent_rule_type := NULL;
			IF NOT r_to_import.parent_rule_uid IS NULL THEN
				SELECT INTO r_parent_rule rule_id, rule_head_text, action_name FROM rule LEFT JOIN stm_action USING (action_id) WHERE rule_uid=r_to_import.parent_rule_uid AND rule_last_seen=i_control_id;
				IF NOT FOUND THEN
					RAISE WARNING 'rule_change found reference to parent rule with uid (%) that cannot be found in import. Importing without parent reference.',
						r_to_import.parent_rule_uid;
					i_parent_rule_id := NULL;
				ELSE
					i_parent_rule_id := r_parent_rule.rule_id;
					RAISE DEBUG 'rule_change_change setting parent rule type, uid: %', r_to_import.rule_uid;
					IF r_parent_rule.action_name = 'inline-layer' THEN
						i_parent_rule_type := 2; -- layer guard
					ELSIF NOT r_parent_rule.rule_head_text IS NULL AND NOT r_parent_rule.rule_head_text='Placeholder for domain rules' THEN
						i_parent_rule_type := 1; -- standard section
					ELSIF NOT r_parent_rule.rule_head_text IS NULL AND r_parent_rule.rule_head_text='Placeholder for domain rules' THEN
						i_parent_rule_type := 3; -- domain rule section
					END IF;
				END IF;
			END IF;
			BEGIN -- catch insert rule exception block
				INSERT INTO rule
					(mgm_id,rule_name,rule_num,rule_ruleid,rule_uid,rule_disabled,rule_src_neg,rule_dst_neg,rule_svc_neg,
					action_id,track_id,rule_src,rule_dst,rule_svc,rule_src_refs,rule_dst_refs,rule_svc_refs,rule_action,rule_track,rule_installon,rule_time,
					rule_from_zone,rule_to_zone,rule_comment,rule_implied,rule_head_text,last_change_admin,
					rule_create,rule_last_seen, dev_id, parent_rule_id, parent_rule_type, access_rule, nat_rule)
				VALUES (i_mgm_id,r_to_import.rule_name,i_rule_num,r_to_import.rule_ruleid,r_to_import.rule_uid,
					r_to_import.rule_disabled,r_to_import.rule_src_neg,r_to_import.rule_dst_neg,r_to_import.rule_svc_neg,
					i_action_id,i_track_id,r_to_import.rule_src,r_to_import.rule_dst,r_to_import.rule_svc,
					r_to_import.rule_src_refs,r_to_import.rule_dst_refs,r_to_import.rule_svc_refs,
					lower(r_to_import.rule_action),r_to_import.rule_track,r_to_import.rule_installon,r_to_import.rule_time,
					i_fromzone,i_tozone, r_to_import.rule_comment,r_to_import.rule_implied,r_to_import.rule_head_text,i_admin_id,
					i_control_id,i_control_id, i_dev_id, i_parent_rule_id, i_parent_rule_type, b_access_rule, b_nat_rule)
				RETURNING rule_id INTO i_new_rule_id;
			EXCEPTION WHEN OTHERS THEN
				-- v_local_error := 'ERR-insert_single_rule@rule_uid: ' || CAST (r_to_import.rule_uid AS VARCHAR);
				-- RAISE WARNING '%', v_local_error;
				-- -- adding the error to potential other errors alread in import_control.import_errors
				-- SELECT INTO v_error_str import_errors FROM import_control WHERE control_id=i_current_import_id;
				-- IF NOT v_error_str IS NULL THEN
				-- 	v_error_str := v_error_str || '\n' || v_local_error;
				-- ELSE
				-- 	v_error_str := v_local_error;
				-- END IF;
				-- UPDATE import_control SET import_errors = v_error_str WHERE control_id=i_current_import_id;
				-- -- RETURN NULL;
				RAISE EXCEPTION 'rule_change_change exception while inserting rule: 
					mgm_id=%
					rule_name=%
					rule_num=%
					rule_ruleid=%
					rule_uid=%
					rule_disabled=%
					rule_src_neg=%
					rule_dst_neg=%
					rule_svc_neg=%
					action_id=%
					track_id=%
					rule_src=%
					rule_dst=%
					rule_svc=%
					rule_src_refs=%
					rule_dst_refs=%
					rule_svc_refs=%
					rule_action=%
					rule_track=%
					rule_installon=%
					rule_time=%
					rule_from_zone=%
					rule_to_zone=%
					rule_comment=%
					rule_implied=%
					rule_head_text=%
					last_change_admin=%
					rule_create=%
					rule_last_seen=%
					dev_id=%
					parent_rule_id=%
					parent_rule_type=%
					access_rule=%
					nat_rule=%',
					i_mgm_id,r_to_import.rule_name,i_rule_num,r_to_import.rule_ruleid,r_to_import.rule_uid,
					r_to_import.rule_disabled,r_to_import.rule_src_neg,r_to_import.rule_dst_neg,r_to_import.rule_svc_neg,
					i_action_id,i_track_id,r_to_import.rule_src,r_to_import.rule_dst,r_to_import.rule_svc,
					r_to_import.rule_src_refs,r_to_import.rule_dst_refs,r_to_import.rule_svc_refs,
					lower(r_to_import.rule_action),r_to_import.rule_track,r_to_import.rule_installon,r_to_import.rule_time,
					i_fromzone,i_tozone, r_to_import.rule_comment,r_to_import.rule_implied,r_to_import.rule_head_text,i_admin_id,
					i_control_id,i_control_id, i_dev_id, i_parent_rule_id, i_parent_rule_type, b_access_rule, b_nat_rule;
			END;
	
			-- make changelog entry
			RAISE DEBUG 'rule_change_or_insert_before_select_into: %', r_to_import.rule_uid;
			-- SELECT INTO i_new_rule_id MAX(rule_id) FROM rule WHERE mgm_id=i_mgm_id; -- todo: dubious
			IF (b_insert) THEN  -- rule was inserted and is no header rule
				RAISE DEBUG 'rule_change_or_insert_insert_zweig: %', r_to_import.rule_uid;
				IF b_is_initial_import
				THEN	b_is_documented := TRUE; i_change_type := 2;
				ELSE	b_is_documented := FALSE; t_outtext := NULL; i_change_type := 3;
				END IF;
				RAISE DEBUG 'rule_change_or_insert_insert_zweig_after_initial_import: %', r_to_import.rule_uid;
				v_change_action := 'I';
				i_old_rule_id := NULL;
				IF r_to_import.rule_head_text IS NULL THEN
					RAISE DEBUG 'rule_change_or_insert_insert_zweig_rule_head_text_is_null: %', r_to_import.rule_uid;
					b_change_sr := TRUE;
					t_outtext := NULL;
					b_is_documented := FALSE;
				ELSE
					RAISE DEBUG 'rule_change_or_insert_insert_zweig_rule_head_text_is_not_null: %', r_to_import.rule_uid;
					t_outtext := 'not sec relevant';
					RAISE DEBUG 'rule_change_or_insert_insert_zweig_rule_head_text_is_not_null_after_NON_SECURITY_RELEVANT_CHANGE: %', r_to_import.rule_uid;
					b_is_documented := TRUE;
					b_change_sr := FALSE;
				END IF;
				RAISE DEBUG 'rule_change_or_insert_insert_zweig_END: %', r_to_import.rule_uid;
			ELSE -- change
				RAISE DEBUG 'rule_change_or_insert_change_zweig: %', r_to_import.rule_uid;
				i_change_type := 3;
				IF (b_change_sr) THEN
					t_outtext := NULL;
					b_is_documented := FALSE;
				ELSE
					t_outtext := 'NON_SECURITY_RELEVANT_CHANGE';
					b_is_documented := TRUE;
				END IF;
				-- todo: add test if it is only a rename, then we would not add a changelog_rule entry in the future
				v_change_action := 'C';
				i_old_rule_id := r_existing.rule_id;
				RAISE DEBUG 'rule_change_or_insert_change_zweig_before_update_rule: %', r_to_import.rule_uid;
				UPDATE rule SET active = FALSE WHERE rule_id = i_old_rule_id; -- change old rule version to non-active
				RAISE DEBUG 'rule_change_or_insert_change_zweig_after_update_rule: %', r_to_import.rule_uid;
                PERFORM import_rule_resolved_nwobj(i_mgm_id, i_old_rule_id, NULL, NULL, i_control_id, v_change_action, 'R');
			END IF;
			RAISE DEBUG 'rule_change_or_insert_before_changelog_rule_insert: %', r_to_import.rule_uid;
			INSERT INTO changelog_rule
				(control_id,new_rule_id,old_rule_id,change_action,import_admin,documented,changelog_rule_comment,
				 mgm_id,dev_id,change_type_id,security_relevant)
			VALUES
				(i_control_id,i_new_rule_id,i_old_rule_id,v_change_action,i_admin_id,b_is_documented,
				 t_outtext,i_mgm_id,i_dev_id,i_change_type,b_change_sr);
		END IF;
	END IF;
	RETURN i_new_rule_id;
END;
$$ LANGUAGE plpgsql;
