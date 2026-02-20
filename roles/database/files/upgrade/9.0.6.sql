create table if not EXISTS time_object
(
	time_obj_id BIGSERIAL PRIMARY KEY,
	mgm_id Integer NOT NULL,
	time_obj_uid Varchar,
	time_obj_name Varchar,
	start_time TIMESTAMP WITHOUT TIME ZONE,
	end_time TIMESTAMP WITHOUT TIME ZONE,
	created BIGINT,
	removed BIGINT
);

create table if not EXISTS rule_time
(
	rule_time_id BIGSERIAL PRIMARY KEY,
	rule_id BIGINT,
  	time_obj_id BIGINT,
	created BIGINT,
  	removed BIGINT
);


-- create fks
ALTER TABLE time_object DROP CONSTRAINT IF EXISTS time_object_mgm_id_fkey;
ALTER TABLE time_object ADD CONSTRAINT time_object_mgm_id_fkey FOREIGN KEY (mgm_id) REFERENCES management (mgm_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE time_object DROP CONSTRAINT IF EXISTS time_object_created_fkey;
ALTER TABLE time_object ADD CONSTRAINT time_object_created_fkey FOREIGN KEY (created) REFERENCES import_control (control_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE time_object DROP CONSTRAINT IF EXISTS time_object_removed_fkey;
ALTER TABLE time_object ADD CONSTRAINT time_object_removed_fkey FOREIGN KEY (removed) REFERENCES import_control (control_id) ON UPDATE RESTRICT ON DELETE CASCADE;

ALTER TABLE rule_time DROP CONSTRAINT IF EXISTS rule_time_rule_id_fkey;
ALTER TABLE rule_time ADD CONSTRAINT rule_time_rule_id_fkey FOREIGN KEY (rule_id) REFERENCES rule (rule_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE rule_time DROP CONSTRAINT IF EXISTS rule_time_time_obj_id_fkey;
ALTER TABLE rule_time ADD CONSTRAINT rule_time_time_obj_id_fkey FOREIGN KEY (time_obj_id) REFERENCES time_object (time_obj_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE rule_time DROP CONSTRAINT IF EXISTS rule_time_created_fkey;
ALTER TABLE rule_time ADD CONSTRAINT rule_time_created_fkey FOREIGN KEY (created) REFERENCES import_control (control_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE rule_time DROP CONSTRAINT IF EXISTS rule_time_removed_fkey;
ALTER TABLE rule_time ADD CONSTRAINT rule_time_removed_fkey FOREIGN KEY (removed) REFERENCES import_control (control_id) ON UPDATE RESTRICT ON DELETE CASCADE;
