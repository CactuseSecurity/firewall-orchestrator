-- SEC-11: a dn is unique only inside the directory that holds it, but uiuser.uuid (the dn of the
-- user) was unique across all LDAP connections. The same dn in two connected directories therefore
-- resolved to one local user: the login of the second directory's user took over the row of the
-- first one, overwrote its tenant and directory, and received a token for that local subject.
-- A local user is now identified by the pair (ldap_connection_id, uuid).

-- Rows that were never bound to a directory cannot be matched by the new key, so their owners would
-- get a fresh local user at the next login and lose their reports, templates and settings. Bind them
-- where the directory is unambiguous: either only one LDAP connection exists, or exactly one
-- connection searches for users in a subtree that contains the dn. Everything else is left for the
-- administrator, because guessing would hand a row to the wrong directory - the very merge this
-- upgrade removes. uiuser_id 0 is the system user holding global settings and has no directory.
DO $$
DECLARE
    v_bound_rows INT;
    v_unbound_users TEXT;
BEGIN
    UPDATE uiuser
    SET ldap_connection_id = candidate.ldap_connection_id
    FROM (
        SELECT u.uiuser_id, MIN(l.ldap_connection_id) AS ldap_connection_id
        FROM uiuser u
        JOIN ldap_connection l
            ON (SELECT COUNT(*) FROM ldap_connection) = 1
            OR RIGHT(LOWER(u.uuid), LENGTH(l.ldap_searchpath_for_users) + 1) = ',' || LOWER(l.ldap_searchpath_for_users)
        WHERE u.ldap_connection_id IS NULL AND u.uiuser_id <> 0
        GROUP BY u.uiuser_id
        HAVING COUNT(*) = 1
    ) AS candidate
    WHERE uiuser.uiuser_id = candidate.uiuser_id;
    GET DIAGNOSTICS v_bound_rows = ROW_COUNT;

    IF v_bound_rows > 0 THEN
        RAISE NOTICE 'uiuser: bound % local user(s) without directory to their LDAP connection', v_bound_rows;
    END IF;

    SELECT string_agg(uiuser_id || ' (' || uuid || ')', ', ' ORDER BY uiuser_id)
    INTO v_unbound_users
    FROM uiuser
    WHERE ldap_connection_id IS NULL AND uiuser_id <> 0;

    IF v_unbound_users IS NOT NULL THEN
        RAISE WARNING 'uiuser: the local user(s) % belong to no LDAP connection and could not be assigned unambiguously. Such a user gets a new local user at the next login. To keep the existing one, set uiuser.ldap_connection_id to the directory of the user before that login.', v_unbound_users;
    END IF;
END $$;

-- The new key is strictly weaker than the old one, so no existing rows can collide and no rows have
-- to be merged or removed. Add it before dropping the old one so that the table is never without a
-- key for the user subject.
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname = 'uiuser_ldap_connection_id_uuid_key' AND conrelid = 'uiuser'::regclass
    ) THEN
        ALTER TABLE uiuser ADD CONSTRAINT uiuser_ldap_connection_id_uuid_key UNIQUE (ldap_connection_id, uuid);
    END IF;
END $$;

ALTER TABLE uiuser DROP CONSTRAINT IF EXISTS uiuser_uuid_key;
