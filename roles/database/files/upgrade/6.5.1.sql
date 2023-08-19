insert into config (config_key, config_value, config_user) VALUES ('unusedTolerance', '400', 0) ON CONFLICT DO NOTHING;
insert into config (config_key, config_value, config_user) VALUES ('creationTolerance', '90', 0) ON CONFLICT DO NOTHING;
