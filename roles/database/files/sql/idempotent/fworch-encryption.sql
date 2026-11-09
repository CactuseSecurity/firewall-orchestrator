-------------------------------------
-- credentials/secrets encryption
-- the following functions are needed for the upgrade and during installation (to encrypt the ldap passwords in ldap_connection table)
-- for existing installations all encrytion/decryption is done in the UI or in the MW server (for ldap binding)
--
-- Secrets are encrypted in the authenticated format 'fwo-v2:' || base64(IV | AES-256-CBC ciphertext | HMAC-SHA256 tag)
-- (encrypt-then-MAC, pgcrypto offers no AES-GCM). Encryption and authentication key are derived from the main key with
-- HMAC-SHA256. The tag lets decryption with a wrong main key fail reliably (#5476). The legacy format (base64 of
-- IV | AES-CBC ciphertext with the main key itself) is still read, and encryptText re-encrypts it in the new format.
-- Same format as FWO.Encryption.AesEnc (C#) and fwo_encrypt.py (importer).

CREATE EXTENSION IF NOT EXISTS pgcrypto;

CREATE OR REPLACE FUNCTION custom_secret_key(key TEXT, label TEXT) RETURNS BYTEA AS $$
DECLARE
    ba_main_key BYTEA := convert_to(key, 'UTF8');
BEGIN
    IF ba_main_key IS NULL OR octet_length(ba_main_key) NOT IN (16, 24, 32) THEN
        RAISE EXCEPTION 'the main key must be 16, 24 or 32 bytes long';
    END IF;
    RETURN hmac(convert_to(label, 'UTF8'), ba_main_key, 'sha256');
END;
$$ LANGUAGE plpgsql;

CREATE OR REPLACE FUNCTION custom_aes_cbc_hmac_encrypt_base64(plaintext TEXT, key TEXT) RETURNS TEXT AS $$
DECLARE
    iv BYTEA := gen_random_bytes(16);
    authenticated_data BYTEA;
BEGIN
    authenticated_data := iv || encrypt_iv(convert_to(plaintext, 'UTF8'), custom_secret_key(key, 'fwo-secret-encryption-v2'), iv, 'aes-cbc/pad:pkcs');
    -- encode wraps base64 lines after 76 characters
    RETURN 'fwo-v2:' || replace(encode(authenticated_data || hmac(authenticated_data, custom_secret_key(key, 'fwo-secret-authentication-v2'), 'sha256'), 'base64'), E'\n', '');
END;
$$ LANGUAGE plpgsql;

CREATE OR REPLACE FUNCTION custom_aes_cbc_hmac_decrypt_base64(ciphertext TEXT, key TEXT) RETURNS TEXT AS $$
DECLARE
    payload BYTEA;
    authenticated_data BYTEA;
    i_ciphertext_length INTEGER;
BEGIN
    IF ciphertext IS NULL OR left(ciphertext, 7) <> 'fwo-v2:' THEN
        RAISE EXCEPTION 'the secret is not encrypted in the authenticated format';
    END IF;
    payload := decode(substring(ciphertext from 8), 'base64');
    i_ciphertext_length := octet_length(payload) - 16 - 32;
    IF i_ciphertext_length < 16 OR i_ciphertext_length % 16 <> 0 THEN
        RAISE EXCEPTION 'the secret has an invalid length';
    END IF;

    -- verify the tag before decrypting anything: a wrong main key or modified data fails here
    authenticated_data := substring(payload from 1 for 16 + i_ciphertext_length);
    IF hmac(authenticated_data, custom_secret_key(key, 'fwo-secret-authentication-v2'), 'sha256') <> substring(payload from 17 + i_ciphertext_length) THEN
        RAISE EXCEPTION 'the authentication tag of the secret does not match';
    END IF;

    RETURN convert_from(decrypt_iv(substring(authenticated_data from 17), custom_secret_key(key, 'fwo-secret-encryption-v2'),
        substring(authenticated_data from 1 for 16), 'aes-cbc/pad:pkcs'), 'UTF8');
END;
$$ LANGUAGE plpgsql;

-- legacy format, only used for reading secrets encrypted before #5476
CREATE OR REPLACE FUNCTION custom_aes_cbc_encrypt_base64(plaintext TEXT, key TEXT) RETURNS TEXT AS $$
DECLARE
    iv BYTEA;
    encrypted_text BYTEA;
BEGIN
    -- Generate a random IV (Initialization Vector)
    iv := gen_random_bytes(16); -- IV size for AES is typically 16 bytes

    -- Perform AES CBC encryption
    encrypted_text := encrypt_iv(plaintext::BYTEA, key::BYTEA, iv, 'aes-cbc/pad:pkcs');

    -- Combine IV and encrypted text and encode them to base64
    RETURN encode(iv || encrypted_text, 'base64');
END;
$$ LANGUAGE plpgsql;

CREATE OR REPLACE FUNCTION custom_aes_cbc_decrypt_base64(ciphertext TEXT, key TEXT) RETURNS TEXT AS $$
DECLARE
    iv BYTEA;
    encrypted_text BYTEA;
    decrypted_text BYTEA;
BEGIN
    -- Decode the base64 string into IV and encrypted text
    encrypted_text := decode(ciphertext, 'base64');
    
    -- Extract IV from the encrypted text
    iv := substring(encrypted_text from 1 for 16);
    
    -- Extract encrypted text without IV
    encrypted_text := substring(encrypted_text from 17);
    
    -- Perform AES CBC decryption
    decrypted_text := decrypt_iv(encrypted_text, key::BYTEA, iv, 'aes-cbc/pad:pkcs');
    
    -- Return the decrypted text
    RETURN convert_from(decrypted_text, 'UTF8');
END;
$$ LANGUAGE plpgsql;

-- encrypts a secret in the authenticated format, unless it is already encrypted with the key in this format
-- a secret encrypted in the legacy format is re-encrypted in the authenticated format
CREATE OR REPLACE FUNCTION encryptText (plaintext_in text, key_in text) RETURNS text AS $$
DECLARE
    t_plaintext TEXT;
BEGIN
    IF plaintext_in IS NULL THEN
        RETURN NULL;
    END IF;
    -- check if plaintext is actually ciphertext
    BEGIN
        SELECT INTO t_plaintext decryptText(plaintext_in, key_in);
    EXCEPTION WHEN OTHERS THEN
        RETURN custom_aes_cbc_hmac_encrypt_base64(plaintext_in, key_in);
    END;
    -- if we get here without error, the plaintext passed in was actually already encrypted
    IF left(plaintext_in, 7) = 'fwo-v2:' THEN
        RETURN plaintext_in;
    END IF;
    RETURN custom_aes_cbc_hmac_encrypt_base64(t_plaintext, key_in);
END;
$$ LANGUAGE plpgsql VOLATILE;

CREATE OR REPLACE FUNCTION decryptText (cyphertext_in text, key text) RETURNS text AS $$
BEGIN
    IF left(cyphertext_in, 7) = 'fwo-v2:' THEN
        RETURN custom_aes_cbc_hmac_decrypt_base64(cyphertext_in, key);
    END IF;
    RETURN custom_aes_cbc_decrypt_base64(cyphertext_in, key);
EXCEPTION WHEN OTHERS THEN
    RAISE EXCEPTION 'decryption with the given key failed!';
END;
$$ LANGUAGE plpgsql VOLATILE;

-- encrypts all stored secrets that are in plain text or still in the legacy format, runs with every installation
CREATE OR REPLACE FUNCTION encryptPasswords (key text) RETURNS VOID AS $$
DECLARE
    r_cred RECORD;
    t_encrypted TEXT;
    t_secret TEXT;
    j_ticket_system jsonb;
BEGIN
    -- encrypt pwds in credential table
    FOR r_cred IN 
        SELECT id, secret FROM credential
    LOOP
        SELECT INTO t_encrypted * FROM encryptText(r_cred.secret, key);
        UPDATE credential SET secret=t_encrypted WHERE id=r_cred.id;
    END LOOP;

    --encrypt pwds in ldap_connection table
    FOR r_cred IN 
        SELECT ldap_search_user_pwd, ldap_write_user_pwd, ldap_connection_id FROM ldap_connection
    LOOP
        SELECT INTO t_encrypted * FROM encryptText(r_cred.ldap_search_user_pwd, key);
        UPDATE ldap_connection SET ldap_search_user_pwd=t_encrypted WHERE ldap_connection_id=r_cred.ldap_connection_id;
        SELECT INTO t_encrypted * FROM encryptText(r_cred.ldap_write_user_pwd, key);
        UPDATE ldap_connection SET ldap_write_user_pwd=t_encrypted WHERE ldap_connection_id=r_cred.ldap_connection_id;
    END LOOP;

    -- encrypt smtp email user pwds in config table
    SELECT INTO r_cred config_value FROM config WHERE config_key='emailPassword';
    SELECT INTO t_encrypted * FROM encryptText(r_cred.config_value, key);
    UPDATE config SET config_value=t_encrypted WHERE config_key='emailPassword';

    -- encrypted authorization header strings kept by external requests from before 9.7.1 (see upgrade 9.7.1)
    FOR r_cred IN
        SELECT id, ext_ticket_system FROM ext_request WHERE ext_ticket_system LIKE '%"Authorization"%'
    LOOP
        BEGIN
            j_ticket_system := r_cred.ext_ticket_system::jsonb;
        EXCEPTION WHEN OTHERS THEN
            RAISE WARNING 'ticket system of external request % is no valid JSON and keeps its authorization', r_cred.id;
            CONTINUE;
        END;
        IF jsonb_typeof(j_ticket_system) <> 'object' OR COALESCE(j_ticket_system ->> 'Authorization', '') = '' THEN
            CONTINUE;
        END IF;
        t_secret := j_ticket_system ->> 'Authorization';
        SELECT INTO t_encrypted * FROM encryptText(t_secret, key);
        IF t_encrypted IS DISTINCT FROM t_secret THEN
            UPDATE ext_request SET ext_ticket_system = jsonb_set(j_ticket_system, '{Authorization}', to_jsonb(t_encrypted))::text
            WHERE id = r_cred.id;
        END IF;
    END LOOP;

    RETURN;
END;
$$ LANGUAGE plpgsql;

-- get encryption key from filesystem
CREATE OR REPLACE FUNCTION getMainKey() RETURNS TEXT AS $$
DECLARE
    t_key TEXT;
BEGIN
    CREATE TEMPORARY TABLE temp_main_key (key text);
    COPY temp_main_key FROM '/etc/fworch/secrets/main_key' CSV DELIMITER ',';
    SELECT INTO t_key * FROM temp_main_key;
    -- RAISE NOTICE 'main key: "%"', t_key;
    DROP TABLE temp_main_key;
    RETURN t_key;
END; 
$$ LANGUAGE plpgsql;

-- finally do the encryption in the db tables
SELECT * FROM encryptPasswords (getMainKey());

-- function for adding local ldap data with encrypted pwds into ldap_connection
CREATE OR REPLACE FUNCTION insertLocalLdapWithEncryptedPasswords(
    serverName TEXT, 
    port INTEGER,
    userSearchPath TEXT,
    roleSearchPath TEXT, 
    groupSearchPath TEXT,
    groupWritePath TEXT,
    tenantLevel INTEGER,
    searchUser TEXT,
    searchUserPwd TEXT,
    writeUser TEXT,
    writeUserPwd TEXT,
    ldapType INTEGER
) RETURNS VOID AS $$
DECLARE
    t_key TEXT;
    t_encryptedReadPwd TEXT;
    t_encryptedWritePwd TEXT;
BEGIN
    IF NOT EXISTS (SELECT * FROM ldap_connection WHERE serverName = ANY(ldap_server))
    THEN
        SELECT INTO t_key * FROM getMainKey();
        SELECT INTO t_encryptedReadPwd * FROM encryptText(searchUserPwd, t_key);
        SELECT INTO t_encryptedWritePwd * FROM encryptText(writeUserPwd, t_key);
        INSERT INTO ldap_connection
            (ldap_server, ldap_port, ldap_searchpath_for_users, ldap_searchpath_for_roles, ldap_searchpath_for_groups, ldap_writepath_for_groups,
            ldap_tenant_level, ldap_search_user, ldap_search_user_pwd, ldap_write_user, ldap_write_user_pwd, ldap_type)
            VALUES (ARRAY[serverName], port, userSearchPath, roleSearchPath, groupSearchPath, groupWritePath, tenantLevel, searchUser, t_encryptedReadPwd, writeUser, t_encryptedWritePwd, ldapType);
    END IF;
END;
$$ LANGUAGE plpgsql;
