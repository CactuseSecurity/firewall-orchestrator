-- $Id: 20070731-correct-xxxgrp_flat_bug.sql,v 1.1.2.2 2007-12-13 10:47:31 tim Exp $
-- $Source: /home/cvs/iso/package/install/migration/Attic/20070731-correct-xxxgrp_flat_bug.sql,v $

-- Zweck:     Alle _flat-Gruppen neu generieren (wg. Fehler in Import-Prozess)

-- create correction functions

---------- network object funtions -------------------------------------------------------------------------------------------------------------------------------

CREATE OR REPLACE FUNCTION correct_flat_groups_nwobj_main () RETURNS VOID AS $$
DECLARE
	r_obj 	RECORD;	-- temp object
	r_nwobj_info	RECORD;	-- temp object	
	i_obj_id INTEGER;
BEGIN
	DELETE FROM objgrp_flat; -- 1) alte memberbeziehungen löschen
	FOR r_obj IN
		SELECT obj_id FROM object
	LOOP
		i_obj_id := r_obj.obj_id;
		SELECT INTO r_nwobj_info mgm_id, obj_member_refs, obj_last_seen, obj_create, active FROM object WHERE obj_id=i_obj_id;
		IF is_obj_group(i_obj_id) THEN -- wenn Gruppe, dann werden die Beziehungen zwischen Gruppe und Mitgliedern eingetragen
--			2) neue Member-Beziehungen von i_new_id korrekt eintragen
			PERFORM correct_flat_groups_nwobj_add_group (i_obj_id, i_obj_id, 0, r_nwobj_info.obj_create, r_nwobj_info.obj_last_seen, r_nwobj_info.active);
		END IF;
		-- add self for all objects, not only groups (makes filtering by ip within the DB easier)
		INSERT INTO objgrp_flat (objgrp_flat_id,objgrp_flat_member_id,import_created,import_last_seen, active)
			VALUES (i_obj_id, i_obj_id, r_nwobj_info.obj_create, r_nwobj_info.obj_last_seen, r_nwobj_info.active);
	END LOOP;
	RETURN;
END; 
$$ LANGUAGE plpgsql;

CREATE OR REPLACE FUNCTION correct_flat_groups_nwobj_add_group (INTEGER,INTEGER,INTEGER,INTEGER,INTEGER,BOOLEAN) RETURNS VOID AS $$
DECLARE
    i_top_group_id	ALIAS FOR $1;
    i_group_id		ALIAS FOR $2;
    i_rec_level		ALIAS FOR $3;
	i_obj_create	ALIAS FOR $4;
	i_obj_last_seen	ALIAS FOR $5;
	b_active		ALIAS FOR $6;
    r_member		RECORD;
    r_obj			RECORD;
    r_member_exists	RECORD;
BEGIN 
	IF i_rec_level>20 THEN
		PERFORM error_handling('ERR_OBJ_GROUP_REC_LVL_EXCEEDED');
	END IF;
	FOR r_member IN
		SELECT objgrp_member_id FROM objgrp WHERE objgrp_id=i_group_id
	LOOP
		SELECT INTO r_member_exists * FROM objgrp_flat WHERE
            objgrp_flat_id=i_top_group_id AND objgrp_flat_member_id=r_member.objgrp_member_id
            AND objgrp_flat.import_created=i_obj_create AND objgrp_flat.import_last_seen=i_obj_last_seen AND active=b_active;
        IF NOT FOUND THEN
			INSERT INTO objgrp_flat (objgrp_flat_id,objgrp_flat_member_id,import_created,import_last_seen, active)
				VALUES (i_top_group_id, r_member.objgrp_member_id, i_obj_create, i_obj_last_seen, b_active);
			IF is_obj_group(r_member.objgrp_member_id) THEN
				PERFORM correct_flat_groups_nwobj_add_group
					(i_top_group_id, r_member.objgrp_member_id, i_rec_level+1, i_obj_create, i_obj_last_seen, b_active);
			END IF;
		ELSE
		END IF;
	END LOOP;
	RETURN;
END; 
$$ LANGUAGE plpgsql;

