ALTER TABLE ext_request ADD COLUMN IF NOT EXISTS wait_cycles int DEFAULT 0;

insert into config (config_key, config_value, config_user) VALUES ('externalRequestWaitCycles', '0', 0) ON CONFLICT DO NOTHING;
