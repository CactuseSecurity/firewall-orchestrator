/*
	changed:
		importer:
			iso-importer-single.pl
			CACTUS::ISO::import.pm
			checkpoint.pm
			phion.pm
		install:
			itsecorg-db-model.sql
		design:
			itsecorg.dm2
		web:
			config_single_mgm.php
			config_dev.php
			include/db-config.php
*/


SET statement_timeout = 0;
SET client_encoding = 'UTF8';
SET standard_conforming_strings = off;
SET check_function_bodies = false;
SET client_min_messages = warning;
SET escape_string_warning = off;
SET search_path = public, pg_catalog;

ALTER TABLE management ADD "config_path" VARCHAR;
ALTER TABLE management ADD "ssh_port" Integer NOT NULL Default 22;

UPDATE management SET ssh_port=22 WHERE ssh_port IS NULL;
UPDATE management SET config_path='./' WHERE config_path IS NULL;