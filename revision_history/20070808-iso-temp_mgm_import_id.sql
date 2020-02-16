-- $Id: 20070808-iso-temp_mgm_import_id.sql,v 1.1.2.2 2007-12-13 10:47:31 tim Exp $
-- $Source: /home/cvs/iso/package/install/migration/Attic/20070808-iso-temp_mgm_import_id.sql,v $

Create table "temp_mgmid_importid_at_report_time"
(
	"control_id" Integer NOT NULL Default nextval('public.import_control_id_seq'::text),
	"mgm_id" Integer NOT NULL Default nextval('public.management_mgm_id_seq'::text),
	"report_id" Integer NOT NULL
) With Oids;

Alter Table "temp_mgmid_importid_at_report_time" add Constraint "Alter_Key13" UNIQUE ("control_id","mgm_id","report_id");

Create index "IX_Relationship184" on "temp_mgmid_importid_at_report_time" ("mgm_id");
Alter table "temp_mgmid_importid_at_report_time" add  foreign key ("mgm_id") references "management" ("mgm_id") on update restrict on delete restrict;

Create index "IX_Relationship183" on "temp_mgmid_importid_at_report_time" ("control_id");
Alter table "temp_mgmid_importid_at_report_time" add  foreign key ("control_id") references "import_control" ("control_id") on update restrict on delete restrict;

Grant select on "temp_mgmid_importid_at_report_time" to group "secuadmins";
Grant update on "temp_mgmid_importid_at_report_time" to group "secuadmins";
Grant delete on "temp_mgmid_importid_at_report_time" to group "secuadmins";
Grant insert on "temp_mgmid_importid_at_report_time" to group "secuadmins";
Grant select on "temp_mgmid_importid_at_report_time" to group "dbbackupusers";
Grant select on "temp_mgmid_importid_at_report_time" to group "reporters";
Grant update on "temp_mgmid_importid_at_report_time" to group "reporters";
Grant delete on "temp_mgmid_importid_at_report_time" to group "reporters";
Grant insert on "temp_mgmid_importid_at_report_time" to group "reporters";
Grant select on "temp_mgmid_importid_at_report_time" to group "isoadmins";
Grant update on "temp_mgmid_importid_at_report_time" to group "isoadmins";
Grant delete on "temp_mgmid_importid_at_report_time" to group "isoadmins";
Grant insert on "temp_mgmid_importid_at_report_time" to group "isoadmins";
