-- cleanup objects removed in branch clean/remove-stale-v8-code vs develop

-- remove explicit table grants that existed for legacy import staging tables
DO $$
DECLARE
    target_table TEXT;
BEGIN
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'configimporters') THEN
        FOREACH target_table IN ARRAY ARRAY[
            'import_service',
            'import_object',
            'import_user',
            'import_rule',
            'import_zone'
        ]
        LOOP
            IF to_regclass('public.' || target_table) IS NOT NULL THEN
                EXECUTE format(
                    'REVOKE ALL ON TABLE public.%I FROM %I;',
                    target_table,
                    'configimporters'
                );
            END IF;
        END LOOP;
    END IF;
END
$$;

-- remove foreign keys for removed import staging tables
ALTER TABLE IF EXISTS import_object DROP CONSTRAINT IF EXISTS import_object_control_id_fkey;
ALTER TABLE IF EXISTS import_rule DROP CONSTRAINT IF EXISTS import_rule_control_id_fkey;
ALTER TABLE IF EXISTS import_service DROP CONSTRAINT IF EXISTS import_service_control_id_fkey;
ALTER TABLE IF EXISTS import_user DROP CONSTRAINT IF EXISTS import_user_control_id_fkey;
ALTER TABLE IF EXISTS import_zone DROP CONSTRAINT IF EXISTS import_zone_control_id_fkey;

-- remove explicit indexes for removed import staging tables
DROP INDEX IF EXISTS idx_import_object01;
DROP INDEX IF EXISTS idx_import_object02;
DROP INDEX IF EXISTS idx_import_rule01;
DROP INDEX IF EXISTS "IX_Relationship59";
DROP INDEX IF EXISTS "IX_Relationship61";
DROP INDEX IF EXISTS "IX_Relationship62";
DROP INDEX IF EXISTS "IX_Relationship132";

-- remove legacy import staging tables
DROP TABLE IF EXISTS import_service;
DROP TABLE IF EXISTS import_object;
DROP TABLE IF EXISTS import_user;
DROP TABLE IF EXISTS import_rule;
DROP TABLE IF EXISTS import_zone;

-- remove routines of the legacy SQL-based import pipeline
DO $$
DECLARE
    current_routine RECORD;
BEGIN
    FOR current_routine IN
        SELECT
            proc_namespace.nspname AS schema_name,
            proc_definition.proname AS routine_name,
            pg_get_function_identity_arguments(proc_definition.oid) AS routine_arguments,
            proc_definition.prokind AS routine_kind
        FROM pg_proc proc_definition
        JOIN pg_namespace proc_namespace
            ON proc_namespace.oid = proc_definition.pronamespace
        WHERE proc_namespace.nspname = 'public'
          AND proc_definition.proname = ANY (ARRAY[
              'import_all_main',
              'debug_show_time',
              'import_global_refhandler_main',
              'import_changelog_sync',
              'undocumented_rule_changes_exist',
              'undocumented_svc_changes_exist',
              'undocumented_usr_changes_exist',
              'undocumented_obj_changes_exist',
              'undocumented_changes_exist',
              'is_import_running',
              'get_previous_import_id_for_mgmt',
              'get_last_import_id_for_mgmt',
              'get_previous_import_id',
              'get_import_id_for_dev_at_time',
              'get_import_id_for_mgmt_at_time',
              'rollback_current_import',
              'rollback_import_of_mgm',
              'remove_import_lock',
              'show_change_summary',
              'found_changes_in_import',
              'clean_up_tables',
              'import_networking_main',
              'import_zone_main',
              'import_zone_single',
              'import_zone_mark_deleted',
              'import_nwobj_main',
              'import_nwobj_mark_deleted',
              'import_nwobj_single',
              'get_first_ip_of_cidr',
              'get_last_ip_of_cidr',
              'import_rules',
              'import_rules_xlate',
              'import_rules_access',
              'import_rules_combined',
              'import_rules_set_rule_num_numeric',
              'security_relevant_change',
              'non_security_relevant_change',
              'insert_single_rule',
              'import_svc_main',
              'import_svc_mark_deleted',
              'import_svc_single',
              'import_usr_main',
              'import_usr_mark_deleted',
              'import_usr_single'
          ])
    LOOP
        EXECUTE format(
            'DROP %s IF EXISTS %I.%I(%s);',
            CASE
                WHEN current_routine.routine_kind = 'p' THEN 'PROCEDURE'
                ELSE 'FUNCTION'
            END,
            current_routine.schema_name,
            current_routine.routine_name,
            current_routine.routine_arguments
        );
    END LOOP;
END
$$;
