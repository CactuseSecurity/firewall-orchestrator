-- adjust rule/owner entries in recertification table

-- select * from recert_refresh_one_owner_one_mgm(2,1,NULL::TIMESTAMP);
-- select * from recert_refresh_per_management(1);

-- function used during import of a single management config
CREATE OR REPLACE FUNCTION recert_refresh_per_management (i_mgm_id INTEGER) RETURNS VOID AS $$
DECLARE
	r_owner   RECORD;
BEGIN
	BEGIN		
		FOR r_owner IN
			SELECT id, name FROM owner
		LOOP
			PERFORM recert_refresh_one_owner_one_mgm (r_owner.id, i_mgm_id, NULL::TIMESTAMP);
		END LOOP;

	EXCEPTION WHEN OTHERS THEN
		RAISE EXCEPTION 'Exception caught in recert_refresh_per_management while handling owner %', r_owner.name;
	END;
	RETURN;
END;
$$ LANGUAGE plpgsql;


-- function used during import of owner data
CREATE OR REPLACE FUNCTION recert_refresh_per_owner(i_owner_id INTEGER) RETURNS VOID AS $$
DECLARE
	r_mgm    RECORD;
BEGIN
	BEGIN
		FOR r_mgm IN
			SELECT mgm_id, mgm_name FROM management
		LOOP
			PERFORM recert_refresh_one_owner_one_mgm (i_owner_id, r_mgm.mgm_id, NULL::TIMESTAMP);
		END LOOP;

	EXCEPTION WHEN OTHERS THEN
		RAISE EXCEPTION 'Exception caught in recert_refresh_per_owner while handling management %', r_mgm.mgm_name;
	END;
	RETURN;
END;
$$ LANGUAGE plpgsql;


-- this function deletes existing (future) open recert entries and inserts the new ones into the recertificaiton table
-- the new recert date will only replace an existing one, if it is closer (smaller)
CREATE OR REPLACE FUNCTION recert_refresh_one_owner_one_mgm
	(i_owner_id INTEGER, i_mgm_id INTEGER, t_requested_next_recert_date TIMESTAMP) RETURNS VOID AS $$
DECLARE
	r_rule   RECORD;
	i_recert_entry_id BIGINT;
	b_super_owner BOOLEAN := FALSE;
	t_rule_created TIMESTAMP;
	t_current_next_recert_date TIMESTAMP;
	t_next_recert_date_by_interval TIMESTAMP;
	t_rule_last_recertified TIMESTAMP;
	t_next_recert_date TIMESTAMP;
	i_recert_inverval INTEGER;
	b_never_recertified BOOLEAN := FALSE;
	b_no_current_next_recert_date BOOLEAN := FALSE;
	b_super_owner_exists BOOLEAN := FALSE;
	i_previous_import BIGINT;
	i_current_import_id BIGINT;
	i_super_owner_id INT;
