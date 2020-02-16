/*
$Id: 20071003-iso-import-new-method.sql,v 1.1.2.2 2007-12-13 10:47:31 tim Exp $
$Source: /home/cvs/iso/package/install/migration/Attic/20071003-iso-import-new-method.sql,v $
Vorbereitung, um nicht-FW-Systeme einzubinden

die folgenden Dateien müssen aktualisiert werden:
	der gesamte importer-Zweig
außerdem müssen abschliessend die folgenden SQL-Dateien ausgefuehrt werden:
	\i "install/database/iso-import.sql"
	\i "install/database/iso-import-main.sql"
*/

Create sequence "public"."import_changelog_seq"
Increment 1
Minvalue 1
Maxvalue 9223372036854775807
Cache 1;

Create table "import_changelog"
(
	"change_time" Timestamp,
	"management_name" Varchar,
	"changed_object_name" Varchar,
	"changed_object_uid" Varchar,
	"changed_object_type" Varchar,
	"change_action" Varchar NOT NULL,
	"change_admin" Varchar,
	"control_id" Integer NOT NULL,
	"import_changelog_nr" Integer,
	"import_changelog_id" Integer NOT NULL Default nextval('public.import_changelog_seq'::text) UNIQUE,
 primary key ("import_changelog_id")
) With Oids;

Alter Table "import_changelog" add Constraint "Alter_Key14" UNIQUE ("import_changelog_nr","control_id");

Create index "IX_Relationship185" on "import_changelog" ("control_id");
Alter table "import_changelog" add  foreign key ("control_id") references "import_control" ("control_id") on update restrict on delete restrict;

GRANT SELECT ON TABLE import_changelog_seq TO public;
GRANT UPDATE ON TABLE import_changelog_seq TO public;

Grant select on "import_changelog" to group "dbbackupusers";
Grant select on "import_changelog" to group "configimporters";
Grant update on "import_changelog" to group "configimporters";
Grant delete on "import_changelog" to group "configimporters";
Grant insert on "import_changelog" to group "configimporters";
Grant select on "import_changelog" to group "isoadmins";
Grant update on "import_changelog" to group "isoadmins";
Grant delete on "import_changelog" to group "isoadmins";
Grant insert on "import_changelog" to group "isoadmins";
