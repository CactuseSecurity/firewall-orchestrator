-- $Id: 20071205-iso-c001-patchcluster.sql,v 1.1.2.1 2008-06-02 17:04:15 tim Exp $
-- $Source: /home/cvs/iso/package/install/migration/Attic/20071205-iso-c001-patchcluster.sql,v $
-- funktionsfaehiges backup bereithalten
-- import stoppen

insert into stm_obj_typ (obj_typ_id,obj_typ_name) VALUES (17,'voip_sip');

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

-- Bugfix fuer Aendern von Dokumentation

CREATE OR REPLACE FUNCTION set_change_request_info () RETURNS VOID AS $$
DECLARE
	r_change	RECORD;
	v_req_str	VARCHAR;
BEGIN -- jeder changelog_xxx Eintrag wird im Feld change_request_info aktualiert
	FOR r_change IN SELECT log_rule_id FROM changelog_rule
	LOOP UPDATE changelog_rule SET change_request_info = get_request_str('rule', changelog_rule.log_rule_id) WHERE log_rule_id=r_change.log_rule_id;
	END LOOP;

	FOR r_change IN SELECT log_obj_id FROM changelog_object
	LOOP UPDATE changelog_object SET change_request_info = get_request_str('object', changelog_object.log_obj_id) WHERE log_obj_id=r_change.log_obj_id;
	END LOOP;

	FOR r_change IN SELECT log_svc_id FROM changelog_service
	LOOP UPDATE changelog_service SET change_request_info = get_request_str('service', changelog_service.log_svc_id) WHERE log_svc_id=r_change.log_svc_id;
	END LOOP;

	FOR r_change IN SELECT log_usr_id FROM changelog_user
	LOOP UPDATE changelog_user SET change_request_info = get_request_str('user', changelog_user.log_usr_id) WHERE log_usr_id=r_change.log_usr_id;
	END LOOP;
	RETURN;
END; 
$$ LANGUAGE plpgsql;

-- run update on changelog_rule (can take up to 10 minutes)
SELECT * FROM set_change_request_info();

DROP FUNCTION set_change_request_info();

ALTER TABLE import_control ADD COLUMN successful_import Boolean NOT NULL Default TRUE;
ALTER TABLE import_control ADD COLUMN import_errors Varchar;

UPDATE import_control SET successful_import=TRUE;  -- initialize all old imports as successful

UPDATE stm_dev_typ SET dev_typ_version='5.x' WHERE dev_typ_version='5.0';
UPDATE stm_dev_typ SET dev_typ_version='6.x' WHERE dev_typ_version='5.1';
UPDATE stm_dev_typ SET dev_typ_version='R5x' WHERE dev_typ_version='R55';
UPDATE stm_dev_typ SET dev_typ_version='R6x' WHERE dev_typ_version='R60';
UPDATE stm_dev_typ SET dev_typ_version='3.x' WHERE dev_typ_version='3.2';
insert into stm_dev_typ	(dev_typ_id,dev_typ_name,dev_typ_version,dev_typ_manufacturer,dev_typ_predef_svc)
	VALUES (6,'phion netfence','3.x','phion','');

/* manuelle Nachbearbeitung:
	- replace web & importer dir (& check userrights)
	- device administration:
		* check etc/gui.conf for:	usergroup isoadmins privileges:		admin-users admin-devices admin-clients
		* check that deviceadmin users are part of db group isoadmins!
	- die sql-funktionen neu laden: install/bin/db-init2-functions.sh
	- cronjob einrichten fuer import_status: install/bin/write_import_status_file.sh
	- php4 vs. 5 prüfen
	- install/database/iso-grants.sql nachziehen
*/