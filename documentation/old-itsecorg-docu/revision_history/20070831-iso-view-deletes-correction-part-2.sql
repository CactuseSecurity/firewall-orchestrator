-- $Id: 20070831-iso-view-deletes-correction-part-2.sql,v 1.1.2.2 2007-12-13 10:47:32 tim Exp $
-- $Source: /home/cvs/iso/package/install/migration/20070831-view-corrections-for-deletes/Attic/20070831-iso-view-deletes-correction-part-2.sql,v $
										
CREATE OR REPLACE FUNCTION add_change_admin_for_rule_deletes () RETURNS VOID AS $$
DECLARE
	r_rulechange RECORD;
	i_change_admin INTEGER;
BEGIN
	FOR r_rulechange IN -- jeder changelog_rule Eintrag ohne import_admin wird versucht zu füllen
		SELECT log_rule_id, dev_id, control_id FROM changelog_rule WHERE import_admin IS NULL AND change_action='D'
	LOOP
		i_change_admin := get_last_change_admin_of_rulebase_change(r_rulechange.control_id, r_rulechange.dev_id);
		UPDATE changelog_rule SET import_admin = i_change_admin WHERE log_rule_id=r_rulechange.log_rule_id;
	END LOOP;
	RETURN;
END; 
$$ LANGUAGE plpgsql;

-- run update on changelog_rule
SELECT * FROM add_change_admin_for_rule_deletes();

DROP FUNCTION add_change_admin_for_rule_deletes();

---

CREATE OR REPLACE FUNCTION add_change_admin_for_obj_deletes () RETURNS VOID AS $$
DECLARE
	r_objchange RECORD;
	i_change_admin INTEGER;
BEGIN
	FOR r_objchange IN -- jeder changelog_object Eintrag ohne import_admin wird versucht zu füllen
		SELECT log_obj_id, control_id FROM changelog_object WHERE import_admin IS NULL AND change_action='D'
	LOOP
		i_change_admin := get_last_change_admin_of_obj_delete (r_objchange.control_id);
		UPDATE changelog_object SET import_admin = i_change_admin WHERE log_obj_id=r_objchange.log_obj_id;
	END LOOP;
	FOR r_objchange IN -- jeder changelog_service Eintrag ohne import_admin wird versucht zu füllen
		SELECT log_svc_id, control_id FROM changelog_service WHERE import_admin IS NULL AND change_action='D'
	LOOP
		i_change_admin := get_last_change_admin_of_obj_delete (r_objchange.control_id);
		UPDATE changelog_service SET import_admin = i_change_admin WHERE log_svc_id=r_objchange.log_svc_id;
	END LOOP;
	FOR r_objchange IN -- jeder changelog_user Eintrag ohne import_admin wird versucht zu füllen
		SELECT log_usr_id, control_id FROM changelog_user WHERE import_admin IS NULL AND change_action='D'
	LOOP
		i_change_admin := get_last_change_admin_of_obj_delete (r_objchange.control_id);
		UPDATE changelog_user SET import_admin = i_change_admin WHERE log_usr_id=r_objchange.log_usr_id;
	END LOOP;
	RETURN;
END; 
$$ LANGUAGE plpgsql;

-- run update on changelog_object, _service, _user
SELECT * FROM add_change_admin_for_obj_deletes();

DROP FUNCTION add_change_admin_for_obj_deletes();

---

CREATE OR REPLACE FUNCTION set_change_request_info () RETURNS VOID AS $$
DECLARE
	r_change	RECORD;
	v_req_str	VARCHAR;
BEGIN -- jeder changelog_xxx Eintrag wird im Feld change_request_info aktualiert
	FOR r_change IN SELECT log_rule_id FROM changelog_rule
	LOOP UPDATE changelog_rule SET change_request_info = get_request_str('rule', changelog_rule.log_rule_id) WHERE log_rule_id=r_change.log_rule_id;
	END LOOP;

	FOR r_change IN SELECT log_obj_id FROM changelog_object
	LOOP UPDATE changelog_object SET change_request_info = get_request_str('object', changelog_object.log_obj_id) WHERE log_obj_id=r_change.log_obj_id;
	END LOOP;

	FOR r_change IN SELECT log_svc_id FROM changelog_service
	LOOP UPDATE changelog_service SET change_request_info = get_request_str('service', changelog_service.log_svc_id) WHERE log_svc_id=r_change.log_svc_id;
	END LOOP;

	FOR r_change IN SELECT log_usr_id FROM changelog_user
	LOOP UPDATE changelog_user SET change_request_info = get_request_str('user', changelog_user.log_usr_id) WHERE log_usr_id=r_change.log_usr_id;
	END LOOP;
	RETURN;
END; 
$$ LANGUAGE plpgsql;

-- run update on changelog_rule (can take up to 10 minutes)
SELECT * FROM set_change_request_info();

DROP FUNCTION set_change_request_info();