BEGIN
	IF i_owner_id IS NULL OR i_mgm_id IS NULL THEN
		IF i_owner_id IS NULL THEN
			RAISE WARNING 'found undefined owner_id in recert_refresh_one_owner_one_mgm';
		ELSE -- mgm_id NULL
			RAISE WARNING 'found undefined mgm_id in recert_refresh_one_owner_one_mgm';
		END IF;
	ELSE

		-- get id of previous import:
		SELECT INTO i_current_import_id control_id FROM import_control WHERE mgm_id=i_mgm_id AND stop_time IS NULL;
		SELECT INTO i_previous_import * FROM get_previous_import_id_for_mgmt(i_mgm_id,i_current_import_id);
		IF NOT FOUND OR i_previous_import IS NULL THEN
			i_previous_import := -1;	-- prevent match for previous import
		END IF;

		SELECT INTO i_super_owner_id id FROM owner WHERE is_default;
		IF FOUND THEN 
			b_super_owner_exists := TRUE;
		END IF;

		SELECT INTO i_super_owner_id id FROM owner WHERE id=i_owner_id AND is_default;
		IF FOUND THEN 
			b_super_owner := TRUE;
		END IF;

		SELECT INTO i_recert_inverval recert_interval FROM owner WHERE id=i_owner_id;

		FOR r_rule IN
		SELECT rule_uid, rule_id FROM rule WHERE mgm_id=i_mgm_id AND (active OR NOT active AND rule_last_seen=i_previous_import)
		LOOP

			IF recert_owner_responsible_for_rule (i_owner_id, r_rule.rule_id) THEN

				-- collects dates
				SELECT INTO t_current_next_recert_date next_recert_date FROM recertification 
				WHERE owner_id=i_owner_id AND rule_id=r_rule.rule_id AND recert_date IS NULL;

				IF NOT FOUND THEN
					b_no_current_next_recert_date := TRUE;
				END IF;

				SELECT INTO t_rule_last_recertified MAX(recert_date)
					FROM recertification
					WHERE rule_id=r_rule.rule_id AND NOT recert_date IS NULL;

				IF NOT FOUND OR t_rule_last_recertified IS NULL THEN	-- no prior recertification, use initial rule import date 
					b_never_recertified := TRUE;
					SELECT INTO t_rule_created rule_metadata.rule_created
						FROM rule
						LEFT JOIN rule_metadata ON (rule.rule_uid=rule_metadata.rule_uid AND rule.dev_id=rule_metadata.dev_id)
						WHERE rule_id=r_rule.rule_id;
				END IF;

				IF t_requested_next_recert_date IS NULL THEN
					-- if the currenct next recert date is before the intended fixed input date, ignore it 
					IF b_never_recertified THEN
						t_next_recert_date := t_rule_created + make_interval (days => i_recert_inverval);
					ELSE 
						t_next_recert_date := t_rule_last_recertified + make_interval (days => i_recert_inverval);
					END IF;
				ELSE
					t_next_recert_date := t_requested_next_recert_date;
				END IF;

				-- do not set next recert date later than actually calculated date
				IF NOT b_no_current_next_recert_date THEN
					IF t_next_recert_date>t_current_next_recert_date THEN
						t_next_recert_date := t_current_next_recert_date;
					END IF;
				END IF;

				-- delete old recert entry:
				DELETE FROM recertification WHERE owner_id=i_owner_id AND rule_id=r_rule.rule_id AND recert_date IS NULL;

				-- add new recert entry:
				IF b_super_owner THEN	-- special case for super owner (convert NULL to ID)
					INSERT INTO recertification (rule_metadata_id, next_recert_date, rule_id, ip_match, owner_id)
						SELECT rule_metadata_id, 
							t_next_recert_date AS next_recert_date,
							rule_id, 
							matches as ip_match, 
							i_owner_id AS owner_id
						FROM view_rule_with_owner 
						LEFT JOIN rule USING (rule_id)
						LEFT JOIN rule_metadata ON (rule.rule_uid=rule_metadata.rule_uid AND rule.dev_id=rule_metadata.dev_id)
						WHERE view_rule_with_owner.rule_id=r_rule.rule_id AND view_rule_with_owner.owner_id IS NULL;
				ELSE
					INSERT INTO recertification (rule_metadata_id, next_recert_date, rule_id, ip_match, owner_id)
						SELECT rule_metadata_id, 
							t_next_recert_date AS next_recert_date,
							rule_id, 
							matches as ip_match, 
							i_owner_id AS owner_id
						FROM view_rule_with_owner 
						LEFT JOIN rule USING (rule_id)
						LEFT JOIN rule_metadata ON (rule.rule_uid=rule_metadata.rule_uid AND rule.dev_id=rule_metadata.dev_id)
						WHERE view_rule_with_owner.rule_id=r_rule.rule_id AND view_rule_with_owner.owner_id=i_owner_id;
				END IF;
			ELSE
				-- delete old outdated recert entry if owner is not responsible any more 
				DELETE FROM recertification WHERE owner_id=i_owner_id AND rule_id=r_rule.rule_id AND recert_date IS NULL;
			END IF;
		END LOOP;

		-- finally, when not super user - recalculate super user recert entries - since these might change with each owner change
		IF NOT b_super_owner AND b_super_owner_exists THEN
			PERFORM recert_refresh_one_owner_one_mgm (i_super_owner_id, i_mgm_id, t_requested_next_recert_date);
		END IF;
	END IF;
