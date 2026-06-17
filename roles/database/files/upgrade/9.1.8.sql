-- Ticket Template Tufin save

DO $$
DECLARE
    rec RECORD;
    migrated_value text;
BEGIN
    FOR rec IN
        SELECT config_user, config_value
        FROM config
        WHERE config_key = 'extTicketSystems'
          AND config_value IS NOT NULL
          AND config_value <> ''
    LOOP
        SELECT jsonb_agg(
            CASE
                WHEN elem ? 'ExternalTicketSystemType' AND NOT (elem ? 'TypeId') THEN
                    (
                        (elem - 'ExternalTicketSystemType')
                        || jsonb_build_object(
                            'TypeId',
                            CASE (elem->>'ExternalTicketSystemType')::int
                                WHEN 0 THEN 1
                                WHEN 1 THEN 2
                                WHEN 2 THEN 3
                                WHEN 3 THEN 4
                                ELSE 1
                            END
                        )
                    )
                    ||
                    CASE
                        WHEN COALESCE(elem->>'Name', '') <> '' THEN
                            '{}'::jsonb
                        WHEN (elem->>'ExternalTicketSystemType')::int = 1 THEN
                            jsonb_build_object('Name', 'Tufin SecureChange')
                        WHEN (elem->>'ExternalTicketSystemType')::int = 0 THEN
                            jsonb_build_object('Name', 'Generic')
                        WHEN (elem->>'ExternalTicketSystemType')::int = 2 THEN
                            jsonb_build_object('Name', 'AlgoSec')
                        WHEN (elem->>'ExternalTicketSystemType')::int = 3 THEN
                            jsonb_build_object('Name', 'ServiceNow')
                        ELSE
                            '{}'::jsonb
                    END
                ELSE
                    elem - 'ExternalTicketSystemType'
            END
        )::text
        INTO migrated_value
        FROM jsonb_array_elements(rec.config_value::jsonb) AS elem;

        UPDATE config
        SET config_value = COALESCE(migrated_value, '[]')
        WHERE config_key = 'extTicketSystems'
          AND config_user = rec.config_user;
    END LOOP;
END $$;


ALTER TABLE "management" ADD COLUMN IF NOT EXISTS "export_credential_id" Integer;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'management_export_credential_id_foreign_key'
    ) THEN
        ALTER TABLE "management"
        ADD CONSTRAINT management_export_credential_id_foreign_key
        FOREIGN KEY ("export_credential_id")
        REFERENCES import_credential(id)
        ON UPDATE RESTRICT
        ON DELETE CASCADE;
    END IF;
END $$;