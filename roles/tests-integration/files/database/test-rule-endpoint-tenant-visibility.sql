-- Regression probe for GHSA-v8hx-cx2q-j75v: the tenant visibility of a rule_from / rule_to row must depend only on
-- the row itself, its rule and the gateways of that rule - never on other devices, rules or rule endpoints.
-- The gateways of a rule are those in rule_enforced_on_gateway valid for the rule version, or for a rule with an
-- all-gateways entry or a nat rule without entries those linking its rulebase. An access rule without entries is
-- installed on targets unknown to the management and is never fully visible.
-- rule.dev_id is obsolete: it is set like in v8 data, so that the original flaw (taking the device of an arbitrary
-- rule) would show, but the functions must not use it.
-- Each case is checked for rule_from, rule_to and rule, and for the tenant simulation functions.
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

CREATE FUNCTION pg_temp.tenant_session(p_tenant_id bigint) RETURNS json AS $$
    SELECT json_build_object('x-hasura-tenant-id', p_tenant_id::text)
$$ LANGUAGE sql IMMUTABLE;

CREATE FUNCTION pg_temp.rule_from_visible(p_rule_key text, p_tenant_id bigint) RETURNS boolean AS $$
    SELECT rule_from_relevant_for_tenant(rf, pg_temp.tenant_session(p_tenant_id))
    FROM rule_from rf WHERE rf.rule_id = pg_temp.probe(p_rule_key)
$$ LANGUAGE sql STABLE;

CREATE FUNCTION pg_temp.rule_to_visible(p_rule_key text, p_tenant_id bigint) RETURNS boolean AS $$
    SELECT rule_to_relevant_for_tenant(rt, pg_temp.tenant_session(p_tenant_id))
    FROM rule_to rt WHERE rt.rule_id = pg_temp.probe(p_rule_key)
$$ LANGUAGE sql STABLE;

CREATE FUNCTION pg_temp.rule_visible(p_rule_key text, p_tenant_id bigint) RETURNS boolean AS $$
    SELECT rule_relevant_for_tenant(r, pg_temp.tenant_session(p_tenant_id))
    FROM rule r WHERE r.rule_id = pg_temp.probe(p_rule_key)
$$ LANGUAGE sql STABLE;

-- tenant simulation by the admin tenant: the rule is listed for the first gateway of its management
-- (both gateways use the rulebase) and shows its endpoints
CREATE FUNCTION pg_temp.simulated_rule_visible(p_rule_key text, p_tenant_id bigint) RETURNS boolean AS $$
    SELECT pg_temp.probe(p_rule_key) IN (
            SELECT sim.rule_id FROM device d, get_rules_for_tenant(d, p_tenant_id::integer, pg_temp.tenant_session(1)) sim
            WHERE d.dev_id = pg_temp.probe('dev_' || split_part(p_rule_key, '_', 1)))
$$ LANGUAGE sql STABLE;

CREATE FUNCTION pg_temp.simulated_endpoints_visible(p_rule_key text, p_tenant_id bigint) RETURNS boolean AS $$
    SELECT (SELECT count(*) FROM get_rule_froms_for_tenant(r, p_tenant_id::integer, pg_temp.tenant_session(1))) > 0
        AND (SELECT count(*) FROM get_rule_tos_for_tenant(r, p_tenant_id::integer, pg_temp.tenant_session(1))) > 0
    FROM rule r WHERE r.rule_id = pg_temp.probe(p_rule_key)
$$ LANGUAGE sql STABLE;

CREATE FUNCTION pg_temp.expect(p_case text, p_actual boolean, p_expected boolean) RETURNS void AS $$
BEGIN
    IF p_actual IS DISTINCT FROM p_expected THEN
        RAISE EXCEPTION 'rule endpoint tenant visibility: % - expected %, got %', p_case, p_expected, p_actual;
    END IF;
END;
$$ LANGUAGE plpgsql;

