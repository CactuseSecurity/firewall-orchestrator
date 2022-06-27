insert into config (config_key, config_value, config_user) VALUES ('sessionTimeoutNoticePeriod', '60', 0) ON CONFLICT DO NOTHING;
