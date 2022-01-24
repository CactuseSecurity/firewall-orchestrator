
-- settings backup permissions

GRANT USAGE ON SCHEMA hdb_catalog TO dbbackupusers;
GRANT SELECT ON ALL SEQUENCES IN SCHEMA public TO group "dbbackupusers";
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT SELECT ON SEQUENCES TO group "dbbackupusers";
Grant select on ALL TABLES in SCHEMA public to group dbbackupusers;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT SELECT ON TABLES TO group dbbackupusers;
Grant select on ALL TABLES in SCHEMA hdb_catalog to group dbbackupusers;
ALTER DEFAULT PRIVILEGES IN SCHEMA hdb_catalog GRANT SELECT ON TABLES TO group dbbackupusers;


--  grants for all (implicit) sequences
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO group "secuadmins";
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT USAGE, SELECT ON SEQUENCES TO group "secuadmins";

GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO group "configimporters";
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT USAGE, SELECT ON SEQUENCES TO group "configimporters";

GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO group "reporters";
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT USAGE, SELECT ON SEQUENCES TO group "reporters";

GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO group "fworchadmins";
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT USAGE, SELECT ON SEQUENCES TO group "fworchadmins";

-- Group permissions on tables

-- general grants:
Grant ALL on ALL tables in SCHEMA public to group fworchadmins; -- todo: could be reduced
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON TABLES TO group fworchadmins;

Grant select on ALL TABLES in SCHEMA public to group configimporters;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT SELECT ON TABLES TO group configimporters;

Grant select on ALL TABLES in SCHEMA public to group reporters;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT SELECT ON TABLES TO group reporters;

Grant select on ALL TABLES in SCHEMA public to group secuadmins;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT SELECT ON TABLES TO group secuadmins;

-- config importers:
Grant ALL on "import_service" to group "configimporters";
Grant ALL on "import_object" to group "configimporters";
Grant ALL on "import_user" to group "configimporters";
Grant ALL on "import_rule" to group "configimporters";
Grant ALL on "import_control" to group "configimporters";
Grant ALL on "import_zone" to group "configimporters";
Grant ALL on "import_changelog" to group "configimporters";

Grant update on "device" to group "configimporters";
Grant update on "management" to group "configimporters";
Grant update,insert on "object" to group "configimporters";
Grant update,insert on "objgrp" to group "configimporters";
Grant update,insert on "rule" to group "configimporters";
Grant update,insert on "rule_metadata" to group "configimporters";
Grant update,insert on "rule_from" to group "configimporters";
Grant update,insert on "rule_review" to group "configimporters";
Grant update,insert on "rule_service" to group "configimporters";
Grant update,insert on "rule_to" to group "configimporters";
Grant update,insert on "service" to group "configimporters";
Grant update,insert on "svcgrp" to group "configimporters";
Grant update,insert on "usr" to group "configimporters";
Grant update,insert on "zone" to group "configimporters";
Grant update,insert on "usergrp" to group "configimporters";
Grant update,insert on "usergrp_flat" to group "configimporters";
Grant update,insert on "objgrp_flat" to group "configimporters";
Grant update,insert on "svcgrp_flat" to group "configimporters";
Grant update,insert on "tenant_user" to group "configimporters";
Grant insert on "changelog_object" to group "configimporters";
Grant insert on "changelog_service" to group "configimporters";
Grant insert on "changelog_user" to group "configimporters";
Grant insert on "changelog_rule" to group "configimporters";
Grant insert on "error_log" to group "configimporters";
Grant insert,update on "rule_nwobj_resolved" to group "configimporters";
Grant insert,update on "rule_svc_resolved" to group "configimporters";
Grant insert,update on "rule_user_resolved" to group "configimporters";

-- secuadmins:
Grant ALL on "request" to group "secuadmins";
Grant ALL on "request_object_change" to group "secuadmins";
Grant ALL on "request_service_change" to group "secuadmins";
Grant ALL on "request_rule_change" to group "secuadmins";
Grant ALL on "request_user_change" to group "secuadmins";
Grant ALL on "tenant_username" to group "secuadmins";

Grant update on "uiuser" to group "secuadmins";
Grant update,insert on "changelog_object" to group "secuadmins";
Grant update,insert on "changelog_service" to group "secuadmins";
Grant update,insert on "changelog_user" to group "secuadmins";
Grant update,insert on "changelog_rule" to group "secuadmins";
Grant update,insert on "error_log" to group "secuadmins";
Grant insert on "report" to group "secuadmins";

-- reporters:
Grant update on "uiuser" to group "reporters";
Grant insert on "error_log" to group "reporters";
Grant insert on "report" to group "reporters";
Grant insert on "report_template" to group "reporters";
