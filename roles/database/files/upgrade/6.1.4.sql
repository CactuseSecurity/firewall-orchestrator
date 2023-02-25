ALTER TABLE request.reqelement ALTER COLUMN original_nat_id TYPE bigint;
ALTER TABLE request.reqelement ADD COLUMN IF NOT EXISTS device_id int;
ALTER TABLE request.reqelement ADD COLUMN IF NOT EXISTS rule_uid varchar;
ALTER TABLE request.reqelement DROP CONSTRAINT IF EXISTS request_reqelement_device_foreign_key;
ALTER TABLE request.reqelement ADD CONSTRAINT request_reqelement_device_foreign_key FOREIGN KEY (device_id) REFERENCES device(dev_id) ON UPDATE RESTRICT ON DELETE CASCADE;

ALTER TABLE request.implelement ALTER COLUMN original_nat_id TYPE bigint;
ALTER TABLE request.implelement ADD COLUMN IF NOT EXISTS rule_uid varchar;

ALTER TYPE rule_field_enum ADD VALUE IF NOT EXISTS 'rule';

insert into config (config_key, config_value, config_user) VALUES ('recAutocreateDeleteTicket', 'False', 0) ON CONFLICT DO NOTHING;
insert into config (config_key, config_value, config_user) VALUES ('recDeleteRuleTicketTitle', 'Ticket Title', 0) ON CONFLICT DO NOTHING;
insert into config (config_key, config_value, config_user) VALUES ('recDeleteRuleTicketReason', 'Ticket Reason', 0) ON CONFLICT DO NOTHING;
insert into config (config_key, config_value, config_user) VALUES ('recDeleteRuleReqTaskTitle', 'Task Title', 0) ON CONFLICT DO NOTHING;
insert into config (config_key, config_value, config_user) VALUES ('recDeleteRuleReqTaskReason', 'Task Reason', 0) ON CONFLICT DO NOTHING;
insert into config (config_key, config_value, config_user) VALUES ('recDeleteRuleTicketPriority', '3', 0) ON CONFLICT DO NOTHING;
insert into config (config_key, config_value, config_user) VALUES ('recDeleteRuleInitState', '0', 0) ON CONFLICT DO NOTHING;
insert into config (config_key, config_value, config_user) VALUES ('recCheckEmailSubject', 'Upcoming rule recertifications', 0) ON CONFLICT DO NOTHING;
insert into config (config_key, config_value, config_user) VALUES ('recCheckEmailUpcomingText', 'The following rules are upcoming to be recertified:', 0) ON CONFLICT DO NOTHING;
insert into config (config_key, config_value, config_user) VALUES ('recCheckEmailOverdueText', 'The following rules are overdue to be recertified:', 0) ON CONFLICT DO NOTHING;
insert into config (config_key, config_value, config_user) VALUES ('recCheckActive', 'False', 0) ON CONFLICT DO NOTHING;

ALTER TABLE owner ADD COLUMN IF NOT EXISTS last_recert_check Timestamp;
ALTER TABLE owner ADD COLUMN IF NOT EXISTS recert_check_params Varchar;

drop index if exists only_one_future_recert_per_owner_per_rule;
create unique index if not exists only_one_future_recert_per_owner_per_rule on recertification(owner_id,rule_metadata_id,recert_date) 
    where recert_date IS NULL;

ALTER TABLE owner_network DROP CONSTRAINT IF EXISTS owner_network_ip_unique;
ALTER TABLE owner_network ADD CONSTRAINT owner_network_ip_unique UNIQUE (owner_id, ip);

ALTER TABLE owner DROP COLUMN IF EXISTS next_recert_date;

Create index IF NOT EXISTS idx_object04 on object (obj_ip);
Create index IF NOT EXISTS idx_rule04 on rule (action_id);


-- replacing view by materialized view


CREATE OR REPLACE FUNCTION purge_view_rule_with_owner () RETURNS VOID AS $$
DECLARE
    r_temp_record RECORD;
BEGIN
    select INTO r_temp_record schemaname, viewname from pg_catalog.pg_views
    where schemaname NOT IN ('pg_catalog', 'information_schema') and viewname='view_rule_with_owner'
    order by schemaname, viewname;
    IF FOUND THEN
        DROP VIEW IF EXISTS view_rule_with_owner CASCADE;
    END IF;
    DROP MATERIALIZED VIEW IF EXISTS view_rule_with_owner CASCADE;
    RETURN;
END;
$$ LANGUAGE plpgsql;

SELECT * FROM purge_view_rule_with_owner ();
DROP FUNCTION purge_view_rule_with_owner();

CREATE MATERIALIZED VIEW view_rule_with_owner AS
	SELECT DISTINCT r.rule_num_numeric, r.track_id, r.action_id, r.rule_from_zone, r.rule_to_zone, r.dev_id, r.mgm_id, r.rule_uid, uno.rule_id, uno.owner_id, uno.owner_name, uno.rule_last_certified, uno.rule_last_certifier, 
	rule_action, rule_name, rule_comment, rule_track, rule_src_neg, rule_dst_neg, rule_svc_neg,
	rule_head_text, rule_disabled, access_rule, xlate_rule, nat_rule,
	string_agg(DISTINCT match_in || ':' || matching_ip::VARCHAR, '; ' order by match_in || ':' || matching_ip::VARCHAR desc) as matches,
	recert_interval
	FROM ( SELECT DISTINCT * FROM v_rule_with_src_owner UNION SELECT DISTINCT * FROM v_rule_with_dst_owner ) AS uno
	LEFT JOIN rule AS r USING (rule_id)
	GROUP BY rule_id, owner_id, owner_name, rule_last_certified, rule_last_certifier, r.rule_from_zone, r.rule_to_zone,  recert_interval,
		r.dev_id, r.mgm_id, r.rule_uid, rule_num_numeric, track_id, action_id, 	rule_action, rule_name, rule_comment, rule_track, rule_src_neg, rule_dst_neg, rule_svc_neg,
		rule_head_text, rule_disabled, access_rule, xlate_rule, nat_rule;


----------------------
-- make sure we have the current functions needed for recertification updates
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
	i_current_owner_id_tmp INT;
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

		SELECT INTO i_current_owner_id_tmp id FROM owner WHERE id=i_owner_id AND is_default;
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
---------------------------------------

-- LargeOwnerChange: uncomment to disable triggers (e.g. for large installations without recert needs)
-- ALTER TABLE owner DISABLE TRIGGER owner_change;
-- ALTER TABLE owner_network DISABLE TRIGGER owner_network_change;

DELETE FROM owner WHERE name='defaultOwner_demo';
UPDATE owner SET is_default=false WHERE id>0;   -- idempotence
INSERT INTO owner (id, name, dn, group_dn, is_default, recert_interval, app_id_external) 
VALUES    (0, 'super-owner', 'dn-of-super-owner', 'group-dn-for-super-owner', true, 365, 'NONE')
ON CONFLICT DO NOTHING; 
