/*
	Beschreibung:
	Um in der DB festzulegen, welches Mgmt von welchem import-Prozess importiert wird, wurde die Tabelle 
	management um das feld importer_hostname erweitert.
	
	!!! Diese Änderung wurde erstmalig nur in itsecorg-db-model.sql und nicht im case-studio-modellierer vorgenommen,
	!!!	da dieser derzeit auf Windows 7 nicht mehr lauffähig ist

	Änderungen gibt es in 	
		install/database/itsecorg-db-model.sql:
		Create table "management"
		(
			"mgm_id" Integer NOT NULL Default nextval('public.management_mgm_id_seq'::text),
			"dev_typ_id" Integer NOT NULL,
			"mgm_name" Varchar NOT NULL,
			"mgm_comment" Text,
			"client_id" Integer,
			"mgm_create" Timestamp NOT NULL Default now(),
			"mgm_update" Timestamp NOT NULL Default now(),
			"ssh_public_key" Text NOT NULL Default 'leer',
			"ssh_private_key" Text NOT NULL,
			"ssh_hostname" Varchar NOT NULL,
			"ssh_port" Integer NOT NULL Default 22,
			"ssh_user" Varchar NOT NULL Default 'itsecorg',
			"last_import_md5_complete_config" Varchar Default 0,
			"last_import_md5_rules" Varchar Default 0,
			"last_import_md5_objects" Varchar Default 0,
			"last_import_md5_users" Varchar Default 0,
			"do_not_import" Boolean NOT NULL Default FALSE,
			"clearing_import_ran" Boolean NOT NULL Default false,
			"force_initial_import" Boolean NOT NULL Default FALSE,
			"config_path" Varchar,
			"importer_hostname" VARCHAR,
		 primary key ("mgm_id")
		) With Oids;
		
		importer/
			iso-importer-loop.pl - import nur bei importer_hostname = NULL oder = local hostname
			CACTUS/ISO/import/checkpoint.pm	--> Parser der (extern definierten) Usergruppen aus rulebases.fws
				sub cp_parse_users_from_rulebase { # ($rulebase_file)
			CACTUS/ISO/read_config.pm
				sub read_config {
					my $param = shift;
					my $confdir =  '/usr/share/itsecorg/etc';

		web/htdocs/
			index.php: php-code multilanguage durch statischen (englischen) Text ersetzt, da User noch nicht bekannt
			inctxt/version.inc.php: 4.3.1
			reporting_tables_config_cli.php: client_filter repariert
			reporting_tables.php: session_start eingebaut und "end of config" ergaenzt
			config/config_dev.php: config fuer importer_name eingebaut
			config/config_single_mgm.php: config fuer importer_name eingebaut
		web/include/
			db-client.php: cosmetics
			db-rule.php: minor - suppressed php warning
			display-filter.hp - client_net_array in filter eingebaut - ist das noch notwendig?
			multi-language.php - minor - suppressed php warning
			display_rule_config.php:
				 1) client-filterung von gruppen in csv-reports
				 2) any-match ausschalten auch in cli-version (dort fest verdrahtet für csv?)
				 3) any-match ausschalten auch fuer client-filter

		in import.conf ergaenzen:
		ssh_client_screenos		/usr/share/itsecorg/importer/iso-ssh-client.pl
		
		Generell wurde alles von /usr/local/itsecorg nach /usr/share/itsecorg verschoben 
		kann man mittels softlink sanft migrieren:
		ln -s /usr/local/itsecorg /usr/share/itsecorg		
			
		etc/import.conf
			ssh_client_screenos		/usr/share/itsecorg/importer/iso-ssh-client.pl
 */

--		Umbenennen der Datenbank von isov1 auf isodb	
ALTER DATABASE isov1 rename to isodb;
-- anschließend alle Browser zumachen und postgres service restarten

-- Hinzufuegen des importer_hostname Felds: 
ALTER TABLE management ADD "importer_hostname" VARCHAR;

-- existiert evtl. schon:
insert into isoadmin (isoadmin_id,isoadmin_first_name,isoadmin_last_name,isoadmin_username) VALUES (1,'Check Point Security Management Server Update Process','Check Point','auto');
insert into stm_dev_typ (dev_typ_id,dev_typ_name,dev_typ_version,dev_typ_manufacturer,dev_typ_predef_svc) VALUES (7,'Check Point','R7x','Check Point','');
insert into stm_dev_typ (dev_typ_id,dev_typ_name,dev_typ_version,dev_typ_manufacturer,dev_typ_predef_svc) VALUES (9,'Check Point','R8x','Check Point','');

/*	
Patch-Anleitung:
================
1) Import-Prozess anhalten
2) Filesystem-Änderungen:
  2.1) Auf allen Systemen:
	entweder:
		ln -s /usr/local/itsecorg /usr/share/itsecorg
	oder direkt in /usr/share/itsecorg installieren:
		sudo mv /usr/local/itsecorg /usr/share/
		mv /usr/local/itsecorg /usr/local/itsecorg/old
	in etc/iso.conf den DB-Namen von isov1 auf isodb ändern
	cron.daily wg. backup (isodb)	
  2.2) Auf allen Importer-Modulen:
    importer-Zweig austauschen
	in import.conf ergaenzen:
	ssh_client_screenos		/usr/share/itsecorg/importer/iso-ssh-client.pl
	init-script vom Importer --> Pfad anpassen
  2.3) Auf allen GUI-Systemen:
    web-Zweig austauschen
		/etc/php5/apache2/php.ini, /etc/php5/cli/php.ini
		 include_path
		 doc_root
		apache-site
	service apache2 restart
3) Änderungen an der DB via psql:
# sessions zur DB terminieren:
sudo service postgresql restart
# DB umbenennen:
sudo -u postgres psql -c "ALTER DATABASE isov1 rename to isodb"
/etc/passwd --> itsecorg home auf /usr/share/itsecorg aendern
# anschließend alle GUI-Browser zumachen

-- Hinzufuegen des importer_hostname Felds: 
sudo -u postgres psql -d isodb -c 'ALTER TABLE management ADD "importer_hostname" VARCHAR';

-- einige der Einträge existieren evtl. schon, dann schlägt der Befehlt fehl - nicht weiter schlimm:
sudo -u postgres psql -d isodb
insert into isoadmin (isoadmin_id,isoadmin_first_name,isoadmin_last_name,isoadmin_username) VALUES (1,'Check Point Security Management Server Update Process','Check Point','auto');
insert into stm_dev_typ (dev_typ_id,dev_typ_name,dev_typ_version,dev_typ_manufacturer,dev_typ_predef_svc) VALUES (7,'Check Point','R7x','Check Point','');
insert into stm_dev_typ (dev_typ_id,dev_typ_name,dev_typ_version,dev_typ_manufacturer,dev_typ_predef_svc) VALUES (17,'Check Point','R8x','Check Point','');

4) importer_hostname für jedes Management füllen
sudo -u postgres psql -d isodb -c "UPDATE management SET importer_hostname='hostname_of_main_importer'"
anschließend via GUI die einzelnen Ausnahmen setzen

5) testen
a) einzelne Imports für alle Plattformen  
b) GUI auf ellen GUI-Servern
c) Reports via GUI und auch cmd-line
 
6) Import-Prozess starten
*/