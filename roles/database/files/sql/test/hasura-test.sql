
-- cannot test hasura before API was installed, so can only run this on upgrade

BEGIN;

CREATE EXTENSION IF NOT EXISTS pgtap;

-- CREATE OR REPLACE FUNCTION hdb_catalog.test_1_hdb_catalog_schema()
-- RETURNS SETOF TEXT LANGUAGE plpgsql AS $$
-- BEGIN
--     RETURN NEXT has_table( 'hdb_catalog.hdb_action_log' );
--     RETURN NEXT has_table( 'hdb_catalog.hdb_metadata' );
--     RETURN NEXT has_table( 'hdb_catalog.hdb_version' );
-- END;
-- $$;

CREATE OR REPLACE FUNCTION hdb_catalog.test_2_hdb_catalog_data()
RETURNS SETOF TEXT LANGUAGE plpgsql AS $$
BEGIN
    RETURN NEXT results_eq('SELECT cast((select COUNT(*) FROM hdb_catalog.hdb_metadata) as integer)', 'SELECT cast (1 as integer)', 'there should be exactly one metadata entry');
END;
$$;

CREATE OR REPLACE FUNCTION hdb_catalog.shutdown_1() RETURNS VOID LANGUAGE plpgsql AS $$
BEGIN
    drop function if exists hdb_catalog.test_1_hdb_catalog_schema();
    drop function if exists hdb_catalog.test_2_hdb_catalog_data();
END;
$$;

SELECT * FROM runtests('hdb_catalog'::name);
