-- 9.0.1 cleanup: remove legacy import, QA, and test functions

-- pgtap test functions
DROP FUNCTION IF EXISTS hdb_catalog.test_2_hdb_catalog_data();
DROP FUNCTION IF EXISTS hdb_catalog.shutdown_1();
DROP FUNCTION IF EXISTS public.test_1_schema();
DROP FUNCTION IF EXISTS public.test_2_functions();
DROP FUNCTION IF EXISTS public.shutdown_1();

-- temporary import tables
DROP TABLE IF EXISTS public.import_service CASCADE;
DROP TABLE IF EXISTS public.import_object CASCADE;
DROP TABLE IF EXISTS public.import_user CASCADE;
DROP TABLE IF EXISTS public.import_rule CASCADE;
DROP TABLE IF EXISTS public.import_zone CASCADE;
DROP TRIGGER IF EXISTS import_config_insert ON public.import_config;
DROP FUNCTION IF EXISTS public.import_config_from_json();
DROP FUNCTION IF EXISTS public.import_object_obj_id_seq();
DROP FUNCTION IF EXISTS public.import_service_svc_id_seq();
DROP FUNCTION IF EXISTS public.import_user_user_id_seq();
DROP FUNCTION IF EXISTS public.import_rule_rule_id_seq();

-- import orchestration
DROP FUNCTION IF EXISTS public.import_all_main(bigint, boolean);
DROP FUNCTION IF EXISTS public.import_global_refhandler_main(bigint);
DROP FUNCTION IF EXISTS public.import_changelog_sync(bigint, integer);
DROP FUNCTION IF EXISTS public.debug_show_time(varchar, timestamp);

-- import control helpers
DROP FUNCTION IF EXISTS public.rollback_current_import();
DROP FUNCTION IF EXISTS public.rollback_import_of_mgm(integer);
DROP FUNCTION IF EXISTS public.clean_up_tables(bigint);
DROP FUNCTION IF EXISTS public.show_change_summary(bigint);
DROP FUNCTION IF EXISTS public.found_changes_in_import(bigint);
DROP FUNCTION IF EXISTS public.remove_import_lock(bigint);
DROP FUNCTION IF EXISTS public.undocumented_rule_changes_exist();
DROP FUNCTION IF EXISTS public.undocumented_svc_changes_exist();
DROP FUNCTION IF EXISTS public.undocumented_usr_changes_exist();
DROP FUNCTION IF EXISTS public.undocumented_obj_changes_exist();
DROP FUNCTION IF EXISTS public.undocumented_changes_exist();
DROP FUNCTION IF EXISTS public.is_import_running();
DROP FUNCTION IF EXISTS public.is_import_running(integer);
DROP FUNCTION IF EXISTS public.get_previous_import_id_for_mgmt(integer, bigint);
DROP FUNCTION IF EXISTS public.get_last_import_id_for_mgmt(integer);
DROP FUNCTION IF EXISTS public.get_previous_import_id(bigint);
DROP FUNCTION IF EXISTS public.get_import_id_for_dev_at_time(integer, timestamp);
DROP FUNCTION IF EXISTS public.get_import_id_for_mgmt_at_time(integer, timestamp);

-- networking + zones
DROP FUNCTION IF EXISTS public.import_networking_main(bigint, boolean);
DROP FUNCTION IF EXISTS public.import_zone_main(bigint, boolean);
DROP FUNCTION IF EXISTS public.import_zone_single(bigint, integer, varchar, bigint);
DROP FUNCTION IF EXISTS public.import_zone_mark_deleted(bigint, integer);

-- network objects
DROP FUNCTION IF EXISTS public.import_nwobj_main(bigint, boolean);
DROP FUNCTION IF EXISTS public.import_nwobj_single(bigint, integer, bigint, boolean);
DROP FUNCTION IF EXISTS public.import_nwobj_mark_deleted(bigint, integer);
DROP FUNCTION IF EXISTS public.get_first_ip_of_cidr(cidr);
DROP FUNCTION IF EXISTS public.get_last_ip_of_cidr(cidr);
ALTER TABLE IF EXISTS public.object DROP CONSTRAINT IF EXISTS object_obj_ip_is_host;
ALTER TABLE IF EXISTS public.object DROP CONSTRAINT IF EXISTS object_obj_ip_end_is_host;
ALTER TABLE IF EXISTS public.owner_network DROP CONSTRAINT IF EXISTS owner_network_ip_is_host;
ALTER TABLE IF EXISTS public.owner_network DROP CONSTRAINT IF EXISTS owner_network_ip_end_is_host;
DROP FUNCTION IF EXISTS public.is_single_ip(cidr);