-- expects the rule and both its endpoints to have the same visibility (a rule is shown either completely or not at all)
CREATE FUNCTION pg_temp.expect_rule(p_case text, p_rule_key text, p_tenant_id bigint, p_expected boolean) RETURNS void AS $$
BEGIN
    PERFORM pg_temp.expect(p_case || ' (rule_from)', pg_temp.rule_from_visible(p_rule_key, p_tenant_id), p_expected);
    PERFORM pg_temp.expect(p_case || ' (rule_to)', pg_temp.rule_to_visible(p_rule_key, p_tenant_id), p_expected);
    PERFORM pg_temp.expect(p_case || ' (rule)', pg_temp.rule_visible(p_rule_key, p_tenant_id), p_expected);
    IF p_tenant_id != 1 THEN -- the admin tenant cannot be simulated
        PERFORM pg_temp.expect(p_case || ' (simulated rule)', pg_temp.simulated_rule_visible(p_rule_key, p_tenant_id), p_expected);
        PERFORM pg_temp.expect(p_case || ' (simulated endpoints)', pg_temp.simulated_endpoints_visible(p_rule_key, p_tenant_id), p_expected);
    END IF;
END;
$$ LANGUAGE plpgsql;

DO $$
DECLARE
    i_credential_id integer;
    i_dev_typ_id integer;
    i_import_type_id integer;
    i_action_id integer;
    i_track_id integer;
    i_host_typ_id integer;
    i_network_typ_id integer;
    i_mgm_id integer;
    i_dev_id integer;
    i_gw2_id integer;
    i_rulebase_id integer;
    i_import_id bigint;
    i_tenant_id integer;
    i_rule_id bigint;
    i_obj_id bigint;
    r_mgm RECORD;
    r_obj RECORD;
    r_rule RECORD;
