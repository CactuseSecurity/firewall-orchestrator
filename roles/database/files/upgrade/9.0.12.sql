insert into config (config_key, config_value, config_user) VALUES ('ruleExpiryEmailBody', '', 0) ON CONFLICT DO NOTHING;
insert into config (config_key, config_value, config_user) VALUES ('ruleExpiryInitiatorKeys', '{}', 0) ON CONFLICT DO NOTHING;
