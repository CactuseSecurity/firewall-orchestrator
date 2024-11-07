
alter table modelling.connection add column if not exists extra_params Varchar;

insert into config (config_key, config_value, config_user) VALUES ('modExtraConfigs', '[]', 0) ON CONFLICT DO NOTHING;