BEGIN
    SELECT min(dev_typ_id) INTO i_dev_typ_id FROM stm_dev_typ;
    SELECT import_type_id INTO i_import_type_id FROM stm_import WHERE import_type_name = 'rule';
    SELECT min(action_id) INTO i_action_id FROM stm_action;
    SELECT min(track_id) INTO i_track_id FROM stm_track;
    SELECT obj_typ_id INTO i_host_typ_id FROM stm_obj_typ WHERE obj_typ_name = 'host';
    SELECT obj_typ_id INTO i_network_typ_id FROM stm_obj_typ WHERE obj_typ_name = 'network';

    INSERT INTO import_credential (credential_name, username, secret)
        VALUES ('ghsa_v8hx_probe', 'probe', 'probe') RETURNING id INTO i_credential_id;

    INSERT INTO tenant (tenant_name) VALUES ('ghsa_v8hx_probe_tenant') RETURNING tenant_id INTO i_tenant_id;
    INSERT INTO probe_id VALUES ('tenant', i_tenant_id);
    INSERT INTO tenant_network (tenant_id, tenant_net_ip, tenant_net_ip_end)
        VALUES (i_tenant_id, '10.200.0.0/32', '10.200.0.255/32');

    -- "other" is created first, so that its rule endpoints are the first rows of rule_from / rule_to
    -- in an otherwise empty database: the vulnerable functions took the device of exactly these rows
    FOR r_mgm IN SELECT * FROM (VALUES (1, 'other'), (2, 'target')) AS m(sort_order, mgm_key) ORDER BY sort_order
    LOOP
        INSERT INTO management (dev_typ_id, mgm_name, import_credential_id, ssh_hostname)
            VALUES (i_dev_typ_id, 'ghsa_v8hx_probe_' || r_mgm.mgm_key, i_credential_id, '127.0.0.1')
            RETURNING mgm_id INTO i_mgm_id;
        INSERT INTO device (mgm_id, dev_typ_id, dev_name)
            VALUES (i_mgm_id, i_dev_typ_id, 'ghsa_v8hx_probe_' || r_mgm.mgm_key) RETURNING dev_id INTO i_dev_id;
        INSERT INTO device (mgm_id, dev_typ_id, dev_name)
            VALUES (i_mgm_id, i_dev_typ_id, 'ghsa_v8hx_probe_' || r_mgm.mgm_key || '_gw2') RETURNING dev_id INTO i_gw2_id;
        INSERT INTO import_control (mgm_id, import_type_id) VALUES (i_mgm_id, i_import_type_id)
            RETURNING control_id INTO i_import_id;
        INSERT INTO rulebase (name, uid, mgm_id)
            VALUES ('ghsa_v8hx_probe', 'ghsa_v8hx_probe_' || r_mgm.mgm_key, i_mgm_id) RETURNING id INTO i_rulebase_id;
        -- both gateways use the rulebase
        INSERT INTO rulebase_link (gw_id, to_rulebase_id, is_initial, created)
            VALUES (i_dev_id, i_rulebase_id, true, i_import_id), (i_gw2_id, i_rulebase_id, true, i_import_id);
        INSERT INTO probe_id VALUES ('mgm_' || r_mgm.mgm_key, i_mgm_id), ('dev_' || r_mgm.mgm_key, i_dev_id),
            ('gw2_' || r_mgm.mgm_key, i_gw2_id);

        FOR r_obj IN SELECT * FROM (VALUES
                ('outside_src', '192.168.10.1/32', '192.168.10.1/32'),
                ('outside_dst', '192.168.20.1/32', '192.168.20.1/32'),
                ('tenant_host', '10.200.0.1/32', '10.200.0.1/32'),
                ('any', '0.0.0.0/32', '255.255.255.255/32')
            ) AS o(obj_key, ip, ip_end)
        LOOP
            INSERT INTO firewall.nw_object (mgm_id, obj_typ_id, obj_create, obj_name, obj_ip, obj_ip_end)
                VALUES (i_mgm_id, CASE WHEN r_obj.ip = r_obj.ip_end THEN i_host_typ_id ELSE i_network_typ_id END,
                    i_import_id, r_obj.obj_key, r_obj.ip::cidr, r_obj.ip_end::cidr)
                RETURNING obj_id INTO i_obj_id;
            INSERT INTO objgrp_flat (objgrp_flat_id, objgrp_flat_member_id, import_created)
                VALUES (i_obj_id, i_obj_id, i_import_id);
            INSERT INTO probe_id VALUES (r_mgm.mgm_key || '_' || r_obj.obj_key, i_obj_id);
        END LOOP;
        INSERT INTO probe_id VALUES ('rulebase_' || r_mgm.mgm_key, i_rulebase_id), ('import_' || r_mgm.mgm_key, i_import_id);

        -- enforced_on: the gateways in rule_enforced_on_gateway ("all": an all-gateways entry, "none": no entry)
        FOR r_rule IN SELECT * FROM (VALUES
                ('hidden', 'outside_src', false, 'outside_dst', false, 'dev', true),
                ('src_in_tenant', 'tenant_host', false, 'outside_dst', false, 'dev', true),
                ('dst_in_tenant', 'outside_src', false, 'tenant_host', false, 'dev', true),
                ('src_negated_host', 'outside_src', true, 'outside_dst', false, 'dev', true),
                ('src_negated_any', 'any', true, 'outside_dst', false, 'dev', true),
                ('dst_negated_host', 'outside_src', false, 'outside_dst', true, 'dev', true),
                ('dst_negated_any', 'outside_src', false, 'any', true, 'dev', true),
                ('on_gw2_only', 'outside_src', false, 'outside_dst', false, 'gw2', true),
                ('on_both', 'outside_src', false, 'outside_dst', false, 'both', true),
                ('on_all', 'outside_src', false, 'outside_dst', false, 'all', true),
                ('install_on_unknown', 'outside_src', false, 'outside_dst', false, 'none', true),
                ('nat_not_enforced', 'outside_src', false, 'outside_dst', false, 'none', false)
            ) AS r(rule_key, src_key, src_neg, dst_key, dst_neg, enforced_on, is_access_rule)
        LOOP
            INSERT INTO rule (mgm_id, dev_id, rulebase_id, rule_create, action_id, track_id, rule_name,
                    rule_src, rule_dst, rule_svc, rule_action, rule_track, rule_src_neg, rule_dst_neg, access_rule, nat_rule)
                VALUES (i_mgm_id, i_dev_id, i_rulebase_id, i_import_id, i_action_id, i_track_id, r_rule.rule_key,
                    r_rule.src_key, r_rule.dst_key, 'any', 'accept', 'none', r_rule.src_neg, r_rule.dst_neg,
                    r_rule.is_access_rule, NOT r_rule.is_access_rule)
                RETURNING rule_id INTO i_rule_id;
            INSERT INTO rule_enforced_on_gateway (rule_id, dev_id, created)
                SELECT i_rule_id, gw.dev_id, i_import_id FROM (VALUES ('dev', i_dev_id), ('gw2', i_gw2_id), ('all', NULL)) AS gw(gw_key, dev_id)
                WHERE r_rule.enforced_on IN (gw.gw_key, 'both') AND (gw.gw_key != 'all' OR r_rule.enforced_on = 'all');
            INSERT INTO rule_from (rule_id, obj_id, rf_create)
                VALUES (i_rule_id, pg_temp.probe(r_mgm.mgm_key || '_' || r_rule.src_key), i_import_id);
            INSERT INTO rule_to (rule_id, obj_id, rt_create)
                VALUES (i_rule_id, pg_temp.probe(r_mgm.mgm_key || '_' || r_rule.dst_key), i_import_id);
            INSERT INTO probe_id VALUES (r_mgm.mgm_key || '_' || r_rule.rule_key, i_rule_id);
        END LOOP;
    END LOOP;
