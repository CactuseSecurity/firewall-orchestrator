-- Test of the data changes of upgrade/9.7.1.sql: the seeding of the certificate check switches for email servers and
-- external ticket systems, and the dating back of the rulebase links and gateway entries of the 9.0 migration (rule
-- versions removed before the upgrade to v9 stay visible to the tenants of their gateways).
-- The test task inserts the upgrade file at the two run-upgrade markers below. It runs twice: first with email
-- and ticket system endpoints configured, then without, which also shows that the upgrade can be repeated.
--
-- Runs in a single transaction that is always rolled back, so it leaves no data behind. Any failed expectation
-- raises an exception, which makes psql (ON_ERROR_STOP) exit with a non-zero code.

\set ON_ERROR_STOP 1
\o /dev/null
SET client_min_messages TO WARNING;

BEGIN;

CREATE TEMP TABLE probe_id (probe_key text PRIMARY KEY, probe_value bigint NOT NULL) ON COMMIT DROP;

CREATE FUNCTION pg_temp.probe(p_key text) RETURNS bigint AS $$
    SELECT probe_value FROM probe_id WHERE probe_key = p_key
$$ LANGUAGE sql STABLE;

CREATE FUNCTION pg_temp.expect(p_case text, p_actual text, p_expected text) RETURNS void AS $$
BEGIN
    IF p_actual IS DISTINCT FROM p_expected THEN
        RAISE EXCEPTION 'upgrade 9.7.1: % - expected %, got %', p_case, p_expected, p_actual;
    END IF;
END;
$$ LANGUAGE plpgsql;

CREATE FUNCTION pg_temp.config_value(p_key text) RETURNS text AS $$
    SELECT config_value FROM config WHERE config_key = p_key AND config_user = 0
$$ LANGUAGE sql STABLE;

CREATE FUNCTION pg_temp.set_config_value(p_key text, p_value text) RETURNS void AS $$
    INSERT INTO config (config_key, config_value, config_user) VALUES (p_key, p_value, 0)
    ON CONFLICT (config_key, config_user) DO UPDATE SET config_value = EXCLUDED.config_value
$$ LANGUAGE sql;

-- created and removed of a gateway entry or rulebase link, e.g. "3-5" or "3-"
CREATE FUNCTION pg_temp.gateway_entry(p_rule_key text, p_gw_key text) RETURNS text AS $$
    SELECT string_agg(concat(reg.created - pg_temp.probe('import_base'), '-', reg.removed - pg_temp.probe('import_base')), ',')
    FROM rule_enforced_on_gateway reg
    WHERE reg.rule_id = pg_temp.probe(p_rule_key) AND reg.dev_id = pg_temp.probe(p_gw_key)
$$ LANGUAGE sql STABLE;

CREATE FUNCTION pg_temp.rulebase_link(p_gw_key text, p_rulebase_key text DEFAULT 'rulebase') RETURNS text AS $$
    SELECT string_agg(concat(rl.created - pg_temp.probe('import_base'), '-', rl.removed - pg_temp.probe('import_base')), ',')
    FROM rulebase_link rl WHERE rl.gw_id = pg_temp.probe(p_gw_key)
        AND rl.to_rulebase_id = pg_temp.probe(p_rulebase_key)
$$ LANGUAGE sql STABLE;

CREATE FUNCTION pg_temp.fully_visible(p_rule_key text, p_gw_key text) RETURNS text AS $$
DECLARE
    b_visible boolean;
BEGIN
    INSERT INTO tenant_to_device (tenant_id, device_id, shared) VALUES (pg_temp.probe('tenant'), pg_temp.probe(p_gw_key), false);
    SELECT rule_fully_visible_to_tenant(r, pg_temp.probe('tenant')::integer) INTO b_visible
    FROM rule r WHERE r.rule_id = pg_temp.probe(p_rule_key);
    DELETE FROM tenant_to_device WHERE tenant_id = pg_temp.probe('tenant');
    RETURN b_visible::text;
