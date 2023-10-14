
---------------------------------------------------------------------------------------------
-- object views
---------------------------------------------------------------------------------------------
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

CREATE OR REPLACE VIEW view_reportable_changes AS
 	SELECT * FROM view_changes
--	WHERE change_type_id = 3 AND security_relevant
 	ORDER BY change_time,mgm_name,change_admin,change_element_order;

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

-- views used for reporters, too
GRANT SELECT ON TABLE view_reportable_changes TO GROUP secuadmins, reporters;
GRANT SELECT ON TABLE view_changes TO GROUP secuadmins, reporters;
GRANT SELECT ON TABLE view_rule_source_or_destination TO GROUP secuadmins, reporters;
