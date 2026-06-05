DELETE FROM config
WHERE config_key IN ('accessTokenLifetimeHours', 'refreshTokenLifetimeDays', 'sessionTimeout', 'sessionTimeoutNoticePeriod');
