
---------------------------------------------------------------------------------------------
-- object views
---------------------------------------------------------------------------------------------
CREATE OR REPLACE VIEW view_obj_changes AS
	SELECT
		abs_change_id,
		log_obj_id AS local_change_id,
		get_request_str(CAST('object' as VARCHAR), changelog_object.log_obj_id) as change_request_info,
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
		get_request_str('object', changelog_object.log_obj_id) as change_request_info,
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


---------------------------------------------------------------------------------------------
-- user views
---------------------------------------------------------------------------------------------

CREATE OR REPLACE VIEW view_user_changes AS
	SELECT
		abs_change_id,
		log_usr_id AS local_change_id,
		change_request_info,
		CAST('usr' AS VARCHAR) as change_element,
		CAST('basic_element' AS VARCHAR) as change_element_order,				
		changelog_user.old_user_id AS old_id,	
		changelog_user.new_user_id AS new_id,	
		changelog_user.documented as change_documented,
		changelog_user.change_type_id as change_type_id,
		change_action as change_type,
		changelog_user_comment as change_comment,
		user_comment as obj_comment,
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
		usr.user_name AS unique_name,
		CAST (NULL AS VARCHAR) AS change_diffs,
		CAST (NULL AS VARCHAR) AS change_new_element		
	FROM
		changelog_user
		LEFT JOIN (import_control LEFT JOIN management using (mgm_id)) using (control_id)
		LEFT JOIN usr ON (old_user_id=user_id)
		LEFT JOIN uiuser AS t_change_admin ON (changelog_user.import_admin=t_change_admin.uiuser_id)
		LEFT JOIN uiuser AS t_doku_admin ON (changelog_user.doku_admin=t_doku_admin.uiuser_id)
	WHERE change_type_id = 3 AND security_relevant AND change_action='D' AND successful_import
	UNION
	SELECT
		abs_change_id,
		log_usr_id AS local_change_id,
		change_request_info,
		CAST('usr' AS VARCHAR) as change_element,
		CAST('basic_element' AS VARCHAR) as change_element_order,				
		changelog_user.old_user_id AS old_id,	
		changelog_user.new_user_id AS new_id,	
		changelog_user.documented as change_documented,
		changelog_user.change_type_id as change_type_id,
		change_action as change_type,
		changelog_user_comment as change_comment,
		user_comment as obj_comment,
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
		usr.user_name AS unique_name,
		CAST (NULL AS VARCHAR) AS change_diffs,
		CAST (NULL AS VARCHAR) AS change_new_element		
	FROM
		changelog_user
		LEFT JOIN (import_control LEFT JOIN management using (mgm_id)) using (control_id)
		LEFT JOIN usr ON (new_user_id=user_id)
		LEFT JOIN uiuser AS t_change_admin ON (changelog_user.import_admin=t_change_admin.uiuser_id)
		LEFT JOIN uiuser AS t_doku_admin ON (changelog_user.doku_admin=t_doku_admin.uiuser_id)
	WHERE change_type_id = 3 AND security_relevant AND change_action<>'D' AND successful_import;

---------------------------------------------------------------------------------------------
-- service views
---------------------------------------------------------------------------------------------