END;
$$ LANGUAGE plpgsql;

-- the tenant simulation lists the rule for the gateway (the tenant is mapped to the gateway, as in fully_visible)
CREATE FUNCTION pg_temp.simulated(p_rule_key text, p_gw_key text) RETURNS text AS $$
DECLARE
    b_listed boolean;
BEGIN
    INSERT INTO tenant_to_device (tenant_id, device_id, shared) VALUES (pg_temp.probe('tenant'), pg_temp.probe(p_gw_key), false);
    SELECT pg_temp.probe(p_rule_key) IN (
            SELECT sim.rule_id FROM device d,
                get_rules_for_tenant(d, pg_temp.probe('tenant')::integer, json_build_object('x-hasura-tenant-id', '1')) sim
            WHERE d.dev_id = pg_temp.probe(p_gw_key))
        INTO b_listed;
    DELETE FROM tenant_to_device WHERE tenant_id = pg_temp.probe('tenant');
    RETURN b_listed::text;
END;
$$ LANGUAGE plpgsql;

-- first run: an email server with TLS and a ticket system with a URL are configured, so the switches keep the
-- unchecked behaviour
DELETE FROM config WHERE config_key IN ('emailCheckCertificates', 'extTicketSystemsCheckCertificates') AND config_user = 0;
SELECT pg_temp.set_config_value('emailServerAddress', 'mail.example.com');
SELECT pg_temp.set_config_value('emailTls', 'StartTls');
SELECT pg_temp.set_config_value('extTicketSystems', '[{"Id":1,"TypeId":2,"Name":"probe","Url":"https://ticket.example.com"}]');

-- A management migrated from v8: rules carry dev_id, and the migration dated the gateway entries and the rulebase link
-- with the last import before the upgrade ("migration"). Imports are numbered relative to import_base:
-- 1 first import, 2 removal of the historic rule version, 3 migration, 4 first import after the upgrade.
-- A second management was only imported by v9 (no dev_id). Its gateway was added after one of its rule versions was
-- removed, and that link must stay as it is.
DO $$
DECLARE
    i_credential_id integer;
    i_dev_typ_id integer;
    i_import_type_id integer;
    i_action_id integer;
    i_track_id integer;
    i_mgm_id integer;
    i_mgm_v9_id integer;
    i_gw_id integer;
    i_gw_added_id integer;
    i_gw_v9_id integer;
    i_rulebase_id integer;
    i_rulebase_v9_id integer;
    i_import_ids bigint[];
    i_import_v9_ids bigint[];
    i_rule_id bigint;
    i_tenant_id integer;
    r_rule RECORD;
