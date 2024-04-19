alter table modelling.connection add column if not exists is_requested boolean default false;
alter table modelling.connection add column if not exists ticket_id bigint;
alter table modelling.connection add column if not exists is_published boolean default false;
alter table modelling.connection add column if not exists proposed_app_id int;
alter table owner_network add column if not exists custom_type int;
alter table request.reqtask add column if not exists additional_info varchar;


insert into request.state (id,name) VALUES (205,'Rework') ON CONFLICT DO NOTHING;
insert into config (config_key, config_value, config_user) VALUES ('reqNewIntStateMatrix', '{"config_value":{"request":{"matrix":{"0":[0,49,620]},"derived_states":{"0":0},"lowest_input_state":0,"lowest_start_state":0,"lowest_end_state":49,"active":true},"approval":{"matrix":{"0":[0]},"derived_states":{"0":0},"lowest_input_state":0,"lowest_start_state":0,"lowest_end_state":0,"active":false},"planning":{"matrix":{"0":[0]},"derived_states":{"0":0},"lowest_input_state":0,"lowest_start_state":0,"lowest_end_state":0,"active":false},"verification":{"matrix":{"0":[0]},"derived_states":{"0":0},"lowest_input_state":0,"lowest_start_state":0,"lowest_end_state":0,"active":false},"implementation":{"matrix":{"205":[205,249],"49":[210],"210":[610,210,249]},"derived_states":{"205":205,"49":49,"210":210},"lowest_input_state":49,"lowest_start_state":205,"lowest_end_state":249,"active":true},"review":{"matrix":{"249":[249,205,299]},"derived_states":{"249":249},"lowest_input_state":249,"lowest_start_state":249,"lowest_end_state":299,"active":true},"recertification":{"matrix":{"0":[0]},"derived_states":{"0":0},"lowest_input_state":0,"lowest_start_state":0,"lowest_end_state":0,"active":false}}}', 0) ON CONFLICT DO NOTHING;
insert into config (config_key, config_value, config_user) VALUES ('reqNewIntStateMatrixDefault', '{"config_value":{"request":{"matrix":{"0":[0,49,620]},"derived_states":{"0":0},"lowest_input_state":0,"lowest_start_state":0,"lowest_end_state":49,"active":true},"approval":{"matrix":{"0":[0]},"derived_states":{"0":0},"lowest_input_state":0,"lowest_start_state":0,"lowest_end_state":0,"active":false},"planning":{"matrix":{"0":[0]},"derived_states":{"0":0},"lowest_input_state":0,"lowest_start_state":0,"lowest_end_state":0,"active":false},"verification":{"matrix":{"0":[0]},"derived_states":{"0":0},"lowest_input_state":0,"lowest_start_state":0,"lowest_end_state":0,"active":false},"implementation":{"matrix":{"205":[205,249],"49":[210],"210":[610,210,249]},"derived_states":{"205":205,"49":49,"210":210},"lowest_input_state":49,"lowest_start_state":205,"lowest_end_state":249,"active":true},"review":{"matrix":{"249":[249,205,299]},"derived_states":{"249":249},"lowest_input_state":249,"lowest_start_state":249,"lowest_end_state":299,"active":true},"recertification":{"matrix":{"0":[0]},"derived_states":{"0":0},"lowest_input_state":0,"lowest_start_state":0,"lowest_end_state":0,"active":false}}}', 0) ON CONFLICT DO NOTHING;

insert into config (config_key, config_value, config_user) VALUES ('modReqInterfaceName', '', 0) ON CONFLICT DO NOTHING;
insert into config (config_key, config_value, config_user) VALUES ('modReqEmailSubject', '', 0) ON CONFLICT DO NOTHING;
insert into config (config_key, config_value, config_user) VALUES ('modReqEmailBody', '', 0) ON CONFLICT DO NOTHING;
insert into config (config_key, config_value, config_user) VALUES ('modReqTicketTitle', '', 0) ON CONFLICT DO NOTHING;
insert into config (config_key, config_value, config_user) VALUES ('modReqTaskTitle', '', 0) ON CONFLICT DO NOTHING;
insert into config (config_key, config_value, config_user) VALUES ('reqOwnerBased', 'False', 0) ON CONFLICT DO NOTHING;
insert into config (config_key, config_value, config_user) VALUES ('reqShowCompliance', 'False', 0) ON CONFLICT DO NOTHING;
insert into config (config_key, config_value, config_user) VALUES ('uiHostName', 'http://localhost:5000', 0) ON CONFLICT DO NOTHING;