CREATE OR REPLACE VIEW view_svc_changes AS
	SELECT
		abs_change_id,
		log_svc_id AS local_change_id,
		change_request_info,
		CAST('service' AS VARCHAR) as change_element,
		CAST('basic_element' AS VARCHAR) as change_element_order,		
		changelog_service.old_svc_id AS old_id,	
		changelog_service.new_svc_id AS new_id,	
		changelog_service.documented as change_documented,
		changelog_service.change_type_id as change_type_id,
		change_action as change_type,
		changelog_svc_comment as change_comment,
		svc_comment as obj_comment,
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
		service.svc_name AS unique_name,
		CAST (NULL AS VARCHAR) AS change_diffs,
		CAST (NULL AS VARCHAR) AS change_new_element
		FROM
			changelog_service
			LEFT JOIN (import_control LEFT JOIN management using (mgm_id)) using (control_id)
			LEFT JOIN service ON (old_svc_id=svc_id)
			LEFT JOIN uiuser AS t_change_admin ON (changelog_service.import_admin=t_change_admin.uiuser_id)
			LEFT JOIN uiuser AS t_doku_admin ON (changelog_service.doku_admin=t_doku_admin.uiuser_id)
		WHERE change_type_id = 3 AND security_relevant AND change_action='D' AND successful_import
	UNION
	SELECT	
		abs_change_id,
		log_svc_id AS local_change_id,
		change_request_info,
		CAST('service' AS VARCHAR) as change_element,
		CAST('basic_element' AS VARCHAR) as change_element_order,		
		changelog_service.old_svc_id AS old_id,	
		changelog_service.new_svc_id AS new_id,	
		changelog_service.documented as change_documented,
		changelog_service.change_type_id as change_type_id,
		change_action as change_type,
		changelog_svc_comment as change_comment,
		svc_comment as obj_comment,
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
		service.svc_name AS unique_name,
		CAST (NULL AS VARCHAR) AS change_diffs,
		CAST (NULL AS VARCHAR) AS change_new_element
		FROM
			changelog_service
			LEFT JOIN (import_control LEFT JOIN management using (mgm_id)) using (control_id)
			LEFT JOIN service ON (new_svc_id=svc_id)
			LEFT JOIN uiuser AS t_change_admin ON (changelog_service.import_admin=t_change_admin.uiuser_id)
			LEFT JOIN uiuser AS t_doku_admin ON (changelog_service.doku_admin=t_doku_admin.uiuser_id)
		WHERE change_type_id = 3 AND security_relevant AND change_action<>'D' AND successful_import;

---------------------------------------------------------------------------------------------
-- rule views
---------------------------------------------------------------------------------------------


