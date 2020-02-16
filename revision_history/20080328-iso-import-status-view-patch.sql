-- $Id: 20080328-iso-import-status-view-patch.sql,v 1.1.2.1 2008-03-28 13:45:36 tim Exp $
-- $Source: /home/cvs/iso/package/install/migration/Attic/20080328-iso-import-status-view-patch.sql,v $

-- die Datei /usr/local/itsecorg/importer/iso-importer-single.pl ersetzen

-- als owner der Tabelle import_control:
ALTER TABLE import_control ALTER COLUMN successful_import SET DEFAULT FALSE;

-- Die beiden Views neu definieren:
CREATE OR REPLACE VIEW view_import_status_successful AS 
	SELECT mgm_id, mgm_name, dev_typ_name, do_not_import, MAX(last_import) AS last_import, MAX(import_count_24hours) AS import_count_24hours FROM (
		SELECT management.mgm_id, mgm_name, dev_typ_name, do_not_import, successful_import, MAX(start_time) AS last_import, 
			COUNT(import_control.control_id) AS import_count_24hours
		FROM management LEFT JOIN import_control ON (management.mgm_id=import_control.mgm_id)
			LEFT JOIN stm_dev_typ USING (dev_typ_id)
		WHERE start_time>(now() - interval '24 hours') AND successful_import AND NOT stop_time IS NULL
		GROUP BY management.mgm_id, mgm_name, successful_import, do_not_import, dev_typ_name
		UNION 
		SELECT management.mgm_id, mgm_name, dev_typ_name, do_not_import, successful_import, MAX(start_time) AS last_import, 
			0 AS import_count_24hours
		FROM management LEFT JOIN import_control ON (management.mgm_id=import_control.mgm_id)
			LEFT JOIN stm_dev_typ USING (dev_typ_id)
		WHERE start_time<=(now() - interval '24 hours') AND successful_import AND NOT stop_time IS NULL
		GROUP BY management.mgm_id, mgm_name, successful_import, do_not_import, dev_typ_name
		UNION 
		SELECT management.mgm_id, mgm_name, dev_typ_name, do_not_import, successful_import, NULL AS last_import, 
			0 AS import_count_24hours
		FROM management LEFT JOIN import_control USING (mgm_id)
			LEFT JOIN stm_dev_typ USING (dev_typ_id)
		WHERE successful_import IS NULL
		GROUP BY management.mgm_id, mgm_name, successful_import, do_not_import, dev_typ_name
	) AS foo GROUP BY mgm_id, mgm_name, successful_import, do_not_import, dev_typ_name ORDER BY dev_typ_name, mgm_name;

CREATE OR REPLACE VIEW view_import_status_errors AS 
	SELECT mgm_id, mgm_name, dev_typ_name, do_not_import, MAX(last_import) AS last_import,  MAX(import_count_24hours) AS import_count_24hours, import_errors FROM (
		SELECT management.mgm_id, mgm_name, dev_typ_name, do_not_import, successful_import, MAX(start_time) AS last_import, 
			COUNT(import_control.control_id) AS import_count_24hours, import_control.import_errors
		FROM management LEFT JOIN import_control ON (management.mgm_id=import_control.mgm_id)
			LEFT JOIN stm_dev_typ USING (dev_typ_id)
		WHERE start_time>(now() - interval '24 hours') AND NOT successful_import AND NOT stop_time IS NULL
		GROUP BY management.mgm_id, mgm_name, successful_import, do_not_import, dev_typ_name, import_errors
		UNION 
		SELECT management.mgm_id, mgm_name, dev_typ_name, do_not_import, successful_import, NULL AS last_import, 
			0 AS import_count_24hours, NULL AS import_errors
		FROM management LEFT JOIN import_control USING (mgm_id)
			LEFT JOIN stm_dev_typ USING (dev_typ_id)
		WHERE successful_import IS NULL AND NOT stop_time IS NULL
		GROUP BY management.mgm_id, mgm_name, successful_import, do_not_import, dev_typ_name, import_errors
	) AS foo 
	GROUP BY mgm_id, mgm_name, successful_import, do_not_import, dev_typ_name, import_errors ORDER BY dev_typ_name, mgm_name;
