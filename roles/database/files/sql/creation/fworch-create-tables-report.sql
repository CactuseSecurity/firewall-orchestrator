-- reporting -------------------------------------------------------

Create table "report_template"
(
	"report_template_id" SERIAL,
	"report_filter" Varchar,
	"report_template_name" Varchar, --  NOT NULL Default "Report_"|"report_id"::VARCHAR,  -- user given name of a report
	"report_template_comment" TEXT,
	"report_template_create" Timestamp DEFAULT now(),
	"report_template_owner" Integer, --FK
	"filterline_history" Boolean Default TRUE, -- every time a filterline is sent, we save it for future usage (auto-deleted every 90 days)
	"report_parameters" json,
	primary key ("report_template_id")
);

Create table "report_format"
(
	"report_format_name" varchar not null,
 	primary key ("report_format_name")
);

Create table "report_schedule_format"
(
	"report_schedule_format_name" VARCHAR not null,
	"report_schedule_id" BIGSERIAL,
 	primary key ("report_schedule_format_name","report_schedule_id")
);

Create table "report"
(
	"report_id" BIGSERIAL,
	"report_template_id" Integer,
	"report_start_time" Timestamp,
	"report_end_time" Timestamp,
	"report_json" json NOT NULL,
	"report_pdf" text,
	"report_csv" text,
	"report_html" text,
	"report_name" varchar NOT NULL,
	"report_owner_id" Integer NOT NULL, --FK to uiuser
	"tenant_wide_visible" Integer,
	"report_type" Integer,
	"description" varchar,
	"read_only" Boolean default FALSE,
	"owner_id" Integer,
 	primary key ("report_id")
);

Create table "report_schedule"
(
	"report_schedule_id" BIGSERIAL,
	"report_schedule_name" Varchar, --  NOT NULL Default "Report_"|"report_id"::VARCHAR,  -- user given name of a report
	"report_template_id" Integer, --FK
	"report_schedule_owner" Integer NOT NULL, --FK
	"report_schedule_start_time" Timestamp NOT NULL,  -- if day is bigger than 28, simply use the 1st of the next month, 00:00 am
	"report_schedule_repeat" Integer Not NULL Default 0, -- 0 do not repeat, 1 daily, 2 weekly, 3 monthly, 4 yearly 
	"report_schedule_every" Integer Not NULL Default 1, -- x - every x days/weeks/months/years
	"report_schedule_active" Boolean Default TRUE,
	"report_schedule_repetitions" Integer,
	"report_schedule_counter" Integer Not NULL Default 0,
	"archive" Boolean Not NULL Default FALSE,
 	primary key ("report_schedule_id")
);

Create table "report_template_viewable_by_user"
(
	"report_template_id" Integer NOT NULL,
	"uiuser_id" Integer NOT NULL,
 	primary key ("uiuser_id","report_template_id")
);
