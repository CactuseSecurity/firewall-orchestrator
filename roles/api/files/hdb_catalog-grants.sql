
GRANT USAGE ON SCHEMA hdb_catalog TO dbbackupusers;
Grant select on ALL TABLES in SCHEMA hdb_catalog to group dbbackupusers;
ALTER DEFAULT PRIVILEGES IN SCHEMA hdb_catalog GRANT SELECT ON TABLES TO group dbbackupusers;
