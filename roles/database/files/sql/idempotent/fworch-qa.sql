
CREATE OR REPLACE FUNCTION fix_broken_refs () RETURNS VARCHAR AS $$
DECLARE
	r_mgm	RECORD;
	v_result VARCHAR;
BEGIN
	v_result := '';
	FOR r_mgm IN SELECT mgm_id FROM management WHERE NOT management.do_not_import LOOP
		v_result := v_result || get_active_rules_with_broken_refs_per_mgm('|', TRUE, r_mgm.mgm_id);
	END LOOP;
	RETURN v_result;
END;
$$ LANGUAGE plpgsql;		


CREATE OR REPLACE FUNCTION check_broken_refs (VARCHAR, BOOLEAN) RETURNS VARCHAR AS $$
DECLARE
	v_delimiter ALIAS FOR $1;
	b_heal		ALIAS FOR $2;
	r_mgm	RECORD;
	v_result VARCHAR;
BEGIN
	v_result := '';
	FOR r_mgm IN SELECT mgm_id FROM management WHERE NOT management.do_not_import LOOP
--		RAISE NOTICE 'checking device %', CAST(r_dev.dev_id AS VARCHAR);
		v_result := v_result || get_active_rules_with_broken_refs_per_mgm(v_delimiter, b_heal, r_mgm.mgm_id);
	END LOOP;
	RETURN v_result;
END;
$$ LANGUAGE plpgsql;		
		
CREATE OR REPLACE FUNCTION get_active_rules_with_broken_refs_per_mgm (VARCHAR, BOOLEAN, INTEGER) RETURNS VARCHAR AS $$
DECLARE
	v_delimiter ALIAS FOR $1;
	b_heal		ALIAS FOR $2;
	i_mgm_id	ALIAS FOR $3;
	r_dev	RECORD;
	v_result VARCHAR;
BEGIN
	v_result := '';
	FOR r_dev IN SELECT dev_id FROM device LEFT JOIN management using (mgm_id) WHERE management.mgm_id=i_mgm_id AND 
			NOT management.do_not_import AND NOT device.do_not_import LOOP
--		RAISE NOTICE 'checking device %', CAST(r_dev.dev_id AS VARCHAR);
		v_result := v_result || get_active_rules_with_broken_refs_per_dev(v_delimiter, b_heal, r_dev.dev_id);
	END LOOP;
	RETURN v_result;
END;
$$ LANGUAGE plpgsql;


----------------------------------------------------
-- FUNCTION:	get_active_rules_with_broken_refs_per_dev (VARCHAR, BOOLEAN, INTEGER) 
-- Zweck:		get rule_ids of all rules that are active and belong to a device that is imported
-- Zweck:		where rule references are broken (rule_to, rule_from, rule_service)
-- Parameter1:	delimiter (varchar) default = |, delimiter between objects of a rule
-- Parameter2:	heal (boolean), if set to true, the reference is added to the respective table
-- Parameter3:	device id of device to check
-- RETURNS:		a set of all affected rules
-- Example: a) SELECT mgm_id,dev_id,rule_id, rule_create, rule_last_seen, rule_src_refs, rule_dst_refs, rule_svc_refs FROM rule WHERE 
-- 				rule_id in (select * from get_active_rules_with_broken_refs ('|', false)) order by rule_create;
-- Example: b) select * from get_active_rules_with_broken_refs ('|', true); -- repair all broken refs
-- Example: c) select * from get_active_rules_with_broken_refs ('|', false); -- just display NOTICES about broken refs

CREATE OR REPLACE FUNCTION get_active_rules_with_broken_refs_per_dev (VARCHAR, BOOLEAN, INTEGER) RETURNS VARCHAR AS $$
DECLARE
	v_delimiter ALIAS FOR $1;
	b_heal		ALIAS FOR $2;
	i_dev_id	ALIAS FOR $3;
