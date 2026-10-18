-- Time objects as single objects of the object_create and object_modify request tasks.
-- An element with field 'time' carries name, start and end of the time object; for object_modify both
-- elements refer to the imported time object, like network_object_id and service_id do for the other kinds.
ALTER TYPE rule_field_enum ADD VALUE IF NOT EXISTS 'time';

ALTER TABLE request.reqelement ADD COLUMN IF NOT EXISTS start_time Timestamp with time zone;
ALTER TABLE request.reqelement ADD COLUMN IF NOT EXISTS end_time Timestamp with time zone;
ALTER TABLE request.reqelement ADD COLUMN IF NOT EXISTS time_obj_id bigint;
ALTER TABLE request.reqelement ADD COLUMN IF NOT EXISTS flow_timeobj_id bigint;

ALTER TABLE request.implelement ADD COLUMN IF NOT EXISTS start_time Timestamp with time zone;
ALTER TABLE request.implelement ADD COLUMN IF NOT EXISTS end_time Timestamp with time zone;
ALTER TABLE request.implelement ADD COLUMN IF NOT EXISTS time_obj_id bigint;

ALTER TABLE request.reqelement DROP CONSTRAINT IF EXISTS request_reqelement_time_object_foreign_key;
ALTER TABLE request.reqelement ADD CONSTRAINT request_reqelement_time_object_foreign_key FOREIGN KEY (time_obj_id) REFERENCES time_object(time_obj_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.reqelement DROP CONSTRAINT IF EXISTS request_reqelement_flow_timeobject_foreign_key;
ALTER TABLE request.reqelement ADD CONSTRAINT request_reqelement_flow_timeobject_foreign_key FOREIGN KEY (flow_timeobj_id) REFERENCES flow.timeobject(timeobj_id) ON UPDATE RESTRICT ON DELETE SET NULL;
ALTER TABLE request.implelement DROP CONSTRAINT IF EXISTS request_implelement_time_object_foreign_key;
ALTER TABLE request.implelement ADD CONSTRAINT request_implelement_time_object_foreign_key FOREIGN KEY (time_obj_id) REFERENCES time_object(time_obj_id) ON UPDATE RESTRICT ON DELETE CASCADE;

-- Planned setting: objects are created by their own object tasks only, groups just assign them.
-- Without effect so far.
INSERT INTO config (config_key, config_value, config_user)
VALUES ('reqCreateObjectsSeparately', 'False', 0)
ON CONFLICT (config_key, config_user) DO NOTHING;
