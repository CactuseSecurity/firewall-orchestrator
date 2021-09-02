Create table IF NOT EXISTS "import_config"
(
	"import_id" BIGINT NOT NULL,
	"config" jsonb NOT NULL,
 primary key ("import_id")
);

Create table "import_full_config"
(
	"import_id" BIGINT NOT NULL,
	"config" jsonb NOT NULL,
 primary key ("import_id")
);

Alter table "import_config" drop constraint if exists "import_config_import_id_f_key" CASCADE;
Alter table "import_config" add constraint "import_config_import_id_f_key"  foreign key ("import_id") references "import_control" ("control_id") on update restrict on delete cascade;

Alter table "import_full_config" drop constraint if exists "import_full_config_import_id_f_key" CASCADE;
Alter table "import_full_config" add constraint "import_full_config_import_id_f_key"  foreign key ("import_id") references "import_control" ("control_id") on update restrict on delete cascade;

DROP INDEX IF EXISTS import_control_only_one_null_stop_time_per_mgm_when_null;

CREATE UNIQUE INDEX import_control_only_one_null_stop_time_per_mgm_when_null
    ON import_control
       (mgm_id)
 WHERE stop_time IS NULL;
 