-- network object refs
DROP FUNCTION IF EXISTS public.import_nwobj_refhandler_main(bigint);
DROP FUNCTION IF EXISTS public.import_nwobj_refhandler_change(bigint, bigint, bigint);
DROP FUNCTION IF EXISTS public.import_nwobj_refhandler_insert(bigint, varchar, bigint);
DROP FUNCTION IF EXISTS public.import_nwobj_refhandler_change_flat(bigint, bigint, bigint);
DROP FUNCTION IF EXISTS public.import_nwobj_refhandler_insert_flat(bigint, varchar, bigint);
DROP FUNCTION IF EXISTS public.import_nwobj_refhandler_objgrp_add_group(bigint, varchar, varchar, integer, bigint);
DROP FUNCTION IF EXISTS public.import_nwobj_refhandler_objgrp_add_single_groupmember(varchar, bigint, integer, bigint);
DROP FUNCTION IF EXISTS public.import_nwobj_refhandler_objgrp_flat_add_group(bigint, bigint, integer, bigint);
DROP FUNCTION IF EXISTS public.import_nwobj_refhandler_objgrp_flat_add_self(bigint, bigint);
DROP FUNCTION IF EXISTS public.import_nwobj_refhandler_change_objgrp_member_refs(bigint, bigint, bigint);
DROP FUNCTION IF EXISTS public.import_nwobj_refhandler_change_objgrp_flat_member_refs(bigint, bigint, bigint);
DROP FUNCTION IF EXISTS public.import_nwobj_refhandler_change_rule_from_refs(bigint, bigint, bigint);
DROP FUNCTION IF EXISTS public.import_nwobj_refhandler_change_rule_to_refs(bigint, bigint, bigint);

-- services
DROP FUNCTION IF EXISTS public.import_svc_main(bigint, boolean);
DROP FUNCTION IF EXISTS public.import_svc_single(bigint, integer, bigint, integer, boolean);
DROP FUNCTION IF EXISTS public.import_svc_mark_deleted(bigint, integer);

-- service refs
DROP FUNCTION IF EXISTS public.import_svc_refhandler_main(bigint);
DROP FUNCTION IF EXISTS public.import_svc_refhandler_change(bigint, bigint, bigint);
DROP FUNCTION IF EXISTS public.import_svc_refhandler_insert(bigint, varchar, bigint);
DROP FUNCTION IF EXISTS public.import_svc_refhandler_change_flat(bigint, bigint, bigint);
DROP FUNCTION IF EXISTS public.import_svc_refhandler_insert_flat(bigint, varchar, bigint);
DROP FUNCTION IF EXISTS public.import_svc_refhandler_svcgrp_add_group(bigint, varchar, varchar, integer, bigint);
DROP FUNCTION IF EXISTS public.import_svc_refhandler_svcgrp_flat_add_self(bigint, bigint);
DROP FUNCTION IF EXISTS public.import_svc_refhandler_svcgrp_add_single_groupmember(varchar, bigint, integer, bigint);
DROP FUNCTION IF EXISTS public.import_svc_refhandler_svcgrp_flat_add_group(bigint, bigint, integer, bigint);
DROP FUNCTION IF EXISTS public.import_svc_refhandler_change_svcgrp_member_refs(bigint, bigint, bigint);
DROP FUNCTION IF EXISTS public.import_svc_refhandler_change_svcgrp_flat_member_refs(bigint, bigint, bigint);
DROP FUNCTION IF EXISTS public.import_svc_refhandler_change_rule_service_refs(bigint, bigint, bigint);

-- users
DROP FUNCTION IF EXISTS public.import_usr_main(bigint, boolean);
DROP FUNCTION IF EXISTS public.import_usr_single(bigint, integer, bigint, boolean);
DROP FUNCTION IF EXISTS public.import_usr_mark_deleted(bigint, integer);

-- user refs
DROP FUNCTION IF EXISTS public.import_usr_refhandler_main(bigint);
DROP FUNCTION IF EXISTS public.import_usr_refhandler_change(bigint, bigint, bigint);
DROP FUNCTION IF EXISTS public.import_usr_refhandler_insert(bigint, varchar, bigint);
DROP FUNCTION IF EXISTS public.import_usr_refhandler_change_flat(bigint, bigint, bigint);
DROP FUNCTION IF EXISTS public.import_usr_refhandler_insert_flat(bigint, varchar, bigint);
DROP FUNCTION IF EXISTS public.import_usr_refhandler_usergrp_add_group(bigint, varchar, varchar, integer, bigint);
DROP FUNCTION IF EXISTS public.import_usr_refhandler_usergrp_add_single_groupmember(varchar, bigint, integer, bigint);
DROP FUNCTION IF EXISTS public.import_usr_refhandler_change_usergrp_member_refs(bigint, bigint, bigint);
DROP FUNCTION IF EXISTS public.import_usr_refhandler_usergrp_flat_add_self(bigint, bigint);
DROP FUNCTION IF EXISTS public.import_usr_refhandler_usergrp_flat_add_group(bigint, bigint, integer, bigint);
DROP FUNCTION IF EXISTS public.import_usr_refhandler_change_usergrp_flat_member_refs(bigint, bigint, bigint);
DROP FUNCTION IF EXISTS public.import_usr_refhandler_change_rule_usr_refs(bigint, bigint, bigint);

