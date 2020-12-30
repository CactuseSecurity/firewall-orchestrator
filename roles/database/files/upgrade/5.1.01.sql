
DROP FUNCTION IF EXISTS get_tenant_relevant_changes(INTEGER, INTEGER, INTEGER, TIMESTAMP, TIMESTAMP);
DROP FUNCTION IF EXISTS get_svc_ids_for_tenant();
DROP FUNCTION IF EXISTS get_user_ids_for_tenant();
DROP FUNCTION IF EXISTS get_obj_ids_for_tenant();
DROP FUNCTION IF EXISTS get_obj_ids_of_filtered_ruleset(INTEGER[], INTEGER, TIMESTAMP);
DROP FUNCTION IF EXISTS get_obj_ids_of_filtered_management(INTEGER, INTEGER, INTEGER);
DROP FUNCTION IF EXISTS get_rule_ids(int4, "timestamp", int4, VARCHAR);
DROP FUNCTION IF EXISTS get_rule_ids_no_tenant_filter(int4, "timestamp", VARCHAR);
DROP FUNCTION IF EXISTS get_import_ids_for_time (TIMESTAMP);
DROP FUNCTION IF EXISTS get_rule_src_flat (BIGINT, integer, timestamp without time zone);
DROP FUNCTION IF EXISTS get_rule_dst_flat (BIGINT, integer, timestamp without time zone);
