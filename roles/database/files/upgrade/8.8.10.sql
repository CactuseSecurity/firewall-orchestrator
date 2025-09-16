alter table report add column if not exists read_only Boolean default FALSE;
alter table report add column if not exists owner_id Integer;

alter table report drop constraint if exists report_owner_foreign_key;
ALTER TABLE report ADD CONSTRAINT report_owner_foreign_key FOREIGN KEY (owner_id) REFERENCES owner(id) ON UPDATE RESTRICT ON DELETE CASCADE;

alter table owner_recertification drop constraint if exists owner_recertification_owner_foreign_key;
ALTER TABLE owner_recertification ADD CONSTRAINT owner_recertification_owner_foreign_key FOREIGN KEY (owner_id) REFERENCES owner(id) ON UPDATE RESTRICT ON DELETE CASCADE;

alter table owner add column if not exists recert_active boolean default false;
