
-- all functions around the rule_<obj>_resolved tables introduced with version 5.1.16 in May 2021
-- these functions add an entry to the respective rule_<objtype>_resolved table for each object contained in a rule
-- used for quick reporting of objects used in a reported ruleset

CREATE OR REPLACE FUNCTION import_rule_resolved_nwobj (INT,BIGINT,BIGINT,BIGINT,BIGINT,CHAR,CHAR) RETURNS VOID AS $$
DECLARE
	i_mgm_id ALIAS FOR $1;
	i_rule_id ALIAS FOR $2;
	i_old_obj_id ALIAS FOR $3;
	i_new_obj_id ALIAS FOR $4;
	i_current_import_id ALIAS FOR $5;
    c_action ALIAS FOR $6;
    c_changelog_table ALIAS FOR $7;
    r_null RECORD;
    i_member BIGINT;
    i_matching_rule_id BIGINT;
    i_matching_obj_id BIGINT;
    r_search RECORD;
BEGIN
    BEGIN -- catch
        RAISE DEBUG 'import_rule_resolved_nwobj 0 enter - i_mgm_id=%, i_rule_id=%, i_old_obj_id=%, i_new_obj_id=%, i_current_import_id=%, c_action=%, c_changelog_table=%', 
            i_mgm_id, i_rule_id, i_old_obj_id, i_new_obj_id, i_current_import_id, c_action, c_changelog_table;

        -- rule has been deleted resp. changed, marking all existing rule/obj refs as removed
        IF i_old_obj_id IS NULL AND i_new_obj_id IS NULL AND NOT i_rule_id IS NULL AND NOT c_action='I' THEN  
            -- handle ocurrences in all rules, if rule_id is NULL
            RAISE DEBUG 'import_rule_resolved_nwobj 0a all obj of a rule - i_mgm_id=%, i_rule_id=%, i_old_obj_id=%, i_new_obj_id=%, i_current_import_id=%, c_action=%, c_changelog_table=%', 
                i_mgm_id, i_rule_id, i_old_obj_id, i_new_obj_id, i_current_import_id, c_action, c_changelog_table;
            FOR i_matching_obj_id IN
                SELECT obj_id FROM rule_nwobj_resolved WHERE mgm_id=i_mgm_id AND rule_id=i_rule_id AND removed IS NULL
            LOOP
                RAISE DEBUG 'import_rule_resolved_nwobj 0b obj loop - i_mgm_id=%, i_rule_id=%, i_old_obj_id=%, i_new_obj_id=%, i_current_import_id=%, c_action=%, c_changelog_table=%', 
                    i_mgm_id, i_rule_id, i_matching_obj_id, NULL, i_current_import_id, c_action, c_changelog_table;
                PERFORM import_rule_resolved_nwobj(i_mgm_id, i_rule_id, i_matching_obj_id, NULL, i_current_import_id, 'D', c_changelog_table);
            END LOOP;

        -- handle ocurrences in all rules, if rule_id is NULL
        ELSIF i_rule_id IS NULL THEN
            RAISE DEBUG 'import_rule_resolved_nwobj 0c no rule - i_mgm_id=%, i_rule_id=%, i_old_obj_id=%, i_new_obj_id=%, i_current_import_id=%, c_action=%, c_changelog_table=%', 
                i_mgm_id, i_rule_id, i_old_obj_id, i_new_obj_id, i_current_import_id, c_action, c_changelog_table;
            IF NOT c_action='I' THEN -- no valid case, cannot insert everywhere, using C to replace old with new obj ref
                FOR i_matching_rule_id IN
                    SELECT rule_id FROM rule_nwobj_resolved WHERE mgm_id=i_mgm_id AND obj_id=i_old_obj_id AND removed IS NULL
                LOOP
                    RAISE DEBUG 'import_rule_resolved_nwobj 0d rule loop - i_mgm_id=%, i_rule_id=%, i_old_obj_id=%, i_new_obj_id=%, i_current_import_id=%, c_action=%, c_changelog_table=%', 
                        i_mgm_id, i_rule_id, i_old_obj_id, i_new_obj_id, i_current_import_id, c_action, c_changelog_table;
                    PERFORM import_rule_resolved_nwobj(i_mgm_id, i_matching_rule_id, i_old_obj_id, i_new_obj_id, i_current_import_id, c_action, c_changelog_table);
                END LOOP;        
            END IF;

        -- standard case: both one obj_id and rule_id are given:
        ELSE
            RAISE DEBUG 'import_rule_resolved_nwobj 1 ELSE enter - i_mgm_id=%, i_rule_id=%, i_old_obj_id=%, i_new_obj_id=%, i_current_import_id=%, c_action=%, c_changelog_table=%', 
                i_mgm_id, i_rule_id, i_old_obj_id, i_new_obj_id, i_current_import_id, c_action, c_changelog_table;
            IF c_action = 'I' THEN
                SELECT INTO r_null * FROM rule_nwobj_resolved WHERE mgm_id=i_mgm_id AND rule_id=i_rule_id AND obj_id=i_new_obj_id AND created=i_current_import_id;
                RAISE DEBUG 'import_rule_resolved_nwobj 1 insert - i_mgm_id=%, i_rule_id=%, i_old_obj_id=%, i_new_obj_id=%, i_current_import_id=%, c_action=%, c_changelog_table=%', 
                    i_mgm_id, i_rule_id, i_old_obj_id, i_new_obj_id, i_current_import_id, c_action, c_changelog_table;
                IF NOT FOUND THEN
                    RAISE DEBUG 'import_rule_resolved_nwobj 1a insert - i_mgm_id=%, i_rule_id=%, i_old_obj_id=%, i_new_obj_id=%, i_current_import_id=%, c_action=%, c_changelog_table=%', 
                        i_mgm_id, i_rule_id, i_old_obj_id, i_new_obj_id, i_current_import_id, c_action, c_changelog_table;
                    INSERT INTO rule_nwobj_resolved (mgm_id, rule_id, obj_id, created) VALUES (i_mgm_id, i_rule_id, i_new_obj_id, i_current_import_id);
                END IF;
            ELSIF c_action = 'D' THEN
                RAISE DEBUG 'import_rule_resolved_nwobj 2 delete - i_mgm_id=%, i_rule_id=%, i_old_obj_id=%, i_new_obj_id=%, i_current_import_id=%, c_action=%, c_changelog_table=%', 
                    i_mgm_id, i_rule_id, i_old_obj_id, i_new_obj_id, i_current_import_id, c_action, c_changelog_table;
                UPDATE rule_nwobj_resolved SET removed=i_current_import_id WHERE rule_id=i_rule_id AND obj_id=i_old_obj_id AND removed IS NULL;
            ELSIF c_action = 'C' THEN
                RAISE DEBUG 'import_rule_resolved_nwobj 3 change - i_mgm_id=%, i_rule_id=%, i_old_obj_id=%, i_new_obj_id=%, i_current_import_id=%, c_action=%, c_changelog_table=%', 
                    i_mgm_id, i_rule_id, i_old_obj_id, i_new_obj_id, i_current_import_id, c_action, c_changelog_table;
                IF NOT i_old_obj_id IS NULL THEN
                    UPDATE rule_nwobj_resolved SET removed=i_current_import_id WHERE rule_id=i_rule_id AND obj_id=i_old_obj_id AND removed IS NULL;
                END IF;
                SELECT INTO r_null * FROM rule_nwobj_resolved WHERE mgm_id=i_mgm_id AND rule_id=i_rule_id AND obj_id=i_new_obj_id; -- AND created=i_current_import_id;
                IF NOT FOUND THEN
                    RAISE DEBUG 'import_rule_resolved_nwobj 3a change - insert - i_mgm_id=%, i_rule_id=%, i_old_obj_id=%, i_new_obj_id=%, i_current_import_id=%, c_action=%, c_changelog_table=%', 
                        i_mgm_id, i_rule_id, i_old_obj_id, i_new_obj_id, i_current_import_id, c_action, c_changelog_table;
                    IF NOT i_new_obj_id IS NULL THEN
                        INSERT INTO rule_nwobj_resolved (mgm_id, rule_id, obj_id, created) VALUES (i_mgm_id, i_rule_id, i_new_obj_id, i_current_import_id);
                    END IF;
                END IF;
            END IF;

            -- if the new object is a group, deal with all flat group members
            IF is_obj_group(i_new_obj_id) THEN -- treat all group members as well
                RAISE DEBUG 'import_rule_resolved_nwobj 4 group - i_mgm_id=%, i_rule_id=%, i_old_obj_id=%, i_new_obj_id=%, i_current_import_id=%, c_action=%, c_changelog_table=%', 
                    i_mgm_id, i_rule_id, i_old_obj_id, i_new_obj_id, i_current_import_id, c_action, c_changelog_table;
                FOR i_member IN
                        SELECT objgrp_flat_member_id FROM objgrp_flat WHERE objgrp_flat_id=i_new_obj_id
                    LOOP
                        RAISE DEBUG 'import_rule_resolved_nwobj 4a group loop - i_mgm_id=%, i_rule_id=%, i_old_obj_id=%, i_new_obj_id=%, i_current_import_id=%, c_action=%, c_changelog_table=%', 
                            i_mgm_id, i_rule_id, i_old_obj_id, i_new_obj_id, i_current_import_id, c_action, c_changelog_table;
                        IF i_new_obj_id <> i_member THEN 
                            IF c_action = 'I' THEN -- insert new obj
                                PERFORM import_rule_resolved_nwobj(i_mgm_id, i_rule_id, NULL, i_member, i_current_import_id, c_action, c_changelog_table);
                            ELSIF c_action = 'D' OR c_action = 'C' THEN -- mark old obj as removed
                                PERFORM import_rule_resolved_nwobj(i_mgm_id, i_rule_id, i_member, NULL, i_current_import_id, c_action, c_changelog_table);
                            END IF;
                        END IF;
                    END LOOP;
            END IF;
        END IF;
    EXCEPTION
        WHEN others THEN
            raise EXCEPTION 'import_rule_resolved_nwobj uncommittable state. ERROR %: %', SQLSTATE, SQLERRM;    
    END;
	RETURN;
