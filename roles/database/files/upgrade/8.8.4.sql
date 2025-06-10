insert into config (config_key, config_value, config_user) VALUES ('useCustomLogo', 'False', 0) ON CONFLICT DO NOTHING;
insert into config (config_key, config_value, config_user) VALUES ('customLogoData', '', 0) ON CONFLICT DO NOTHING;
insert into stm_action (action_id,action_name) VALUES (30,'ask') ON CONFLICT DO NOTHING; -- cp
