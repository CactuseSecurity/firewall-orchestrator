alter table notification add column if not exists recipient_bcc Varchar;
alter table notification add column if not exists email_address_bcc Varchar;
