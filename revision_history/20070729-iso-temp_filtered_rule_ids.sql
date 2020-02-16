-- $Id: 20070729-iso-temp_filtered_rule_ids.sql,v 1.1.2.2 2007-12-13 10:47:31 tim Exp $
-- $Source: /home/cvs/iso/package/install/migration/Attic/20070729-iso-temp_filtered_rule_ids.sql,v $

Create table "temp_filtered_rule_ids"
(
	"report_id" Integer NOT NULL,
	"rule_id" Integer NOT NULL Default nextval('public.rule_rule_id_seq'::text)
) With Oids;

Alter Table "temp_filtered_rule_ids" add Constraint "temp_filtered_rule_ids_alt_key" UNIQUE ("report_id","rule_id");

Create index "IX_Relationship182" on "temp_filtered_rule_ids" ("rule_id");
Alter table "temp_filtered_rule_ids" add  foreign key ("rule_id") references "rule" ("rule_id") on update restrict on delete restrict;

Grant select on "temp_filtered_rule_ids" to group "dbbackupusers";

Grant select on "temp_filtered_rule_ids" to group "secuadmins";
Grant update on "temp_filtered_rule_ids" to group "secuadmins";
Grant delete on "temp_filtered_rule_ids" to group "secuadmins";
Grant insert on "temp_filtered_rule_ids" to group "secuadmins";
Grant references on "temp_filtered_rule_ids" to group "secuadmins";

Grant select on "temp_filtered_rule_ids" to group "reporters";
Grant update on "temp_filtered_rule_ids" to group "reporters";
Grant delete on "temp_filtered_rule_ids" to group "reporters";
Grant insert on "temp_filtered_rule_ids" to group "reporters";

Grant select on "temp_filtered_rule_ids" to group "isoadmins";
Grant update on "temp_filtered_rule_ids" to group "isoadmins";
Grant delete on "temp_filtered_rule_ids" to group "isoadmins";
Grant insert on "temp_filtered_rule_ids" to group "isoadmins";

-- folgende Funktionen aus iso-report.sql werden neu angelegt:
   -- CREATE OR REPLACE FUNCTION get_rule_ids(int4, "timestamp", int4, cidr, cidr, cidr, int4, int4, VARCHAR) RETURNS SETOF int4
   -- CREATE OR REPLACE FUNCTION get_rule_ids(int4, "timestamp", int4, VARCHAR) RETURNS SETOF int4 
