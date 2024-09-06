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

    -- encrypt smtp email user pwds in config table
    SELECT INTO r_cred config_value FROM config WHERE config_key='emailPassword';
    SELECT INTO t_encrypted * FROM encryptText(r_cred.config_value, key);
    UPDATE config SET config_value=t_encrypted WHERE config_key='emailPassword';

    RETURN;
END; 
$$ LANGUAGE plpgsql;

SELECT * FROM encryptPasswords (getMainKey());
