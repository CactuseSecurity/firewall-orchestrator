
ALTER TABLE ext_request ADD COLUMN IF NOT EXISTS attempts int DEFAULT 0;

insert into config (config_key, config_value, config_user) VALUES ('modModelledMarker', 'FWOC', 0) ON CONFLICT DO NOTHING;
insert into config (config_key, config_value, config_user) VALUES ('modModelledMarkerLocation', 'rulename', 0) ON CONFLICT DO NOTHING;
insert into config (config_key, config_value, config_user) VALUES ('ruleRecognitionOption', '{"nwRegardIp":true,"nwRegardName":false,"nwRegardGroupName":false,"nwResolveGroup":false,"svcRegardPortAndProt":true,"svcRegardName":false,"svcRegardGroupName":false,"svcResolveGroup":true}', 0) ON CONFLICT DO NOTHING;
insert into config (config_key, config_value, config_user) VALUES ('availableReportTypes', '[1,2,3,4,5,6,7,8,9,10,21,22]', 0) ON CONFLICT DO NOTHING;
