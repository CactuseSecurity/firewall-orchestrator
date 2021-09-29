
BEGIN;
CREATE EXTENSION IF NOT EXISTS pgtap;

CREATE OR REPLACE FUNCTION public.test_1_schema()
RETURNS SETOF TEXT LANGUAGE plpgsql AS $$
BEGIN
    RETURN NEXT has_table( 'object' );
    RETURN NEXT has_table( 'rule' );
    RETURN NEXT has_table( 'service' );
    RETURN NEXT has_table( 'usr' );
    RETURN NEXT hasnt_table( 'rule_order' );
END;
$$;

CREATE OR REPLACE FUNCTION public.test_2_functions()
RETURNS SETOF TEXT LANGUAGE plpgsql AS $$
BEGIN
    RETURN NEXT results_eq('SELECT * FROM are_equal(CAST(''1.2.3.4'' AS CIDR),CAST(''1.2.3.4/32'' AS CIDR))', 'SELECT TRUE', 'cidr 1.2.3.4==1.2.3.4/32 are_equal should return true');
    RETURN NEXT results_eq('SELECT * FROM are_equal(''1.2.3.4'',''1.2.3.4/32'')', 'SELECT FALSE', 'string 1.2.3.4==1.2.3.4/32 are_equal should return false');
    RETURN NEXT results_eq('SELECT * FROM are_equal(7*0, 0)', 'SELECT TRUE', 'int are_equal should return true');
    RETURN NEXT results_eq('SELECT * FROM remove_spaces(''     abc '')', 'SELECT CAST(''abc'' AS VARCHAR)', 'remove_spaces should return abc');
END;
$$;

CREATE OR REPLACE FUNCTION public.shutdown_1() RETURNS VOID LANGUAGE plpgsql AS $$
BEGIN
    drop function if exists test_1_schema();
    drop function if exists test_2_functions();
END;
$$;

SELECT * FROM runtests('public'::name);
