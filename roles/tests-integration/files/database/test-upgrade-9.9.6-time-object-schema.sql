-- Test the move of time_object to the firewall schema and the repeatability of upgrade/9.9.6.sql.
-- The test task inserts the upgrade file at both run-upgrade markers; the transaction is rolled back.
\set ON_ERROR_STOP 1
BEGIN;
DO $$
BEGIN
    IF to_regclass('firewall.time_object') IS NULL OR to_regclass('public.time_object') IS NOT NULL THEN
        RAISE EXCEPTION 'installation: time_object is not in the firewall schema';
    END IF;
END $$;

-- everything that depends on the table and has to survive the move unchanged
CREATE TEMP VIEW time_object_dependents AS
    SELECT 'index' AS kind, indexname::text AS name, schemaname::text AS detail
    FROM pg_indexes WHERE tablename = 'time_object'
    UNION ALL
    SELECT 'constraint', conname::text, contype::text
    FROM pg_constraint WHERE conrelid = to_regclass('firewall.time_object') AND contype IN ('p', 'f', 'u', 'c')
    UNION ALL
    SELECT 'referencing key', conname::text, conrelid::regclass::text
    FROM pg_constraint WHERE contype = 'f' AND confrelid = to_regclass('firewall.time_object')
    UNION ALL
    SELECT 'sequence', pg_get_serial_sequence('firewall.time_object', 'time_obj_id'), NULL
    UNION ALL
    SELECT 'read access', rolname::text, has_table_privilege(rolname, to_regclass('firewall.time_object'), 'SELECT')::text
    FROM pg_roles WHERE rolname IN ('fwo_ro', 'dbbackupusers');
CREATE TEMP TABLE time_object_expected ON COMMIT DROP AS SELECT * FROM time_object_dependents;

DO $$
DECLARE
    v_missing text;
BEGIN
    SELECT string_agg(name, ', ') INTO v_missing
    FROM (VALUES
        ('index', 'time_object_flow_timeobj_id_active_only_one_per_mgm'),
        ('constraint', 'time_object_pkey'),
        ('constraint', 'time_object_mgm_id_fkey'),
        ('constraint', 'time_object_created_fkey'),
        ('constraint', 'time_object_removed_fkey'),
        ('constraint', 'flow_timeobj_id_foreign_key'),
        ('referencing key', 'rule_time_time_obj_id_fkey'),
        ('referencing key', 'request_reqelement_time_object_foreign_key'),
        ('referencing key', 'request_implelement_time_object_foreign_key'),
        ('sequence', 'firewall.time_object_time_obj_id_seq')
    ) AS required(kind, name)
    WHERE NOT EXISTS (
        SELECT 1 FROM time_object_expected e WHERE e.kind = required.kind AND e.name = required.name
    );
    IF v_missing IS NOT NULL THEN
        RAISE EXCEPTION 'installation: firewall.time_object lacks %', v_missing;
    END IF;
END $$;

CREATE FUNCTION pg_temp.check_time_object_move(i_step text) RETURNS void AS $check$
DECLARE
    v_difference text;
BEGIN
    IF to_regclass('public.time_object') IS NOT NULL OR to_regclass('firewall.time_object') IS NULL THEN
        RAISE EXCEPTION 'upgrade 9.9.6 (%): time_object is not in the firewall schema', i_step;
    END IF;
    SELECT string_agg(kind || ' ' || coalesce(name, '?'), ', ') INTO v_difference
    FROM (
        (SELECT * FROM time_object_expected EXCEPT SELECT * FROM time_object_dependents)
        UNION ALL
        (SELECT * FROM time_object_dependents EXCEPT SELECT * FROM time_object_expected)
    ) AS difference;
    IF v_difference IS NOT NULL THEN
        RAISE EXCEPTION 'upgrade 9.9.6 (%): differs from the installed state: %', i_step, v_difference;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM firewall.time_object WHERE time_obj_id = (SELECT time_obj_id FROM time_object_probe)) THEN
        RAISE EXCEPTION 'upgrade 9.9.6 (%): the existing time object was lost', i_step;
    END IF;
END;
$check$ LANGUAGE plpgsql;

-- simulate a database before the move and add a row
ALTER TABLE firewall.time_object SET SCHEMA public;
CREATE TEMP TABLE time_object_probe (mgm_id int, time_obj_id bigint) ON COMMIT DROP;
WITH new_credential AS (
    INSERT INTO credential (credential_name, username, secret)
    VALUES ('upgrade-time-object-probe', 'upgrade-time-object-probe', '')
    RETURNING id
), new_management AS (
    INSERT INTO management (dev_typ_id, mgm_name, ssh_hostname, import_credential_id)
    SELECT (SELECT min(dev_typ_id) FROM stm_dev_typ), 'upgrade-time-object-probe', 'localhost', id FROM new_credential
    RETURNING mgm_id
), inserted AS (
    INSERT INTO public.time_object (mgm_id, time_obj_uid, time_obj_name)
    SELECT mgm_id, 'upgrade-time-object-probe', 'before the move' FROM new_management
    RETURNING mgm_id, time_obj_id
)
INSERT INTO time_object_probe SELECT mgm_id, time_obj_id FROM inserted;

-- @run-upgrade-9.9.6

DO $$
DECLARE
    i_new_id bigint;
BEGIN
    PERFORM pg_temp.check_time_object_move('first run');

    -- new rows still get their id from the moved sequence, also through an unqualified reference
    INSERT INTO time_object (mgm_id, time_obj_uid, time_obj_name)
    SELECT mgm_id, 'upgrade-time-object-probe-2', 'after the move' FROM time_object_probe
    RETURNING time_obj_id INTO i_new_id;
    IF i_new_id IS NULL OR i_new_id <= (SELECT time_obj_id FROM time_object_probe) THEN
        RAISE EXCEPTION 'upgrade 9.9.6: the moved table does not assign ids from its sequence';
    END IF;
END $$;

-- @run-upgrade-9.9.6

DO $$
BEGIN
    PERFORM pg_temp.check_time_object_move('second run');
END $$;
ROLLBACK;
\echo '9.9.6 time_object schema move and idempotency checks passed'
