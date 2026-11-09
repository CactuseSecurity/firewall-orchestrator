insert into config (config_key, config_value, config_user) VALUES ('modIntegrationMode', 'FullyIntegrated', 0) ON CONFLICT DO NOTHING;
insert into config (config_key, config_value, config_user) VALUES ('modIntegrationStates', '[]', 0) ON CONFLICT DO NOTHING;
insert into config (config_key, config_value, config_user) VALUES ('modIntegrationStateMarker', 'ImplementationState', 0) ON CONFLICT DO NOTHING;
