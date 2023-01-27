ALTER TABLE request.reqelement ALTER COLUMN original_nat_id TYPE bigint;
ALTER TABLE request.reqelement ADD COLUMN IF NOT EXISTS device_id int;
ALTER TABLE request.reqelement ADD COLUMN IF NOT EXISTS rule_uid varchar;
ALTER TABLE request.reqelement DROP CONSTRAINT IF EXISTS request_reqelement_device_foreign_key;
ALTER TABLE request.reqelement ADD CONSTRAINT request_reqelement_device_foreign_key FOREIGN KEY (device_id) REFERENCES device(dev_id) ON UPDATE RESTRICT ON DELETE CASCADE;

ALTER TABLE request.implelement ALTER COLUMN original_nat_id TYPE bigint;
ALTER TABLE request.implelement ADD COLUMN IF NOT EXISTS rule_uid varchar;

ALTER TYPE rule_field_enum ADD VALUE IF NOT EXISTS 'rule';
