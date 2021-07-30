CREATE EXTENSION IF NOT EXISTS pgtap;

BEGIN;

-- cannot test hasura before API was installed
-- CREATE OR REPLACE FUNCTION hdb_catalog.testschema()
-- RETURNS SETOF TEXT LANGUAGE plpgsql AS $$
-- BEGIN
--     RETURN NEXT has_table( 'hdb_action_log' );
--     RETURN NEXT has_table( 'hdb_metadata' );
--     RETURN NEXT has_table( 'hdb_version' );
-- END;
-- $$;
-- SELECT * FROM runtests('hdb_catalog'::name);