CREATE OR REPLACE VIEW view_rule_changes AS
	SELECT     -- first select for deleted rules (join over old_rule_id)
		abs_change_id,
		log_rule_id AS local_change_id,
		change_request_info,
		CAST('rule' AS VARCHAR) as change_element,
		CAST('rule_element' AS VARCHAR) as change_element_order,
		changelog_rule.old_rule_id AS old_id,	
		changelog_rule.new_rule_id AS new_id,	
		changelog_rule.documented as change_documented,
		changelog_rule.change_type_id as change_type_id,
		change_action as change_type,
		changelog_rule_comment as change_comment,
		rule_comment as obj_comment,
		import_control.start_time AS change_time, 
		management.mgm_name AS mgm_name, 
		management.mgm_id AS mgm_id,
		device.dev_name,		
		device.dev_id,		
		CAST(t_change_admin.uiuser_first_name || ' ' || t_change_admin.uiuser_last_name AS VARCHAR) AS change_admin,
		t_change_admin.uiuser_id AS change_admin_id,
		CAST (t_doku_admin.uiuser_first_name || ' ' || t_doku_admin.uiuser_last_name AS VARCHAR) AS doku_admin,
		t_doku_admin.uiuser_id AS doku_admin_id,
		security_relevant,
		CAST((COALESCE (rule.rule_ruleid, rule.rule_uid) || ', Rulebase: ' || device.local_rulebase_name) AS VARCHAR) AS unique_name,
		CAST (NULL AS VARCHAR) AS change_diffs,
		CAST (NULL AS VARCHAR) AS change_new_element
	FROM
		changelog_rule
		LEFT JOIN (import_control LEFT JOIN management using (mgm_id)) using (control_id)
		LEFT JOIN rule ON (old_rule_id=rule_id)
		LEFT JOIN device ON (changelog_rule.dev_id=device.dev_id)
		LEFT JOIN uiuser AS t_change_admin ON (t_change_admin.uiuser_id=changelog_rule.import_admin)
		LEFT JOIN uiuser AS t_doku_admin ON (changelog_rule.doku_admin=t_doku_admin.uiuser_id)
	WHERE changelog_rule.change_action='D' AND change_type_id = 3 AND security_relevant AND successful_import

	UNION

	SELECT   -- second select for changed or inserted rules (join over new_rule_id)
		abs_change_id,
		log_rule_id AS local_change_id,
		change_request_info,
		CAST('rule' AS VARCHAR) as change_element,
		CAST('rule_element' AS VARCHAR) as change_element_order,
		changelog_rule.old_rule_id AS old_id,	
		changelog_rule.new_rule_id AS new_id,	
		changelog_rule.documented as change_documented,
		changelog_rule.change_type_id as change_type_id,
		change_action as change_type,
		changelog_rule_comment as change_comment,
		rule_comment as obj_comment,
		import_control.start_time AS change_time, 
		management.mgm_name AS mgm_name, 
		management.mgm_id AS mgm_id,
		device.dev_name,		
		device.dev_id,		
		CAST(t_change_admin.uiuser_first_name || ' ' || t_change_admin.uiuser_last_name AS VARCHAR) AS change_admin,
		t_change_admin.uiuser_id AS change_admin_id,
		CAST (t_doku_admin.uiuser_first_name || ' ' || t_doku_admin.uiuser_last_name AS VARCHAR) AS doku_admin,
		t_doku_admin.uiuser_id AS doku_admin_id,
		security_relevant,
		CAST((COALESCE (rule.rule_ruleid, rule.rule_uid) || ', Rulebase: ' || device.local_rulebase_name) AS VARCHAR) AS unique_name,
		CAST (NULL AS VARCHAR) AS change_diffs,
		CAST (NULL AS VARCHAR) AS change_new_element
	FROM
		changelog_rule
		LEFT JOIN (import_control LEFT JOIN management using (mgm_id)) using (control_id)
		LEFT JOIN rule ON (new_rule_id=rule_id)
		LEFT JOIN device ON (changelog_rule.dev_id=device.dev_id)
		LEFT JOIN uiuser AS t_change_admin ON (t_change_admin.uiuser_id=changelog_rule.import_admin)
		LEFT JOIN uiuser AS t_doku_admin ON (changelog_rule.doku_admin=t_doku_admin.uiuser_id)
	WHERE changelog_rule.change_action<>'D' AND change_type_id = 3 AND security_relevant AND successful_import;

---------------------------------------------------------------------------------------------
-- top level views
---------------------------------------------------------------------------------------------


--- changes ---------------------------------------------------------------------------------

CREATE OR REPLACE VIEW view_changes AS
 	(SELECT * FROM view_obj_changes)  UNION
  	(SELECT * FROM view_rule_changes) UNION
  	(SELECT * FROM view_svc_changes)  UNION
 	(SELECT * FROM view_user_changes)
 	ORDER BY change_time,mgm_name,change_admin,change_element_order;

CREATE OR REPLACE VIEW view_undocumented_changes AS
 	SELECT * FROM view_changes
	WHERE
--	 change_type_id = 3 AND security_relevant  AND
	 NOT change_documented 
 	ORDER BY change_time,mgm_name,change_admin,change_element_order;

CREATE OR REPLACE VIEW view_reportable_changes AS
 	SELECT * FROM view_changes
--	WHERE change_type_id = 3 AND security_relevant
 	ORDER BY change_time,mgm_name,change_admin,change_element_order;

