-- Replace embedded cloud credentials with a reusable credential reference (#3766).
-- All changes run in one transaction. A failed encryption leaves the old columns and values intact.
DO $$
DECLARE
    r_credential record;
    i_additional_credential_id integer;
    t_main_key text;
BEGIN
    LOCK TABLE credential IN ACCESS EXCLUSIVE MODE;
    ALTER TABLE credential ADD COLUMN IF NOT EXISTS additional_credential_id integer;

    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname = 'credential_additional_credential_id_foreign_key' AND conrelid = 'credential'::regclass
    ) THEN
        ALTER TABLE credential ADD CONSTRAINT credential_additional_credential_id_foreign_key
            FOREIGN KEY (additional_credential_id) REFERENCES credential(id) ON UPDATE RESTRICT ON DELETE RESTRICT;
    END IF;

    IF EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public' AND table_name = 'credential' AND column_name = 'cloud_client_secret'
    ) THEN
        FOR r_credential IN
            SELECT id, credential_name, cloud_client_id, cloud_client_secret, additional_credential_id
            FROM credential
            WHERE COALESCE(cloud_client_id, '') <> '' OR COALESCE(cloud_client_secret, '') <> ''
        LOOP
            IF r_credential.additional_credential_id IS NOT NULL THEN
                RAISE EXCEPTION 'credential % already has additional credentials; resolve its legacy cloud fields before upgrading', r_credential.id;
            END IF;
            IF t_main_key IS NULL THEN
                t_main_key := getMainKey();
                IF t_main_key IS NULL OR octet_length(t_main_key) NOT IN (16, 24, 32) THEN
                    RAISE EXCEPTION 'upgrade 9.7.3 requires a valid main encryption key';
                END IF;
            END IF;
            -- A secret without client id is a Cisco ASA enable password. The UI requires a username, so use a placeholder.
            INSERT INTO credential (credential_name, username, secret)
                VALUES (concat(r_credential.credential_name, ' (additional credential ', r_credential.id, ')'),
                    COALESCE(NULLIF(r_credential.cloud_client_id, ''), 'enable'),
                    encryptText(COALESCE(r_credential.cloud_client_secret, ''), t_main_key))
                RETURNING id INTO i_additional_credential_id;
            UPDATE credential SET additional_credential_id = i_additional_credential_id WHERE id = r_credential.id;
        END LOOP;
    END IF;

    ALTER TABLE credential DROP COLUMN IF EXISTS cloud_client_id;
    ALTER TABLE credential DROP COLUMN IF EXISTS cloud_client_secret;
END $$;
