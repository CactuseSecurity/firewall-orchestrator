insert into config (config_key, config_value, config_user) VALUES ('varianceAnalysisSleepTime', '0', 0) ON CONFLICT DO NOTHING;
insert into config (config_key, config_value, config_user) VALUES ('varianceAnalysisStartAt', '00:00:00', 0) ON CONFLICT DO NOTHING;
insert into config (config_key, config_value, config_user) VALUES ('varianceAnalysisSync', 'false', 0) ON CONFLICT DO NOTHING;
insert into config (config_key, config_value, config_user) VALUES ('varianceAnalysisRefresh', 'false', 0) ON CONFLICT DO NOTHING;
insert into config (config_key, config_value, config_user) VALUES ('resolveNetworkAreas', 'false', 0) ON CONFLICT DO NOTHING;
