-- GHSA-695m-f6mv-9g72 (B): a local user is identified by its directory and dn, but a dn can be handed to another
-- person, e.g. when an account is deleted and a later one gets the same name. The new account then took over the
-- local user with its reports, templates, schedules and workflow history, and a still running session of the old
-- account was refreshed with the rights of the new one. The local user is now bound to the immutable object id of
-- its directory entry (objectGUID, entryUUID, nsUniqueId, ...), which the middleware records at the next password
-- login. Afterwards a different object id under the same dn is refused until an administrator resolves it.
ALTER TABLE uiuser ADD COLUMN IF NOT EXISTS directory_object_id Varchar;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname = 'uiuser_ldap_connection_id_directory_object_id_key' AND conrelid = 'uiuser'::regclass
    ) THEN
        ALTER TABLE uiuser ADD CONSTRAINT uiuser_ldap_connection_id_directory_object_id_key UNIQUE (ldap_connection_id, directory_object_id);
    END IF;
END $$;

-- A refresh renews a session without a password, so it cannot bind a local user that has no object id yet: the
-- dn might already name another person. Sessions of such users therefore end, and the next password login binds
-- the local user. Users that are bound already keep their sessions, so repeating the upgrade changes nothing.
UPDATE refresh_token
SET revoked_at = now()
WHERE revoked_at IS NULL
    AND user_id IN (SELECT uiuser_id FROM uiuser WHERE directory_object_id IS NULL);

-- An LDAP connection lists all servers of its directory (ldap_server becomes an array), which are tried in order.
-- Replicas of one directory used to be configured as separate connections, which the disjoint paths below no
-- longer allow; they belong into the server list of one connection now. Existing connections keep their server.
DO $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public' AND table_name = 'ldap_connection' AND column_name = 'ldap_server' AND data_type <> 'ARRAY'
    ) THEN
        ALTER TABLE ldap_connection ALTER COLUMN ldap_server TYPE Varchar[] USING ARRAY[ldap_server];
    END IF;
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname = 'ldap_connection_server_not_empty' AND conrelid = 'ldap_connection'::regclass
    ) THEN
        ALTER TABLE ldap_connection ADD CONSTRAINT ldap_connection_server_not_empty
            CHECK (cardinality(ldap_server) > 0 AND array_position(ldap_server, NULL) IS NULL AND array_position(ldap_server, '') IS NULL);
    END IF;
END $$;

-- GHSA-695m-f6mv-9g72 (A): roles, groups, owner responsibles, workflow visibility groups and workflow assignments
-- refer to subjects by their bare dn, which is unique only inside one directory. Each active LDAP connection now
-- owns the subtrees below its search and write paths; a dn below the paths of two active connections belongs to
-- neither, and its users can no longer log in. Name the overlapping connections, so that their paths can be made
-- disjoint (Settings - LDAP Connections). The comparison ignores case and blanks around separators.
DO $$
DECLARE
    v_overlaps TEXT;
BEGIN
    WITH ldap_path AS (
        SELECT ldap_connection_id, lower(regexp_replace(path, '\s*([,=])\s*', '\1', 'g')) AS path
        FROM ldap_connection,
            unnest(ARRAY[ldap_searchpath_for_users, ldap_searchpath_for_roles, ldap_searchpath_for_groups, ldap_writepath_for_groups]) AS path
        WHERE active AND COALESCE(btrim(path), '') <> ''
    )
    SELECT string_agg(DISTINCT first_path.ldap_connection_id || '/' || second_path.ldap_connection_id, ', ')
    INTO v_overlaps
    FROM ldap_path first_path
    JOIN ldap_path second_path ON first_path.ldap_connection_id < second_path.ldap_connection_id
    WHERE first_path.path = second_path.path
        OR right(first_path.path, length(second_path.path) + 1) = ',' || second_path.path
        OR right(second_path.path, length(first_path.path) + 1) = ',' || first_path.path;

    IF v_overlaps IS NOT NULL THEN
        RAISE WARNING 'ldap_connection: the search or write paths of the active LDAP connection pairs % overlap. Users and groups below an overlapping path can no longer log in or be resolved. Make the paths of the active connections disjoint (Settings - LDAP Connections). Connections to replicas of the same directory have to be combined into one connection listing all of their servers.', v_overlaps;
    END IF;
END $$;