BEGIN
    SELECT min(dev_typ_id) INTO i_dev_typ_id FROM stm_dev_typ;
    SELECT import_type_id INTO i_import_type_id FROM stm_import WHERE import_type_name = 'rule';
    SELECT min(action_id) INTO i_action_id FROM stm_action;
    SELECT min(track_id) INTO i_track_id FROM stm_track;

    INSERT INTO import_credential (credential_name, username, secret)
        VALUES ('upgrade_963_probe', 'probe', 'probe') RETURNING id INTO i_credential_id;
    INSERT INTO tenant (tenant_name) VALUES ('upgrade_963_probe_tenant') RETURNING tenant_id INTO i_tenant_id;

    INSERT INTO management (dev_typ_id, mgm_name, import_credential_id, ssh_hostname)
        VALUES (i_dev_typ_id, 'upgrade_963_probe_migrated', i_credential_id, '127.0.0.1') RETURNING mgm_id INTO i_mgm_id;
    INSERT INTO management (dev_typ_id, mgm_name, import_credential_id, ssh_hostname)
        VALUES (i_dev_typ_id, 'upgrade_963_probe_v9', i_credential_id, '127.0.0.1') RETURNING mgm_id INTO i_mgm_v9_id;
    INSERT INTO device (mgm_id, dev_typ_id, dev_name)
        VALUES (i_mgm_id, i_dev_typ_id, 'upgrade_963_probe_gw') RETURNING dev_id INTO i_gw_id;
    INSERT INTO device (mgm_id, dev_typ_id, dev_name)
        VALUES (i_mgm_id, i_dev_typ_id, 'upgrade_963_probe_gw_added') RETURNING dev_id INTO i_gw_added_id;
    INSERT INTO device (mgm_id, dev_typ_id, dev_name)
        VALUES (i_mgm_v9_id, i_dev_typ_id, 'upgrade_963_probe_gw_v9') RETURNING dev_id INTO i_gw_v9_id;

    -- finished imports, as only one running import per management is allowed
    WITH imports AS (
        INSERT INTO import_control (mgm_id, import_type_id, stop_time, successful_import)
            SELECT i_mgm_id, i_import_type_id, now(), true FROM generate_series(1, 4)
            RETURNING control_id)
    SELECT array_agg(control_id ORDER BY control_id) INTO i_import_ids FROM imports;
    WITH imports AS (
        INSERT INTO import_control (mgm_id, import_type_id, stop_time, successful_import)
            SELECT i_mgm_v9_id, i_import_type_id, now(), true FROM generate_series(1, 3)
            RETURNING control_id)
    SELECT array_agg(control_id ORDER BY control_id) INTO i_import_v9_ids FROM imports;
    INSERT INTO probe_id VALUES ('tenant', i_tenant_id), ('import_base', i_import_ids[1] - 1),
        ('import_v9_3', i_import_v9_ids[3]), ('gw', i_gw_id), ('gw_added', i_gw_added_id), ('gw_v9', i_gw_v9_id);

    INSERT INTO rulebase (name, uid, mgm_id) VALUES ('upgrade_963_probe', 'upgrade_963_probe_migrated', i_mgm_id)
        RETURNING id INTO i_rulebase_id;
    INSERT INTO rulebase (name, uid, mgm_id) VALUES ('upgrade_963_probe', 'upgrade_963_probe_v9', i_mgm_v9_id)
        RETURNING id INTO i_rulebase_v9_id;
    INSERT INTO probe_id VALUES ('rulebase', i_rulebase_id), ('rulebase_v9', i_rulebase_v9_id);
    INSERT INTO rulebase_link (gw_id, to_rulebase_id, is_initial, created, link_type)
        VALUES (i_gw_id, i_rulebase_id, true, i_import_ids[3], 2),
            (i_gw_added_id, i_rulebase_id, true, i_import_ids[4], 2),
            (i_gw_v9_id, i_rulebase_v9_id, true, i_import_v9_ids[3], 2);

    FOR r_rule IN SELECT * FROM (VALUES
            -- rule key, management, rulebase, dev_id, created, removed, gateway entry created and removed
            ('historic', i_mgm_id, i_rulebase_id, i_gw_id, i_import_ids[1], i_import_ids[2], i_import_ids[3], NULL::bigint),
            ('current', i_mgm_id, i_rulebase_id, i_gw_id, i_import_ids[1], NULL, i_import_ids[3], NULL),
            ('removed_after_upgrade', i_mgm_id, i_rulebase_id, i_gw_id, i_import_ids[1], i_import_ids[4], i_import_ids[3], NULL),
            ('v9_historic', i_mgm_v9_id, i_rulebase_v9_id, NULL, i_import_v9_ids[1], i_import_v9_ids[2], i_import_v9_ids[1], i_import_v9_ids[2])
        ) AS r(rule_key, mgm_id, rulebase_id, dev_id, rule_create, removed, entry_created, entry_removed)
    LOOP
        INSERT INTO rule (mgm_id, dev_id, rulebase_id, rule_create, removed, action_id, track_id, rule_name, rule_src,
                rule_dst, rule_svc, rule_action, rule_track)
            VALUES (r_rule.mgm_id, r_rule.dev_id, r_rule.rulebase_id, r_rule.rule_create, r_rule.removed, i_action_id,
                i_track_id, r_rule.rule_key, 'any', 'any', 'any', 'accept', 'none')
            RETURNING rule_id INTO i_rule_id;
        INSERT INTO rule_enforced_on_gateway (rule_id, dev_id, created, removed)
            VALUES (i_rule_id, COALESCE(r_rule.dev_id, i_gw_v9_id), r_rule.entry_created, r_rule.entry_removed);
        INSERT INTO probe_id VALUES (r_rule.rule_key, i_rule_id);
    END LOOP;
