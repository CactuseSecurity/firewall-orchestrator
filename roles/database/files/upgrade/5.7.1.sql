insert into config (config_key, config_value, config_user) VALUES ('sessionTimeout', '240', 0) ON CONFLICT DO NOTHING;
insert into config (config_key, config_value, config_user) VALUES ('maxMessages', '3', 0) ON CONFLICT DO NOTHING;
