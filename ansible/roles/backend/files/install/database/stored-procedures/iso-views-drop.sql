-- $Id: iso-views-drop.sql,v 1.1.2.3 2011-05-11 08:02:26 tim Exp $
-- $Source: /home/cvs/iso/package/install/database/Attic/iso-views-drop.sql,v $

DROP VIEW view_undocumented_changes CASCADE;
DROP VIEW view_reportable_changes CASCADE;
DROP VIEW view_changes CASCADE;
DROP VIEW view_obj_changes CASCADE;
DROP VIEW view_user_changes CASCADE;
DROP VIEW view_svc_changes CASCADE;
DROP VIEW view_rule_changes CASCADE;
DROP VIEW view_undocumented_change_counter;
DROP VIEW view_documented_change_counter;
DROP VIEW view_change_counter;
-- DROP VIEW view_import_status_successful CASCADE;
DROP VIEW view_import_status_errors CASCADE;
DROP VIEW view_device_names CASCADE;