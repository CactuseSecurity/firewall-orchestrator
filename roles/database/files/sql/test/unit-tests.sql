-- \set ECHO none
-- \set QUIET 1
-- \set ON_ERROR_ROLLBACK 1
-- \set ON_ERROR_STOP true
-- \set QUIET 1

-- \pset format unaligned
-- \pset tuples_only true
-- \pset pager

CREATE EXTENSION IF NOT EXISTS pgtap;

BEGIN;
    
-- SELECT is(select * from is_obj_group(select obj_id from object where obj_name='AuxiliaryNet'), false);
-- SELECT is(select * from is_obj_group(select obj_id from object where obj_name='CactusDA'), true);

CREATE OR REPLACE FUNCTION public.testschema()
RETURNS SETOF TEXT LANGUAGE plpgsql AS $$
BEGIN
    RETURN NEXT has_table( 'object' );
    RETURN NEXT has_table( 'rule' );
    RETURN NEXT has_table( 'service' );
    RETURN NEXT has_table( 'usr' );
    RETURN NEXT hasnt_table( 'rule_order' );
END;
$$;

-- CREATE OR REPLACE FUNCTION hdb_catalog.testschema()
-- RETURNS SETOF TEXT LANGUAGE plpgsql AS $$
-- BEGIN
--     RETURN NEXT has_table( 'hdb_action_log' );
--     RETURN NEXT has_table( 'hdb_metadata' );
--     RETURN NEXT has_table( 'hdb_version' );
-- END;
-- $$;

SELECT * FROM runtests('public'::name);
-- SELECT * FROM runtests('hdb_catalog'::name);

--SELECT * FROM finish();
ROLLBACK;