-- rules
DROP FUNCTION IF EXISTS public.import_rules_combined(int, int, bigint, varchar, boolean, boolean, varchar);
DROP FUNCTION IF EXISTS public.import_rules_access(int, int, bigint, varchar, boolean, boolean);
DROP FUNCTION IF EXISTS public.import_rules_xlate(int, int, bigint, varchar, boolean, boolean);
DROP FUNCTION IF EXISTS public.import_rules(integer, bigint);
DROP FUNCTION IF EXISTS public.import_rules_set_rule_num_numeric(bigint, integer);
DROP FUNCTION IF EXISTS public.security_relevant_change(record, record, int, int, int, int);
DROP FUNCTION IF EXISTS public.non_security_relevant_change(record, record);
DROP FUNCTION IF EXISTS public.insert_single_rule(bigint, integer, integer, bigint, boolean);

-- rule refs
DROP FUNCTION IF EXISTS public.import_rule_refhandler_main(bigint, integer);
DROP FUNCTION IF EXISTS public.resolve_rule_list(bigint, varchar, varchar, integer, bigint, varchar, bigint);
DROP FUNCTION IF EXISTS public.f_add_single_rule_from_element(bigint, varchar, integer, bigint, bigint);
DROP FUNCTION IF EXISTS public.f_add_single_rule_to_element(bigint, varchar, integer, bigint, bigint);
DROP FUNCTION IF EXISTS public.f_add_single_rule_svc_element(bigint, varchar, integer, bigint);

-- resolved rule helpers
DROP FUNCTION IF EXISTS public.import_rule_resolved_nwobj(int, bigint, bigint, bigint, bigint, char, char);
DROP FUNCTION IF EXISTS public.import_rule_resolved_svc(int, bigint, bigint, bigint, bigint, char, char);
DROP FUNCTION IF EXISTS public.import_rule_resolved_usr(int, bigint, bigint, bigint, bigint, char, char);

-- QA helpers
DROP FUNCTION IF EXISTS public.fix_broken_refs();
DROP FUNCTION IF EXISTS public.check_broken_refs(varchar, boolean);
DROP FUNCTION IF EXISTS public.get_active_rules_with_broken_refs_per_mgm(varchar, boolean, integer);
DROP FUNCTION IF EXISTS public.get_active_rules_with_broken_refs_per_dev(varchar, boolean, integer);
DROP FUNCTION IF EXISTS public.get_active_rules_with_broken_src_refs_per_dev(varchar, boolean, integer);
DROP FUNCTION IF EXISTS public.get_active_rules_with_broken_dst_refs_per_dev(varchar, boolean, integer);
DROP FUNCTION IF EXISTS public.get_active_rules_with_broken_svc_refs_per_dev(varchar, boolean, integer);

-- basic helpers
DROP FUNCTION IF EXISTS public.is_numeric(varchar);
DROP FUNCTION IF EXISTS public.are_equal(boolean, boolean);
DROP FUNCTION IF EXISTS public.are_equal(jsonb, jsonb);
DROP FUNCTION IF EXISTS public.are_equal(varchar, varchar);
DROP FUNCTION IF EXISTS public.are_equal(text, text);
DROP FUNCTION IF EXISTS public.are_equal(cidr, cidr);
DROP FUNCTION IF EXISTS public.are_equal(integer, integer);
DROP FUNCTION IF EXISTS public.are_equal(bigint, bigint);
DROP FUNCTION IF EXISTS public.are_equal(smallint, smallint);
DROP FUNCTION IF EXISTS public.is_svc_group(bigint);
DROP FUNCTION IF EXISTS public.is_obj_group(bigint);
DROP FUNCTION IF EXISTS public.is_user_group(bigint);
DROP FUNCTION IF EXISTS public.get_admin_id_from_name(varchar);
DROP FUNCTION IF EXISTS public.error_handling(varchar);
DROP FUNCTION IF EXISTS public.error_handling(varchar, varchar);
DROP FUNCTION IF EXISTS public.remove_spaces(varchar);
DROP FUNCTION IF EXISTS public.del_surrounding_spaces(varchar);
DROP FUNCTION IF EXISTS public.get_last_change_admin_of_rulebase_change(bigint, integer);
DROP FUNCTION IF EXISTS public.get_last_change_admin_of_obj_delete(bigint);
DROP FUNCTION IF EXISTS public.get_previous_import_id(integer, timestamp);
