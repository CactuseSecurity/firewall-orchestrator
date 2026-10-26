-- Authenticated secret encryption of fworch-encryption.sql (#5476). Roll back every probe.
\set ON_ERROR_STOP 1
BEGIN;

CREATE TEMP TABLE secret_probe (name text PRIMARY KEY, id integer) ON COMMIT DROP;

-- secrets in the legacy format (AES-CBC without tag), as stored before 9.8.2
WITH inserted AS (
    INSERT INTO credential (credential_name, username, secret)
        VALUES ('secret-encryption-legacy', 'importer', custom_aes_cbc_encrypt_base64('legacy-secret', getMainKey()))
        RETURNING id)
INSERT INTO secret_probe SELECT 'credential', id FROM inserted;
WITH inserted AS (
    INSERT INTO ext_request (ext_ticket_system, ext_request_state, finish_date)
        VALUES (jsonb_build_object('Id', 1, 'Name', 'probe', 'Authorization', custom_aes_cbc_encrypt_base64('Basic legacy', getMainKey()))::text,
            'ExtReqDone', now())
        RETURNING id)
INSERT INTO secret_probe SELECT 'ext_request', id FROM inserted;

DO $$
DECLARE
    t_key TEXT := '0123456789ABCDEF0123456789ABCDEF';
    t_other_key TEXT := 'FEDCBA9876543210FEDCBA9876543210';
    -- shared with AesEncryptionTest.cs and test_fwo_encrypt.py: secrets encrypted by C# must be readable here
    t_vector TEXT := 'fwo-v2:AAECAwQFBgcICQoLDA0OD1ufs4AnvnYj6EC6iJI2Uy3RsaYUWNvBkQ7DpC0hylIZYIegs7IKHHLf45uJxN/AnTf5hrKcTwy/SDPHrST8ET0=';
    t_long_secret TEXT := repeat('long-secret-', 20);
    t_encrypted TEXT;
    t_legacy TEXT;
    ba_payload BYTEA;
    i_accepted INTEGER := 0;
    t_secret TEXT;
BEGIN
    IF decryptText(t_vector, t_key) IS DISTINCT FROM U&'pw:with:colon \00E4\00F6\00FC\20AC' THEN
        RAISE EXCEPTION 'secret encryption: the cross-language vector was not decrypted';
    END IF;

    t_encrypted := encryptText(t_long_secret, t_key);
    IF left(t_encrypted, 7) <> 'fwo-v2:' OR position(E'\n' in t_encrypted) > 0 OR decryptText(t_encrypted, t_key) <> t_long_secret THEN
        RAISE EXCEPTION 'secret encryption: no single line authenticated round trip';
    END IF;
    IF encryptText(t_encrypted, t_key) <> t_encrypted THEN
        RAISE EXCEPTION 'secret encryption: an authenticated secret was encrypted again';
    END IF;

    t_legacy := custom_aes_cbc_encrypt_base64('legacy-secret', t_key);
    t_encrypted := encryptText(t_legacy, t_key);
    IF left(t_encrypted, 7) <> 'fwo-v2:' OR decryptText(t_encrypted, t_key) <> 'legacy-secret' OR decryptText(t_legacy, t_key) <> 'legacy-secret' THEN
        RAISE EXCEPTION 'secret encryption: a legacy secret was not re-encrypted';
    END IF;

    -- the legacy format accepted about 1 in 250 wrong keys through a valid padding by chance
    FOR i IN 1..500 LOOP
        BEGIN
            PERFORM decryptText(encryptText('secret' || i, t_other_key), t_key);
            i_accepted := i_accepted + 1;
        EXCEPTION WHEN OTHERS THEN
            NULL;
        END;
    END LOOP;
    IF i_accepted > 0 THEN
        RAISE EXCEPTION 'secret encryption: % secrets were decrypted with a wrong key', i_accepted;
    END IF;

    ba_payload := decode(substring(t_vector from 8), 'base64');
    ba_payload := set_byte(ba_payload, 20, get_byte(ba_payload, 20) # 1);
    BEGIN
        PERFORM decryptText('fwo-v2:' || replace(encode(ba_payload, 'base64'), E'\n', ''), t_key);
        RAISE EXCEPTION 'secret encryption: a modified secret was decrypted';
    EXCEPTION WHEN raise_exception THEN
        IF SQLERRM <> 'decryption with the given key failed!' THEN
            RAISE;
        END IF;
    END;

    -- installation and upgrade re-encrypt stored legacy secrets
    PERFORM encryptPasswords(getMainKey());
    SELECT secret INTO t_secret FROM credential WHERE id = (SELECT id FROM secret_probe WHERE name = 'credential');
    IF left(t_secret, 7) <> 'fwo-v2:' OR decryptText(t_secret, getMainKey()) <> 'legacy-secret' THEN
        RAISE EXCEPTION 'secret encryption: the legacy credential secret was not migrated';
    END IF;
    SELECT ext_ticket_system::jsonb ->> 'Authorization' INTO t_secret FROM ext_request WHERE id = (SELECT id FROM secret_probe WHERE name = 'ext_request');
    IF left(t_secret, 7) <> 'fwo-v2:' OR decryptText(t_secret, getMainKey()) <> 'Basic legacy' THEN
        RAISE EXCEPTION 'secret encryption: the legacy authorization of the external request was not migrated';
    END IF;
END $$;

ROLLBACK;
