-- $Id: 20071107-iso-add-view-import-status.sql,v 1.1.2.2 2007-12-13 10:47:31 tim Exp $
-- $Source: /home/cvs/iso/package/install/migration/Attic/20071107-iso-add-view-import-status.sql,v $
-- this adds import status reporint capability (both via web and via file)

ALTER TABLE import_control ADD COLUMN successful_import Boolean NOT NULL Default TRUE;
ALTER TABLE import_control ADD COLUMN import_errors Varchar;

UPDATE import_control SET successful_import=TRUE;  -- initialize all old imports as successful

-- the following files need to be replaced:

-- importer/CACTUS/ISO/import.pm
-- importer/CACTUS/ISO.pm
-- importer/iso-importer-single.pl
-- web/include/db-import-ids.php
-- web/include/db-nwobject.php
-- web/include/db-rule.php
-- web/include/db-service.php
-- web/include/db-user.php
-- web/include/display-filter.php
-- web/htdocs/inctxt/navi_vert_config_main.inc.php
-- web/htdocs/config/import_status.php

\i database/iso-import.sql
\i database/iso-report-basics.sql
\i database/iso-report.sql
\i database/iso-obj-import.sql
\i database/iso-svc-import.sql
\i database/iso-usr-import.sql
\i database/iso-rule-import.sql
\i database/iso-views.sql
