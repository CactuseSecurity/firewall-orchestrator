
-- first drop all views depending on columns to be altered:
DROP VIEW view_obj_changes CASCADE;
DROP VIEW view_change_counter CASCADE;
DROP VIEW view_svc_changes CASCADE;
DROP VIEW view_user_changes CASCADE;
DROP VIEW view_rule_changes CASCADE;
DROP VIEW view_rule_source_or_destination CASCADE;

-- increasing number space of fields expected to potentially above integer 

Alter table "object" ALTER COLUMN "obj_create" TYPE BIGINT;
Alter table "object" ALTER COLUMN "obj_last_seen" TYPE BIGINT;

Alter table "service" ALTER COLUMN "svc_create" TYPE BIGINT;
Alter table "service" ALTER COLUMN "svc_last_seen" TYPE BIGINT;

Alter table "usr" ALTER COLUMN "user_create" TYPE BIGINT;
Alter table "usr" ALTER COLUMN "user_last_seen" TYPE BIGINT;

Alter table "rule" ALTER COLUMN "rule_create" TYPE BIGINT;
Alter table "rule" ALTER COLUMN "rule_last_seen" TYPE BIGINT;

Alter table "import_changelog" ALTER COLUMN "control_id" TYPE BIGINT;
Alter table "import_changelog" ALTER COLUMN "import_changelog_nr" TYPE BIGINT;

Alter table "changelog_object" ALTER COLUMN "control_id" TYPE BIGINT;
Alter table "changelog_object" ALTER COLUMN "new_obj_id" TYPE BIGINT;
Alter table "changelog_object" ALTER COLUMN "old_obj_id" TYPE BIGINT;
Alter table "changelog_object" ALTER COLUMN "abs_change_id" TYPE BIGINT;

Alter table "changelog_service" ALTER COLUMN "control_id" TYPE BIGINT;
Alter table "changelog_service" ALTER COLUMN "new_svc_id" TYPE BIGINT;
Alter table "changelog_service" ALTER COLUMN "old_svc_id" TYPE BIGINT;
Alter table "changelog_service" ALTER COLUMN "abs_change_id" TYPE BIGINT;

Alter table "changelog_user" ALTER COLUMN "control_id" TYPE BIGINT;
Alter table "changelog_user" ALTER COLUMN "new_user_id" TYPE BIGINT;
Alter table "changelog_user" ALTER COLUMN "old_user_id" TYPE BIGINT;
Alter table "changelog_user" ALTER COLUMN "abs_change_id" TYPE BIGINT;

Alter table "changelog_rule" ALTER COLUMN "control_id" TYPE BIGINT;
Alter table "changelog_rule" ALTER COLUMN "new_rule_id" TYPE BIGINT;
Alter table "changelog_rule" ALTER COLUMN "old_rule_id" TYPE BIGINT;
Alter table "changelog_rule" ALTER COLUMN "abs_change_id" TYPE BIGINT;


Alter table "import_object" ALTER COLUMN "control_id" TYPE BIGINT;
Alter table "import_service" ALTER COLUMN "control_id" TYPE BIGINT;
Alter table "import_user" ALTER COLUMN "control_id" TYPE BIGINT;
Alter table "import_rule" ALTER COLUMN "control_id" TYPE BIGINT;
Alter table "import_zone" ALTER COLUMN "control_id" TYPE BIGINT;

Alter table "objgrp" ALTER COLUMN "objgrp_id" TYPE BIGINT;
Alter table "objgrp" ALTER COLUMN "objgrp_member_id" TYPE BIGINT;
Alter table "objgrp" ALTER COLUMN "import_created" TYPE BIGINT;
Alter table "objgrp" ALTER COLUMN "import_last_seen" TYPE BIGINT;

Alter table "rule_from" ALTER COLUMN "rf_create" TYPE BIGINT;
Alter table "rule_from" ALTER COLUMN "rf_last_seen" TYPE BIGINT;
Alter table "rule_from" ALTER COLUMN "rule_id" TYPE BIGINT;
Alter table "rule_from" ALTER COLUMN "obj_id" TYPE BIGINT;
Alter table "rule_from" ALTER COLUMN "user_id" TYPE BIGINT;

