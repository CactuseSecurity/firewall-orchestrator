alter table report add column if not exists read_only Boolean default FALSE;

alter table owner_recertification drop constraint if exists owner_recertification_owner_foreign_key;
ALTER TABLE owner_recertification ADD CONSTRAINT owner_recertification_owner_foreign_key FOREIGN KEY (owner_id) REFERENCES owner(id) ON UPDATE RESTRICT ON DELETE CASCADE;