BEGIN
	RETURN 
		get_active_rules_with_broken_src_refs_per_dev (v_delimiter, b_heal, i_dev_id) || 
		get_active_rules_with_broken_dst_refs_per_dev (v_delimiter, b_heal, i_dev_id) ||
		get_active_rules_with_broken_svc_refs_per_dev (v_delimiter, b_heal, i_dev_id);
END;
$$ LANGUAGE plpgsql;


CREATE OR REPLACE FUNCTION get_active_rules_with_broken_src_refs_per_dev (VARCHAR, BOOLEAN, INTEGER) RETURNS VARCHAR AS $$
DECLARE
	v_delimiter ALIAS FOR $1;
	b_heal		ALIAS FOR $2;
	i_dev_id	ALIAS FOR $3;
	i_mgm_id	INTEGER;
	i_zone_id	INTEGER;
	i_rule_created BIGINT;
	i_rule_last_seen BIGINT;
	i_count		INTEGER;
	i_rule_count	INTEGER;
	i_ref_count	INTEGER;
	r_obj	RECORD;
	r_rule	RECORD;
	r_dev	RECORD;
	r_debug	RECORD;
	r_debug2	RECORD;
	v_current_obj	VARCHAR;
	v_user_group VARCHAR;
	i_user_id BIGINT;
	v_result VARCHAR;
	v_result_single VARCHAR;
