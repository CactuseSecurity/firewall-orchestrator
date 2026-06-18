ALTER TABLE request.ticket ADD COLUMN IF NOT EXISTS locked boolean NOT NULL DEFAULT FALSE;
ALTER TABLE request.reqtask ADD COLUMN IF NOT EXISTS locked boolean NOT NULL DEFAULT FALSE;

insert into config (config_key, config_value, config_user) VALUES ('reqFlowIntegration', '{"select_objects":"both","select_services":"both","select_time_objects":"both","time_object_precision":"seconds"}', 0) ON CONFLICT DO NOTHING;
delete from config where config_key = 'reqAllowObjectSearch';
insert into config (config_key, config_value, config_user) VALUES ('reqConsiderBundling', 'False', 0) ON CONFLICT DO NOTHING;