Alter table "rule_review" ALTER COLUMN "rule_id" TYPE BIGINT;

Alter table "rule_service" ALTER COLUMN "rule_id" TYPE BIGINT;
Alter table "rule_service" ALTER COLUMN "svc_id" TYPE BIGINT;
Alter table "rule_service" ALTER COLUMN "rs_create" TYPE BIGINT;
Alter table "rule_service" ALTER COLUMN "rs_last_seen" TYPE BIGINT;

Alter table "rule_to" ALTER COLUMN "rule_id" TYPE BIGINT;
Alter table "rule_to" ALTER COLUMN "obj_id" TYPE BIGINT;
Alter table "rule_to" ALTER COLUMN "rt_create" TYPE BIGINT;
Alter table "rule_to" ALTER COLUMN "rt_last_seen" TYPE BIGINT;

Alter table "objgrp" ALTER COLUMN "import_created" TYPE BIGINT;
Alter table "objgrp" ALTER COLUMN "import_last_seen" TYPE BIGINT;

Alter table "svcgrp" ALTER COLUMN "svcgrp_id" TYPE BIGINT;
Alter table "svcgrp" ALTER COLUMN "svcgrp_member_id" TYPE BIGINT;
Alter table "svcgrp" ALTER COLUMN "import_created" TYPE BIGINT;
Alter table "svcgrp" ALTER COLUMN "import_last_seen" TYPE BIGINT;

Alter table "zone" ALTER COLUMN "zone_create" TYPE BIGINT;
Alter table "zone" ALTER COLUMN "zone_last_seen" TYPE BIGINT;

Alter table "usergrp" ALTER COLUMN "usergrp_id" TYPE BIGINT;
Alter table "usergrp" ALTER COLUMN "usergrp_member_id" TYPE BIGINT;
Alter table "usergrp" ALTER COLUMN "import_created" TYPE BIGINT;
Alter table "usergrp" ALTER COLUMN "import_last_seen" TYPE BIGINT;

Alter table "usergrp_flat" ALTER COLUMN "usergrp_flat_id" TYPE BIGINT;
Alter table "usergrp_flat" ALTER COLUMN "usergrp_flat_member_id" TYPE BIGINT;
Alter table "usergrp_flat" ALTER COLUMN "import_created" TYPE BIGINT;
Alter table "usergrp_flat" ALTER COLUMN "import_last_seen" TYPE BIGINT;

Alter table "objgrp_flat" ALTER COLUMN "objgrp_flat_id" TYPE BIGINT;
Alter table "objgrp_flat" ALTER COLUMN "objgrp_flat_member_id" TYPE BIGINT;
Alter table "objgrp_flat" ALTER COLUMN "import_created" TYPE BIGINT;
Alter table "objgrp_flat" ALTER COLUMN "import_last_seen" TYPE BIGINT;

Alter table "svcgrp_flat" ALTER COLUMN "svcgrp_flat_id" TYPE BIGINT;
Alter table "svcgrp_flat" ALTER COLUMN "svcgrp_flat_member_id" TYPE BIGINT;
Alter table "svcgrp_flat" ALTER COLUMN "import_created" TYPE BIGINT;
Alter table "svcgrp_flat" ALTER COLUMN "import_last_seen" TYPE BIGINT;

Alter table "tenant_object" ALTER COLUMN "obj_id" TYPE BIGINT;

Alter table "request_object_change" ALTER COLUMN "log_obj_id" TYPE BIGINT;
Alter table "request_service_change" ALTER COLUMN "log_svc_id" TYPE BIGINT;
Alter table "request_rule_change" ALTER COLUMN "log_rule_id" TYPE BIGINT;
Alter table "request_user_change" ALTER COLUMN "log_usr_id" TYPE BIGINT;

-- add some missing foreign keys

Alter table "usr" add foreign key ("user_create") references "import_control" ("control_id") on update restrict on delete cascade;
Alter table "usr" add foreign key ("user_last_seen") references "import_control" ("control_id") on update restrict on delete cascade;
