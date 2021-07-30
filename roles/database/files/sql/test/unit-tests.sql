\set ECHO none
\set QUIET 1
\set ON_ERROR_ROLLBACK 1
\set ON_ERROR_STOP true
\set QUIET 1

\pset format unaligned
\pset tuples_only true
\pset pager

CREATE EXTENSION IF NOT EXISTS pgtap;

BEGIN;
SELECT plan(5);
    
SELECT is(select * from is_obj_group(select obj_id from object where obj_name='AuxiliaryNet'), false);
SELECT is(select * from is_obj_group(select obj_id from object where obj_name='CactusDA'), true);

CREATE OR REPLACE FUNCTION public.testschema()
RETURNS SETOF TEXT LANGUAGE plpgsql AS $$
BEGIN
    RETURN NEXT has_table( 'object' );
    RETURN NEXT has_table( 'rule' );
    RETURN NEXT has_table( 'service' );
    RETURN NEXT has_table( 'usr' );
END;
$$;

CREATE OR REPLACE FUNCTION hdb_catalog.testschema()
RETURNS SETOF TEXT LANGUAGE plpgsql AS $$
BEGIN
    RETURN NEXT has_table( 'hdb_action_log' );
    RETURN NEXT has_table( 'hdb_metadata' );
    RETURN NEXT has_table( 'hdb_version' );
END;
$$;

SELECT * FROM runtests('public'::name);
SELECT * FROM runtests('hdb_catalog'::name);

SELECT * FROM finish();
ROLLBACK;

-- SELECT
--   is(sign('{"sub":"1234567890","name":"John Doe","admin":true}', 'secret'),
--   'eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMjM0NTY3ODkwIiwibmFtZSI6IkpvaG4gRG9lIiwiYWRtaW4iOnRydWV9.TJVA95OrM7E2cBab30RMHrHDcEfxjoYZgeFONFh7HgQ');

-- INSERT into object () values ();

-- SELECT
--   throws_ok(
--     $$SELECT header::text, payload::text, valid FROM verify(
--     'eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMjM0NTY3ODkwIiwibmFtZSI6IkpvaG4gRG9lIiwiYWRtaW4iOnRydWV9.TJVA95OrM7E2cBab30RMHrHDcEfxjoYZgeFONFh7HgQ',
--     'secret', 'bogus')$$,
--     '22023',
--     'Cannot use "": No such hash algorithm',
--     'verify() should raise on bogus algorithm'
-- );

-- SELECT throws_ok( -- bogus header
--     $$SELECT header::text, payload::text, valid FROM verify(
--     'eyJhbGciOiJIUzI1NiIBOGUScCI6IkpXVCJ9.eyJzdWIiOiIxMjM0NTY3ODkwIiwibmFtZSI6IkpvaG4gRG9lIiwiYWRtaW4iOnRydWV9.TJVA95OrM7E2cBab30RMHrHDcEfxjoYZgeFONFh7HgQ',
--     'secret', 'HS256')$$
--     );

-- SELECT
--   results_eq(
--     $$SELECT header::text, payload::text, valid FROM verify(
--     'eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMjM0NTY3ODkwIiwibmFtZSI6IkpvaG4gRG9lIiwiYWRtaW4iOnRydWV9.TJVA95OrM7E2cBab30RMHrHDcEfxjoYZgeFONFh7HgQ',
--     'secret')$$,
--     $$VALUES ('{"alg":"HS256","typ":"JWT"}', '{"sub":"1234567890","name":"John Doe","admin":true}', true)$$,
--     'verify() should return return data marked valid'
-- );
