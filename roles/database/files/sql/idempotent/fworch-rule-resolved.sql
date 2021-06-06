
-- all functions around the rule_<obj>_resolved tables introduced with version 5.1.16 in May 2021
-- these functions add an entry to the respective rule_<objtype>_resolved table for each object contained in a rule
-- used for quick reporting of objects used in a reported ruleset

CREATE OR REPLACE FUNCTION import_rule_resolved_nwobj (INT,BIGINT,BIGINT,BIGINT) RETURNS VOID AS $$
DECLARE
	i_mgm_id ALIAS FOR $1;
	i_rule_id ALIAS FOR $2;
	i_nw_obj_id ALIAS FOR $3;
	i_current_import_id ALIAS FOR $4;
    i_member BIGINT;
    i_obj_id_searched BIGINT;
    r_search RECORD;
BEGIN 
    SELECT INTO r_search * FROM rule_nwobj_resolved WHERE mgm_id=i_mgm_id AND rule_id=i_rule_id AND obj_id=i_nw_obj_id;
    IF NOT FOUND THEN
        INSERT INTO rule_nwobj_resolved (mgm_id, rule_id, obj_id, created) VALUES (i_mgm_id, i_rule_id, i_nw_obj_id, i_current_import_id, i_current_import_id);
    END IF;
	IF is_obj_group(i_nw_obj_id) THEN -- add all members seperately
        FOR i_member IN
            SELECT objgrp_flat_member_id FROM objgrp_flat WHERE objgrp_flat_id=i_nw_obj_id
        LOOP
            IF i_nw_obj_id <> i_member THEN 
                PERFORM import_rule_resolved_nwobj(i_mgm_id, i_rule_id, i_member, i_current_import_id);
            END IF;
        END LOOP;
	END IF;
	RETURN;
END;
$$ LANGUAGE plpgsql;

CREATE OR REPLACE FUNCTION import_rule_resolved_svc (INT,BIGINT,BIGINT,BIGINT) RETURNS VOID AS $$
DECLARE
	i_mgm_id ALIAS FOR $1;
	i_rule_id ALIAS FOR $2;
	i_svc_id ALIAS FOR $3;
	i_current_import_id ALIAS FOR $4;
    i_member BIGINT;
    i_svc_id_searched BIGINT;
BEGIN 
    SELECT INTO i_svc_id_searched svc_id FROM rule_svc_resolved WHERE mgm_id=i_mgm_id AND rule_id=i_rule_id AND svc_id=i_svc_id;
    IF NOT FOUND THEN
        INSERT INTO rule_svc_resolved (mgm_id, rule_id, svc_id, created) VALUES (i_mgm_id, i_rule_id, i_svc_id, i_current_import_id);
    END IF;
	IF is_svc_group(i_svc_id) THEN -- add all flat group members seperately
        FOR i_member IN
            SELECT svcgrp_flat_member_id FROM svcgrp_flat WHERE svcgrp_flat_id=i_svc_id
        LOOP
            IF i_svc_id <> i_member THEN 
                PERFORM import_rule_resolved_svc(i_mgm_id, i_rule_id, i_member, i_current_import_id);
            END IF;
        END LOOP;
	END IF;
	RETURN;
END;
$$ LANGUAGE plpgsql;

CREATE OR REPLACE FUNCTION import_rule_resolved_usr (INT,BIGINT,BIGINT,BIGINT) RETURNS VOID AS $$
DECLARE
	i_mgm_id ALIAS FOR $1;
	i_rule_id ALIAS FOR $2;
    i_usr_id ALIAS FOR $3;
	i_current_import_id ALIAS FOR $4;
    i_member BIGINT;
    i_usr_id_searched BIGINT;
BEGIN 
    SELECT INTO i_usr_id_searched user_id FROM rule_user_resolved WHERE mgm_id=i_mgm_id AND rule_id=i_rule_id AND user_id=i_usr_id;
    IF NOT FOUND THEN
        INSERT INTO rule_user_resolved (mgm_id, rule_id, user_id, created) VALUES (i_mgm_id, i_rule_id, i_usr_id, i_current_import_id);
    END IF;
	IF is_user_group(i_usr_id) THEN -- add all flat group members seperately
        FOR i_member IN
            SELECT usergrp_flat_member_id FROM usergrp_flat WHERE usergrp_flat_id=i_usr_id
        LOOP
            IF i_usr_id <> i_member THEN 
                PERFORM import_rule_resolved_usr(i_mgm_id, i_rule_id,i_member, i_current_import_id);
            END IF;
        END LOOP;
	END IF;
	RETURN;
END;
$$ LANGUAGE plpgsql;


----------------------------------------------------
-- FUNCTION:  import_nwobj_refhandler_change_rule_nwobj_resolved
-- Purpose:   change rule_nwobj_resolved refs of changed nw objects
-- Parameter: old_obj_id, new_obj_id, current_import_id
-- RETURNS:   VOID
CREATE OR REPLACE FUNCTION import_nwobj_refhandler_change_rule_nwobj_resolved (integer, BIGINT) RETURNS VOID AS $$
DECLARE
    i_mgm_id ALIAS FOR $1;
    i_current_import_id	ALIAS FOR $2;
    i_prev_import_id BIGINT;
	r_resolved rule_nwobj_resolved%ROWTYPE;
    i_obj_id BIGINT;
    i_rule_id BIGINT;
    r_rule_obj_pair RECORD;
