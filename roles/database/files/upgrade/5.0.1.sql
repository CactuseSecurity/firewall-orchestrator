
-- create table report_schedule

Create table if not exists "report_schedule"
(
	"report_schedule_id" BIGSERIAL,
	"report_template_id" Integer, --FK
	"report_schedule_owner" Integer, --FK
	"report_schedule_start_time" Timestamp NOT NULL,
	"report_schedule_repeat" Integer Not NULL Default 0, -- 0 do not repeat, 2 daily, 2 weekly, 3 monthly, 4 yearly 
	"report_schedule_every" Integer Not NULL Default 1, -- x - every x days/weeks/months/years
 primary key ("report_schedule_id")
);

Alter table if exists "report_schedule" add foreign key ("report_template_id") references "report_template" ("report_template_id") on update restrict on delete cascade;
Alter table if exists "report_schedule" if not exists add foreign key ("report_schedule_owner") references "uiuser" ("uiuser_id") on update restrict on delete cascade;
