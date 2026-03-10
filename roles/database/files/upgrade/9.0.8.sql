insert into config (config_key, config_value, config_user) VALUES ('modRolloutRemovedAppServers', 'false', 0) ON CONFLICT DO NOTHING;

-- import_control -> stm_import FK for hasura relations
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM information_schema.table_constraints tc
        JOIN information_schema.key_column_usage kcu
          ON tc.constraint_name = kcu.constraint_name
         AND tc.table_schema = kcu.table_schema
        WHERE tc.table_name = 'import_control'
          AND tc.constraint_type = 'FOREIGN KEY'
          AND kcu.column_name = 'import_type_id'
    ) THEN
        ALTER TABLE import_control
        ADD CONSTRAINT import_control_import_type_id_stm_import_foreign_key
        FOREIGN KEY (import_type_id)
        REFERENCES stm_import(import_type_id)
        ON UPDATE RESTRICT ON DELETE RESTRICT;
    END IF;
END
$$;
