-- add default config value to avoid warnings
insert into config (config_key, config_value, config_user) VALUES ('modCommonAreas', '', 0) ON CONFLICT DO NOTHING;

-- add custom fields as jsonb
Alter table rule add column if not exists rule_custom_fields JSONB;
Alter table import_rule add column if not exists rule_custom_fields JSONB;


-- adding imported custom rule fields 
-- replaced CREATE OR REPLACE FUNCTION insert_single_rule(BIGINT,INTEGER,INTEGER,BIGINT,BOOLEAN) RETURNS BIGINT AS $$
-- new compare function for jsonb necessary for custom rule fields
CREATE OR REPLACE FUNCTION are_equal (jsonb, jsonb)
    RETURNS boolean
    AS $$
BEGIN
    IF (($1 IS NULL AND $2 IS NULL) OR $1 = $2) THEN
        RETURN TRUE;
    ELSE
        RETURN FALSE;
    END IF;
END;
$$
LANGUAGE plpgsql;

-------------------------------------
-- credentials/secrets encryption
-- the following functions are needed for the upgrade and during installation (to encrypt the ldap passwords in ldap_connection table)
-- for existing installations all encrytion/decryption is done in the UI or in the MW server (for ldap binding)

CREATE EXTENSION IF NOT EXISTS pgcrypto;

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

CREATE OR REPLACE FUNCTION encryptText (plaintext_in text, key_in text) RETURNS text AS $$
DECLARE
    t_cyphertext TEXT;
    t_plaintext TEXT;
    t_crypt_algo TEXT := 'cipher-algo=aes256';
    t_coding_algo TEXT := 'base64';
    -- ba_iv bytea;
BEGIN
    -- check if plaintext is actually ciphertext
    BEGIN
        SELECT into t_plaintext custom_aes_cbc_decrypt_base64(plaintext_in, key_in);
        -- if we get here without error, the plaintext passed in was actually already encrypted
        RETURN plaintext_in;
    EXCEPTION WHEN OTHERS THEN
        RETURN custom_aes_cbc_encrypt_base64(plaintext_in, key_in);
    END;
END; 
$$ LANGUAGE plpgsql VOLATILE;

CREATE OR REPLACE FUNCTION decryptText (cyphertext_in text, key text) RETURNS text AS $$
DECLARE
    t_plaintext TEXT;
    t_crypt_algo TEXT := 'cipher-algo=aes-256-cbc/pad:pkcs';
    t_coding_algo TEXT := 'base64';
BEGIN
    BEGIN
        SELECT INTO t_plaintext custom_aes_cbc_decrypt_base64(cyphertext_in, key); 
        RETURN t_plaintext;
    EXCEPTION WHEN OTHERS THEN
        -- decryption did not work out, so assuming that text was not encrypted
        RAISE EXCEPTION 'decryption with the given key failed!';
    END;

END; 
$$ LANGUAGE plpgsql VOLATILE;

CREATE OR REPLACE FUNCTION encryptPasswords (key text) RETURNS VOID AS $$
DECLARE
    r_cred RECORD;
    t_encrypted TEXT;
BEGIN
    -- encrypt pwds in import_credential table
    FOR r_cred IN 
        SELECT id, secret FROM import_credential
    LOOP
        SELECT INTO t_encrypted * FROM encryptText(r_cred.secret, key);
        UPDATE import_credential SET secret=t_encrypted WHERE id=r_cred.id;
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
-- test using: SELECT * FROM custom_aes_cbc_decrypt_base64(custom_aes_cbc_encrypt_base64('xxx', 'xxx'), 'xxx');

-- function for adding local ldap data with encrypted pwds into ldap_connection
CREATE OR REPLACE FUNCTION insertLocalLdapWithEncryptedPasswords(
    serverName TEXT, 
    port INTEGER,
    userSearchPath TEXT,
    roleSearchPath TEXT, 
    groupSearchPath TEXT,
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
    IF NOT EXISTS (SELECT * FROM ldap_connection WHERE ldap_server = serverName)
    THEN
        SELECT INTO t_key * FROM getMainKey();
        SELECT INTO t_encryptedReadPwd * FROM encryptText(searchUserPwd, t_key);
        SELECT INTO t_encryptedWritePwd * FROM encryptText(writeUserPwd, t_key);
        INSERT INTO ldap_connection
            (ldap_server, ldap_port, ldap_searchpath_for_users, ldap_searchpath_for_roles, ldap_searchpath_for_groups,
            ldap_tenant_level, ldap_search_user, ldap_search_user_pwd, ldap_write_user, ldap_write_user_pwd, ldap_type)
            VALUES (serverName, port, userSearchPath, roleSearchPath, groupSearchPath, tenantLevel, searchUser, t_encryptedReadPwd, writeUser, t_encryptedWritePwd, ldapType);
    END IF;
END;
$$ LANGUAGE plpgsql;
-- test using: SELECT * FROM insertLocalLdapWithEncryptedPasswords('127.0.0.3', 636, 'ou=operator,ou=user,dc=fworch,dc=internal','ou=role,dc=fworch,dc=internal','ou=group,dc=fworch,dc=internal',5,'inspector','xxx','ldapwriter','xxx',2);
