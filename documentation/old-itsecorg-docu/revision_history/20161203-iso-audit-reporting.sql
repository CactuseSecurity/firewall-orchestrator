/*
 * 

Austausch importer und web Zweig komplett 
optional: Austauch bin-Zeig (eventuell cronjob anpassen (bin/write_config_import_status_file.sh --> generate_config_import_status_file.sh)

Ubuntu 16.04 javascript packages:
  sudo apt-get install libjs-handlebars libjs-jquery libjs-jquery-datatables libjs-underscore

create alias directory in /etc/apache2/sites-available/itsecorg for above javascript libraries:
Alias /os-js /usr/share/javascript
<Directory "/usr/share/javascript/">
    Options Indexes MultiViews FollowSymLinks
    AllowOverride None
    Order deny,allow
    Allow from all
</Directory>

----

Datenbankänderungen:
	Verstecken alter nicht mehr existenter Systeme:
		device & management
			add field hide_in_gui: boolean
	Hinzufuegen einer neuen "report" Tabelle zum Cachen von reports
	
GUI-Änderungen hierzu: Einstellungen - Systeme - mgmt & dev enthält nun "in GUI verstecken" Feld

Änderungen gui-config:
	usergroup usergroup visible-reports:  config change usage search audit details
	#	audit: audit report for auditor without filter changes
	#	details: audit report for BLB internally with filter config 
	
	Sample setting all non-importing systems to hiding ...
	UPDATE device set hide_in_gui=TRUE WHERE do_not_import AND NOT dev_name LIKE 'zzz%';
	UPDATE management set hide_in_gui=TRUE WHERE do_not_import AND NOT mgm_name LIKE 'zzz%';

*/

-- Database changes:

ALTER TABLE management ADD "hide_in_gui" Boolean NOT NULL Default false;
ALTER TABLE device ADD "hide_in_gui" Boolean NOT NULL Default false;

INSERT INTO text_msg VALUES ('dev_hide_in_gui', 'Device in GUI verstecken', 'hide device in GUI');
INSERT INTO text_msg VALUES ('mgm_hide_in_gui', 'Management in GUI verstecken', 'hide management in GUI');

insert into stm_report_typ (report_typ_id, report_typ_name_german, report_typ_name_english,report_typ_comment_german,report_typ_comment_english)
	VALUES (5, 'Audit-&Auml;nderungen', 'Audit changes', 'Auditorientierte &Auml;nderungsdarstellung', 'Shows changes in an audit-friendly way');

insert into stm_report_typ (report_typ_id, report_typ_name_german, report_typ_name_english,report_typ_comment_german,report_typ_comment_english)
	VALUES (6, 'Audit-&Auml;nderungen Details', 'Audit changes details', 'Auditorientierte &Auml;nderungsdarstellung mit unver&auml;nderten Regeln',
	'Shows changes in an audit-friendly way including unchanged rules');

CREATE OR REPLACE FUNCTION get_import_id_for_dev_at_time (INTEGER,TIMESTAMP) RETURNS INTEGER AS $$
DECLARE
	i_dev_id ALIAS FOR $1; -- ID des Devices
	t_time ALIAS FOR $2; -- Report-Zeitpunkt
	i_mgm_id INTEGER; -- ID des Managements
	i_import_id INTEGER; -- Result
BEGIN
	SELECT INTO i_mgm_id mgm_id FROM device WHERE dev_id=i_dev_id;
	RETURN get_import_id_for_mgmt_at_time(i_mgm_id, t_time);
END;
$$ LANGUAGE plpgsql;

-- drop sequence "public"."report_report_id_seq";
-- drop table report;

Create sequence "public"."report_report_id_seq"
Increment 1
Minvalue 1
Maxvalue 9223372036854775807
Cache 1;

GRANT SELECT, UPDATE ON TABLE report_report_id_seq TO public;

Create table "report"
(
	"report_id" Integer NOT NULL Default nextval('public.report_report_id_seq'::text) UNIQUE,
	"report_typ_id" Integer NOT NULL,
	"start_import_id" Integer NOT NULL,
	"stop_import_id" Integer,
	"dev_id" Integer NOT NULL,
	"report_generation_time" Timestamp NOT NULL Default now(),
	"report_start_time" Timestamp,
	"report_end_time" Timestamp,
	"report_document" Text NOT NULL,
	"client_id" Integer,
 primary key ("report_id")
) With Oids;

