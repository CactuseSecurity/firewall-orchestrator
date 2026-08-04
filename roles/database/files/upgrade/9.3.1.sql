INSERT INTO
    config (
        config_key,
        config_value,
        config_user
    )
VALUES (
        'CustomFieldChangeIdKey',
        '["field-2","ChangeId"]',
        0
    )
ON CONFLICT (config_key, config_user) DO UPDATE
    -- repair rows written as an empty list by the former importer settings page,
    -- which persisted the key unconditionally even when no key was ever entered
    SET config_value = EXCLUDED.config_value
    WHERE config.config_value IS NULL
        OR trim(config.config_value) IN ('', '[]');