END $$;

-- ip based visibility of the target rules (no device or management of the tenant is unfiltered)
SELECT pg_temp.expect_rule('rule outside the tenant networks', 'target_hidden', pg_temp.probe('tenant'), false);
SELECT pg_temp.expect_rule('source in a tenant network', 'target_src_in_tenant', pg_temp.probe('tenant'), true);
SELECT pg_temp.expect_rule('destination in a tenant network', 'target_dst_in_tenant', pg_temp.probe('tenant'), true);
SELECT pg_temp.expect_rule('negated source host', 'target_src_negated_host', pg_temp.probe('tenant'), true);
SELECT pg_temp.expect_rule('negated source any', 'target_src_negated_any', pg_temp.probe('tenant'), false);
SELECT pg_temp.expect_rule('negated destination host', 'target_dst_negated_host', pg_temp.probe('tenant'), true);
SELECT pg_temp.expect_rule('negated destination any', 'target_dst_negated_any', pg_temp.probe('tenant'), false);

-- an endpoint negated on its own on top of a negated rule side is not negated
UPDATE rule_from SET negated = true WHERE rule_id = pg_temp.probe('target_src_negated_any');
SELECT pg_temp.expect_rule('double negated source any', 'target_src_negated_any', pg_temp.probe('tenant'), true);
UPDATE rule_from SET negated = false WHERE rule_id = pg_temp.probe('target_src_negated_any');

-- an unrelated device that is fully visible to the tenant must not change the visibility of the target rule
INSERT INTO tenant_to_device (tenant_id, device_id, shared) VALUES (pg_temp.probe('tenant'), pg_temp.probe('dev_other'), false);
SELECT pg_temp.expect_rule('unrelated device fully visible', 'other_hidden', pg_temp.probe('tenant'), true);
SELECT pg_temp.expect_rule('target rule with unrelated device fully visible', 'target_hidden', pg_temp.probe('tenant'), false);
DELETE FROM tenant_to_device WHERE device_id = pg_temp.probe('dev_other');
SELECT pg_temp.expect_rule('unrelated device no longer visible', 'other_hidden', pg_temp.probe('tenant'), false);
SELECT pg_temp.expect_rule('target rule with unrelated device removed', 'target_hidden', pg_temp.probe('tenant'), false);

