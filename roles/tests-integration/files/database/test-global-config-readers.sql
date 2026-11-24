-- Readers of global settings only read the global rows of table config (config_user 0), also when a user has a row
-- for a global setting, e.g. one written directly in the database (#5734). Roll back every probe.
\set ON_ERROR_STOP 1
BEGIN;

DO $$
DECLARE
    t_key TEXT := getMainKey();
    t_user_password CONSTANT TEXT := 'value of a user row';
    i_user_id INTEGER;
    i_mode_rows BIGINT;
    t_global_mode TEXT;
    t_global_password_before TEXT;
    t_global_password_after TEXT;
    t_password TEXT;
BEGIN
    INSERT INTO uiuser (uiuser_username, uuid) VALUES ('global-config-reader-probe', 'uid=global-config-reader-probe')
        RETURNING uiuser_id INTO i_user_id;

    -- rule ownership mode of the rule owner views
    SELECT config_value INTO t_global_mode FROM config WHERE config_key = 'ruleOwnershipMode' AND config_user = 0;
    INSERT INTO config (config_key, config_value, config_user)
        VALUES ('ruleOwnershipMode', CASE WHEN t_global_mode IS DISTINCT FROM 'exclusive' THEN 'exclusive' ELSE 'mixed' END, i_user_id);

    SELECT count(*) INTO i_mode_rows FROM v_rule_ownership_mode;
    IF i_mode_rows <> 1 THEN
        RAISE EXCEPTION 'global config readers: v_rule_ownership_mode returned % rows instead of the global row only', i_mode_rows;
    END IF;
    IF (SELECT mode FROM v_rule_ownership_mode) IS DISTINCT FROM t_global_mode THEN
        RAISE EXCEPTION 'global config readers: v_rule_ownership_mode did not return the global value';
    END IF;

    -- encryption of the stored email password
    SELECT config_value INTO t_global_password_before FROM config WHERE config_key = 'emailPassword' AND config_user = 0;
    INSERT INTO config (config_key, config_value, config_user) VALUES ('emailPassword', t_user_password, i_user_id);

    PERFORM encryptPasswords(t_key);

    SELECT config_value INTO t_password FROM config WHERE config_key = 'emailPassword' AND config_user = i_user_id;
    IF t_password IS DISTINCT FROM t_user_password THEN
        RAISE EXCEPTION 'global config readers: encryptPasswords changed the emailPassword row of a user';
    END IF;
    IF t_global_password_before IS NOT NULL THEN
        SELECT config_value INTO t_global_password_after FROM config WHERE config_key = 'emailPassword' AND config_user = 0;
        IF decryptText(t_global_password_after, t_key) IS DISTINCT FROM decryptText(encryptText(t_global_password_before, t_key), t_key) THEN
            RAISE EXCEPTION 'global config readers: encryptPasswords did not keep the global emailPassword';
        END IF;
    END IF;
END $$;

ROLLBACK;
