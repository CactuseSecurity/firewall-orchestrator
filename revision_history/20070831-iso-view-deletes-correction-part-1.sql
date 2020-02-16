-- $Id: 20070831-iso-view-deletes-correction-part-1.sql,v 1.1.2.2 2007-12-13 10:47:32 tim Exp $
-- $Source: /home/cvs/iso/package/install/migration/20070831-view-corrections-for-deletes/Attic/20070831-iso-view-deletes-correction-part-1.sql,v $
-- preparations for pre 01.02.07 databases, has no effect for newer databases
update changelog_rule set import_admin=NULL where import_admin=0 and change_type_id=3;
update changelog_object set import_admin=NULL where import_admin=0 and change_type_id=3;
update changelog_service set import_admin=NULL where import_admin=0 and change_type_id=3;
update changelog_user set import_admin=NULL where import_admin=0 and change_type_id=3;

ALTER TABLE changelog_rule ADD COLUMN change_request_info VARCHAR;
ALTER TABLE changelog_rule ADD COLUMN change_time Timestamp;
ALTER TABLE changelog_rule ADD COLUMN unique_name VARCHAR;
ALTER TABLE changelog_object ADD COLUMN change_request_info VARCHAR;
ALTER TABLE changelog_object ADD COLUMN change_time Timestamp;
ALTER TABLE changelog_object ADD COLUMN unique_name VARCHAR;
ALTER TABLE changelog_service ADD COLUMN change_request_info VARCHAR;
ALTER TABLE changelog_service ADD COLUMN change_time Timestamp;
ALTER TABLE changelog_service ADD COLUMN unique_name VARCHAR;
ALTER TABLE changelog_user ADD COLUMN change_request_info VARCHAR;
ALTER TABLE changelog_user ADD COLUMN change_time Timestamp;
ALTER TABLE changelog_user ADD COLUMN unique_name VARCHAR;

DROP VIEW view_svc_changes_basics CASCADE;
DROP VIEW view_obj_changes_basics CASCADE;
DROP VIEW view_user_changes_basics CASCADE;
DROP VIEW view_rule_changes_basics CASCADE;
DROP FUNCTION import_rules (INTEGER,INTEGER);
DROP FUNCTION insert_single_rule(INTEGER,INTEGER,INTEGER,INTEGER,BOOLEAN);

-- source iso-report-basics.sql for definition of functions
-- - get_last_change_admin_of_rulebase_change (INTEGER, INTEGER)
-- - get_last_change_admin_of_obj (INTEGER)

-- source iso-views.sql for redefinition of views view_xxxx_changes
-- source iso-rule-import.sql for redefintion of import_rules functions
-- source iso-obj-import.sql for redefintion of import_nwobj functions
-- source iso-svc-import.sql for redefintion of import_svc functions
-- source iso-usr-import.sql for redefintion of import_usr functions

-- replace web/htdocs/submit_documentation_data.php (automatic adjustment of change_request_info in changelog_xxx during documentation process)