BEGIN
	-- BEGIN
		v_result := '';
		FOR r_rule IN SELECT rule_id,rule_uid,rule_src, rule_dst, rule_svc, rule_src_refs, rule_dst_refs, rule_svc_refs, 
				rule_from_zone, rule_to_zone, rule_create, rule_last_seen
			FROM rule WHERE dev_id=i_dev_id and active LOOP
			SELECT INTO i_mgm_id mgm_id FROM rule WHERE rule_id=r_rule.rule_id;
			SELECT INTO i_rule_created rule_create FROM rule WHERE rule_id=r_rule.rule_id;
			SELECT INTO i_rule_last_seen rule_last_seen FROM rule WHERE rule_id=r_rule.rule_id;
			
			-- checking rule source references
			FOR v_current_obj IN SELECT obj FROM regexp_split_to_table(r_rule.rule_src_refs, E'\\' || v_delimiter) AS obj LOOP
				IF NOT (v_current_obj IS NULL OR v_current_obj='') THEN 
					SELECT INTO i_count count(*) FROM regexp_split_to_table(v_current_obj, '@');
					v_user_group := '';
					IF i_count=2 THEN	-- AT in string
						SELECT INTO v_user_group regexp_replace(v_current_obj, '@.*$', '');
						SELECT INTO v_current_obj regexp_replace(v_current_obj, '^.+@', '');
						SELECT INTO i_user_id user_id FROM usr WHERE user_uid=v_user_group AND mgm_id=i_mgm_id AND active;
	--					checking user refs
						SELECT INTO r_obj user_id FROM usr WHERE user_uid=v_user_group AND mgm_id=i_mgm_id AND active;
						IF NOT FOUND THEN
							v_result_single := 'mgmt ' || CAST(i_mgm_id AS VARCHAR) || ', dev ' || CAST(i_dev_id AS VARCHAR) || ', fail 1 (src user_id not found at all): ' || v_user_group;
							v_result := v_result || v_result_single || '; ';
							-- --                        1                    2         3         4               5              6           7
							-- PERFORM add_data_issue(i_rule_last_seen,  v_user_group, NULL, r_rule.rule_uid, r_rule.rule_id, i_dev_id, 'user in rule', 
							-- 	'non-existant user obj "' || v_user_group || '" referenced in rule with UID ' || r_rule.rule_uid, NULL);
							RAISE NOTICE '%', v_result_single;
						ELSE		-- check if exactly one object is returned
							IF (NOT COUNT(r_obj)=1) THEN 
								v_result_single := 'mgmt ' || CAST(i_mgm_id AS VARCHAR) || ', dev ' || CAST(i_dev_id AS VARCHAR) || ', fail 2 (not exactly one src obj found): ' || CAST(r_obj.user_id AS VARCHAR) || ', COUNT=' || CAST(count(r_obj) AS VARCHAR);
								v_result := v_result || v_result_single || '; ';
								RAISE NOTICE '%', v_result_single;
								-- --                        1                    2         3         4               5              6           7
								-- PERFORM add_data_issue(i_rule_last_seen,  v_user_group, NULL, r_rule.rule_uid, r_rule.rule_id, i_dev_id, 'user in rule', 
								-- 	'more than one matching user obj "' || v_user_group || '" found in rule with UID ' || r_rule.rule_uid, NULL);
							ELSE
								SELECT INTO r_debug rule_id,user_id FROM rule_from WHERE rule_id=r_rule.rule_id AND user_id=r_obj.user_id AND active;
								IF NOT FOUND THEN
									v_result_single := 'mgmt ' || CAST(i_mgm_id AS VARCHAR) || ', dev ' || CAST(i_dev_id AS VARCHAR) || ', fail 3 (src user not found in rule_from), id=' || CAST(r_obj.user_id AS VARCHAR) || ', uid=' || v_current_obj;
									v_result := v_result || v_result_single || '; ';
									RAISE NOTICE '%', v_result_single;
									-- --                        1                    2         3         4               5              6           7
									-- PERFORM add_data_issue(i_rule_last_seen,  v_user_group, NULL, r_rule.rule_uid, r_rule.rule_id, i_dev_id, 'user in rule', 
									-- 	'user in rule undefined', 'user "' || v_user_group || '" found in rule with UID ' || r_rule.rule_uid || ' could not be found');
								END IF;
							END IF;
						END IF;
	-- 					end of user checks, no fixes here because they are done below 
					END IF;						
					IF r_rule.rule_from_zone IS NULL THEN
						SELECT INTO r_obj obj_id FROM object WHERE obj_uid=v_current_obj AND mgm_id=i_mgm_id AND active;
					ELSE
						SELECT INTO r_obj obj_id FROM object WHERE obj_uid=v_current_obj AND (zone_id=r_rule.rule_from_zone) 
							AND mgm_id=i_mgm_id AND active;
						IF NOT FOUND THEN -- last chance: global zone for juniper devices
							SELECT INTO i_zone_id zone_id FROM zone WHERE (zone_name='global') AND zone.mgm_id=i_mgm_id;
							IF FOUND THEN
								SELECT INTO r_obj obj_id FROM object WHERE obj_uid=v_current_obj AND zone_id=i_zone_id AND 
									object.mgm_id=i_mgm_id AND object.active;
							ELSE -- zone_id is null - pick any object with same name disregarding the zone
								SELECT INTO r_obj obj_id FROM object WHERE obj_uid=v_current_obj AND object.mgm_id=i_mgm_id AND object.active;
							END IF;
						END IF;
					END IF;
					IF NOT FOUND THEN
						v_result_single := 'mgmt ' || CAST(i_mgm_id AS VARCHAR) || ', dev ' || CAST(i_dev_id AS VARCHAR) || ', fail 4 (src obj_id not found at all): ' || v_current_obj;
						v_result := v_result || v_result_single || '; ';
						RAISE NOTICE '%', v_result_single;
						-- --                        1                    2           3                 4               5              6          7
						-- PERFORM add_data_issue(i_rule_last_seen, v_current_obj, v_current_obj, r_rule.rule_uid, r_rule.rule_id, i_dev_id, 'source network object in rule', 
						-- 	'non-existant source network object "' || v_current_obj || '" referenced in rule with UID ' || r_rule.rule_uid, NULL);
					ELSE		-- check if exactly one object is returned
						IF (NOT COUNT(r_obj)=1) THEN 
							v_result_single := 'mgmt ' || CAST(i_mgm_id AS VARCHAR) || ', dev ' || CAST(i_dev_id AS VARCHAR) || ', fail 5 (not exactly one src obj found): ' || CAST(r_obj.obj_id AS VARCHAR) || ', COUNT=' || CAST(count(r_obj) AS VARCHAR);
							v_result := v_result || v_result_single || '; ';
							RAISE NOTICE '%', v_result_single;
						ELSE
							SELECT INTO r_debug rule_id,obj_id FROM rule_from WHERE rule_id=r_rule.rule_id AND obj_id=r_obj.obj_id AND active;
							IF NOT FOUND THEN
								v_result_single := 'mgmt ' || CAST(i_mgm_id AS VARCHAR) || ', dev ' || CAST(i_dev_id AS VARCHAR) || ', fail 6 (src obj not found in rule_from), id=' || CAST(r_obj.obj_id AS VARCHAR) || ', uid=' ||  v_current_obj;
								v_result := v_result || v_result_single || '; ';
								RAISE NOTICE '%', v_result_single;
								-- --                        1                    2         3         4               5              6           7
								-- PERFORM add_data_issue(i_rule_last_seen,  v_user_group, NULL, r_rule.rule_uid, r_rule.rule_id, i_dev_id, 'user in rule', 
								-- 	'no matching source network obj "' || r_obj.obj_uid || '" found in rule with UID ' || r_rule.rule_uid, NULL);
								IF b_heal THEN	-- healing:
									IF v_user_group='' THEN -- no auth rule with user group
										INSERT INTO rule_from (rule_id, obj_id, rf_create, rf_last_seen) 
											VALUES (r_rule.rule_id, r_obj.obj_id, r_rule.rule_create, r_rule.rule_last_seen);
									ELSE -- auth rule with user group
										INSERT INTO rule_from (rule_id, user_id, obj_id, rf_create, rf_last_seen) 
											VALUES (r_rule.rule_id, i_user_id, r_obj.obj_id, r_rule.rule_create, r_rule.rule_last_seen);
									END IF;
								END IF;
							END IF;
						END IF;
					END IF;
				END IF;
			END LOOP;
		END LOOP;
	-- EXCEPTION WHEN OTHERS THEN 
	-- 	--                        1                  2                     3         4                    5              6           7
	-- 	PERFORM add_data_issue(i_rule_last_seen,  r_rule.rule_uid, r_rule.rule_uid, r_rule.rule_uid, r_rule.rule_id, i_dev_id, 'rule ref error', 
	-- 		'exception raised while importing rule with UID "' || r_rule.rule_uid || '"', 'Unknown exception in get_active_rules_with_broken_src_refs_per_dev');
	-- 	RAISE EXCEPTION 'Unknown exception in get_active_rules_with_broken_src_refs_per_dev';
	-- END;
	RETURN v_result;
