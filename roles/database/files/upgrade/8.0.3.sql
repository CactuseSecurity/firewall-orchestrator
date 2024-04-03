alter table modelling.connection add column if not exists is_requested boolean default false;
alter table modelling.connection add column if not exists ticket_id bigint;
alter table request.ticket add column if not exists owner_id int;

ALTER TABLE request.reqtask DROP CONSTRAINT IF EXISTS request_ticket_owner_foreign_key;
ALTER TABLE request.ticket ADD CONSTRAINT request_ticket_owner_foreign_key FOREIGN KEY (owner_id) REFERENCES owner(id) ON UPDATE RESTRICT ON DELETE CASCADE;


-- insert into request.state (id,name) VALUES (10,'ToAccept') ON CONFLICT DO NOTHING;
-- insert into config (config_key, config_value, config_user) VALUES ('reqNewIntStateMatrix', '{"config_value":{"request":{"matrix":{"0":[0,49,620],"10":[10,600]},"derived_states":{"0":0,"10":10},"lowest_input_state":0,"lowest_start_state":0,"lowest_end_state":49,"active":true},"approval":{"matrix":{},"derived_states":{},"lowest_input_state":0,"lowest_start_state":0,"lowest_end_state":0,"active":false},"planning":{"matrix":{},"derived_states":{},"lowest_input_state":0,"lowest_start_state":0,"lowest_end_state":0,"active":false},"verification":{"matrix":{},"derived_states":{},"lowest_input_state":0,"lowest_start_state":0,"lowest_end_state":0,"active":false},"implementation":{"matrix":{"210":[210,10,610],"49":[210,49,610]},"derived_states":{"210":210,"49":49},"lowest_input_state":49,"lowest_start_state":210,"lowest_end_state":600,"active":true},"review":{"matrix":{},"derived_states":{},"lowest_input_state":0,"lowest_start_state":0,"lowest_end_state":0,"active":false},"recertification":{"matrix":{},"derived_states":{},"lowest_input_state":0,"lowest_start_state":0,"lowest_end_state":0,"active":false}}}', 0) ON CONFLICT DO NOTHING;
-- insert into config (config_key, config_value, config_user) VALUES ('reqNewIntStateMatrixDefault', '{"config_value":{"request":{"matrix":{"0":[0,49,620],"10":[10,600]},"derived_states":{"0":0,"10":10},"lowest_input_state":0,"lowest_start_state":0,"lowest_end_state":49,"active":true},"approval":{"matrix":{},"derived_states":{},"lowest_input_state":0,"lowest_start_state":0,"lowest_end_state":0,"active":false},"planning":{"matrix":{},"derived_states":{},"lowest_input_state":0,"lowest_start_state":0,"lowest_end_state":0,"active":false},"verification":{"matrix":{},"derived_states":{},"lowest_input_state":0,"lowest_start_state":0,"lowest_end_state":0,"active":false},"implementation":{"matrix":{"210":[210,10,610],"49":[210,49,610]},"derived_states":{"210":210,"49":49},"lowest_input_state":49,"lowest_start_state":210,"lowest_end_state":600,"active":true},"review":{"matrix":{},"derived_states":{},"lowest_input_state":0,"lowest_start_state":0,"lowest_end_state":0,"active":false},"recertification":{"matrix":{},"derived_states":{},"lowest_input_state":0,"lowest_start_state":0,"lowest_end_state":0,"active":false}}}', 0) ON CONFLICT DO NOTHING;

insert into config (config_key, config_value, config_user) VALUES ('modReqInterfaceName', '', 0) ON CONFLICT DO NOTHING;
insert into config (config_key, config_value, config_user) VALUES ('modReqEmailSubject', '', 0) ON CONFLICT DO NOTHING;
insert into config (config_key, config_value, config_user) VALUES ('modReqEmailBody', '', 0) ON CONFLICT DO NOTHING;
insert into config (config_key, config_value, config_user) VALUES ('modReqTicketTitle', '', 0) ON CONFLICT DO NOTHING;
insert into config (config_key, config_value, config_user) VALUES ('modReqTaskTitle', '', 0) ON CONFLICT DO NOTHING;
insert into config (config_key, config_value, config_user) VALUES ('reqOwnerBased', 'False', 0) ON CONFLICT DO NOTHING;
