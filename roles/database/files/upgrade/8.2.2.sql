ALTER TABLE modelling.connection DROP CONSTRAINT IF EXISTS modelling_connection_used_interface_foreign_key;
ALTER TABLE modelling.connection ADD CONSTRAINT modelling_connection_used_interface_foreign_key FOREIGN KEY (used_interface_id) REFERENCES modelling.connection(id) ON UPDATE RESTRICT;