-- Zusammenfassung aller geaenderten Element-IDs (erzeugt #(change_type='C') mehr Eintr�ge)
-- erzeugt keine Dubletten unter der Praemisse, dass stets old_id<>new_id
CREATE OR REPLACE VIEW view_changes_by_changed_element_id AS
	SELECT old_id as element_id, * FROM view_reportable_changes WHERE NOT old_id IS NULL 
	UNION
	SELECT new_id as element_id, * FROM view_reportable_changes WHERE NOT new_id IS NULL;

-- slim view for counting number of changes

CREATE OR REPLACE VIEW 	view_change_counter AS
	(SELECT mgm_id,CAST(NULL AS INTEGER) as dev_id,import_admin,abs_change_id,documented FROM changelog_user WHERE change_type_id=3 AND security_relevant)
		UNION
	(SELECT mgm_id,CAST(NULL AS INTEGER) as dev_id,import_admin,abs_change_id,documented FROM changelog_object WHERE change_type_id=3 AND security_relevant)
		UNION
	(SELECT mgm_id,CAST(NULL AS INTEGER) as dev_id,import_admin,abs_change_id,documented FROM changelog_service WHERE change_type_id=3 AND security_relevant)
		UNION
	(SELECT mgm_id,dev_id,import_admin,abs_change_id,documented FROM changelog_rule WHERE change_type_id=3 AND security_relevant);

CREATE OR REPLACE VIEW 	view_undocumented_change_counter AS
	SELECT * FROM view_change_counter WHERE NOT documented;

CREATE OR REPLACE VIEW 	view_documented_change_counter AS
	SELECT * FROM view_change_counter WHERE documented;

-- einheitliche View auf source und destination aller regeln - Verwendung in ChangeList bei tenant-Filterung
CREATE OR REPLACE VIEW view_rule_source_or_destination AS
         SELECT rule.rule_id, rule.rule_dst_neg AS rule_neg, objgrp_flat.objgrp_flat_member_id AS obj_id
           FROM rule
      LEFT JOIN rule_to USING (rule_id)
   LEFT JOIN objgrp_flat ON rule_to.obj_id = objgrp_flat.objgrp_flat_id
   LEFT JOIN object ON objgrp_flat.objgrp_flat_member_id = object.obj_id
UNION
         SELECT rule.rule_id, rule.rule_src_neg AS rule_neg, objgrp_flat.objgrp_flat_member_id AS obj_id
           FROM rule
      LEFT JOIN rule_from USING (rule_id)
   LEFT JOIN objgrp_flat ON rule_from.obj_id = objgrp_flat.objgrp_flat_id
   LEFT JOIN object ON objgrp_flat.objgrp_flat_member_id = object.obj_id;

--- import status -----------------------------------------------------------------------------

CREATE OR REPLACE VIEW view_import_status_successful AS 
	SELECT mgm_id, mgm_name, dev_typ_name, do_not_import, MAX(last_import) AS last_import, MAX(import_count_24hours) AS import_count_24hours FROM (
		SELECT management.mgm_id, mgm_name, dev_typ_name, do_not_import, successful_import, MAX(start_time) AS last_import, 
			COUNT(import_control.control_id) AS import_count_24hours
		FROM management LEFT JOIN import_control ON (management.mgm_id=import_control.mgm_id)
			LEFT JOIN stm_dev_typ USING (dev_typ_id)
		WHERE start_time>(now() - interval '24 hours') AND successful_import AND NOT stop_time IS NULL
		GROUP BY management.mgm_id, mgm_name, successful_import, do_not_import, dev_typ_name
		UNION 
		SELECT management.mgm_id, mgm_name, dev_typ_name, do_not_import, successful_import, MAX(start_time) AS last_import, 
			0 AS import_count_24hours
		FROM management LEFT JOIN import_control ON (management.mgm_id=import_control.mgm_id)
			LEFT JOIN stm_dev_typ USING (dev_typ_id)
		WHERE start_time<=(now() - interval '24 hours') AND successful_import AND NOT stop_time IS NULL
		GROUP BY management.mgm_id, mgm_name, successful_import, do_not_import, dev_typ_name
		UNION 
		SELECT management.mgm_id, mgm_name, dev_typ_name, do_not_import, successful_import, NULL AS last_import, 
			0 AS import_count_24hours
		FROM management LEFT JOIN import_control USING (mgm_id)
			LEFT JOIN stm_dev_typ USING (dev_typ_id)
		WHERE successful_import IS NULL
		GROUP BY management.mgm_id, mgm_name, successful_import, do_not_import, dev_typ_name
	) AS foo GROUP BY mgm_id, mgm_name, successful_import, do_not_import, dev_typ_name ORDER BY dev_typ_name, mgm_name;

CREATE OR REPLACE VIEW view_import_status_errors AS 
	SELECT mgm_id, mgm_name, dev_typ_name, do_not_import, MAX(last_import) AS last_import,  MAX(import_count_24hours) AS import_count_24hours, import_errors FROM (
		SELECT management.mgm_id, mgm_name, dev_typ_name, do_not_import, successful_import, MAX(start_time) AS last_import, 
			COUNT(import_control.control_id) AS import_count_24hours, import_control.import_errors
		FROM management LEFT JOIN import_control ON (management.mgm_id=import_control.mgm_id)
			LEFT JOIN stm_dev_typ USING (dev_typ_id)
		WHERE start_time>(now() - interval '24 hours') AND NOT successful_import AND NOT stop_time IS NULL
		GROUP BY management.mgm_id, mgm_name, successful_import, do_not_import, dev_typ_name, import_errors
--		UNION ALL
--		SELECT management.mgm_id, mgm_name, dev_typ_name, do_not_import, successful_import, MAX(start_time) AS last_import, 
--			0 AS import_count_24hours, NULL AS import_errors
--		FROM management LEFT JOIN import_control ON (management.mgm_id=import_control.mgm_id)
--			LEFT JOIN stm_dev_typ USING (dev_typ_id)
--		WHERE start_time<=(now() - interval '24 hours') AND NOT successful_import
--		GROUP BY management.mgm_id, mgm_name, successful_import, do_not_import, dev_typ_name, import_errors
		UNION 
		SELECT management.mgm_id, mgm_name, dev_typ_name, do_not_import, successful_import, NULL AS last_import, 
			0 AS import_count_24hours, NULL AS import_errors
		FROM management LEFT JOIN import_control USING (mgm_id)
			LEFT JOIN stm_dev_typ USING (dev_typ_id)
		WHERE successful_import IS NULL AND NOT stop_time IS NULL
		GROUP BY management.mgm_id, mgm_name, successful_import, do_not_import, dev_typ_name, import_errors
	) AS foo 
--	WHERE NOT import_errors IS NULL
	GROUP BY mgm_id, mgm_name, successful_import, do_not_import, dev_typ_name, import_errors ORDER BY dev_typ_name, mgm_name;

CREATE OR REPLACE VIEW view_import_status_table_unsorted AS 
	SELECT *,
		CASE
			WHEN import_is_active AND import_count_successful=0 AND import_count_errors>=5 THEN VARCHAR 'red'
			WHEN (NOT import_is_active AND last_successful_import IS NULL AND last_import_with_errors IS NULL)
				OR (last_successful_import>last_import_with_errors) THEN VARCHAR 'green'
			WHEN (last_successful_import IS NULL AND last_import_with_errors IS NULL)
				OR (last_successful_import<last_import_with_errors) THEN VARCHAR 'yellow'
			ELSE VARCHAR 'green'
		END
		AS status
	FROM (
		SELECT
			COALESCE(view_import_status_successful.mgm_id, view_import_status_errors.mgm_id) AS mgm_id,
			COALESCE(view_import_status_successful.mgm_name, view_import_status_errors.mgm_name) AS management_name,
			COALESCE(view_import_status_successful.dev_typ_name, view_import_status_errors.dev_typ_name) AS device_type,
			COALESCE(NOT view_import_status_successful.do_not_import, view_import_status_errors.do_not_import) AS import_is_active,
			view_import_status_successful.last_import AS last_successful_import,
			view_import_status_errors.last_import AS last_import_with_errors,
--			CAST(SUBSTR(CAST(view_import_status_successful.last_import AS VARCHAR), 1, 16) AS timestamp) AS last_successful_import,
--			CAST(SUBSTR(CAST(view_import_status_errors.last_import AS VARCHAR), 1, 16) AS timestamp) AS last_import_with_errors,
			COALESCE(view_import_status_successful.import_count_24hours,0) AS import_count_successful, 
			COALESCE(view_import_status_errors.import_count_24hours, 0) AS import_count_errors,
			COALESCE(view_import_status_successful.import_count_24hours + view_import_status_errors.import_count_24hours, 0) AS import_count_total,
			view_import_status_errors.import_errors AS last_import_error
			FROM view_import_status_successful LEFT JOIN view_import_status_errors USING (mgm_id)
	) AS FOO;

CREATE OR REPLACE VIEW view_import_status_table AS 
	SELECT *, CASE
			WHEN status='red' THEN 1
			WHEN status='yellow' THEN  2
			WHEN status='green' THEN 3
		END AS status_sorter
	FROM view_import_status_table_unsorted
	ORDER BY import_is_active DESC, status_sorter, management_name;

---------------------------------------------------------------------------------------------
-- tenant views
---------------------------------------------------------------------------------------------

/*
-- get all rules of a tenant
CREATE OR REPLACE VIEW view_tenant_rules AS 
	select x.rule_id, x.rule_create, x.rule_last_seen, x.tenant_id, x.mgm_id from (
		SELECT rule.rule_id, rule.rule_create, rule.rule_last_seen, tenant_network.tenant_id, rule.mgm_id, rule_order.dev_id
			FROM rule
				LEFT JOIN rule_order ON (rule.rule_id=rule_order.rule_id)
				LEFT JOIN rule_to ON (rule.rule_id=rule_to.rule_id)
				LEFT JOIN objgrp_flat ON (rule_to.obj_id=objgrp_flat_id)
				LEFT JOIN object ON (objgrp_flat_member_id=object.obj_id)
				LEFT JOIN tenant_network ON
					(
						(NOT rule_dst_neg AND (obj_ip<<tenant_net_ip OR obj_ip>>tenant_net_ip OR obj_ip=tenant_net_ip))
						 OR (rule_dst_neg AND (NOT obj_ip<<tenant_net_ip AND NOT obj_ip>>tenant_net_ip AND NOT obj_ip=tenant_net_ip))
					)
				WHERE rule_head_text IS NULL
			UNION
		SELECT rule.rule_id, rule.rule_create, rule.rule_last_seen, tenant_network.tenant_id, rule.mgm_id, rule_order.dev_id
			FROM rule
				LEFT JOIN rule_order ON (rule.rule_id=rule_order.rule_id)
				LEFT JOIN rule_from ON (rule.rule_id=rule_from.rule_id)
				LEFT JOIN objgrp_flat ON (rule_from.obj_id=objgrp_flat.objgrp_flat_id)
				LEFT JOIN object ON (objgrp_flat.objgrp_flat_member_id=object.obj_id)
				LEFT JOIN tenant_network ON
					(
						(NOT rule_src_neg AND (obj_ip<<tenant_net_ip OR obj_ip>>tenant_net_ip OR obj_ip=tenant_net_ip))
						 OR (rule_src_neg AND (NOT obj_ip<<tenant_net_ip AND NOT obj_ip>>tenant_net_ip AND NOT obj_ip=tenant_net_ip))
					)
				WHERE rule_head_text IS NULL
	) AS x; 	-- GROUP BY rule_id,tenant_id,mgm_id,rule_create, rule_last_seen
	
-- examples for tenant filtering:	
-- select rule_id from view_tenant_rules where tenant_network.tenant_id=1 and rule.mgm_id=4
-- select rule_id,rule_create from view_tenant_rules where mgm_id=4 group by rule_id,rule_create
*/


CREATE OR REPLACE VIEW view_device_names AS
	SELECT 'Management: ' || mgm_name || ', Device: ' || dev_name AS dev_string, dev_id, mgm_id, dev_name, mgm_name FROM device LEFT JOIN management USING (mgm_id);

-- view for ip address filtering
DROP MATERIALIZED VIEW IF EXISTS nw_object_limits;
CREATE MATERIALIZED VIEW nw_object_limits AS
	select obj_id, mgm_id,
		host ( object.obj_ip )::cidr as first_ip,
		CASE 
			WHEN object.obj_ip_end IS NULL
			THEN host(broadcast(object.obj_ip))::cidr 
			ELSE host(broadcast(object.obj_ip_end))::cidr 
		END last_ip
	from object;

-- adding indexes for view
Create index IF NOT EXISTS idx_nw_object_limits_obj_id on nw_object_limits (obj_id);
Create index IF NOT EXISTS idx_nw_object_limits_mgm_id on nw_object_limits (mgm_id);



DROP MATERIALIZED VIEW IF EXISTS view_tenant_rule_tos CASCADE;

CREATE MATERIALIZED VIEW view_tenant_rule_tos AS
    select tenant_rule_tos.* from (
        SELECT DISTINCT rule_to.*, rule.mgm_id, rule.dev_id, tenant_network.tenant_id
            FROM rule_to
								LEFT JOIN rule ON (rule_to.rule_id=rule.rule_id) -- is this really necessary? (enables check for x-hasura-visible-managements and x-hasura-visible-devices in this view)
                LEFT JOIN objgrp_flat ON (rule_to.obj_id=objgrp_flat.objgrp_flat_id)
                LEFT JOIN object ON (rule_to.obj_id=object.obj_id)
                LEFT JOIN tenant_network ON
                    (obj_ip>>=tenant_net_ip OR obj_ip<<=tenant_net_ip)
    ) AS tenant_rule_tos;

-- adding indexes for view
Create index IF NOT EXISTS idx_view_tenant_rule_tos_tenant_id on view_tenant_rule_tos(tenant_id);
Create index IF NOT EXISTS idx_view_tenant_rule_tos_rule_to_id on view_tenant_rule_tos(rule_to_id);
CREATE UNIQUE INDEX IF NOT EXISTS idx_view_tenant_rule_tos_unique ON view_tenant_rule_tos(rule_to_id, tenant_id);



DROP MATERIALIZED VIEW IF EXISTS view_tenant_rule_froms CASCADE;

CREATE MATERIALIZED VIEW view_tenant_rule_froms AS
    select tenant_rule_from.* from (
        SELECT DISTINCT rule_from.*, rule.mgm_id, rule.dev_id, tenant_network.tenant_id
            FROM rule_from
								LEFT JOIN rule ON (rule_from.rule_id=rule.rule_id) -- is this really necessary? (enables check for x-hasura-visible-managements and x-hasura-visible-devices in this view)
                LEFT JOIN objgrp_flat ON (rule_from.obj_id=objgrp_flat.objgrp_flat_id)
                LEFT JOIN object ON (rule_from.obj_id=object.obj_id)
                LEFT JOIN tenant_network ON
                    (obj_ip>>=tenant_net_ip OR obj_ip<<=tenant_net_ip)
    ) AS tenant_rule_from;

-- adding indexes for view
Create index IF NOT EXISTS idx_view_tenant_rule_froms_tenant_id on view_tenant_rule_froms(tenant_id);
Create index IF NOT EXISTS idx_view_tenant_rule_froms_rule_from_id on view_tenant_rule_froms(rule_from_id);
CREATE UNIQUE INDEX IF NOT EXISTS idx_view_tenant_rule_froms_unique ON view_tenant_rule_froms(rule_from_id, tenant_id);


DROP MATERIALIZED VIEW IF EXISTS view_tenant_rules;
CREATE MATERIALIZED VIEW IF NOT EXISTS view_tenant_rules AS
    select tenant_rules.* from (
        SELECT rule.*, tenant_network.tenant_id
            FROM rule
                LEFT JOIN rule_to ON (rule.rule_id=rule_to.rule_id)
                LEFT JOIN objgrp_flat ON (rule_to.obj_id=objgrp_flat_id)
                LEFT JOIN object ON (objgrp_flat_member_id=object.obj_id)
                LEFT JOIN tenant_network ON
                    ( NOT rule_dst_neg AND (obj_ip>>=tenant_net_ip OR obj_ip<<=tenant_net_ip))
                WHERE rule_head_text IS NULL
            UNION
        SELECT rule.*, tenant_network.tenant_id
            FROM rule
                LEFT JOIN rule_from ON (rule.rule_id=rule_from.rule_id)
                LEFT JOIN objgrp_flat ON (rule_from.obj_id=objgrp_flat.objgrp_flat_id)
                LEFT JOIN object ON (objgrp_flat.objgrp_flat_member_id=object.obj_id)
                LEFT JOIN tenant_network ON
                    ( NOT rule_src_neg AND (obj_ip>>=tenant_net_ip OR obj_ip<<=tenant_net_ip) )
                WHERE rule_head_text IS NULL
    ) AS tenant_rules;

-- adding indexes for view
Create index IF NOT EXISTS idx_view_tenant_rules_tenant_id on view_tenant_rules(tenant_id);
Create index IF NOT EXISTS idx_view_tenant_rules_mgm_id on view_tenant_rules(mgm_id);
CREATE UNIQUE INDEX IF NOT EXISTS idx_view_tenant_rules_unique ON view_tenant_rules(rule_id, tenant_id);

/*

	query filterRulesByTenant($importId: bigint) {
	view_tenant_rules(where: {access_rule: {_eq: true}, rule_last_seen: {_gte: $importId},  rule_create: {_lte: $importId}}) {
		rule_id
		rule_src
		rule_dst
		rule_create
		rule_last_seen
		tenant_id
	}
	}

*/

-- example tenant_network data:
-- insert into tenant_network (tenant_id, tenant_net_ip) values (123, '10.9.8.0/24');

-- test query: 
-- select dev_id, rule_num_numeric, view_tenant_rules.rule_id, rule_src,rule_dst
-- from view_tenant_rules 
-- where access_rule, tenant_id=123 and mgm_id=8 and rule_last_seen>=28520 
-- order by dev_id asc, rule_num_numeric asc

---------------------------------------------------------------------------------------------
-- GRANTS on exportable Views
---------------------------------------------------------------------------------------------

-- views for docu admins
GRANT SELECT ON TABLE view_change_counter TO GROUP secuadmins;
GRANT SELECT ON TABLE view_undocumented_change_counter TO GROUP secuadmins;
GRANT SELECT ON TABLE view_documented_change_counter TO GROUP secuadmins;
GRANT SELECT ON TABLE view_undocumented_changes TO GROUP secuadmins;

-- views used for reporters, too
GRANT SELECT ON TABLE view_reportable_changes TO GROUP secuadmins, reporters;
GRANT SELECT ON TABLE view_changes TO GROUP secuadmins, reporters;
-- GRANT SELECT ON TABLE view_tenant_rules TO GROUP secuadmins, reporters;
GRANT SELECT ON TABLE view_changes_by_changed_element_id TO GROUP secuadmins, reporters;
GRANT SELECT ON TABLE view_device_names TO GROUP secuadmins, reporters;
GRANT SELECT ON TABLE view_rule_source_or_destination TO GROUP secuadmins, reporters;

-- view for import status
GRANT SELECT ON TABLE view_import_status_table TO fworch;  -- {{fworch_home}}/bin/write_import_status_file.sh is run as fworch as it will also be invoked via cli
GRANT SELECT ON TABLE view_import_status_table TO GROUP secuadmins, reporters; -- not really neccessary

-- views for tenant filtering
GRANT SELECT ON TABLE view_tenant_rules TO GROUP secuadmins, reporters;
GRANT SELECT ON TABLE view_tenant_rule_tos TO GROUP secuadmins, reporters;
GRANT SELECT ON TABLE view_tenant_rule_froms TO GROUP secuadmins, reporters;
