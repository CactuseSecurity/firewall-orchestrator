-- clean up database functions and views

DROP FUNCTION IF EXISTS get_tenant_list(REFCURSOR);
DROP FUNCTION IF EXISTS get_dev_list(REFCURSOR,INTEGER);
DROP FUNCTION IF EXISTS get_mgmt_list(REFCURSOR);
DROP FUNCTION IF EXISTS get_mgmt_dev_list(REFCURSOR);
DROP FUNCTION IF EXISTS get_obj_ids_of_filtered_management(INTEGER, INTEGER, INTEGER);
DROP FUNCTION IF EXISTS rule_src_contains_tenant_obj (BIGINT, INTEGER);
DROP FUNCTION IF EXISTS rule_dst_contains_tenant_obj (BIGINT, INTEGER);
DROP FUNCTION IF EXISTS obj_belongs_to_tenant (BIGINT, INTEGER);
DROP FUNCTION IF EXISTS obj_neg_belongs_to_tenant (BIGINT, INTEGER);
DROP FUNCTION IF EXISTS flatten_obj_list (BIGINT[]);
DROP FUNCTION IF EXISTS get_changed_newrules(refcursor, _int4);
DROP FUNCTION IF EXISTS get_changed_oldrules(refcursor, _int4);
DROP FUNCTION IF EXISTS get_undocumented_changelog_entries(VARCHAR);
DROP FUNCTION IF EXISTS get_import_ids_for_time (TIMESTAMP);
DROP FUNCTION IF EXISTS get_negated_tenant_ip_filter(INTEGER);
DROP FUNCTION IF EXISTS get_ip_filter(CIDR);
DROP FUNCTION IF EXISTS get_tenant_ip_filter(INTEGER);
DROP FUNCTION IF EXISTS get_exploded_src_of_rule(BIGINT);
DROP FUNCTION IF EXISTS get_exploded_dst_of_rule(BIGINT);
DROP FUNCTION IF EXISTS get_rule_action (BIGINT);
DROP FUNCTION IF EXISTS is_rule_src_negated (BIGINT);
DROP FUNCTION IF EXISTS is_rule_dst_negated (BIGINT);
DROP FUNCTION IF EXISTS explode_objgrp (BIGINT);
DROP FUNCTION IF EXISTS get_matching_import_id(INTEGER, TIMESTAMP);
DROP FUNCTION IF EXISTS get_next_import_id(INTEGER,TIMESTAMP);
DROP FUNCTION IF EXISTS get_previous_import_ids(TIMESTAMP);
DROP FUNCTION IF EXISTS instr (varchar, varchar, integer, integer);
DROP FUNCTION IF EXISTS instr (varchar, varchar, integer);
DROP FUNCTION IF EXISTS instr (varchar, varchar);
DROP FUNCTION IF EXISTS get_dev_typ_id (varchar);
DROP FUNCTION IF EXISTS object_relevant_for_tenant(object object, hasura_session json);

CREATE OR REPLACE VIEW view_obj_changes AS
	SELECT
		abs_change_id,
		log_obj_id AS local_change_id,
		''::VARCHAR as change_request_info,
		CAST('object' AS VARCHAR) as change_element,
		CAST('basic_element' AS VARCHAR) as change_element_order,
		changelog_object.old_obj_id AS old_id,	
		changelog_object.new_obj_id AS new_id,	
		changelog_object.documented as change_documented,
		changelog_object.change_type_id as change_type_id,
		change_action as change_type,
		changelog_obj_comment as change_comment,
		obj_comment,
		import_control.start_time AS change_time, 
		management.mgm_name AS mgm_name,
		management.mgm_id AS mgm_id,
		CAST(NULL AS VARCHAR) as dev_name,		
		CAST(NULL AS INTEGER) as dev_id,		
		t_change_admin.uiuser_first_name || ' ' || t_change_admin.uiuser_last_name AS change_admin,
		t_change_admin.uiuser_id AS change_admin_id,
		t_doku_admin.uiuser_first_name || ' ' || t_doku_admin.uiuser_last_name AS doku_admin,
		t_doku_admin.uiuser_id AS doku_admin_id,
		security_relevant,
		object.obj_name AS unique_name,
		CAST (NULL AS VARCHAR) AS change_diffs,
		CAST (NULL AS VARCHAR) AS change_new_element
	FROM
		changelog_object
		LEFT JOIN (import_control LEFT JOIN management using (mgm_id)) using (control_id)
		LEFT JOIN object ON (old_obj_id=obj_id)
		LEFT JOIN uiuser AS t_change_admin ON (changelog_object.import_admin=t_change_admin.uiuser_id)
		LEFT JOIN uiuser AS t_doku_admin ON (changelog_object.doku_admin=t_doku_admin.uiuser_id)
	WHERE change_type_id = 3 AND security_relevant AND change_action='D' AND successful_import

	UNION

	SELECT
		abs_change_id,
		log_obj_id AS local_change_id,
		''::VARCHAR as change_request_info,
		CAST('object' AS VARCHAR) as change_element,
		CAST('basic_element' AS VARCHAR) as change_element_order,
		changelog_object.old_obj_id AS old_id,	
		changelog_object.new_obj_id AS new_id,	
		changelog_object.documented as change_documented,
		changelog_object.change_type_id as change_type_id,
		change_action as change_type,
		changelog_obj_comment as change_comment,
		obj_comment,
		import_control.start_time AS change_time, 
		management.mgm_name AS mgm_name,
		management.mgm_id AS mgm_id,
		CAST(NULL AS VARCHAR) as dev_name,		
		CAST(NULL AS INTEGER) as dev_id,		
		t_change_admin.uiuser_first_name || ' ' || t_change_admin.uiuser_last_name AS change_admin,
		t_change_admin.uiuser_id AS change_admin_id,
		t_doku_admin.uiuser_first_name || ' ' || t_doku_admin.uiuser_last_name AS doku_admin,
		t_doku_admin.uiuser_id AS doku_admin_id,
		security_relevant,
		object.obj_name AS unique_name,
		CAST (NULL AS VARCHAR) AS change_diffs,
		CAST (NULL AS VARCHAR) AS change_new_element
	FROM
		changelog_object
		LEFT JOIN (import_control LEFT JOIN management using (mgm_id)) using (control_id)
		LEFT JOIN object ON (new_obj_id=obj_id)
		LEFT JOIN uiuser AS t_change_admin ON (changelog_object.import_admin=t_change_admin.uiuser_id)
		LEFT JOIN uiuser AS t_doku_admin ON (changelog_object.doku_admin=t_doku_admin.uiuser_id)
		WHERE change_type_id = 3 AND security_relevant AND change_action<>'D' AND successful_import;

DROP FUNCTION IF EXISTS get_request_str(VARCHAR,BIGINT);



DROP VIEW IF EXISTS view_undocumented_changes CASCADE;
DROP VIEW IF EXISTS view_changes_by_changed_element_id CASCADE;
DROP VIEW IF EXISTS view_change_counter CASCADE;
DROP VIEW IF EXISTS view_undocumented_change_counter CASCADE;
DROP VIEW IF EXISTS view_documented_change_counter CASCADE;

---
-- DROP VIEW IF EXISTS view_obj_changes CASCADE;
-- DROP VIEW IF EXISTS view_change_counter CASCADE;
-- DROP VIEW IF EXISTS view_svc_changes CASCADE;
-- DROP VIEW IF EXISTS view_user_changes CASCADE;
-- DROP VIEW IF EXISTS view_rule_changes CASCADE;
-- DROP VIEW IF EXISTS view_rule_source_or_destination CASCADE;

