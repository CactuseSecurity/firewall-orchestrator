-- AI settings (AI provider, model, credential of the API key or of the OAuth 2.0 client, authentication method, token
-- URL, additional HTTP headers to the provider, a JSON array of objects with name and value, timeout of a request to
-- the provider in seconds, agent loop and the switches
-- of the AI functions chat and reason check, both off by default, with the roles allowed
-- to use them, a JSON array of role names, empty for every user), edited on the AI settings page and read by the AI
-- harness with its technical user (role ai-harness), see FWO.Data.Ai.AiSettings. The view v_ai_provider_credential is
-- (re)defined by the idempotent fworch-ai.sql.
INSERT INTO config (config_key, config_value, config_user)
VALUES
    ('aiProvider', 'None', 0),
    ('aiModel', '', 0),
    ('aiEndpoint', '', 0),
    ('aiCredentialId', '0', 0),
    ('aiContextWindowTokens', '0', 0),
    ('aiMaxOutputTokens', '0', 0),
    ('aiRequestTimeoutSeconds', '600', 0),
    ('aiMaxIterationsPerRequest', '10', 0),
    ('aiMaxConsecutiveErrorsPerRequest', '3', 0),
    ('aiCompactionReserveTokens', '0', 0),
    ('aiAuthMethod', 'ApiKey', 0),
    ('aiTokenUrl', '', 0),
    ('aiCustomHeaders', '[]', 0),
    ('aiChat', 'False', 0),
    ('aiReasonCheck', 'False', 0),
    ('aiChatRoles', '[]', 0),
    ('aiReasonCheckRoles', '[]', 0)
ON CONFLICT (config_key, config_user) DO NOTHING;
