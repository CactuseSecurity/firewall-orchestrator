CREATE TABLE if not exists owner_lifecycle_state (
    id SERIAL PRIMARY KEY,
    name Varchar NOT NULL
);

alter table owner add column if not exists owner_lifecycle_state_id int;

alter table owner drop constraint if exists owner_owner_lifecycle_state_foreign_key;
ALTER TABLE owner ADD CONSTRAINT owner_owner_lifecycle_state_foreign_key FOREIGN KEY (owner_lifecycle_state_id)REFERENCES owner_lifecycle_state(id) ON DELETE SET NULL;