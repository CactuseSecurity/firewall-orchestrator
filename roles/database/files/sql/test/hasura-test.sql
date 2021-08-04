
-- cannot test hasura before API was installed, so can only run this on upgrade

BEGIN;

CREATE EXTENSION IF NOT EXISTS pgtap;

CREATE OR REPLACE FUNCTION hdb_catalog.test_1_schema()
RETURNS SETOF TEXT LANGUAGE plpgsql AS $$
BEGIN
    RETURN NEXT has_table( 'hdb_action_log' );
    RETURN NEXT has_table( 'hdb_metadata' );
    RETURN NEXT has_table( 'hdb_version' );
END;
$$;

CREATE OR REPLACE FUNCTION public.shutdown_1() RETURNS VOID LANGUAGE plpgsql AS $$
BEGIN
    drop function if exists test_1_schema();
END;
$$;

SELECT * FROM runtests('hdb_catalog'::name);
