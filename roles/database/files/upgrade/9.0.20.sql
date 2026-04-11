alter table notification add column if not exists email_body Varchar;
alter table notification add column if not exists schedule_id Integer;
alter table notification add column if not exists bundle_type Varchar;
alter table notification add column if not exists bundle_id Varchar;
alter table report_schedule add column if not exists archive Boolean NOT NULL Default FALSE;

alter table notification drop constraint if exists notification_report_schedule_foreign_key;
alter table notification add constraint notification_report_schedule_foreign_key
    foreign key (schedule_id) references report_schedule(report_schedule_id) on update restrict on delete set null;