BEGIN
    -- set previous import_id

    RAISE DEBUG 'import_nwobj_refhandler_change_rule_nwobj_resolved - start';
    i_prev_import_id := get_previous_import_id_for_mgmt(i_mgm_id, i_current_import_id);

    RAISE DEBUG 'import_nwobj_refhandler_change_rule_nwobj_resolved - 2, prev_import_id=%', cast(i_prev_import_id as VARCHAR);

    BEGIN
    -- mark only objects no longer in rule as removed
    -- resolved for a rule which are not related to any objects changes in current import:

        FOR r_rule_obj_pair IN
            SELECT rule_from.obj_id, rule_id FROM changelog_object 
                LEFT JOIN objgrp_flat ON (new_obj_id=objgrp_flat_member_id) 
                LEFT JOIN rule_from ON (rule_from.obj_id=objgrp_flat_member_id)
                -- TODO: rule_to is missing
                WHERE (change_action='D' OR change_action='C') AND control_id=i_current_import_id AND NOT rule_id IS NULL
        LOOP
            RAISE DEBUG 'import_nwobj_refhandler_change_rule_nwobj_resolved - rule_from loop';
            BEGIN
                UPDATE rule_nwobj_resolved SET removed=i_current_import_id 
                    WHERE rule_id=r_rule_obj_pair.rule_id AND r_rule_obj_pair.obj_id=obj_id;
            EXCEPTION
                WHEN others THEN
                    raise notice 'import_nwobj_refhandler_change_rule_nwobj_resolved - UPDATE removed fields - uncommittable state. Rolling back';
                    raise notice '% %', SQLERRM, SQLSTATE;    
            END;
        END LOOP;
    EXCEPTION
        WHEN others THEN
            raise notice 'import_nwobj_refhandler_change_rule_nwobj_resolved - rule_from LOOP - uncommittable state. Rolling back';
            raise notice '% %', SQLERRM, SQLSTATE;    
    END;

    -- BEGIN
    -- UPDATE rule_nwobj_resolved SET last_seen=i_current_import_id 
    --     WHERE 
    --         mgm_id=i_mgm_id AND removed IS NULL AND 
    --         NOT (obj_id IN (SELECT old_obj_id FROM changelog_object WHERE NOT old_obj_id IS NULL AND control_id=i_current_import_id AND mgm_id=i_mgm_id) 
    --             OR 
    --             obj_id IN (SELECT objgrp_flat_member_id FROM changelog_object LEFT JOIN objgrp_flat ON (old_obj_id=objgrp_flat_id) 
    --                 WHERE change_action <> 'I' AND control_id=i_current_import_id AND mgm_id=i_mgm_id));
    -- EXCEPTION
    --     WHEN others THEN
    --         raise notice 'import_nwobj_refhandler_change_rule_nwobj_resolved - UPDATE - uncommittable state. Rolling back';
    --         raise notice '% %', SQLERRM, SQLSTATE;    
    -- END;

    RAISE DEBUG 'import_nwobj_refhandler_change_rule_nwobj_resolved - 3';

    -- insert new object resolved rule relationships rule_from
    -- BEGIN
    --     FOR r_rule_obj_pair IN
    --         SELECT rule_from.obj_id, rule_id FROM changelog_object 
    --             LEFT JOIN objgrp_flat ON (new_obj_id=objgrp_flat_member_id) 
    --             LEFT JOIN rule_from ON (rule_from.obj_id=objgrp_flat_member_id)
    --             -- rule_to missing
    --             WHERE change_action<>'D' AND control_id=i_current_import_id AND NOT rule_id IS NULL
    --     LOOP
    --         RAISE DEBUG 'import_nwobj_refhandler_change_rule_nwobj_resolved - rule_from loop';
    --         BEGIN
    --             INSERT INTO rule_nwobj_resolved 
    --                 (mgm_id, rule_id, obj_id, created)
    --                 VALUES (i_mgm_id, r_rule_obj_pair.rule_id, r_rule_obj_pair.obj_id, i_current_import_id);
    --         EXCEPTION
    --             WHEN others THEN
    --                 raise notice 'import_nwobj_refhandler_change_rule_nwobj_resolved - INSERT rule_from - uncommittable state. Rolling back';
    --                 raise notice '% %', SQLERRM, SQLSTATE;    
    --         END;
    --     END LOOP;
    -- EXCEPTION
    --     WHEN others THEN
    --         raise notice 'import_nwobj_refhandler_change_rule_nwobj_resolved - rule_from LOOP - uncommittable state. Rolling back';
    --         raise notice '% %', SQLERRM, SQLSTATE;    
    -- END;

    -- -- insert new object resolved rule relationships rule_to



    RAISE DEBUG 'import_nwobj_refhandler_change_rule_nwobj_resolved - final';

	RETURN;
END;
$$ language plpgsql;
