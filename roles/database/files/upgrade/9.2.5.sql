INSERT INTO config (config_key, config_value, config_user)
VALUES ('CustomFieldChangeIdKey', '["field-2","ChangeId"]', 0)
ON CONFLICT (config_key, config_user) DO NOTHING;
