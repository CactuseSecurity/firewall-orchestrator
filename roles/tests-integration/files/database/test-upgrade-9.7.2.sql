-- Test of the data changes of upgrade/9.7.2.sql (GHSA-695m-f6mv-9g72): local users are bound to the immutable object
-- id of their directory entry. Sessions of local users without object id end, as a refresh must not bind them; bound
-- local users keep their sessions. An object id belongs to one local user per LDAP connection. The server of an LDAP
-- connection becomes a list of servers; the test turns it back into the single server of 9.7.1 first.
-- The test task inserts the upgrade file at the two run-upgrade markers below, which also shows that the upgrade can
-- be repeated.
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
        RAISE EXCEPTION 'upgrade 9.7.2: % - expected %, got %', p_case, p_expected, p_actual;
    END IF;
END;
$$ LANGUAGE plpgsql;

-- whether the refresh token of a probe user is still usable
CREATE FUNCTION pg_temp.session_active(p_user_key text) RETURNS text AS $$
    SELECT bool_and(revoked_at IS NULL)::text FROM refresh_token WHERE user_id = pg_temp.probe(p_user_key)
$$ LANGUAGE sql STABLE;

CREATE FUNCTION pg_temp.add_user(p_key text, p_object_id text) RETURNS void AS $$
DECLARE
    i_user_id INT;
BEGIN
    INSERT INTO uiuser (uiuser_username, uuid, ldap_connection_id)
    VALUES (p_key, 'uid=' || p_key || ',ou=upgrade-9-7-2,dc=example,dc=com', pg_temp.probe('ldap'))
    RETURNING uiuser_id INTO i_user_id;
    INSERT INTO probe_id VALUES (p_key, i_user_id);
    INSERT INTO refresh_token (user_id, token_hash, expires_at) VALUES (i_user_id, 'upgrade-9.7.2-' || p_key, now() + interval '1 day');
    IF p_object_id IS NOT NULL THEN
        UPDATE uiuser SET directory_object_id = p_object_id WHERE uiuser_id = i_user_id;
    END IF;
END;
$$ LANGUAGE plpgsql;

CREATE FUNCTION pg_temp.expect_object_id_unique_per_directory() RETURNS void AS $$
BEGIN
    BEGIN
        INSERT INTO uiuser (uiuser_username, uuid, ldap_connection_id, directory_object_id)
        VALUES ('duplicate', 'uid=duplicate,ou=upgrade-9-7-2,dc=example,dc=com', pg_temp.probe('ldap'), 'entryUUID:upgrade-9-7-2-bound');
    EXCEPTION WHEN unique_violation THEN
        RETURN;
    END;
    RAISE EXCEPTION 'upgrade 9.7.2: a second local user with the same object id in the same directory was accepted';
END;
$$ LANGUAGE plpgsql;

CREATE FUNCTION pg_temp.ldap_servers() RETURNS text AS $$
    SELECT string_agg(ldap_connection_id || '=' || ldap_server::text, ';' ORDER BY ldap_connection_id) FROM ldap_connection
$$ LANGUAGE sql STABLE;

CREATE FUNCTION pg_temp.expect_server_list_required() RETURNS void AS $$
BEGIN
    BEGIN
        UPDATE ldap_connection SET ldap_server = '{}' WHERE ldap_connection_id = pg_temp.probe('ldap');
    EXCEPTION WHEN check_violation THEN
        RETURN;
    END;
    RAISE EXCEPTION 'upgrade 9.7.2: an LDAP connection without server was accepted';
END;
$$ LANGUAGE plpgsql;

-- the state of 9.7.1: one server per connection
ALTER TABLE ldap_connection DROP CONSTRAINT IF EXISTS ldap_connection_server_not_empty;
ALTER TABLE ldap_connection ALTER COLUMN ldap_server TYPE Varchar USING ldap_server[1];

-- an inactive connection does not take part in the overlap check of the upgrade
INSERT INTO ldap_connection (ldap_server, ldap_searchpath_for_users, ldap_search_user, ldap_search_user_pwd, active)
VALUES ('upgrade-9-7-2.example.com', 'ou=upgrade-9-7-2,dc=example,dc=com', 'cn=search', 'unused', false);
INSERT INTO probe_id SELECT 'ldap', ldap_connection_id FROM ldap_connection WHERE ldap_server = 'upgrade-9-7-2.example.com';

-- every connection keeps its single server as the only entry of its server list
CREATE TEMP TABLE expected_servers ON COMMIT DROP AS
    SELECT string_agg(ldap_connection_id || '={' || ldap_server || '}', ';' ORDER BY ldap_connection_id) AS servers FROM ldap_connection;

SELECT pg_temp.add_user('unbound', NULL);
SELECT pg_temp.add_user('bound', 'entryUUID:upgrade-9-7-2-bound');

-- @run-upgrade-9.7.2

SELECT pg_temp.expect('session of a local user without object id', pg_temp.session_active('unbound'), 'false');
SELECT pg_temp.expect('session of a bound local user', pg_temp.session_active('bound'), 'true');
SELECT pg_temp.expect_object_id_unique_per_directory();
SELECT pg_temp.expect('servers of the LDAP connections', pg_temp.ldap_servers(), (SELECT servers FROM expected_servers));
SELECT pg_temp.expect_server_list_required();

-- @run-upgrade-9.7.2

SELECT pg_temp.expect('session of a bound local user (repeated)', pg_temp.session_active('bound'), 'true');
SELECT pg_temp.expect('servers of the LDAP connections (repeated)', pg_temp.ldap_servers(), (SELECT servers FROM expected_servers));
SELECT pg_temp.expect('object id column', (SELECT count(*) FROM information_schema.columns
    WHERE table_name = 'uiuser' AND column_name = 'directory_object_id')::text, '1');

ROLLBACK;
