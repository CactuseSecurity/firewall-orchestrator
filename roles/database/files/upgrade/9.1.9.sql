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