END $$;

-- F29: both gateways below already exist when v9 migrates the management. The late gateway has no old rule
-- evidence; the evidenced gateway only enforced a later rule version [2,3). Neither enforced the historic [1,2)
-- version, although the migration gave both spurious Policy Targets entries for it.
DO $$
DECLARE
    i_mgm_id integer;
    i_dev_typ_id integer;
    i_rulebase_id integer;
    i_late_gw_id integer;
    i_evidenced_gw_id integer;
    i_unproven_rulebase_id integer;
    i_rule_id bigint;
BEGIN
    SELECT d.mgm_id, d.dev_typ_id INTO i_mgm_id, i_dev_typ_id
        FROM device d WHERE d.dev_id = pg_temp.probe('gw');
    SELECT r.rulebase_id INTO i_rulebase_id FROM rule r WHERE r.rule_id = pg_temp.probe('historic');
    INSERT INTO device (mgm_id, dev_typ_id, dev_name)
        VALUES (i_mgm_id, i_dev_typ_id, 'upgrade_963_probe_late_before_migration') RETURNING dev_id INTO i_late_gw_id;
    INSERT INTO device (mgm_id, dev_typ_id, dev_name)
        VALUES (i_mgm_id, i_dev_typ_id, 'upgrade_963_probe_evidenced') RETURNING dev_id INTO i_evidenced_gw_id;
    INSERT INTO probe_id VALUES ('gw_late', i_late_gw_id), ('gw_evidenced', i_evidenced_gw_id);

    INSERT INTO rulebase_link (gw_id, to_rulebase_id, is_initial, created, link_type)
        VALUES (i_late_gw_id, i_rulebase_id, true, pg_temp.probe('import_base') + 3, 2),
            (i_evidenced_gw_id, i_rulebase_id, true, pg_temp.probe('import_base') + 3, 2);
    INSERT INTO rule_enforced_on_gateway (rule_id, dev_id, created)
        VALUES (pg_temp.probe('historic'), i_late_gw_id, pg_temp.probe('import_base') + 3),
            (pg_temp.probe('historic'), i_evidenced_gw_id, pg_temp.probe('import_base') + 3);

    INSERT INTO rule (mgm_id, dev_id, rulebase_id, rule_create, removed, action_id, track_id, rule_name,
            rule_src, rule_dst, rule_svc, rule_action, rule_track)
        SELECT r.mgm_id, i_evidenced_gw_id, r.rulebase_id, pg_temp.probe('import_base') + 2,
            pg_temp.probe('import_base') + 3, r.action_id, r.track_id, 'historic_evidenced',
            'any', 'any', 'any', 'accept', 'none'
        FROM rule r WHERE r.rule_id = pg_temp.probe('historic')
        RETURNING rule_id INTO i_rule_id;
    INSERT INTO rule_enforced_on_gateway (rule_id, dev_id, created)
        VALUES (i_rule_id, i_evidenced_gw_id, pg_temp.probe('import_base') + 3);
    INSERT INTO probe_id VALUES ('historic_evidenced', i_rule_id);

    -- NAT rules use the rulebase-link fallback rather than explicit gateway entries. The same isolation applies.
    INSERT INTO rule (mgm_id, dev_id, rulebase_id, rule_create, removed, action_id, track_id, rule_name,
            rule_src, rule_dst, rule_svc, rule_action, rule_track, access_rule)
        SELECT r.mgm_id, r.dev_id, r.rulebase_id, r.rule_create, r.removed, r.action_id, r.track_id,
            'historic_nat', 'any', 'any', 'any', 'accept', 'none', false
        FROM rule r WHERE r.rule_id = pg_temp.probe('historic')
        RETURNING rule_id INTO i_rule_id;
    INSERT INTO probe_id VALUES ('historic_nat', i_rule_id);

    -- Evidence for the original gateway's first rulebase must not backdate a different rulebase on that gateway.
    INSERT INTO rulebase (name, uid, mgm_id)
        VALUES ('upgrade_963_probe_unproven', 'upgrade_963_probe_unproven', i_mgm_id)
        RETURNING id INTO i_unproven_rulebase_id;
    INSERT INTO rulebase_link (gw_id, to_rulebase_id, is_initial, created, link_type)
        VALUES (pg_temp.probe('gw'), i_unproven_rulebase_id, true, pg_temp.probe('import_base') + 3, 2);
    INSERT INTO probe_id VALUES ('unproven_rulebase', i_unproven_rulebase_id);
