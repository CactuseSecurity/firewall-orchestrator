-- issue #5493: move time_object to the firewall schema, following the tables moved in 9.2.1.
--
-- ALTER TABLE ... SET SCHEMA moves indexes, constraints and the owned sequence along with the table.
-- The table keeps its name, so the generated names stay identical to those of a fresh install, and the
-- foreign keys of firewall.rule_time, request.reqelement and request.implelement stay valid because
-- PostgreSQL tracks them by OID. Guarded so the upgrade can be re-run safely.
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM pg_tables WHERE schemaname = 'public' AND tablename = 'time_object') THEN
        ALTER TABLE public.time_object SET SCHEMA firewall;
    END IF;
END $$;
