-- network areas which must not be used in common service connections (empty by default)
insert into config (config_key, config_value, config_user) VALUES ('modComSvcExcludedAreas', '[]', 0) ON CONFLICT DO NOTHING;
