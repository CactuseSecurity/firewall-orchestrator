-- allow the admin to perform a full rollback (deletion of all import data) of a management.
-- defaults to disabled so existing installations keep the safe behaviour after upgrade.
INSERT INTO config (config_key, config_value, config_user)
VALUES ('allowFullRollback', 'false', 0)
ON CONFLICT DO NOTHING;