-- the same for an unrelated management
INSERT INTO tenant_to_management (tenant_id, management_id, shared) VALUES (pg_temp.probe('tenant'), pg_temp.probe('mgm_other'), false);
SELECT pg_temp.expect_rule('target rule with unrelated management fully visible', 'target_hidden', pg_temp.probe('tenant'), false);
DELETE FROM tenant_to_management WHERE management_id = pg_temp.probe('mgm_other');

-- full visibility through the gateways the rule is enforced on
INSERT INTO tenant_to_device (tenant_id, device_id, shared) VALUES (pg_temp.probe('tenant'), pg_temp.probe('dev_target'), false);
SELECT pg_temp.expect_rule('rule enforced on another gateway only', 'target_on_gw2_only', pg_temp.probe('tenant'), false);
SELECT pg_temp.expect_rule('rule enforced on two gateways, one fully visible', 'target_on_both', pg_temp.probe('tenant'), true);
SELECT pg_temp.expect_rule('rule for all gateways, rulebase linked to a visible gateway', 'target_on_all', pg_temp.probe('tenant'), true);
SELECT pg_temp.expect_rule('nat rule without enforcing gateways, rulebase linked to a visible gateway', 'target_nat_not_enforced', pg_temp.probe('tenant'), true);
SELECT pg_temp.expect_rule('access rule installed on targets unknown to the management', 'target_install_on_unknown', pg_temp.probe('tenant'), false);
DELETE FROM tenant_to_device WHERE device_id = pg_temp.probe('dev_target');
INSERT INTO tenant_to_device (tenant_id, device_id, shared) VALUES (pg_temp.probe('tenant'), pg_temp.probe('gw2_target'), false);
SELECT pg_temp.expect_rule('rule enforced on the fully visible gateway only', 'target_on_gw2_only', pg_temp.probe('tenant'), true);
SELECT pg_temp.expect_rule('rule enforced on the other gateway only', 'target_hidden', pg_temp.probe('tenant'), false);
DELETE FROM tenant_to_device WHERE device_id = pg_temp.probe('gw2_target');
INSERT INTO tenant_to_device (tenant_id, device_id, shared) VALUES (pg_temp.probe('tenant'), pg_temp.probe('dev_other'), false);
SELECT pg_temp.expect_rule('nat rule without enforcing gateways, unrelated gateway visible', 'target_nat_not_enforced', pg_temp.probe('tenant'), false);
DELETE FROM tenant_to_device WHERE device_id = pg_temp.probe('dev_other');

-- only links valid for the rule version count: a current rule moved to another gateway, a historic rule version that
-- was enforced on the gateway, and a rulebase moved to another gateway (with a nat rule, which follows the rulebase)
DO $$
DECLARE
    i_import2_id bigint;
    i_rule_id bigint;
    i_rulebase_id integer;
