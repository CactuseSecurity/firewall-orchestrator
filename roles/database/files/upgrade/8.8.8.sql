create table if not exists owner_recertification
(
    id BIGSERIAL PRIMARY KEY,
    owner_id int NOT NULL,
    user_dn varchar,
    recertified boolean default false,
    recert_date Timestamp,
    comment varchar,
    next_recert_date Timestamp
);

alter table "owner" add column if not exists last_recertified Timestamp;
alter table "owner" add column if not exists last_recertifier int;
alter table "owner" add column if not exists last_recertifier_dn Varchar;
alter table "owner" add column if not exists next_recert_date Timestamp;

alter table "owner" drop constraint if exists owner_last_recertifier_uiuser_uiuser_id_f_key;
alter table "owner" add constraint owner_last_recertifier_uiuser_uiuser_id_f_key foreign key (last_recertifier) references uiuser (uiuser_id) on update restrict;
