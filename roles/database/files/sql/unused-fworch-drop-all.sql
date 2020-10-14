-- drops the whole content of the database but not the database itself
-- not in use

/* Drop Indexes */
Drop index "firewall_akey";
Drop index "kunden_akey";
Drop index "kundennetze_akey";
Drop index "management_akey";
Drop index "rule_from_unique_index";
Drop index "stm_color_akey";
Drop index "stm_fw_typ_a2key";
Drop index "stm_fw_typ_akey";
Drop index "stm_nattypes_akey";
Drop index "stm_obj_typ_akey";


/* Drop Tables */
Drop table "stm_change_type" Cascade;
Drop table "usergrp_flat" Cascade;
Drop table "request_user_change" Cascade;
Drop table "request_rule_change" Cascade;
Drop table "request_service_change" Cascade;
Drop table "request_object_change" Cascade;
Drop table "request" Cascade;
Drop table "tenant_user" Cascade;
Drop table "stm_report_typ" Cascade;
Drop table "svcgrp_flat" Cascade;
Drop table "objgrp_flat" Cascade;
Drop table "rule_order" Cascade;
Drop table "text_msg" Cascade;
Drop table "stm_usr_typ" Cascade;
Drop table "config" Cascade;
Drop table "error_log" Cascade;
Drop table "changelog_rule" Cascade;
Drop table "changelog_user" Cascade;
Drop table "changelog_service" Cascade;
Drop table "error" Cascade;
Drop table "isoadmin" Cascade;
Drop table "changelog_object" Cascade;
Drop table "import_zone" Cascade;
Drop table "import_control" Cascade;
Drop table "import_rule" Cascade;
Drop table "import_user" Cascade;
Drop table "import_object" Cascade;
Drop table "import_service" Cascade;
Drop table "usergrp" Cascade;
Drop table "usr" Cascade;
Drop table "zone" Cascade;
Drop table "stm_svc_typ" Cascade;
Drop table "stm_ip_proto" Cascade;
Drop table "stm_track" Cascade;
Drop table "stm_obj_typ" Cascade;
Drop table "stm_nattyp" Cascade;
Drop table "stm_dev_typ" Cascade;
Drop table "stm_color" Cascade;
Drop table "stm_action" Cascade;
Drop table "svcgrp" Cascade;
Drop table "service" Cascade;
Drop table "rule_to" Cascade;
Drop table "rule_service" Cascade;
Drop table "rule_review" Cascade;
Drop table "rule_from" Cascade;
Drop table "rule" Cascade;
Drop table "objgrp" Cascade;
Drop table "object" Cascade;
Drop table "management" Cascade;
Drop table "tenant_network" Cascade;
Drop table "tenant_object" Cascade;
Drop table "tenant" Cascade;
Drop table "tenant_project" Cascade;
Drop table "device" Cascade;
---
Drop table "tenant_username" Cascade;
Drop table "import_changelog" Cascade;
Drop table "manual" Cascade;
Drop table "report" Cascade;
Drop table "request_type" Cascade;
Drop table "temp_filtered_rule_ids" Cascade;
Drop table "temp_mgmid_importid_at_report_time" Cascade;
Drop table "temp_table_for_tenant_filtered_rule_ids" Cascade;


Drop sequence "public"."abs_change_id_seq" Cascade;

/* Drop functions */

