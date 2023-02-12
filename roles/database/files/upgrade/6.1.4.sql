ALTER TABLE request.reqelement ALTER COLUMN original_nat_id TYPE bigint;
ALTER TABLE request.reqelement ADD COLUMN IF NOT EXISTS device_id int;
ALTER TABLE request.reqelement ADD COLUMN IF NOT EXISTS rule_uid varchar;
ALTER TABLE request.reqelement DROP CONSTRAINT IF EXISTS request_reqelement_device_foreign_key;
ALTER TABLE request.reqelement ADD CONSTRAINT request_reqelement_device_foreign_key FOREIGN KEY (device_id) REFERENCES device(dev_id) ON UPDATE RESTRICT ON DELETE CASCADE;

ALTER TABLE request.implelement ALTER COLUMN original_nat_id TYPE bigint;
ALTER TABLE request.implelement ADD COLUMN IF NOT EXISTS rule_uid varchar;

ALTER TYPE rule_field_enum ADD VALUE IF NOT EXISTS 'rule';

insert into config (config_key, config_value, config_user) VALUES ('recAutocreateDeleteTicket', 'False', 0) ON CONFLICT DO NOTHING;
insert into config (config_key, config_value, config_user) VALUES ('recDeleteRuleTicketTitle', 'Ticket Title', 0) ON CONFLICT DO NOTHING;
insert into config (config_key, config_value, config_user) VALUES ('recDeleteRuleTicketReason', 'Ticket Reason', 0) ON CONFLICT DO NOTHING;
insert into config (config_key, config_value, config_user) VALUES ('recDeleteRuleReqTaskTitle', 'Task Title', 0) ON CONFLICT DO NOTHING;
insert into config (config_key, config_value, config_user) VALUES ('recDeleteRuleReqTaskReason', 'Task Reason', 0) ON CONFLICT DO NOTHING;
insert into config (config_key, config_value, config_user) VALUES ('recDeleteRuleTicketPriority', '3', 0) ON CONFLICT DO NOTHING;
insert into config (config_key, config_value, config_user) VALUES ('recDeleteRuleInitState', '0', 0) ON CONFLICT DO NOTHING;

drop index if exists only_one_future_recert_per_owner_per_rule;
create unique index if not exists only_one_future_recert_per_owner_per_rule on recertification(owner_id,rule_metadata_id,recert_date) 
    where recert_date IS NULL;

ALTER TABLE owner_network DROP CONSTRAINT IF EXISTS owner_network_ip_unique;
ALTER TABLE owner_network ADD CONSTRAINT owner_network_ip_unique UNIQUE (owner_id, ip);

DELETE FROM owner WHERE name='defaultOwner_demo';
UPDATE owner SET is_default=false WHERE id>0;   -- idempotence
INSERT INTO owner (id, name, dn, group_dn, is_default, tenant_id, recert_interval, app_id_external) 
VALUES    (0, 'super-owner', 'dn-of-super-owner', 'group-dn-for-super-owner', true, 1, 365, 'NONE')
ON CONFLICT DO NOTHING; 