BEGIN
    -- a finished import, as only one running import per management is allowed
    INSERT INTO import_control (mgm_id, import_type_id, stop_time)
        SELECT pg_temp.probe('mgm_target'), import_type_id, now() FROM stm_import WHERE import_type_name = 'rule'
        RETURNING control_id INTO i_import2_id;

    -- the importer keeps the rule_id of an unchanged rule whose install-on changed and marks the old entry removed
    INSERT INTO rule (mgm_id, rulebase_id, rule_create, action_id, track_id, rule_name, rule_src, rule_dst, rule_svc,
            rule_action, rule_track)
        SELECT r.mgm_id, r.rulebase_id, r.rule_create, r.action_id, r.track_id, 'moved_to_gw2', r.rule_src, r.rule_dst,
            r.rule_svc, r.rule_action, r.rule_track
        FROM rule r WHERE r.rule_id = pg_temp.probe('target_hidden')
        RETURNING rule_id INTO i_rule_id;
    INSERT INTO rule_enforced_on_gateway (rule_id, dev_id, created, removed)
        VALUES (i_rule_id, pg_temp.probe('dev_target'), pg_temp.probe('import_target'), i_import2_id),
            (i_rule_id, pg_temp.probe('gw2_target'), i_import2_id, NULL);
    INSERT INTO rule_from (rule_id, obj_id, rf_create) VALUES (i_rule_id, pg_temp.probe('target_outside_src'), pg_temp.probe('import_target'));
    INSERT INTO rule_to (rule_id, obj_id, rt_create) VALUES (i_rule_id, pg_temp.probe('target_outside_dst'), pg_temp.probe('import_target'));
    INSERT INTO probe_id VALUES ('target_moved_to_gw2', i_rule_id);

    -- a rule version removed by the second import, while it was enforced on the first gateway
    INSERT INTO rule (mgm_id, rulebase_id, rule_create, removed, action_id, track_id, rule_name, rule_src, rule_dst,
            rule_svc, rule_action, rule_track)
        SELECT r.mgm_id, r.rulebase_id, r.rule_create, i_import2_id, r.action_id, r.track_id, 'historic_on_dev',
            r.rule_src, r.rule_dst, r.rule_svc, r.rule_action, r.rule_track
        FROM rule r WHERE r.rule_id = pg_temp.probe('target_hidden')
        RETURNING rule_id INTO i_rule_id;
    INSERT INTO rule_enforced_on_gateway (rule_id, dev_id, created, removed)
        VALUES (i_rule_id, pg_temp.probe('dev_target'), pg_temp.probe('import_target'), i_import2_id);
    INSERT INTO rule_from (rule_id, obj_id, rf_create) VALUES (i_rule_id, pg_temp.probe('target_outside_src'), pg_temp.probe('import_target'));
    INSERT INTO rule_to (rule_id, obj_id, rt_create) VALUES (i_rule_id, pg_temp.probe('target_outside_dst'), pg_temp.probe('import_target'));
    INSERT INTO probe_id VALUES ('target_historic_on_dev', i_rule_id);

    -- a rulebase linked to the first gateway until the second import, and to the second gateway since then
    INSERT INTO rulebase (name, uid, mgm_id)
        VALUES ('ghsa_v8hx_probe_moved', 'ghsa_v8hx_probe_target_moved', pg_temp.probe('mgm_target')) RETURNING id INTO i_rulebase_id;
    INSERT INTO rulebase_link (gw_id, to_rulebase_id, is_initial, created, removed)
        VALUES (pg_temp.probe('dev_target'), i_rulebase_id, true, pg_temp.probe('import_target'), i_import2_id),
            (pg_temp.probe('gw2_target'), i_rulebase_id, true, i_import2_id, NULL);
    INSERT INTO rule (mgm_id, rulebase_id, rule_create, action_id, track_id, rule_name, rule_src, rule_dst, rule_svc,
            rule_action, rule_track, access_rule, nat_rule)
        SELECT r.mgm_id, i_rulebase_id, i_import2_id, r.action_id, r.track_id, 'nat_in_moved_rulebase', r.rule_src,
            r.rule_dst, r.rule_svc, r.rule_action, r.rule_track, false, true
        FROM rule r WHERE r.rule_id = pg_temp.probe('target_hidden')
        RETURNING rule_id INTO i_rule_id;
    INSERT INTO rule_from (rule_id, obj_id, rf_create) VALUES (i_rule_id, pg_temp.probe('target_outside_src'), i_import2_id);
    INSERT INTO rule_to (rule_id, obj_id, rt_create) VALUES (i_rule_id, pg_temp.probe('target_outside_dst'), i_import2_id);
    INSERT INTO probe_id VALUES ('target_nat_in_moved_rulebase', i_rule_id);
END $$;

