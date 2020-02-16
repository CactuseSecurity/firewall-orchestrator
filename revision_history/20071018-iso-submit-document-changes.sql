-- $Id: 20071018-iso-submit-document-changes.sql,v 1.1.2.2 2007-12-13 10:47:31 tim Exp $
-- $Source: /home/cvs/iso/package/install/migration/Attic/20071018-iso-submit-document-changes.sql,v $
-- Bugfix fuer Aendern von Dokumentation
-- die folgenden Dateien müssen aktualisiert werden:
--   $ISOHOME/web/htdocs/submit_documentation_data.php


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