-- re-assert the database-level search_path on every install/upgrade run so the
-- firewall schema stays resolvable for unqualified references in functions and
-- triggers at runtime (issue #4793). The setting is initially created by
-- fworch-create-tables-firewall.sql (fresh install) and upgrade/9.2.1.sql
-- (upgrade), but database-level settings are lost e.g. when restoring a dump
-- into a pre-created database, so it is repeated here idempotently.
-- current_database() avoids templating the database name.
DO $$
BEGIN
    EXECUTE format('ALTER DATABASE %I SET search_path = %s', current_database(), '"$user", public, firewall');
END $$;
