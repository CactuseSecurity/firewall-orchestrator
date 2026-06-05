DELETE FROM config
WHERE config_key IN ('accessTokenLifetimeHours', 'refreshTokenLifetimeDays', 'sessionTimeout', 'sessionTimeoutNoticePeriod');

INSERT INTO config (config_key, config_value, config_user) VALUES ('accessTokenLifetime', '1', 0) ON CONFLICT DO NOTHING;
INSERT INTO config (config_key, config_value, config_user) VALUES ('accessTokenLifetimeUnit', 'Hours', 0) ON CONFLICT DO NOTHING;
INSERT INTO config (config_key, config_value, config_user) VALUES ('refreshTokenLifetime', '1', 0) ON CONFLICT DO NOTHING;
INSERT INTO config (config_key, config_value, config_user) VALUES ('refreshTokenLifetimeUnit', 'Days', 0) ON CONFLICT DO NOTHING;