END;
$$ LANGUAGE plpgsql;

CREATE OR REPLACE FUNCTION get_active_rules_with_broken_dst_refs_per_dev (VARCHAR, BOOLEAN, INTEGER) RETURNS VARCHAR AS $$
DECLARE
	v_delimiter ALIAS FOR $1;
	b_heal		ALIAS FOR $2;
	i_dev_id	ALIAS FOR $3;
	i_mgm_id	INTEGER;
	i_zone_id	INTEGER;
	i_rule_created BIGINT;
	i_rule_last_seen BIGINT;
	r_obj	RECORD;
	r_rule	RECORD;
	r_debug	RECORD;
	r_debug2	RECORD;
	v_current_obj	VARCHAR;
	v_result VARCHAR;
	v_result_single VARCHAR;
BEGIN
	v_result := '';
	FOR r_rule IN SELECT rule_id,rule_src, rule_dst, rule_svc, rule_src_refs, rule_dst_refs, rule_svc_refs, 
			rule_from_zone, rule_to_zone, rule_create, rule_last_seen
			FROM rule WHERE dev_id=i_dev_id and active 
	LOOP
		SELECT INTO i_mgm_id mgm_id FROM rule WHERE rule_id=r_rule.rule_id;
		SELECT INTO i_rule_created rule_create FROM rule WHERE rule_id=r_rule.rule_id;
		SELECT INTO i_rule_last_seen rule_last_seen FROM rule WHERE rule_id=r_rule.rule_id;
		SELECT INTO i_zone_id rule_to_zone FROM rule WHERE rule_id=r_rule.rule_id;
		FOR v_current_obj IN SELECT obj FROM regexp_split_to_table(r_rule.rule_dst_refs, E'\\' || v_delimiter) AS obj LOOP
			IF NOT (v_current_obj IS NULL OR v_current_obj='') THEN 
					IF r_rule.rule_to_zone IS NULL THEN
						SELECT INTO r_obj obj_id FROM object WHERE obj_uid=v_current_obj AND mgm_id=i_mgm_id AND active;
					ELSE
						SELECT INTO r_obj obj_id FROM object WHERE obj_uid=v_current_obj AND (zone_id=r_rule.rule_to_zone) AND mgm_id=i_mgm_id AND active;
						IF NOT FOUND THEN -- last chance: global zone
							SELECT INTO i_zone_id zone_id FROM zone WHERE (zone_name='global') AND zone.mgm_id=i_mgm_id;
							IF FOUND THEN
								SELECT INTO r_obj obj_id FROM object WHERE obj_uid=v_current_obj AND zone_id=i_zone_id AND 
									object.mgm_id=i_mgm_id AND object.active;
							ELSE -- zone_id is null - pick any object with same name disregarding the zone
								SELECT INTO r_obj obj_id FROM object WHERE obj_uid=v_current_obj AND object.mgm_id=i_mgm_id AND object.active;
							END IF;
						END IF;
					END IF;
				IF NOT FOUND THEN -- dst object not found at all (in object table)
					v_result_single := 'mgmt ' || CAST(i_mgm_id AS VARCHAR) || ', dev ' || CAST(i_dev_id AS VARCHAR) || ', fail 7 (dst object not found in object table): ' || v_current_obj;
					v_result := v_result || v_result_single || '; ';
					RAISE NOTICE '%', v_result_single;
				ELSE		-- check if exactly one object is returned
					IF (NOT COUNT(r_obj)=1) THEN 
						v_result_single := 'mgmt ' || CAST(i_mgm_id AS VARCHAR) || ', dev ' || CAST(i_dev_id AS VARCHAR) || ', fail 8 (not exactly one dst obj found): ' || CAST(r_obj.obj_id AS VARCHAR) || ', COUNT=' || CAST(count(r_obj) AS VARCHAR);
						v_result := v_result || v_result_single || '; ';
						RAISE NOTICE '%', v_result_single;
					ELSE
						-- SELECT INTO r_debug rule_id,obj_id FROM rule_to WHERE rule_id=r_rule.rule_id AND obj_id=r_obj.obj_id AND active;
						SELECT INTO r_debug rule_id,obj_id,rule_uid FROM rule_to LEFT JOIN rule USING (rule_id) WHERE rule_id=r_rule.rule_id AND obj_id=r_obj.obj_id AND rule.active AND rule_to.active;
						IF NOT FOUND THEN
							SELECT INTO r_debug2 obj_name FROM object WHERE obj_id=r_obj.obj_id;
							v_result_single := 'mgmt ' || CAST(i_mgm_id AS VARCHAR) || ', dev ' || CAST(i_dev_id AS VARCHAR) || 
								', fail 9 (dst object not found in rule_to table): object: ' || CAST(r_debug2.obj_name AS VARCHAR) || ', id=' || CAST(r_obj.obj_id AS VARCHAR) || ', rule_uid=' || CAST(r_debug.rule_uid AS VARCHAR);
							v_result := v_result || v_result_single || '; ';
							RAISE NOTICE '%', v_result_single;
							IF b_heal THEN	-- healing:
								INSERT INTO rule_to (rule_id, obj_id, rt_create, rt_last_seen) 
									VALUES (r_rule.rule_id, r_obj.obj_id, r_rule.rule_create, r_rule.rule_last_seen);
							END IF;
						END IF;
					END IF;
				END IF;
			END IF;
		END LOOP;
	END LOOP;
	RETURN v_result;