END;
$$ LANGUAGE plpgsql;


CREATE OR REPLACE FUNCTION import_rule_resolved_svc (INT,BIGINT,BIGINT,BIGINT,BIGINT,CHAR,CHAR) RETURNS VOID AS $$
DECLARE
	i_mgm_id ALIAS FOR $1;
	i_rule_id ALIAS FOR $2;
	i_old_obj_id ALIAS FOR $3;
	i_new_obj_id ALIAS FOR $4;
	i_current_import_id ALIAS FOR $5;
    c_action ALIAS FOR $6;
    c_changelog_table ALIAS FOR $7;
    r_null RECORD;
    i_member BIGINT;
    i_matching_rule_id BIGINT;
    i_matching_obj_id BIGINT;
    r_search RECORD;
BEGIN
    BEGIN -- catch
        RAISE DEBUG 'import_rule_resolved_svc 0 enter - i_mgm_id=%, i_rule_id=%, i_old_obj_id=%, i_new_obj_id=%, i_current_import_id=%, c_action=%, c_changelog_table=%', 
            i_mgm_id, i_rule_id, i_old_obj_id, i_new_obj_id, i_current_import_id, c_action, c_changelog_table;

        -- rule has been deleted resp. changed, marking all existing rule/obj refs as removed
        IF i_old_obj_id IS NULL AND i_new_obj_id IS NULL AND NOT i_rule_id IS NULL AND NOT c_action='I' THEN  
            -- handle ocurrences in all rules, if rule_id is NULL
            RAISE DEBUG 'import_rule_resolved_svc 0a all obj of a rule - i_mgm_id=%, i_rule_id=%, i_old_obj_id=%, i_new_obj_id=%, i_current_import_id=%, c_action=%, c_changelog_table=%', 
                i_mgm_id, i_rule_id, i_old_obj_id, i_new_obj_id, i_current_import_id, c_action, c_changelog_table;
            FOR i_matching_obj_id IN
                SELECT svc_id FROM rule_svc_resolved WHERE mgm_id=i_mgm_id AND rule_id=i_rule_id AND removed IS NULL
            LOOP
                RAISE DEBUG 'import_rule_resolved_svc 0b obj loop - i_mgm_id=%, i_rule_id=%, i_old_obj_id=%, i_new_obj_id=%, i_current_import_id=%, c_action=%, c_changelog_table=%', 
                    i_mgm_id, i_rule_id, i_matching_obj_id, NULL, i_current_import_id, c_action, c_changelog_table;
                PERFORM import_rule_resolved_svc(i_mgm_id, i_rule_id, i_matching_obj_id, NULL, i_current_import_id, 'D', c_changelog_table);
            END LOOP;

        -- handle ocurrences in all rules, if rule_id is NULL
        ELSIF i_rule_id IS NULL THEN
            RAISE DEBUG 'import_rule_resolved_svc 0c no rule - i_mgm_id=%, i_rule_id=%, i_old_obj_id=%, i_new_obj_id=%, i_current_import_id=%, c_action=%, c_changelog_table=%', 
                i_mgm_id, i_rule_id, i_old_obj_id, i_new_obj_id, i_current_import_id, c_action, c_changelog_table;
            IF NOT c_action='I' THEN -- no valid case, cannot insert everywhere, using C to replace old with new obj ref
                FOR i_matching_rule_id IN
                    SELECT rule_id FROM rule_svc_resolved WHERE mgm_id=i_mgm_id AND svc_id=i_old_obj_id AND removed IS NULL
                LOOP
                    RAISE DEBUG 'import_rule_resolved_svc 0d rule loop - i_mgm_id=%, i_rule_id=%, i_old_obj_id=%, i_new_obj_id=%, i_current_import_id=%, c_action=%, c_changelog_table=%', 
                        i_mgm_id, i_rule_id, i_old_obj_id, i_new_obj_id, i_current_import_id, c_action, c_changelog_table;
                    PERFORM import_rule_resolved_svc(i_mgm_id, i_matching_rule_id, i_old_obj_id, i_new_obj_id, i_current_import_id, c_action, c_changelog_table);
                END LOOP;        
            END IF;

        -- standard case: both one obj_id and rule_id are given:
        ELSE
            RAISE DEBUG 'import_rule_resolved_svc 1 ELSE enter - i_mgm_id=%, i_rule_id=%, i_old_obj_id=%, i_new_obj_id=%, i_current_import_id=%, c_action=%, c_changelog_table=%', 
                i_mgm_id, i_rule_id, i_old_obj_id, i_new_obj_id, i_current_import_id, c_action, c_changelog_table;
            IF c_action = 'I' THEN
                SELECT INTO r_null * FROM rule_svc_resolved WHERE mgm_id=i_mgm_id AND rule_id=i_rule_id AND svc_id=i_new_obj_id AND created=i_current_import_id;
                RAISE DEBUG 'import_rule_resolved_svc 1 insert - i_mgm_id=%, i_rule_id=%, i_old_obj_id=%, i_new_obj_id=%, i_current_import_id=%, c_action=%, c_changelog_table=%', 
                    i_mgm_id, i_rule_id, i_old_obj_id, i_new_obj_id, i_current_import_id, c_action, c_changelog_table;
                IF NOT FOUND THEN
                    RAISE DEBUG 'import_rule_resolved_svc 1a insert - i_mgm_id=%, i_rule_id=%, i_old_obj_id=%, i_new_obj_id=%, i_current_import_id=%, c_action=%, c_changelog_table=%', 
                        i_mgm_id, i_rule_id, i_old_obj_id, i_new_obj_id, i_current_import_id, c_action, c_changelog_table;
                    INSERT INTO rule_svc_resolved (mgm_id, rule_id, svc_id, created) VALUES (i_mgm_id, i_rule_id, i_new_obj_id, i_current_import_id);
                END IF;
            ELSIF c_action = 'D' THEN
                RAISE DEBUG 'import_rule_resolved_svc 2 delete - i_mgm_id=%, i_rule_id=%, i_old_obj_id=%, i_new_obj_id=%, i_current_import_id=%, c_action=%, c_changelog_table=%', 
                    i_mgm_id, i_rule_id, i_old_obj_id, i_new_obj_id, i_current_import_id, c_action, c_changelog_table;
                UPDATE rule_svc_resolved SET removed=i_current_import_id WHERE rule_id=i_rule_id AND svc_id=i_old_obj_id AND removed IS NULL;
            ELSIF c_action = 'C' THEN
                RAISE DEBUG 'import_rule_resolved_svc 3 change - i_mgm_id=%, i_rule_id=%, i_old_obj_id=%, i_new_obj_id=%, i_current_import_id=%, c_action=%, c_changelog_table=%', 
                    i_mgm_id, i_rule_id, i_old_obj_id, i_new_obj_id, i_current_import_id, c_action, c_changelog_table;
                IF NOT i_old_obj_id IS NULL THEN
                    UPDATE rule_svc_resolved SET removed=i_current_import_id WHERE rule_id=i_rule_id AND svc_id=i_old_obj_id AND removed IS NULL;
                END IF;
                SELECT INTO r_null * FROM rule_svc_resolved WHERE mgm_id=i_mgm_id AND rule_id=i_rule_id AND svc_id=i_new_obj_id; -- AND created=i_current_import_id;
                IF NOT FOUND THEN
                    RAISE DEBUG 'import_rule_resolved_svc 3a change - insert - i_mgm_id=%, i_rule_id=%, i_old_obj_id=%, i_new_obj_id=%, i_current_import_id=%, c_action=%, c_changelog_table=%', 
                        i_mgm_id, i_rule_id, i_old_obj_id, i_new_obj_id, i_current_import_id, c_action, c_changelog_table;
                    IF NOT i_new_obj_id IS NULL THEN
                        INSERT INTO rule_svc_resolved (mgm_id, rule_id, svc_id, created) VALUES (i_mgm_id, i_rule_id, i_new_obj_id, i_current_import_id);
                    END IF;
                END IF;
            END IF;

            -- if the new object is a group, deal with all flat group members
            IF is_svc_group(i_new_obj_id) THEN -- treat all group members as well
                RAISE DEBUG 'import_rule_resolved_svc 4 group - i_mgm_id=%, i_rule_id=%, i_old_obj_id=%, i_new_obj_id=%, i_current_import_id=%, c_action=%, c_changelog_table=%', 
                    i_mgm_id, i_rule_id, i_old_obj_id, i_new_obj_id, i_current_import_id, c_action, c_changelog_table;
                FOR i_member IN
                        SELECT svcgrp_flat_member_id FROM svcgrp_flat WHERE svcgrp_flat_id=i_new_obj_id
                    LOOP
                        RAISE DEBUG 'import_rule_resolved_svc 4a group loop - i_mgm_id=%, i_rule_id=%, i_old_obj_id=%, i_new_obj_id=%, i_current_import_id=%, c_action=%, c_changelog_table=%', 
                            i_mgm_id, i_rule_id, i_old_obj_id, i_new_obj_id, i_current_import_id, c_action, c_changelog_table;
                        IF i_new_obj_id <> i_member THEN 
                            IF c_action = 'I' THEN -- insert new obj
                                PERFORM import_rule_resolved_svc(i_mgm_id, i_rule_id, NULL, i_member, i_current_import_id, c_action, c_changelog_table);
                            ELSIF c_action = 'D' OR c_action = 'C' THEN -- mark old obj as removed
                                PERFORM import_rule_resolved_svc(i_mgm_id, i_rule_id, i_member, NULL, i_current_import_id, c_action, c_changelog_table);
                            END IF;
                        END IF;
                    END LOOP;
            END IF;
        END IF;
    EXCEPTION
        WHEN others THEN
            raise EXCEPTION 'import_rule_resolved_svc uncommittable state. ERROR %: %', SQLSTATE, SQLERRM;    
    END;
	RETURN;
