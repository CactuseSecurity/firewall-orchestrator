-- Exercise the legacy schema on both fresh and upgraded installations. Roll back every probe.
\set ON_ERROR_STOP 1
BEGIN;
ALTER TABLE credential ADD COLUMN IF NOT EXISTS cloud_client_id varchar;
ALTER TABLE credential ADD COLUMN IF NOT EXISTS cloud_client_secret varchar;

CREATE TEMP TABLE additional_credential_probe (parent_id integer, expected_user text, expected_secret text) ON COMMIT DROP;
WITH inserted AS (
    INSERT INTO credential (credential_name, username, secret, cloud_client_id, cloud_client_secret)
    VALUES ('upgrade-9.7.3-cloud', 'importer', encryptText('primary', getMainKey()), 'client-id', 'client-secret'),
        ('upgrade-9.7.3-enable', 'importer', encryptText('primary', getMainKey()), NULL, 'enable-secret'),
        ('upgrade-9.7.3-id-only', 'importer', encryptText('primary', getMainKey()), 'id-only', NULL),
        ('upgrade-9.7.3-empty', 'importer', encryptText('primary', getMainKey()), '', ''),
        ('upgrade-9.7.3-null', 'importer', encryptText('primary', getMainKey()), NULL, NULL),
        ('upgrade-9.7.3-encrypted', 'importer', encryptText('primary', getMainKey()), 'client-encrypted', encryptText('already-encrypted', getMainKey()))
    RETURNING id, cloud_client_id, cloud_client_secret, credential_name
)
INSERT INTO additional_credential_probe
SELECT id,
    CASE WHEN COALESCE(cloud_client_id, '') = '' AND COALESCE(cloud_client_secret, '') = '' THEN ''
        ELSE COALESCE(NULLIF(cloud_client_id, ''), 'enable') END,
    CASE WHEN credential_name = 'upgrade-9.7.3-encrypted' THEN 'already-encrypted' ELSE COALESCE(cloud_client_secret, '') END
FROM inserted;

-- @run-upgrade-9.7.3

DO $$
DECLARE
    r_probe record;
    r_additional record;
    i_link integer;
BEGIN
    FOR r_probe IN SELECT * FROM additional_credential_probe LOOP
        SELECT additional_credential_id INTO i_link FROM credential WHERE id = r_probe.parent_id;
        IF r_probe.expected_user = '' AND r_probe.expected_secret = '' THEN
            IF i_link IS NOT NULL THEN
                RAISE EXCEPTION 'upgrade 9.7.3: empty legacy fields created a credential';
            END IF;
            CONTINUE;
        END IF;
        SELECT * INTO r_additional FROM credential WHERE id = i_link;
        IF NOT FOUND OR r_additional.username IS DISTINCT FROM r_probe.expected_user
            OR decryptText(r_additional.secret, getMainKey()) IS DISTINCT FROM r_probe.expected_secret
            OR r_additional.secret = r_probe.expected_secret THEN
            RAISE EXCEPTION 'upgrade 9.7.3: additional credential was not transferred and encrypted';
        END IF;
        IF decryptText((SELECT secret FROM credential WHERE id = r_probe.parent_id), getMainKey()) <> 'primary' THEN
            RAISE EXCEPTION 'upgrade 9.7.3: primary secret changed';
        END IF;
        BEGIN
            DELETE FROM credential WHERE id = i_link;
            RAISE EXCEPTION 'upgrade 9.7.3: a referenced credential could be deleted';
        EXCEPTION WHEN foreign_key_violation THEN
            NULL;
        END;
    END LOOP;
    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'credential'
        AND column_name IN ('cloud_client_id', 'cloud_client_secret')) THEN
        RAISE EXCEPTION 'upgrade 9.7.3: legacy cleartext columns remain';
    END IF;
END $$;

CREATE TEMP TABLE additional_credential_after_first_run AS SELECT id, additional_credential_id, secret FROM credential;

-- @run-upgrade-9.7.3

DO $$
BEGIN
    IF EXISTS (
        (SELECT id, additional_credential_id, secret FROM credential EXCEPT SELECT * FROM additional_credential_after_first_run)
        UNION ALL
        (SELECT * FROM additional_credential_after_first_run EXCEPT SELECT id, additional_credential_id, secret FROM credential)
    ) THEN
        RAISE EXCEPTION 'upgrade 9.7.3: rerunning the migration changed credentials';
    END IF;
END $$;
ROLLBACK;