END $$;

-- without the upgrade, the historic rule version has no gateway valid during its lifetime
SELECT pg_temp.expect('historic rule version before the upgrade', pg_temp.fully_visible('historic', 'gw'), 'false');
SELECT pg_temp.expect('historic rule version simulated before the upgrade', pg_temp.simulated('historic', 'gw'), 'false');

-- These checks run after both upgrade executions, verifying the historical authorization boundary and idempotency.
CREATE FUNCTION pg_temp.expect_restored_history_isolated() RETURNS void AS $$
BEGIN
    PERFORM pg_temp.expect('spurious gateway entry for a gateway added before migration',
        pg_temp.gateway_entry('historic', 'gw_late'), '3-');
    PERFORM pg_temp.expect('link without historical enforcement evidence', pg_temp.rulebase_link('gw_late'), '3-');
    PERFORM pg_temp.expect('historic rule hidden from a gateway added before migration',
        pg_temp.fully_visible('historic', 'gw_late'), 'false');
    PERFORM pg_temp.expect('historic rule not simulated on a gateway added before migration',
        pg_temp.simulated('historic', 'gw_late'), 'false');

    PERFORM pg_temp.expect('gateway entry restored from its own rule history',
        pg_temp.gateway_entry('historic_evidenced', 'gw_evidenced'), '2-3');
    PERFORM pg_temp.expect('gateway link restored only to its own rule history',
        pg_temp.rulebase_link('gw_evidenced'), '2-');
    PERFORM pg_temp.expect('historic rule visible on its evidenced gateway',
        pg_temp.fully_visible('historic_evidenced', 'gw_evidenced'), 'true');
    PERFORM pg_temp.expect('historic rule simulated on its evidenced gateway',
        pg_temp.simulated('historic_evidenced', 'gw_evidenced'), 'true');
    PERFORM pg_temp.expect('spurious earlier version entry unchanged on an evidenced gateway',
        pg_temp.gateway_entry('historic', 'gw_evidenced'), '3-');
    PERFORM pg_temp.expect('earlier rule hidden from a gateway with later evidence',
        pg_temp.fully_visible('historic', 'gw_evidenced'), 'false');
    PERFORM pg_temp.expect('earlier rule not simulated on a gateway with later evidence',
        pg_temp.simulated('historic', 'gw_evidenced'), 'false');

    PERFORM pg_temp.expect('historic NAT rule visible on its original gateway',
        pg_temp.fully_visible('historic_nat', 'gw'), 'true');
    PERFORM pg_temp.expect('historic NAT rule hidden from a gateway without evidence',
        pg_temp.fully_visible('historic_nat', 'gw_late'), 'false');
    PERFORM pg_temp.expect('historic NAT rule hidden from a gateway with later evidence',
        pg_temp.fully_visible('historic_nat', 'gw_evidenced'), 'false');
    PERFORM pg_temp.expect('evidence for one rulebase does not backdate another rulebase',
        pg_temp.rulebase_link('gw', 'unproven_rulebase'), '3-');