END;
$$ LANGUAGE plpgsql;


-- this function returns a table of future recert entries 
-- but does not write them into the recertification table
CREATE OR REPLACE FUNCTION recert_get_one_owner_one_mgm
	(i_owner_id INTEGER, i_mgm_id INTEGER)
	RETURNS SETOF recertification AS
$$
DECLARE
	b_super_owner BOOLEAN;
	i_recert_entry_id INTEGER;
BEGIN
	b_super_owner := FALSE;
	SELECT INTO i_recert_entry_id id FROM owner WHERE id=i_owner_id AND is_default;
	IF FOUND THEN 
		b_super_owner := TRUE;
	END IF;

	IF b_super_owner THEN
		RETURN QUERY
		SELECT
			NULL::bigint AS id,
			M.rule_metadata_id, 
			R.rule_id, 
			V.matches::VARCHAR as ip_match, 
			i_owner_id,
			NULL::VARCHAR AS user_dn,
			NULL::BOOLEAN AS recertified,
			NULL::TIMESTAMP AS recert_date,
			NULL::VARCHAR AS comment,
			(I.start_time::timestamp + make_interval (days => O.recert_interval)) AS next_recert_date
		FROM 
			view_rule_with_owner V 
			LEFT JOIN rule R USING (rule_id)			
			LEFT JOIN rule_metadata M ON (R.rule_uid=M.rule_uid AND R.dev_id=M.dev_id)
			LEFT JOIN owner O ON (V.owner_id=O.id)
			LEFT JOIN import_control I ON (R.rule_create=I.control_id) 
		WHERE V.owner_id IS NULL AND R.mgm_id=i_mgm_id AND R.active;
	ELSE
		RETURN QUERY
		SELECT
			NULL::bigint AS id,
			M.rule_metadata_id, 
			R.rule_id, 
			V.matches::VARCHAR as ip_match, 
			i_owner_id,
			NULL::VARCHAR AS user_dn,
			NULL::BOOLEAN AS recertified,
			NULL::TIMESTAMP AS recert_date,
			NULL::VARCHAR AS comment,
			(I.start_time::timestamp + make_interval (days => O.recert_interval)) AS next_recert_date
		FROM 
			view_rule_with_owner V 
			LEFT JOIN rule R USING (rule_id)			
			LEFT JOIN rule_metadata M ON (R.rule_uid=M.rule_uid AND R.dev_id=M.dev_id)
			LEFT JOIN owner O ON (V.owner_id=O.id)
			LEFT JOIN import_control I ON (R.rule_create=I.control_id) 
		WHERE V.owner_id=i_owner_id AND R.mgm_id=i_mgm_id AND R.active;
	END IF;
END;
$$ LANGUAGE plpgsql STABLE;


-- fundamental function to check owner <--> rule mapping using the existing view
-- "view_rule_with_owner"
CREATE OR REPLACE FUNCTION recert_owner_responsible_for_rule (i_owner_id INTEGER, i_rule_id BIGINT) RETURNS BOOLEAN AS $$
DECLARE
	i_id BIGINT;
BEGIN
	-- check if this is the super owner:
	SELECT INTO i_id id FROM owner WHERE id=i_owner_id AND is_default;
	IF FOUND THEN -- this is the super owner
		SELECT INTO i_id rule_id FROM view_rule_with_owner WHERE owner_id IS NULL AND rule_id=i_rule_id;
		IF FOUND THEN
			RAISE DEBUG '%', 'rule found for super owner ' || i_rule_id;
			RETURN TRUE;
		ELSE
			RETURN FALSE;
		END IF;
	ELSE -- standard owner
		SELECT INTO i_id rule_id FROM view_rule_with_owner WHERE owner_id=i_owner_id AND rule_id=i_rule_id;
		IF FOUND THEN
			RETURN TRUE;
		ELSE
			RETURN FALSE;
		END IF;
	END IF;
END;
$$ LANGUAGE plpgsql;
