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

create table if not exists notification
(
    id SERIAL PRIMARY KEY,
	notification_client Varchar,
	user_id int,
	owner_id int,
	channel Varchar,
	recipient_to Varchar,
    email_address_to Varchar,
	recipient_cc Varchar,
	email_address_cc Varchar,
	email_subject Varchar,
	layout Varchar,
	deadline Varchar,
	interval_before_deadline int,
	offset_before_deadline int,
	repeat_interval_after_deadline int,
	repeat_offset_after_deadline int,
	repetitions_after_deadline int,
	last_sent Timestamp
);

alter table notification drop constraint if exists notification_owner_foreign_key;
ALTER TABLE notification ADD CONSTRAINT notification_owner_foreign_key FOREIGN KEY (owner_id) REFERENCES owner(id) ON UPDATE RESTRICT ON DELETE CASCADE;
alter table notification drop constraint if exists notification_user_foreign_key;
ALTER TABLE notification ADD CONSTRAINT notification_user_foreign_key FOREIGN KEY (user_id) REFERENCES uiuser(uiuser_id) ON UPDATE RESTRICT ON DELETE CASCADE;

alter table owner add column if not exists last_recertified Timestamp;
alter table owner add column if not exists last_recertifier int;
alter table owner add column if not exists last_recertifier_dn Varchar;
alter table owner add column if not exists next_recert_date Timestamp;

alter table owner drop constraint if exists owner_last_recertifier_uiuser_uiuser_id_f_key;
alter table owner add constraint owner_last_recertifier_uiuser_uiuser_id_f_key foreign key (last_recertifier) references uiuser (uiuser_id) on update restrict;


DO $$
BEGIN
  -- recertification.owner_recert_id → owner_recertification(id)
  IF NOT EXISTS (
    SELECT 1
    FROM pg_constraint c
    JOIN pg_class t ON t.oid = c.conrelid
    JOIN pg_namespace n ON n.oid = t.relnamespace
    WHERE c.conname = 'recertification_owner_recertification_foreign_key'
      AND t.relname = 'recertification'
      AND n.nspname = current_schema()
  ) THEN
    ALTER TABLE recertification
      ADD CONSTRAINT recertification_owner_recertification_foreign_key
      FOREIGN KEY (owner_recert_id)
      REFERENCES owner_recertification(id)
      ON UPDATE RESTRICT
      ON DELETE CASCADE;
  END IF;

  -- owner_recertification.owner_id → owner(id)
  IF NOT EXISTS (
    SELECT 1
    FROM pg_constraint c
    JOIN pg_class t ON t.oid = c.conrelid
    JOIN pg_namespace n ON n.oid = t.relnamespace
    WHERE c.conname = 'owner_recertification_owner_foreign_key'
      AND t.relname = 'owner_recertification'
      AND n.nspname = current_schema()
  ) THEN
    ALTER TABLE owner_recertification
      ADD CONSTRAINT owner_recertification_owner_foreign_key
      FOREIGN KEY (owner_id)
      REFERENCES owner(id)
      ON UPDATE RESTRICT
      ON DELETE CASCADE;
  END IF;

  -- owner_recertification.report_id → report(report_id)
  IF NOT EXISTS (
    SELECT 1
    FROM pg_constraint c
    JOIN pg_class t ON t.oid = c.conrelid
    JOIN pg_namespace n ON n.oid = t.relnamespace
    WHERE c.conname = 'owner_recertification_report_foreign_key'
      AND t.relname = 'owner_recertification'
      AND n.nspname = current_schema()
  ) THEN
    ALTER TABLE owner_recertification
      ADD CONSTRAINT owner_recertification_report_foreign_key
      FOREIGN KEY (report_id)
      REFERENCES report(report_id)
      ON UPDATE RESTRICT
      ON DELETE CASCADE;
  END IF;
END
$$;


insert into config (config_key, config_value, config_user) VALUES ('modDecommEmailReceiver', 'None', 0) ON CONFLICT DO NOTHING;
insert into config (config_key, config_value, config_user) VALUES ('modDecommEmailSubject', '', 0) ON CONFLICT DO NOTHING;
insert into config (config_key, config_value, config_user) VALUES ('modDecommEmailBody', '', 0) ON CONFLICT DO NOTHING;