END;
$$ LANGUAGE plpgsql;

-- @run-upgrade-9.7.1

SELECT pg_temp.expect('email certificates with a TLS server configured', pg_temp.config_value('emailCheckCertificates'), 'False');
SELECT pg_temp.expect('ticket system certificates with a URL configured', pg_temp.config_value('extTicketSystemsCheckCertificates'), 'False');

SELECT pg_temp.expect('gateway entry of the historic rule version', pg_temp.gateway_entry('historic', 'gw'), '1-2');
SELECT pg_temp.expect('gateway entry of the current rule', pg_temp.gateway_entry('current', 'gw'), '3-');
SELECT pg_temp.expect('gateway entry of a rule removed after the upgrade', pg_temp.gateway_entry('removed_after_upgrade', 'gw'), '3-');
SELECT pg_temp.expect('rulebase link of the migration', pg_temp.rulebase_link('gw'), '1-');
SELECT pg_temp.expect('rulebase link of a gateway added after the upgrade', pg_temp.rulebase_link('gw_added'), '4-');
SELECT pg_temp.expect('rulebase link of a v9 management', pg_temp.rulebase_link('gw_v9', 'rulebase_v9'), concat(pg_temp.probe('import_v9_3') - pg_temp.probe('import_base'), '-'));

SELECT pg_temp.expect('historic rule version on its gateway', pg_temp.fully_visible('historic', 'gw'), 'true');
SELECT pg_temp.expect('historic rule version simulated on its gateway', pg_temp.simulated('historic', 'gw'), 'true');
SELECT pg_temp.expect('historic rule version on a gateway added after the upgrade', pg_temp.fully_visible('historic', 'gw_added'), 'false');
SELECT pg_temp.expect('historic rule version simulated on a gateway added after the upgrade', pg_temp.simulated('historic', 'gw_added'), 'false');
SELECT pg_temp.expect('current rule on its gateway', pg_temp.fully_visible('current', 'gw'), 'true');
SELECT pg_temp.expect('v9 rule version removed before its gateway was added', pg_temp.fully_visible('v9_historic', 'gw_v9'), 'false');
SELECT pg_temp.expect('v9 rule version simulated on a gateway added after its removal', pg_temp.simulated('v9_historic', 'gw_v9'), 'false');
SELECT pg_temp.expect_restored_history_isolated();

-- second run: no email server and no ticket system URL, so the certificates are checked; the rule data stays the same
DELETE FROM config WHERE config_key IN ('emailCheckCertificates', 'extTicketSystemsCheckCertificates') AND config_user = 0;
SELECT pg_temp.set_config_value('emailServerAddress', '');
SELECT pg_temp.set_config_value('extTicketSystems', '[{"Id":1,"TypeId":2,"Name":"probe","Url":""}]');

-- @run-upgrade-9.7.1

SELECT pg_temp.expect('email certificates without a server', pg_temp.config_value('emailCheckCertificates'), 'True');
SELECT pg_temp.expect('ticket system certificates without a URL', pg_temp.config_value('extTicketSystemsCheckCertificates'), 'True');

SELECT pg_temp.expect('gateway entry of the historic rule version (repeated)', pg_temp.gateway_entry('historic', 'gw'), '1-2');
SELECT pg_temp.expect('gateway entry of the current rule (repeated)', pg_temp.gateway_entry('current', 'gw'), '3-');
SELECT pg_temp.expect('rulebase link of the migration (repeated)', pg_temp.rulebase_link('gw'), '1-');
SELECT pg_temp.expect('rulebase link of a gateway added after the upgrade (repeated)', pg_temp.rulebase_link('gw_added'), '4-');
SELECT pg_temp.expect_restored_history_isolated();

ROLLBACK;
