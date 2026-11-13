-- interval in minutes in which the middleware performs the state actions with event "While set"
insert into config (config_key, config_value, config_user) VALUES ('whileSetActionSleepTime', '10', 0) ON CONFLICT DO NOTHING;
