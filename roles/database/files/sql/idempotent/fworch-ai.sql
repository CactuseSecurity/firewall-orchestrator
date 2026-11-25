-- The credential of the AI provider: the one the global config item aiCredentialId names
-- (FWO.Data.Ai.AiSettings.kCredentialIdKey, set on the AI settings page). Its secret is the API key, or for OAuth 2.0
-- the client secret, with the client id as user name. The technical role ai-harness of the AI harness reads credentials
-- only through this view, so it gets the secret of this single credential and of no other.
-- The config value is compared as text, so a value that is not a number matches no credential instead of failing.
-- New columns go at the end, as CREATE OR REPLACE VIEW can only append columns to an existing view.
CREATE OR REPLACE VIEW v_ai_provider_credential AS
    SELECT
        credential.id,
        credential.is_key_pair,
        credential.secret,
        credential.username
    FROM credential
    JOIN config ON config.config_key = 'aiCredentialId'
        AND config.config_user = 0
        AND config.config_value = credential.id::VARCHAR;