------------ service functions -----------------------------------------------------------------------------------------------------------------------------------

CREATE OR REPLACE FUNCTION correct_flat_groups_svc_main () RETURNS VOID AS $$
DECLARE
	r_obj 	RECORD;	-- temp object
	r_obj_info	RECORD;	-- temp object	
	i_obj_id INTEGER;
BEGIN
	DELETE FROM svcgrp_flat; -- 1) alte memberbeziehungen löschen
	FOR r_obj IN 
		SELECT svc_id FROM service
	LOOP
		i_obj_id := r_obj.svc_id;
		SELECT INTO r_obj_info mgm_id, svc_member_refs, svc_last_seen, svc_create, active FROM service WHERE svc_id=i_obj_id;
		IF is_svc_group(i_obj_id) THEN -- wenn Gruppe, dann werden die Beziehungen zwischen Gruppe und Mitgliedern eingetragen
--			2) neue Member-Beziehungen der top-level-Gruppe i_obj_id korrekt eintragen
			PERFORM correct_flat_groups_svc_add_group (i_obj_id, i_obj_id, 0, r_obj_info.svc_create, r_obj_info.svc_last_seen, r_obj_info.active);
		END IF;
		-- add self for all services, not only groups (makes filtering within the DB easier)
		INSERT INTO svcgrp_flat (svcgrp_flat_id,svcgrp_flat_member_id,import_created,import_last_seen, active)
			VALUES (i_obj_id, i_obj_id, r_obj_info.svc_create, r_obj_info.svc_last_seen, r_obj_info.active);
	END LOOP;
	RETURN;
END; 
$$ LANGUAGE plpgsql;

CREATE OR REPLACE FUNCTION correct_flat_groups_svc_add_group (INTEGER,INTEGER,INTEGER,INTEGER,INTEGER,BOOLEAN) RETURNS VOID AS $$
DECLARE
    i_top_group_id	ALIAS FOR $1;
    i_group_id		ALIAS FOR $2;
    i_rec_level		ALIAS FOR $3;
	i_obj_create	ALIAS FOR $4;
	i_obj_last_seen	ALIAS FOR $5;
	b_active		ALIAS FOR $6;
    r_member		RECORD;
    r_obj			RECORD;
    r_member_exists	RECORD;
BEGIN 
	IF i_rec_level>20 THEN
		PERFORM error_handling('ERR_OBJ_GROUP_REC_LVL_EXCEEDED');
	END IF;
	FOR r_member IN
		SELECT svcgrp_member_id FROM svcgrp WHERE svcgrp_id=i_group_id
	LOOP
		SELECT INTO r_member_exists * FROM svcgrp_flat WHERE
            svcgrp_flat_id=i_top_group_id AND svcgrp_flat_member_id=r_member.svcgrp_member_id
            AND svcgrp_flat.import_created=i_obj_create AND svcgrp_flat.import_last_seen=i_obj_last_seen AND active=b_active;
        IF NOT FOUND THEN
			INSERT INTO svcgrp_flat (svcgrp_flat_id,svcgrp_flat_member_id,import_created,import_last_seen, active)
				VALUES (i_top_group_id, r_member.svcgrp_member_id, i_obj_create, i_obj_last_seen, b_active);
			IF is_svc_group(r_member.svcgrp_member_id) THEN
				PERFORM correct_flat_groups_svc_add_group
					(i_top_group_id, r_member.svcgrp_member_id, i_rec_level+1, i_obj_create, i_obj_last_seen, b_active);
			END IF;
		ELSE
		END IF;
	END LOOP;
	RETURN;
END; 
$$ LANGUAGE plpgsql;

------------- user functions -------------------------------------------------------------------------------------------------------------------------------------

CREATE OR REPLACE FUNCTION correct_flat_groups_usr_main () RETURNS VOID AS $$
DECLARE
	r_obj 	RECORD;	-- temp object
	r_obj_info	RECORD;	-- temp object	
	i_obj_id INTEGER;