END;
$$ LANGUAGE plpgsql;


CREATE OR REPLACE FUNCTION import_rule_resolved_usr (INT,BIGINT,BIGINT,BIGINT,BIGINT,CHAR,CHAR) RETURNS VOID AS $$
DECLARE
	i_mgm_id ALIAS FOR $1;
	i_rule_id ALIAS FOR $2;
	i_old_obj_id ALIAS FOR $3;
	i_new_obj_id ALIAS FOR $4;
	i_current_import_id ALIAS FOR $5;
    c_action ALIAS FOR $6;
    c_changelog_table ALIAS FOR $7;
    r_null RECORD;
    i_member BIGINT;
    i_matching_rule_id BIGINT;
    i_matching_obj_id BIGINT;
    r_search RECORD;
BEGIN
    BEGIN -- catch
        RAISE DEBUG 'import_rule_resolved_usr 0 enter - i_mgm_id=%, i_rule_id=%, i_old_obj_id=%, i_new_obj_id=%, i_current_import_id=%, c_action=%, c_changelog_table=%', 
            i_mgm_id, i_rule_id, i_old_obj_id, i_new_obj_id, i_current_import_id, c_action, c_changelog_table;

        -- rule has been deleted resp. changed, marking all existing rule/obj refs as removed
        IF i_old_obj_id IS NULL AND i_new_obj_id IS NULL AND NOT i_rule_id IS NULL AND NOT c_action='I' THEN  
            -- handle ocurrences in all rules, if rule_id is NULL
            RAISE DEBUG 'import_rule_resolved_usr 0a all obj of a rule - i_mgm_id=%, i_rule_id=%, i_old_obj_id=%, i_new_obj_id=%, i_current_import_id=%, c_action=%, c_changelog_table=%', 
                i_mgm_id, i_rule_id, i_old_obj_id, i_new_obj_id, i_current_import_id, c_action, c_changelog_table;
            FOR i_matching_obj_id IN
                SELECT user_id FROM rule_user_resolved WHERE mgm_id=i_mgm_id AND rule_id=i_rule_id AND removed IS NULL
            LOOP
                RAISE DEBUG 'import_rule_resolved_usr 0b obj loop - i_mgm_id=%, i_rule_id=%, i_old_obj_id=%, i_new_obj_id=%, i_current_import_id=%, c_action=%, c_changelog_table=%', 
                    i_mgm_id, i_rule_id, i_matching_obj_id, NULL, i_current_import_id, c_action, c_changelog_table;
                PERFORM import_rule_resolved_usr(i_mgm_id, i_rule_id, i_matching_obj_id, NULL, i_current_import_id, 'D', c_changelog_table);
            END LOOP;

        -- handle ocurrences in all rules, if rule_id is NULL
        ELSIF i_rule_id IS NULL THEN
            RAISE DEBUG 'import_rule_resolved_usr 0c no rule - i_mgm_id=%, i_rule_id=%, i_old_obj_id=%, i_new_obj_id=%, i_current_import_id=%, c_action=%, c_changelog_table=%', 
                i_mgm_id, i_rule_id, i_old_obj_id, i_new_obj_id, i_current_import_id, c_action, c_changelog_table;
            IF NOT c_action='I' THEN -- no valid case, cannot insert everywhere, using C to replace old with new obj ref
                FOR i_matching_rule_id IN
                    SELECT rule_id FROM rule_user_resolved WHERE mgm_id=i_mgm_id AND user_id=i_old_obj_id AND removed IS NULL
                LOOP
                    RAISE DEBUG 'import_rule_resolved_usr 0d rule loop - i_mgm_id=%, i_rule_id=%, i_old_obj_id=%, i_new_obj_id=%, i_current_import_id=%, c_action=%, c_changelog_table=%', 
                        i_mgm_id, i_rule_id, i_old_obj_id, i_new_obj_id, i_current_import_id, c_action, c_changelog_table;
                    PERFORM import_rule_resolved_usr(i_mgm_id, i_matching_rule_id, i_old_obj_id, i_new_obj_id, i_current_import_id, c_action, c_changelog_table);
                END LOOP;        
            END IF;

        -- standard case: both one obj_id and rule_id are given:
        ELSE
            RAISE DEBUG 'import_rule_resolved_usr 1 ELSE enter - i_mgm_id=%, i_rule_id=%, i_old_obj_id=%, i_new_obj_id=%, i_current_import_id=%, c_action=%, c_changelog_table=%', 
                i_mgm_id, i_rule_id, i_old_obj_id, i_new_obj_id, i_current_import_id, c_action, c_changelog_table;
            IF c_action = 'I' THEN
                SELECT INTO r_null * FROM rule_user_resolved WHERE mgm_id=i_mgm_id AND rule_id=i_rule_id AND user_id=i_new_obj_id AND created=i_current_import_id;
                RAISE DEBUG 'import_rule_resolved_usr 1 insert - i_mgm_id=%, i_rule_id=%, i_old_obj_id=%, i_new_obj_id=%, i_current_import_id=%, c_action=%, c_changelog_table=%', 
                    i_mgm_id, i_rule_id, i_old_obj_id, i_new_obj_id, i_current_import_id, c_action, c_changelog_table;
                IF NOT FOUND THEN
                    RAISE DEBUG 'import_rule_resolved_usr 1a insert - i_mgm_id=%, i_rule_id=%, i_old_obj_id=%, i_new_obj_id=%, i_current_import_id=%, c_action=%, c_changelog_table=%', 
                        i_mgm_id, i_rule_id, i_old_obj_id, i_new_obj_id, i_current_import_id, c_action, c_changelog_table;
                    INSERT INTO rule_user_resolved (mgm_id, rule_id, user_id, created) VALUES (i_mgm_id, i_rule_id, i_new_obj_id, i_current_import_id);
                END IF;
            ELSIF c_action = 'D' THEN
                RAISE DEBUG 'import_rule_resolved_usr 2 delete - i_mgm_id=%, i_rule_id=%, i_old_obj_id=%, i_new_obj_id=%, i_current_import_id=%, c_action=%, c_changelog_table=%', 
                    i_mgm_id, i_rule_id, i_old_obj_id, i_new_obj_id, i_current_import_id, c_action, c_changelog_table;
                UPDATE rule_user_resolved SET removed=i_current_import_id WHERE rule_id=i_rule_id AND userc_id=i_old_obj_id AND removed IS NULL;
            ELSIF c_action = 'C' THEN
                RAISE DEBUG 'import_rule_resolved_usr 3 change - i_mgm_id=%, i_rule_id=%, i_old_obj_id=%, i_new_obj_id=%, i_current_import_id=%, c_action=%, c_changelog_table=%', 
                    i_mgm_id, i_rule_id, i_old_obj_id, i_new_obj_id, i_current_import_id, c_action, c_changelog_table;
                IF NOT i_old_obj_id IS NULL THEN
                    UPDATE rule_user_resolved SET removed=i_current_import_id WHERE rule_id=i_rule_id AND user_id=i_old_obj_id AND removed IS NULL;
                END IF;
                SELECT INTO r_null * FROM rule_user_resolved WHERE mgm_id=i_mgm_id AND rule_id=i_rule_id AND user_id=i_new_obj_id; -- AND created=i_current_import_id;
                IF NOT FOUND THEN
                    RAISE DEBUG 'import_rule_resolved_usr 3a change - insert - i_mgm_id=%, i_rule_id=%, i_old_obj_id=%, i_new_obj_id=%, i_current_import_id=%, c_action=%, c_changelog_table=%', 
                        i_mgm_id, i_rule_id, i_old_obj_id, i_new_obj_id, i_current_import_id, c_action, c_changelog_table;
                    IF NOT i_new_obj_id IS NULL THEN
                        INSERT INTO rule_user_resolved (mgm_id, rule_id, user_id, created) VALUES (i_mgm_id, i_rule_id, i_new_obj_id, i_current_import_id);
                    END IF;
                END IF;
            END IF;

            -- if the new object is a group, deal with all flat group members
            IF is_user_group(i_new_obj_id) THEN -- treat all group members as well
                RAISE DEBUG 'import_rule_resolved_usr 4 group - i_mgm_id=%, i_rule_id=%, i_old_obj_id=%, i_new_obj_id=%, i_current_import_id=%, c_action=%, c_changelog_table=%', 
                    i_mgm_id, i_rule_id, i_old_obj_id, i_new_obj_id, i_current_import_id, c_action, c_changelog_table;
                FOR i_member IN
                        SELECT usergrp_flat_member_id FROM usergrp_flat WHERE usergrp_flat_id=i_new_obj_id
                    LOOP
                        RAISE DEBUG 'import_rule_resolved_usr 4a group loop - i_mgm_id=%, i_rule_id=%, i_old_obj_id=%, i_new_obj_id=%, i_current_import_id=%, c_action=%, c_changelog_table=%', 
                            i_mgm_id, i_rule_id, i_old_obj_id, i_new_obj_id, i_current_import_id, c_action, c_changelog_table;
                        IF i_new_obj_id <> i_member THEN 
                            IF c_action = 'I' THEN -- insert new obj
                                PERFORM import_rule_resolved_usr(i_mgm_id, i_rule_id, NULL, i_member, i_current_import_id, c_action, c_changelog_table);
                            ELSIF c_action = 'D' OR c_action = 'C' THEN -- mark old obj as removed
                                PERFORM import_rule_resolved_usr(i_mgm_id, i_rule_id, i_member, NULL, i_current_import_id, c_action, c_changelog_table);
                            END IF;
                        END IF;
                    END LOOP;
            END IF;
        END IF;
    EXCEPTION
        WHEN others THEN
            raise EXCEPTION 'import_rule_resolved_usr uncommittable state. ERROR %: %', SQLSTATE, SQLERRM;    
    END;
	RETURN;
END;
$$ LANGUAGE plpgsql;
