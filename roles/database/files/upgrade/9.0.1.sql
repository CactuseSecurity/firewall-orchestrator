CREATE TABLE IF NOT EXISTS rule_to_owner
(
    rule_id bigint NOT NULL,
    owner_id int NOT NULL,
    created bigint NOT NULL,
    removed bigint,
 primary key (rule_id, owner_id, created)
);

ALTER TABLE rule_to_owner DROP CONSTRAINT IF EXISTS rule_to_owner_rule_foreign_key;
ALTER TABLE rule_to_owner ADD CONSTRAINT rule_to_owner_rule_foreign_key FOREIGN KEY (rule_id) REFERENCES rule(rule_id) ON UPDATE RESTRICT ON DELETE CASCADE;

ALTER TABLE rule_to_owner DROP CONSTRAINT IF EXISTS rule_to_owner_owner_foreign_key;
ALTER TABLE rule_to_owner ADD CONSTRAINT rule_to_owner_owner_foreign_key FOREIGN KEY (owner_id) REFERENCES owner(id) ON UPDATE RESTRICT ON DELETE CASCADE;

ALTER TABLE rule_to_owner DROP CONSTRAINT IF EXISTS rule_to_owner_created_import_control_control_id_f_key;
ALTER TABLE rule_to_owner ADD CONSTRAINT rule_to_owner_created_import_control_control_id_f_key FOREIGN KEY (created) REFERENCES import_control(control_id) ON UPDATE RESTRICT ON DELETE CASCADE;

ALTER TABLE rule_to_owner DROP CONSTRAINT IF EXISTS rule_to_owner_removed_import_control_control_id_f_key;
ALTER TABLE rule_to_owner ADD CONSTRAINT rule_to_owner_removed_import_control_control_id_f_key FOREIGN KEY (removed) REFERENCES import_control(control_id) ON UPDATE RESTRICT ON DELETE CASCADE;
