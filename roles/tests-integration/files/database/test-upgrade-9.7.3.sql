-- Exercise the legacy schema on both fresh and upgraded installations. Roll back every probe.
\set ON_ERROR_STOP 1
BEGIN;
ALTER TABLE credential ADD COLUMN IF NOT EXISTS cloud_client_id varchar;
ALTER TABLE credential ADD COLUMN IF NOT EXISTS cloud_client_secret varchar;
ALTER TABLE request.impltask DROP COLUMN IF EXISTS rule_position;

CREATE TEMP TABLE rule_position_probe (impltask_id bigint) ON COMMIT DROP;
WITH inserted AS (
    INSERT INTO request.impltask (state_id, task_type, implementation_action)
    VALUES (0, 'upgrade-rule-position-probe', 'create')
    RETURNING id
)
INSERT INTO rule_position_probe SELECT id FROM inserted;

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
    i_impltask_id bigint;
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
        -- PostgreSQL 18+ reports ON DELETE RESTRICT as restrict_violation, older versions as foreign_key_violation
        EXCEPTION WHEN foreign_key_violation OR restrict_violation THEN
            NULL;
        END;
    END LOOP;
    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'credential'
        AND column_name IN ('cloud_client_id', 'cloud_client_secret')) THEN
        RAISE EXCEPTION 'upgrade 9.7.3: legacy cleartext columns remain';
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'request' AND table_name = 'impltask' AND column_name = 'rule_position'
            AND data_type = 'jsonb' AND is_nullable = 'YES'
    ) THEN
        RAISE EXCEPTION 'upgrade 9.7.3: nullable request.impltask.rule_position jsonb column is missing';
    END IF;
    SELECT impltask_id INTO i_impltask_id FROM rule_position_probe;
    IF (SELECT rule_position FROM request.impltask WHERE id = i_impltask_id) IS NOT NULL THEN
        RAISE EXCEPTION 'upgrade 9.7.3: existing implementation task did not receive a NULL rule position';
    END IF;
    UPDATE request.impltask
    SET rule_position = '{"type":"upgrade.test-position.v1","probe":true}'::jsonb
    WHERE id = i_impltask_id;
END $$;

CREATE TEMP TABLE additional_credential_after_first_run AS SELECT id, additional_credential_id, secret FROM credential;

-- @run-upgrade-9.7.3

DO $$
DECLARE
    i_impltask_id bigint;
BEGIN
    IF EXISTS (
        (SELECT id, additional_credential_id, secret FROM credential EXCEPT SELECT * FROM additional_credential_after_first_run)
        UNION ALL
        (SELECT * FROM additional_credential_after_first_run EXCEPT SELECT id, additional_credential_id, secret FROM credential)
    ) THEN
        RAISE EXCEPTION 'upgrade 9.7.3: rerunning the migration changed credentials';
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'request' AND table_name = 'impltask' AND column_name = 'rule_position'
            AND data_type = 'jsonb' AND is_nullable = 'YES'
    ) THEN
        RAISE EXCEPTION 'upgrade 9.7.3: rerunning the migration changed the rule-position column';
    END IF;
    SELECT impltask_id INTO i_impltask_id FROM rule_position_probe;
    IF (SELECT rule_position FROM request.impltask WHERE id = i_impltask_id)
        IS DISTINCT FROM '{"type":"upgrade.test-position.v1","probe":true}'::jsonb THEN
        RAISE EXCEPTION 'upgrade 9.7.3: rerunning the migration changed an existing rule position';
    END IF;
END $$;
ROLLBACK;
