-- Seed the gateway-based global default without overwriting an existing preference.
INSERT INTO config (config_key, config_value, config_user)
VALUES ('defaultManagementRulebaseView', 'False', 0)
ON CONFLICT (config_key, config_user) DO NOTHING;
