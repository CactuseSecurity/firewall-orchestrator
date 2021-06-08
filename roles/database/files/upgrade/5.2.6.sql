-- the followin functions are replaced with ones containing additional parameters
DROP FUNCTION import_rule_resolved_nwobj (INT,BIGINT,BIGINT);
DROP FUNCTION import_rule_resolved_svc (INT,BIGINT,BIGINT);
DROP FUNCTION import_rule_resolved_usr (INT,BIGINT,BIGINT);

Grant insert,update on "rule_nwobj_resolved" to group "configimporters";
Grant insert,update on "rule_svc_resolved" to group "configimporters";
Grant insert,update on "rule_user_resolved" to group "configimporters";

-- create columns without NOT NULL to allow for migration
Alter table "rule_svc_resolved" ADD COLUMN IF NOT EXISTS "created" BIGINT;
Alter table "rule_svc_resolved" ADD COLUMN IF NOT EXISTS "removed" BIGINT;
Alter table "rule_nwobj_resolved" ADD COLUMN IF NOT EXISTS "created" BIGINT;
Alter table "rule_nwobj_resolved" ADD COLUMN IF NOT EXISTS "removed" BIGINT;
Alter table "rule_user_resolved" ADD COLUMN IF NOT EXISTS "created" BIGINT;
Alter table "rule_user_resolved" ADD COLUMN IF NOT EXISTS "removed" BIGINT;

Alter table "rule_nwobj_resolved" DROP CONSTRAINT IF EXISTS fk_rule_nwobj_resolved_created;
Alter table "rule_nwobj_resolved" DROP CONSTRAINT IF EXISTS fk_rule_nwobj_resolved_removed;
Alter table "rule_svc_resolved" DROP CONSTRAINT IF EXISTS fk_rule_svcobj_resolved_created;
Alter table "rule_svc_resolved" DROP CONSTRAINT IF EXISTS fk_rule_svcobj_resolved_removed;
Alter table "rule_user_resolved" DROP CONSTRAINT IF EXISTS fk_rule_userobj_resolved_created;
Alter table "rule_user_resolved" DROP CONSTRAINT IF EXISTS fk_rule_userobj_resolved_removed;

Alter table "rule_nwobj_resolved" add CONSTRAINT fk_rule_nwobj_resolved_created foreign key ("created") references "import_control" ("control_id") on update restrict on delete cascade;
Alter table "rule_nwobj_resolved" add CONSTRAINT fk_rule_nwobj_resolved_removed foreign key ("removed") references "import_control" ("control_id") on update restrict on delete cascade;
Alter table "rule_svc_resolved" add CONSTRAINT fk_rule_svcobj_resolved_created foreign key ("created") references "import_control" ("control_id") on update restrict on delete cascade;
Alter table "rule_svc_resolved" add CONSTRAINT fk_rule_svcobj_resolved_removed foreign key ("removed") references "import_control" ("control_id") on update restrict on delete cascade;
Alter table "rule_user_resolved" add CONSTRAINT fk_rule_userobj_resolved_created foreign key ("created") references "import_control" ("control_id") on update restrict on delete cascade;
Alter table "rule_user_resolved" add CONSTRAINT fk_rule_userobj_resolved_removed foreign key ("removed") references "import_control" ("control_id") on update restrict on delete cascade;

-- TODO: needs to be replaced by mgm-matching import ids:
UPDATE rule_nwobj_resolved SET created=(select min(control_id) FROM import_control);
UPDATE rule_svc_resolved SET created=(select min(control_id) FROM import_control);
UPDATE rule_user_resolved SET created=(select min(control_id) FROM import_control);

-- now adding not null constraing:
Alter TABLE "rule_nwobj_resolved" alter column created set NOT NULL;
Alter TABLE "rule_svc_resolved" alter column created set NOT NULL;
Alter TABLE "rule_user_resolved" alter column created set NOT NULL;
