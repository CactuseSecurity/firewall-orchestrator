-- rename isoadmin --> uiuser

alter table "isoadmin" IF EXISTS RENAME TO uiuser;

alter table "uiuser" IF EXISTS RENAME COLUMN isoadmin_id TO uiuser_id;
alter table "uiuser" IF EXISTS RENAME COLUMN isoadmin_username TO uiuser_username;
alter table "uiuser" IF EXISTS RENAME COLUMN isoadmin_first_name TO uiuser_first_name;
alter table "uiuser" IF EXISTS RENAME COLUMN isoadmin_last_name TO uiuser_last_name;
alter table "uiuser" IF EXISTS RENAME COLUMN isoadmin_start_date TO uiuser_start_date;
alter table "uiuser" IF EXISTS RENAME COLUMN isoadmin_end_date TO uiuser_end_date;
alter table "uiuser" IF EXISTS RENAME COLUMN isoadmin_email TO uiuser_email;
alter table "uiuser" IF EXISTS RENAME COLUMN isoadmin_password_must_be_changed TO uiuser_password_must_be_changed;
alter table "uiuser" IF EXISTS RENAME COLUMN isoadmin_id TO uiuser_last_login;
alter table "uiuser" IF EXISTS RENAME COLUMN isoadmin_id TO uiuser_last_password_change;

alter table "uiuser" IF EXISTS ADD COLUMN IF NOT EXISTS uiuser_language Varchar Default "English";

-- todo 