Create index "IX_Relationship205" on "report" ("dev_id");
Alter table "report" add  foreign key ("dev_id") references "device" ("dev_id") on update restrict on delete restrict;

Create index "IX_Relationship206" on "report" ("client_id");
Alter table "report" add  foreign key ("client_id") references "client" ("client_id") on update restrict on delete restrict;

Create index "IX_Relationship202" on "report" ("start_import_id");
Alter table "report" add  foreign key ("start_import_id") references "import_control" ("control_id") on update restrict on delete restrict;

Create index "IX_Relationship203" on "report" ("stop_import_id");
Alter table "report" add  foreign key ("stop_import_id") references "import_control" ("control_id") on update restrict on delete restrict;

Create index "IX_Relationship201" on "report" ("report_typ_id");
Alter table "report" add  foreign key ("report_typ_id") references "stm_report_typ" ("report_typ_id") on update restrict on delete restrict;

Grant select on "report" to group "secuadmins";
Grant insert on "report" to group "secuadmins";
Grant select on "report" to group "dbbackupusers";
Grant select on "report" to group "reporters";
Grant insert on "report" to group "reporters";
Grant select on "report" to group "isoadmins";
Grant update on "report" to group "isoadmins";
Grant delete on "report" to group "isoadmins";
Grant insert on "report" to group "isoadmins";

-- if it does not exist: add user confexporter (this is used for running cli db queries)
CREATE ROLE "confexporter" WITH PASSWORD 'l000r' LOGIN;
GRANT reporters TO confexporter; -- add permisions for confexporter to tables (device, ....


/*

documentation of changes to /usr/share/itsecorg files:
	importer
		- fixed missing zone bug in fortinet importer
		- because of warning in import process:
			CGI::param called in list context from , this can lead to vulnerabilities. See the warning in "Fetching the value or values of a single named parameter" at /usr/share/perl5/CGI.pm
			change: 
				my ($mgm_id, $mgm_name) =  &evaluate_parameters((defined(scalar param("mgm_id")))?scalar param("mgm_id"):'', (defined(scalar param("mgm_name")))?scalar param("mgm_name"):'');
	replace web - details:
		- change gui (also # reports)	
		- replace getArgs with getArgs2 (only called in reporting_tables_config_cli.php) for php7 environments (in cli_functions.php)
		- generate report via cli: 
			db-import-ids.php: Also added hide_in_gui filter for All Devices rule search report
		- db-import-ids.php: Also added hide_in_gui filter for All Devices rule search report
		- TODO: remove error from objects, import_ids from each rule

*/

----------------------------------------

/*
	Zukuenftige Erweiterungen (nicht Teil dieses updates):

	--	sudo apt install php-mongodb
	
	Zuordnung von User zu Client (1:n)
		Können wir voraussetzen, dass ein User maximal auf einen Client beschränkt ist?
		table isoadmin
			client_id: integer (wenn = NULL: keine Einschränkung)

	Zuordnung von Clients zu Systemen (n:m)
		Neue Tabellen:
			device_client_map
				client_id
				dev_id
			management_client_map
				client_id
				mgm_id

	Zuordnung von Reporttyp zu Client (n:m)
		Neue Tabellen:
				client_id
				report_typ_id
	Optional: All diese Datenbankänderung per GUI von isoadmin editierbar machen


ALTER TABLE isoadmin ADD "client_id" Integer;
 Testing client filtering:
	UPDATE isoadmin client_id=2 where isoadmin_username='tim';  -- setting to BLB user
 	UPDATE isoadmin client_id=NULL where isoadmin_username='tim';  -- setting to non restricted user

Create table "device_client_map"
(
	"client_id" Integer NOT NULL,
	"dev_id" Integer NOT NULL,
 primary key ("client_id","dev_id")
) With Oids;

Create table "management_client_map"
(
	"client_id" Integer NOT NULL,
	"mgm_id" Integer NOT NULL,
 primary key ("client_id","mgm_id")
) With Oids;

Create table "reporttyp_client_map"
(
	"client_id" Integer NOT NULL,
	"report_typ_id" Integer NOT NULL,
 primary key ("client_id","report_typ_id")
) With Oids;
*/
