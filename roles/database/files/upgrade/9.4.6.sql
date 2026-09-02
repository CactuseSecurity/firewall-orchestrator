-- Flow network-object ranges store individual endpoints. Keep the existing
-- paired-null rule for FQDN objects, but require any populated endpoint to be
-- an IPv4 /32 or IPv6 /128 address.
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conrelid = 'flow.nwobject'::regclass
          AND conname = 'flow_nwobject_ip_start_is_host'
    ) THEN
        ALTER TABLE flow.nwobject
            ADD CONSTRAINT flow_nwobject_ip_start_is_host CHECK
            (
                (family(ip_start) = 4 AND masklen(ip_start) = 32)
                OR (family(ip_start) = 6 AND masklen(ip_start) = 128)
            );
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conrelid = 'flow.nwobject'::regclass
          AND conname = 'flow_nwobject_ip_end_is_host'
    ) THEN
        ALTER TABLE flow.nwobject
            ADD CONSTRAINT flow_nwobject_ip_end_is_host CHECK
            (
                (family(ip_end) = 4 AND masklen(ip_end) = 32)
                OR (family(ip_end) = 6 AND masklen(ip_end) = 128)
            );
    END IF;
END $$;
