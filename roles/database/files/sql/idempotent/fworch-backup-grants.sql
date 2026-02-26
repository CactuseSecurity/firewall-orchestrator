
-- settings backup permissions
DO $$
DECLARE
    SchemaName text;
BEGIN
    FOREACH SchemaName IN ARRAY ARRAY['public', 'request', 'compliance', 'modelling']
    LOOP
        EXECUTE format('GRANT USAGE ON SCHEMA %I TO "dbbackupusers";', SchemaName);
        EXECUTE format('GRANT SELECT ON ALL SEQUENCES IN SCHEMA %I TO "dbbackupusers";', SchemaName);
        EXECUTE format('GRANT SELECT ON ALL TABLES IN SCHEMA %I TO "dbbackupusers";', SchemaName);
        EXECUTE format('ALTER DEFAULT PRIVILEGES IN SCHEMA %I GRANT SELECT ON SEQUENCES TO "dbbackupusers";', SchemaName);
        EXECUTE format('ALTER DEFAULT PRIVILEGES IN SCHEMA %I GRANT SELECT ON TABLES TO "dbbackupusers";', SchemaName);
    END LOOP;
END
$$;