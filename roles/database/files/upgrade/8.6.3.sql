insert into config (config_key, config_value, config_user) VALUES ('dnsLookup', 'False', 0) ON CONFLICT DO NOTHING;
insert into config (config_key, config_value, config_user) VALUES ('overwriteExistingNames', 'False', 0) ON CONFLICT DO NOTHING;
insert into config (config_key, config_value, config_user) VALUES ('autoReplaceAppServer', 'False', 0) ON CONFLICT DO NOTHING;

ALTER TABLE modelling.change_history ADD COLUMN IF NOT EXISTS change_source Varchar default 'manual';
