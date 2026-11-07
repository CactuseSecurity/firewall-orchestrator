-- Regression probe for GHSA-6g84 (fwo-middleware-server-usergroupresolver-001): the change_history_bind_changer
-- trigger names the user behind changer_id as the author of a row, fills changer_id from the Hasura session
-- when the role has no preset (admin), and keeps audit_proof_critical only for a workflow change of an existing
-- ticket by a known user other than its requester.
--
-- The Hasura session is simulated with set_config('hasura.user', ..., true), which is how Hasura passes the
-- session variables of a mutation to Postgres.
--
-- Runs in a single transaction that is always rolled back, so it leaves no data behind. Any failed expectation
-- raises an exception, which makes psql (ON_ERROR_STOP) exit with a non-zero code.

\set ON_ERROR_STOP 1
\o /dev/null
SET client_min_messages TO ERROR;

BEGIN;

CREATE TEMP TABLE probe_id (probe_key text PRIMARY KEY, probe_value bigint NOT NULL) ON COMMIT DROP;

CREATE FUNCTION pg_temp.probe(p_key text) RETURNS bigint AS $$
    SELECT probe_value FROM probe_id WHERE probe_key = p_key
$$ LANGUAGE sql STABLE;

CREATE FUNCTION pg_temp.expect(p_case text, p_actual text, p_expected text) RETURNS void AS $$
BEGIN
    IF p_actual IS DISTINCT FROM p_expected THEN
        RAISE EXCEPTION 'change_history author binding: % - expected %, got %', p_case, p_expected, p_actual;
    END IF;
END;
$$ LANGUAGE plpgsql;

-- writes one history row as the given session and returns its id
CREATE FUNCTION pg_temp.write_row(p_session text, p_module text, p_ticket_id bigint, p_changer text,
    p_changer_id integer, p_audit_proof_critical boolean) RETURNS bigint AS $$
DECLARE
    v_id bigint;
BEGIN
    PERFORM set_config('hasura.user', coalesce(p_session, ''), true);
    INSERT INTO change_history (module, ticket_id, change_type, object_type, object_id, change_text, changer, changer_id,
        change_source, audit_proof_critical)
    VALUES (p_module, p_ticket_id, 2, 101, 1, 'probe', p_changer, p_changer_id, p_module, p_audit_proof_critical)
    RETURNING id INTO v_id;
    PERFORM set_config('hasura.user', '', true);
    RETURN v_id;
END;
$$ LANGUAGE plpgsql;

CREATE FUNCTION pg_temp.author(p_row_id bigint) RETURNS text AS $$
    SELECT coalesce(changer_id::text, '<null>') || '/' || coalesce(changer, '<null>') || '/' || audit_proof_critical::text
    FROM change_history WHERE id = p_row_id
$$ LANGUAGE sql STABLE;

WITH requester AS (
    INSERT INTO uiuser (uiuser_username, uuid) VALUES ('probe-requester', 'uid=probe-requester,ou=probe') RETURNING uiuser_id
), planner AS (
    INSERT INTO uiuser (uiuser_username, uuid) VALUES ('probe-planner', 'uid=probe-planner,ou=probe') RETURNING uiuser_id
)
INSERT INTO probe_id
    SELECT 'requester', uiuser_id FROM requester
    UNION ALL SELECT 'planner', uiuser_id FROM planner;

WITH ticket AS (
    INSERT INTO request.ticket (title, state_id, requester_id)
    VALUES ('probe ticket', (SELECT min(id) FROM request.state), pg_temp.probe('requester')::integer)
    RETURNING id
)
INSERT INTO probe_id SELECT 'ticket', id FROM ticket;

DO $$
DECLARE
    v_planner text := pg_temp.probe('planner')::text;
    v_requester text := pg_temp.probe('requester')::text;
    v_ticket bigint := pg_temp.probe('ticket');
    v_planner_session text := json_build_object('x-hasura-role', 'planner', 'x-hasura-user-id', v_planner)::text;
    v_requester_session text := json_build_object('x-hasura-role', 'requester', 'x-hasura-user-id', v_requester)::text;
    v_admin_session text := json_build_object('x-hasura-role', 'admin', 'x-hasura-user-id', v_planner)::text;
    v_service_session text := json_build_object('x-hasura-role', 'middleware-server')::text;
BEGIN
    -- a planner changes someone else's ticket: the name is bound to the preset changer_id, the flag stays
    PERFORM pg_temp.expect('planner flags change of foreign ticket',
        pg_temp.author(pg_temp.write_row(v_planner_session, 'workflow', v_ticket, 'probe-requester', v_planner::integer, true)),
        v_planner || '/probe-planner/true');

    -- the requester cannot flag a change of the own ticket as someone else's manual change
    PERFORM pg_temp.expect('requester flags change of own ticket',
        pg_temp.author(pg_temp.write_row(v_requester_session, 'workflow', v_ticket, 'probe-planner', v_requester::integer, true)),
        v_requester || '/probe-requester/false');

    -- only workflow rows can be audit-proof critical
    PERFORM pg_temp.expect('modelling row flagged',
        pg_temp.author(pg_temp.write_row(v_planner_session, 'modelling', v_ticket, 'probe-planner', v_planner::integer, true)),
        v_planner || '/probe-planner/false');

    -- a flagged row needs a ticket to compare the requester with
    PERFORM pg_temp.expect('flagged row without ticket',
        pg_temp.author(pg_temp.write_row(v_planner_session, 'workflow', NULL, 'probe-planner', v_planner::integer, true)),
        v_planner || '/probe-planner/false');

    -- the admin role has no changer_id preset: the id is taken from the session
    PERFORM pg_temp.expect('admin session without changer_id',
        pg_temp.author(pg_temp.write_row(v_admin_session, 'workflow', v_ticket, 'probe-requester', NULL, true)),
        v_planner || '/probe-planner/true');

    -- an internal service JWT carries no user id: the row stays automated and cannot be flagged
    PERFORM pg_temp.expect('service session without user id',
        pg_temp.author(pg_temp.write_row(v_service_session, 'workflow', v_ticket, 'middleware-server', NULL, true)),
        '<null>/middleware-server/false');

    -- the middleware acting for an authenticated caller passes the id explicitly; the name follows it
    PERFORM pg_temp.expect('service session on behalf of a user',
        pg_temp.author(pg_temp.write_row(v_service_session, 'workflow', v_ticket, 'probe-requester', v_planner::integer, false)),
        v_planner || '/probe-planner/false');

    -- a direct database write has no Hasura session at all
    PERFORM pg_temp.expect('write without session',
        pg_temp.author(pg_temp.write_row(NULL, 'modelling', NULL, 'import', NULL, false)),
        '<null>/import/false');

    -- a session user id that is not a plain number is ignored
    PERFORM pg_temp.expect('malformed session user id',
        pg_temp.author(pg_temp.write_row(json_build_object('x-hasura-role', 'admin', 'x-hasura-user-id', '1 or 1=1')::text,
            'workflow', v_ticket, 'probe-planner', NULL, true)),
        '<null>/probe-planner/false');

    -- an unknown changer_id keeps the supplied name (only reachable for the middleware-server role)
    PERFORM pg_temp.expect('unknown changer id',
        pg_temp.author(pg_temp.write_row(v_service_session, 'workflow', NULL, 'middleware-server', -1, false)),
        '-1/middleware-server/false');
END $$;

ROLLBACK;
