
delete from config where config_key='ModAppServerTypes';
insert into config (config_key, config_value, config_user) VALUES ('modAppServerTypes', '[{"Id":0,"Name":"Default"}]', 0) ON CONFLICT DO NOTHING;

create table request.ext_state
(
    id SERIAL PRIMARY KEY,
    name Varchar NOT NULL,
	state_id Integer
);

ALTER TABLE request.ext_state DROP CONSTRAINT IF EXISTS request_ext_state_state_foreign_key;
ALTER TABLE request.ext_state ADD CONSTRAINT request_ext_state_state_foreign_key FOREIGN KEY (state_id) REFERENCES request.state(id) ON UPDATE RESTRICT ON DELETE CASCADE;
