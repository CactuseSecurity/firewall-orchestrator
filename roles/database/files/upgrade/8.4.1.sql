ALTER TABLE modelling.connection DROP CONSTRAINT IF EXISTS modelling_connection_proposed_app_id_foreign_key;
ALTER TABLE modelling.connection ADD CONSTRAINT modelling_connection_proposed_app_id_foreign_key FOREIGN KEY (proposed_app_id) REFERENCES owner(id) ON UPDATE RESTRICT ON DELETE CASCADE;
insert into config (config_key, config_value, config_user) VALUES ('[]', '', 0) ON CONFLICT DO NOTHING;
