-- $Id: 20071210-iso-patches.sql,v 1.1.2.2 2007-12-13 10:47:32 tim Exp $
-- $Source: /home/cvs/iso/package/install/migration/Attic/20071210-iso-patches.sql,v $
-- als itsecorg-user:
-- psql -U itsecorg -h localhost -d isov1 -c "\i /usr/local/itsecorg/install/migration/20071210-iso-patches.sql"

-- ok - Import-aktiv Feld ist invers
-- ok - Sanity-Check für input-felder
-- ok - Sortierung status view (import_aktiv, Status)
-- ok - refresh des dev-menus via javascript

-- geänderte Dateien:
-- web/htdocs/config/config_dev.php
-- web/htdocs/config/config_single_dev.php
-- web/htdocs/config/config_single_mgm.php
-- web/include/db-config.php
-- web/include/db-input.php
-- web/htdocs/index.php
-- web/htdocs/inctxt/navi_hor.inc.php

-- import status nur für die Usergruppen sichtbar, die das privilege "view-import-status" haben
-- web/htdocs/config/import_status_iframe.php
-- web/htdocs/inctxt/navi_vert_config_main.inc.php
-- web/include/check_privs.php
-- web/include/db-gui-config.php

-- install/migration/20071210-iso-patches.sql 

DROP VIEW view_import_status_table CASCADE;
CREATE OR REPLACE VIEW view_import_status_table_unsorted AS 
	SELECT *,
		CASE
			WHEN import_is_active AND import_count_successful=0 AND import_count_errors>=5 THEN VARCHAR 'red'
			WHEN (NOT import_is_active AND last_successful_import IS NULL AND last_import_with_errors IS NULL)
				OR (last_successful_import>last_import_with_errors) THEN VARCHAR 'green'
			WHEN (last_successful_import IS NULL AND last_import_with_errors IS NULL)
				OR (last_successful_import<last_import_with_errors) THEN VARCHAR 'yellow'
			ELSE VARCHAR 'green'
		END
		AS status
	FROM (
		SELECT
			COALESCE(view_import_status_successful.mgm_id, view_import_status_errors.mgm_id) AS mgm_id,
			COALESCE(view_import_status_successful.mgm_name, view_import_status_errors.mgm_name) AS management_name,
			COALESCE(view_import_status_successful.dev_typ_name, view_import_status_errors.dev_typ_name) AS device_type,
			COALESCE(NOT view_import_status_successful.do_not_import, view_import_status_errors.do_not_import) AS import_is_active,
			SUBSTR(view_import_status_successful.last_import, 1, 16) AS last_successful_import,
			SUBSTR(view_import_status_errors.last_import, 1, 16) AS last_import_with_errors,
			COALESCE(view_import_status_successful.import_count_24hours,0) AS import_count_successful, 
			COALESCE(view_import_status_errors.import_count_24hours, 0) AS import_count_errors,
			COALESCE(view_import_status_successful.import_count_24hours + view_import_status_errors.import_count_24hours, 0) AS import_count_total,
			view_import_status_errors.import_errors AS last_import_error
			FROM view_import_status_successful LEFT JOIN view_import_status_errors USING (mgm_id)
	) AS FOO;

CREATE OR REPLACE VIEW view_import_status_table AS 
	SELECT *, CASE
			WHEN status='red' THEN 1
			WHEN status='yellow' THEN  2
			WHEN status='green' THEN 3
		END AS status_sorter
	FROM view_import_status_table_unsorted
	ORDER BY import_is_active DESC, status_sorter, management_name;

GRANT SELECT ON TABLE view_import_status_table TO itsecorg;
GRANT SELECT ON TABLE view_import_status_table TO GROUP secuadmins, reporters;