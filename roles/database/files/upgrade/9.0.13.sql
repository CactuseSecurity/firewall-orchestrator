alter table if exists owner_lifecycle_state
    add column if not exists active_state boolean not null default true;