DROP FUNCTION public.add_missing_dev_id_entries_in_rule_table();
DROP FUNCTION public.are_equal(boolean, boolean);
DROP FUNCTION public.are_equal(character varying, character varying);
DROP FUNCTION public.are_equal(cidr, cidr);
DROP FUNCTION public.are_equal(integer, integer);
DROP FUNCTION public.are_equal(text, text);
DROP FUNCTION public.check_broken_refs(character varying, boolean);
DROP FUNCTION public.clean_up_tables(integer);
DROP FUNCTION public.create_rule_dev_initial_entry_all();
DROP FUNCTION public.del_surrounding_spaces(character varying);
DROP FUNCTION public.error_handling(character varying);
DROP FUNCTION public.error_handling(character varying, character varying);
DROP FUNCTION public.explode_objgrp(integer);
DROP FUNCTION public.f_add_single_rule_from_element(integer, character varying, integer, integer, integer);
DROP FUNCTION public.f_add_single_rule_svc_element(integer, character varying, integer, integer);
DROP FUNCTION public.f_add_single_rule_to_element(integer, character varying, integer, integer, integer);
DROP FUNCTION public.fix_broken_refs();
DROP FUNCTION public.flatten_obj_list(integer[]);
DROP FUNCTION public.get_active_rules_with_broken_refs(character varying, boolean);
DROP FUNCTION public.get_active_rules_with_broken_refs_per_dev(character varying, boolean, integer);
DROP FUNCTION public.get_active_rules_with_broken_refs_per_mgm(character varying, boolean, integer);
DROP FUNCTION public.get_admin_id_from_name(character varying);
DROP FUNCTION public.get_changed_newrules(refcursor, integer[]);
DROP FUNCTION public.get_changed_oldrules(refcursor, integer[]);
DROP FUNCTION public.get_tenant_ip_filter(integer);
DROP FUNCTION public.get_tenant_list(refcursor);
DROP FUNCTION public.get_tenant_relevant_changes(integer, integer, integer, timestamp without time zone, timestamp without time zone);
DROP FUNCTION public.get_tenant_rule_list(refcursor, integer);
DROP FUNCTION public.get_dev_list(refcursor, integer);
DROP FUNCTION public.get_dev_typ_id(character varying);
DROP FUNCTION public.get_dst_obj_ids_of_filtered_ruleset(integer, timestamp without time zone, integer);
DROP FUNCTION public.get_exploded_dst_of_rule(integer);
DROP FUNCTION public.get_exploded_src_of_rule(integer);
DROP FUNCTION public.get_import_id_for_dev_at_time(integer, timestamp without time zone);
DROP FUNCTION public.get_import_id_for_mgmt_at_time(integer, timestamp without time zone);
DROP FUNCTION public.get_import_ids_for_time(timestamp without time zone);
DROP FUNCTION public.get_ip_filter(cidr);
DROP FUNCTION public.get_last_change_admin_of_obj_delete(integer);
DROP FUNCTION public.get_last_change_admin_of_rulebase_change(integer, integer);
DROP FUNCTION public.get_last_import_id_for_mgmt(integer);
DROP FUNCTION public.get_matching_import_id(integer, timestamp without time zone);
DROP FUNCTION public.get_mgmt_dev_list(refcursor);
DROP FUNCTION public.get_mgmt_list(refcursor);
DROP FUNCTION public.get_negated_tenant_ip_filter(integer);
DROP FUNCTION public.get_next_import_id(integer, timestamp without time zone);
DROP FUNCTION public.get_obj_ids_for_tenant();
DROP FUNCTION public.get_obj_ids_of_filtered_management(integer, integer, integer);
DROP FUNCTION public.get_obj_ids_of_filtered_ruleset(integer[], integer, timestamp without time zone);
DROP FUNCTION public.get_obj_ids_of_filtered_ruleset_flat(integer[], integer, timestamp without time zone);
DROP FUNCTION public.get_previous_import_id(integer);
DROP FUNCTION public.get_previous_import_id(integer, timestamp without time zone);
DROP FUNCTION public.get_previous_import_id_for_mgmt(integer, integer);
DROP FUNCTION public.get_previous_import_ids(timestamp without time zone);
DROP FUNCTION public.get_report_typ_list(refcursor);
DROP FUNCTION public.get_report_typ_list_eng(refcursor);
DROP FUNCTION public.get_report_typ_list_ger(refcursor);
DROP FUNCTION public.get_request_str(character varying, integer);
-- to be continued