INSERT INTO tenant_to_device (tenant_id, device_id, shared) VALUES (pg_temp.probe('tenant'), pg_temp.probe('dev_target'), false);
SELECT pg_temp.expect_rule('current rule moved away from the visible gateway', 'target_moved_to_gw2', pg_temp.probe('tenant'), false);
SELECT pg_temp.expect_rule('historic rule version enforced on the visible gateway', 'target_historic_on_dev', pg_temp.probe('tenant'), true);
SELECT pg_temp.expect_rule('rule of a rulebase moved away from the visible gateway', 'target_nat_in_moved_rulebase', pg_temp.probe('tenant'), false);
DELETE FROM tenant_to_device WHERE device_id = pg_temp.probe('dev_target');
INSERT INTO tenant_to_device (tenant_id, device_id, shared) VALUES (pg_temp.probe('tenant'), pg_temp.probe('gw2_target'), false);
SELECT pg_temp.expect_rule('current rule moved to the visible gateway', 'target_moved_to_gw2', pg_temp.probe('tenant'), true);
SELECT pg_temp.expect_rule('historic rule version not enforced on the visible gateway', 'target_historic_on_dev', pg_temp.probe('tenant'), false);
-- the simulation lists rules per gateway of the first device only, so the positive case is checked without it
SELECT pg_temp.expect('rule of a rulebase moved to the visible gateway (rule)', pg_temp.rule_visible('target_nat_in_moved_rulebase', pg_temp.probe('tenant')), true);
SELECT pg_temp.expect('rule of a rulebase moved to the visible gateway (rule_from)', pg_temp.rule_from_visible('target_nat_in_moved_rulebase', pg_temp.probe('tenant')), true);
DELETE FROM tenant_to_device WHERE device_id = pg_temp.probe('gw2_target');

-- full rulebase visibility of the target device and management
INSERT INTO tenant_to_device (tenant_id, device_id, shared) VALUES (pg_temp.probe('tenant'), pg_temp.probe('dev_target'), true);
SELECT pg_temp.expect_rule('target device shared (ip filtered)', 'target_hidden', pg_temp.probe('tenant'), false);
UPDATE tenant_to_device SET shared = false WHERE device_id = pg_temp.probe('dev_target');
SELECT pg_temp.expect_rule('target device fully visible', 'target_hidden', pg_temp.probe('tenant'), true);
SELECT pg_temp.expect_rule('target device fully visible, negated any', 'target_src_negated_any', pg_temp.probe('tenant'), true);
DELETE FROM tenant_to_device WHERE device_id = pg_temp.probe('dev_target');

INSERT INTO tenant_to_management (tenant_id, management_id, shared) VALUES (pg_temp.probe('tenant'), pg_temp.probe('mgm_target'), true);
SELECT pg_temp.expect_rule('target management shared (ip filtered)', 'target_hidden', pg_temp.probe('tenant'), false);
UPDATE tenant_to_management SET shared = false WHERE management_id = pg_temp.probe('mgm_target');
SELECT pg_temp.expect_rule('target management fully visible', 'target_hidden', pg_temp.probe('tenant'), true);
DELETE FROM tenant_to_management WHERE management_id = pg_temp.probe('mgm_target');
SELECT pg_temp.expect_rule('target management no longer visible', 'target_hidden', pg_temp.probe('tenant'), false);

-- the admin tenant sees everything
SELECT pg_temp.expect_rule('admin tenant', 'target_hidden', 1, true);
SELECT pg_temp.expect_rule('admin tenant, negated any', 'target_src_negated_any', 1, true);

-- a session without tenant id is refused
DO $$
DECLARE
    b_refused_from boolean := false;
    b_refused_to boolean := false;
BEGIN
    BEGIN
        PERFORM rule_from_relevant_for_tenant(rf, '{}'::json) FROM rule_from rf WHERE rf.rule_id = pg_temp.probe('target_hidden');
    EXCEPTION WHEN raise_exception THEN
        b_refused_from := true;
    END;
    BEGIN
        PERFORM rule_to_relevant_for_tenant(rt, '{}'::json) FROM rule_to rt WHERE rt.rule_id = pg_temp.probe('target_hidden');
    EXCEPTION WHEN raise_exception THEN
        b_refused_to := true;
    END;
    PERFORM pg_temp.expect('session without tenant id (rule_from)', b_refused_from, true);
    PERFORM pg_temp.expect('session without tenant id (rule_to)', b_refused_to, true);
END $$;

ROLLBACK;
