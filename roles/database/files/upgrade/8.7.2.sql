
ALTER TABLE ext_request ADD COLUMN IF NOT EXISTS attempts int DEFAULT 0;

insert into config (config_key, config_value, config_user) VALUES ('modModelledMarker', 'FWOC', 0) ON CONFLICT DO NOTHING;
insert into config (config_key, config_value, config_user) VALUES ('modModelledMarkerLocation', 'rulename', 0) ON CONFLICT DO NOTHING;
