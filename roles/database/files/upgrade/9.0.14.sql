insert into config (config_key, config_value, config_user) VALUES ('ownerActiveRuleEmailBody', '', 0) ON CONFLICT DO NOTHING;
ALTER TABLE owner ADD COLUMN IF NOT EXISTS decomm_date Timestamp;