END;
$$ LANGUAGE plpgsql;

CREATE OR REPLACE FUNCTION get_active_rules_with_broken_svc_refs_per_dev (VARCHAR, BOOLEAN, INTEGER) RETURNS VARCHAR AS $$
DECLARE
	v_delimiter ALIAS FOR $1;
	b_heal		ALIAS FOR $2;
	i_dev_id	ALIAS FOR $3;
	i_mgm_id	INTEGER;
	i_zone_id	INTEGER;
	i_rule_created BIGINT;
	i_rule_last_seen BIGINT;
	i_count		INTEGER;
	i_rule_count	INTEGER;
	i_ref_count	INTEGER;
	r_obj	RECORD;
	r_rule	RECORD;
	r_dev	RECORD;
	r_debug	RECORD;
	r_debug2	RECORD;
	v_current_obj	VARCHAR;
	v_user_group VARCHAR;
	i_user_id BIGINT;
	v_result VARCHAR;
	v_result_single VARCHAR;
BEGIN
	v_result := '';
	FOR r_rule IN SELECT rule_id,rule_src, rule_dst, rule_svc, rule_src_refs, rule_dst_refs, rule_svc_refs, 
			rule_from_zone, rule_to_zone, rule_create, rule_last_seen
		FROM rule WHERE dev_id=i_dev_id and active LOOP
		SELECT INTO i_mgm_id mgm_id FROM rule WHERE rule_id=r_rule.rule_id;
		SELECT INTO i_rule_created rule_create FROM rule WHERE rule_id=r_rule.rule_id;
		SELECT INTO i_rule_last_seen rule_last_seen FROM rule WHERE rule_id=r_rule.rule_id;
		
		FOR v_current_obj IN SELECT obj FROM regexp_split_to_table(r_rule.rule_svc_refs, E'\\' || v_delimiter) AS obj LOOP
			IF NOT (v_current_obj IS NULL OR v_current_obj='') THEN 
				SELECT INTO r_obj svc_id FROM service WHERE svc_uid=v_current_obj AND mgm_id=i_mgm_id AND active;
				IF NOT FOUND THEN 
					v_result_single := 'mgmt ' || CAST(i_mgm_id AS VARCHAR) || ', dev ' || CAST(i_dev_id AS VARCHAR) || ', fail 10 (svc_id not found at all): ' || v_current_obj;
					v_result := v_result || v_result_single || '; ';
					RAISE NOTICE '%', v_result_single;
				ELSE		-- check if exactly one object is returned
					IF (NOT COUNT(r_obj)=1) THEN 
						v_result_single := 'mgmt ' || CAST(i_mgm_id AS VARCHAR) || ', dev ' || CAST(i_dev_id AS VARCHAR) || ', fail 11 (not exactly one svc found): ' || CAST(r_obj.obj_id AS VARCHAR) || ', COUNT=' || CAST(count(r_obj) AS VARCHAR);
						v_result := v_result || v_result_single || '; ';
						RAISE NOTICE '%', v_result_single;
					ELSE
						SELECT INTO r_debug rule_id,svc_id FROM rule_service WHERE rule_id=r_rule.rule_id AND svc_id=r_obj.svc_id AND active;
						IF NOT FOUND THEN
							v_result_single := 'mgmt ' || CAST(i_mgm_id AS VARCHAR) || ', dev ' || CAST(i_dev_id AS VARCHAR) || ', fail 12 "svc not found": id=' || CAST(r_obj.svc_id AS VARCHAR) || ', uid=' || v_current_obj;
							v_result := v_result || v_result_single || '; ';
							RAISE NOTICE '%', v_result_single;
							IF b_heal THEN	-- healing:
								INSERT INTO rule_service (rule_id, svc_id, rs_create, rs_last_seen) 
									VALUES (r_rule.rule_id, r_obj.svc_id, r_rule.rule_create, r_rule.rule_last_seen);
							END IF;
						END IF;
					END IF;
				END IF;
			END IF;
		END LOOP;
	END LOOP;
	RETURN v_result;
END;
$$ LANGUAGE plpgsql;