BEGIN
	DELETE FROM usergrp_flat; -- 1) alte memberbeziehungen löschen
	FOR r_obj IN 
		SELECT user_id FROM usr
	LOOP
		i_obj_id := r_obj.user_id;
		SELECT INTO r_obj_info mgm_id, user_member_refs, user_last_seen, user_create, active FROM usr WHERE user_id=i_obj_id;
		IF is_user_group(i_obj_id) THEN -- wenn Gruppe, dann werden die Beziehungen zwischen Gruppe und Mitgliedern eingetragen
--			2) neue Member-Beziehungen der top-level-Gruppe i_obj_id korrekt eintragen
			PERFORM correct_flat_groups_usr_add_group (i_obj_id, i_obj_id, 0, r_obj_info.user_create, r_obj_info.user_last_seen, r_obj_info.active);
		END IF;
		-- add self for all services, not only groups (makes filtering within the DB easier)
		INSERT INTO usergrp_flat (usergrp_flat_id, usergrp_flat_member_id, import_created, import_last_seen, active)
			VALUES (i_obj_id, i_obj_id, r_obj_info.user_create, r_obj_info.user_last_seen, r_obj_info.active);
	END LOOP;
	RETURN;
END; 
$$ LANGUAGE plpgsql;

CREATE OR REPLACE FUNCTION correct_flat_groups_usr_add_group (INTEGER,INTEGER,INTEGER,INTEGER,INTEGER,BOOLEAN) RETURNS VOID AS $$
DECLARE
    i_top_group_id	ALIAS FOR $1;
    i_group_id		ALIAS FOR $2;
    i_rec_level		ALIAS FOR $3;
	i_obj_create	ALIAS FOR $4;
	i_obj_last_seen	ALIAS FOR $5;
	b_active		ALIAS FOR $6;
    r_member		RECORD;
    r_obj			RECORD;
    r_member_exists	RECORD;
BEGIN 
	IF i_rec_level>20 THEN
		PERFORM error_handling('ERR_OBJ_GROUP_REC_LVL_EXCEEDED');
	END IF;
	FOR r_member IN
		SELECT usergrp_member_id FROM usergrp WHERE usergrp_id=i_group_id
	LOOP
		SELECT INTO r_member_exists * FROM usergrp_flat WHERE
            usergrp_flat_id=i_top_group_id AND usergrp_flat_member_id=r_member.usergrp_member_id
            AND usergrp_flat.import_created=i_obj_create AND usergrp_flat.import_last_seen=i_obj_last_seen AND active=b_active;
        IF NOT FOUND THEN
			INSERT INTO usergrp_flat (usergrp_flat_id, usergrp_flat_member_id, import_created, import_last_seen, active)
				VALUES (i_top_group_id, r_member.usergrp_member_id, i_obj_create, i_obj_last_seen, b_active);
			IF is_user_group(r_member.usergrp_member_id) THEN
				PERFORM correct_flat_groups_usr_add_group
					(i_top_group_id, r_member.usergrp_member_id, i_rec_level+1, i_obj_create, i_obj_last_seen, b_active);
			END IF;
		ELSE
		END IF;
	END LOOP;
	RETURN;
END; 
$$ LANGUAGE plpgsql;

-- call main regroup function ------------------------------------------------------------------------------------------------------------------------------------
SELECT * FROM correct_flat_groups_nwobj_main ();
SELECT * FROM correct_flat_groups_svc_main ();
SELECT * FROM correct_flat_groups_usr_main ();

-- drop regroup functions after execution ------------------------------------------------------------------------------------------------------------------------
DROP FUNCTION correct_flat_groups_nwobj_main ();
DROP FUNCTION correct_flat_groups_nwobj_add_group (INTEGER,INTEGER,INTEGER,INTEGER,INTEGER,BOOLEAN);
DROP FUNCTION correct_flat_groups_svc_main ();
DROP FUNCTION correct_flat_groups_svc_add_group (INTEGER,INTEGER,INTEGER,INTEGER,INTEGER,BOOLEAN);
DROP FUNCTION correct_flat_groups_usr_main ();
DROP FUNCTION correct_flat_groups_usr_add_group (INTEGER,INTEGER,INTEGER,INTEGER,INTEGER,BOOLEAN);
