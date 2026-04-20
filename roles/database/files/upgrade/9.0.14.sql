insert into config (config_key, config_value, config_user) VALUES ('ownerActiveRuleEmailBody', '', 0) ON CONFLICT DO NOTHING;
insert into config (config_key, config_value, config_user) VALUES ('dailyCheckModules', '[1,2,3,4,5,6,7]', 0) ON CONFLICT DO NOTHING;
ALTER TABLE owner ADD COLUMN IF NOT EXISTS decomm_date Timestamp;
