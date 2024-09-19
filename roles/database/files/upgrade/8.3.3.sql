
create table if not exists owner_ticket
(
    owner_id int,
    ticket_id bigint
);

create table if not exists ext_request
(
	id BIGSERIAL PRIMARY KEY,
    owner_id int,
    ticket_id bigint,
    task_number int,
	ext_ticket_system varchar,
	ext_request_type varchar,
	ext_request_content varchar,
	ext_query_variables varchar,
	ext_request_state varchar
);

ALTER TABLE owner_ticket DROP CONSTRAINT IF EXISTS owner_ticket_owner_id_foreign_key;
ALTER TABLE owner_ticket DROP CONSTRAINT IF EXISTS owner_ticket_ticket_id_foreign_key;
ALTER TABLE owner_ticket ADD CONSTRAINT owner_ticket_owner_id_foreign_key FOREIGN KEY (owner_id) REFERENCES owner(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE owner_ticket ADD CONSTRAINT owner_ticket_ticket_id_foreign_key FOREIGN KEY (ticket_id) REFERENCES request.ticket(id) ON UPDATE RESTRICT ON DELETE CASCADE;

ALTER TABLE ext_request DROP CONSTRAINT IF EXISTS ext_request_owner_id_foreign_key;
ALTER TABLE ext_request DROP CONSTRAINT IF EXISTS ext_request_ticket_id_foreign_key;
ALTER TABLE ext_request ADD CONSTRAINT ext_request_owner_id_foreign_key FOREIGN KEY (owner_id) REFERENCES owner(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE ext_request ADD CONSTRAINT ext_request_ticket_id_foreign_key FOREIGN KEY (ticket_id) REFERENCES request.ticket(id) ON UPDATE RESTRICT ON DELETE CASCADE;

ALTER TABLE request.reqelement ADD COLUMN IF NOT EXISTS group_name varchar;
ALTER TABLE request.reqelement ADD COLUMN IF NOT EXISTS ip_end cidr;
ALTER TABLE request.reqelement ADD COLUMN IF NOT EXISTS port_end int;
ALTER TABLE request.implelement ADD COLUMN IF NOT EXISTS group_name varchar;
ALTER TABLE request.implelement ADD COLUMN IF NOT EXISTS ip_end cidr;
ALTER TABLE request.implelement ADD COLUMN IF NOT EXISTS port_end int;

insert into config (config_key, config_value, config_user) VALUES ('externalRequestSleepTime', '0', 0) ON CONFLICT DO NOTHING;
insert into config (config_key, config_value, config_user) VALUES ('externalRequestStartAt', '00:00:00', 0) ON CONFLICT DO NOTHING;
