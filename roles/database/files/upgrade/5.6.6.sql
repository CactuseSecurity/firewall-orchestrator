
-- ALTER table "rule_from" ADD column IF NOT EXISTS rule_from_id BIGSERIAL primary key;
-- ALTER TABLE "rule_from" DROP constraint if exists rule_from_pkey;
-- ALTER TABLE "rule_from" ADD constraint rule_from_pkey primary key ("rule_from_id");

ALTER TABLE "rule_to" DROP CONSTRAINT if exists rule_to_pkey;
ALTER table "rule_to" ADD column IF NOT EXISTS user_id BIGINT;
ALTER table "rule_to" ADD column IF NOT EXISTS rule_to_id BIGSERIAL primary key;
Alter table "rule_to" drop constraint if exists rule_to_user_id_fkey;
Alter table "rule_to" drop constraint if exists rule_to_user_id_usr_user_id;
Alter table "rule_to" add constraint rule_to_user_id_usr_user_id FOREIGN KEY 
    ("user_id") references "usr" ("user_id") on update restrict on delete cascade;


ALTER TABLE "log_data_issue" ADD COLUMN IF NOT EXISTS "user_id" INTEGER DEFAULT 0;

ALTER TABLE "log_data_issue" DROP CONSTRAINT IF EXISTS "log_data_issue_uiuser_uiuser_id_fkey" CASCADE;
Alter table "log_data_issue" add CONSTRAINT log_data_issue_uiuser_uiuser_id_fkey foreign key ("user_id") references "uiuser" ("uiuser_id") on update restrict on delete cascade;

ALTER TABLE "log_data_issue" DROP CONSTRAINT IF EXISTS log_data_issue_import_control_control_id_fkey;
ALTER TABLE "log_data_issue" ADD CONSTRAINT log_data_issue_import_control_control_id_fkey FOREIGN KEY ("import_id") REFERENCES "import_control" ("control_id") ON UPDATE RESTRICT ON DELETE CASCADE;

Create table IF NOT EXISTS "alert"
(
	"alert_id" BIGSERIAL,
	"ref_log_id" BIGINT,
	"ref_alert_id" BIGINT,
	"source" VARCHAR NOT NULL,
	"title" VARCHAR,
	"description" VARCHAR,
	"alert_mgm_id" INTEGER,
	"alert_dev_id" INTEGER,
	"alert_timestamp" TIMESTAMP DEFAULT NOW(),
	"user_id" INTEGER DEFAULT 0,
	"ack_by" INTEGER,
	"ack_timestamp" TIMESTAMP,
	"json_data" json,
 primary key ("alert_id")
);
