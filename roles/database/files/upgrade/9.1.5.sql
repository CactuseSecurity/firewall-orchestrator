INSERT INTO config (config_key, config_value, config_user)
SELECT 'accessTokenLifetime', config_value, config_user
FROM config
WHERE config_key = 'accessTokenLifetimeHours'
ON CONFLICT DO NOTHING;

INSERT INTO config (config_key, config_value, config_user)
SELECT 'accessTokenLifetimeUnit', 'Hours', config_user
FROM config
WHERE config_key = 'accessTokenLifetimeHours'
ON CONFLICT DO NOTHING;

INSERT INTO config (config_key, config_value, config_user)
SELECT 'refreshTokenLifetime', config_value, config_user
FROM config
WHERE config_key = 'refreshTokenLifetimeDays'
ON CONFLICT DO NOTHING;

INSERT INTO config (config_key, config_value, config_user)
SELECT 'refreshTokenLifetimeUnit', 'Days', config_user
FROM config
WHERE config_key = 'refreshTokenLifetimeDays'
ON CONFLICT DO NOTHING;

INSERT INTO config (config_key, config_value, config_user) VALUES ('accessTokenLifetime', '1', 0) ON CONFLICT DO NOTHING;
INSERT INTO config (config_key, config_value, config_user) VALUES ('accessTokenLifetimeUnit', 'Hours', 0) ON CONFLICT DO NOTHING;
INSERT INTO config (config_key, config_value, config_user) VALUES ('refreshTokenLifetime', '1', 0) ON CONFLICT DO NOTHING;
INSERT INTO config (config_key, config_value, config_user) VALUES ('refreshTokenLifetimeUnit', 'Days', 0) ON CONFLICT DO NOTHING;

DELETE FROM config
WHERE config_key IN ('accessTokenLifetimeHours', 'refreshTokenLifetimeDays', 'sessionTimeout', 'sessionTimeoutNoticePeriod');
