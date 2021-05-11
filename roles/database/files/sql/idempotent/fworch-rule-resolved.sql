
-- all functions around the rule_<obj>_resolved tables introduced with version 5.1.16 in May 2021
-- these functions add an entry to the respective rule_<objtype>_resolved table for each object contained in a rule
-- used for quick reporting of objects used in a reported ruleset

CREATE OR REPLACE FUNCTION import_rule_resolved_nwobj (INT,BIGINT,BIGINT) RETURNS VOID AS $$
DECLARE
	i_mgm_id ALIAS FOR $1;
	i_rule_id ALIAS FOR $2;
	i_nw_obj_id ALIAS FOR $3;
    i_member BIGINT;
    i_obj_id_searched BIGINT;
    r_search RECORD;
BEGIN 
    SELECT INTO r_search * FROM rule_nwobj_resolved WHERE mgm_id=i_mgm_id AND rule_id=i_rule_id AND obj_id=i_nw_obj_id;
    IF NOT FOUND THEN
        INSERT INTO rule_nwobj_resolved (mgm_id, rule_id, obj_id) VALUES (i_mgm_id, i_rule_id, i_nw_obj_id);
    END IF;
	IF is_obj_group(i_nw_obj_id) THEN -- add all members seperately
        FOR i_member IN
            SELECT objgrp_flat_member_id FROM objgrp_flat WHERE objgrp_flat_id=i_nw_obj_id
        LOOP
            IF i_nw_obj_id <> i_member THEN 
                PERFORM import_rule_resolved_nwobj(i_mgm_id, i_rule_id, i_member);
            END IF;
        END LOOP;
	END IF;
	RETURN;
END;
$$ LANGUAGE plpgsql;

CREATE OR REPLACE FUNCTION import_rule_resolved_svc (INT,BIGINT,BIGINT) RETURNS VOID AS $$
DECLARE
	i_mgm_id ALIAS FOR $1;
	i_rule_id ALIAS FOR $2;
	i_svc_id ALIAS FOR $3;
    i_member BIGINT;
    i_svc_id_searched BIGINT;
BEGIN 
    SELECT INTO i_svc_id_searched svc_id FROM rule_svc_resolved WHERE mgm_id=i_mgm_id AND rule_id=i_rule_id AND svc_id=i_svc_id;
    IF NOT FOUND THEN
        INSERT INTO rule_svc_resolved (mgm_id, rule_id, svc_id) VALUES (i_mgm_id, i_rule_id, i_svc_id);
    END IF;
	IF is_svc_group(i_svc_id) THEN -- add all flat group members seperately
        FOR i_member IN
            SELECT svcgrp_flat_member_id FROM svcgrp_flat WHERE svcgrp_flat_id=i_svc_id
        LOOP
            IF i_svc_id <> i_member THEN 
                PERFORM import_rule_resolved_svc(i_mgm_id, i_rule_id, i_member);
            END IF;
        END LOOP;
	END IF;
	RETURN;
END;
$$ LANGUAGE plpgsql;

CREATE OR REPLACE FUNCTION import_rule_resolved_usr (INT,BIGINT,BIGINT) RETURNS VOID AS $$
DECLARE
	i_mgm_id ALIAS FOR $1;
	i_rule_id ALIAS FOR $2;
    i_usr_id ALIAS FOR $3;
    i_member BIGINT;
    i_usr_id_searched BIGINT;
BEGIN 
    SELECT INTO i_usr_id_searched user_id FROM rule_user_resolved WHERE mgm_id=i_mgm_id AND rule_id=i_rule_id AND user_id=i_usr_id;
    IF NOT FOUND THEN
        INSERT INTO rule_user_resolved (mgm_id, rule_id, user_id) VALUES (i_mgm_id, i_rule_id, i_usr_id);
    END IF;
	IF is_user_group(i_usr_id) THEN -- add all flat group members seperately
        FOR i_member IN
            SELECT usergrp_flat_member_id FROM usergrp_flat WHERE usergrp_flat_id=i_usr_id
        LOOP
            IF i_usr_id <> i_member THEN 
                PERFORM import_rule_resolved_usr(i_mgm_id, i_rule_id,i_member);
            END IF;
        END LOOP;
	END IF;
	RETURN;
END;
$$ LANGUAGE plpgsql;
