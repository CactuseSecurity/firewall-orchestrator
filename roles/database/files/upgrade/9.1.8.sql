DO
$$
BEGIN
    IF EXISTS
    (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'flow'
            AND table_name = 'nwobject'
            AND column_name = 'ip_start'
            AND udt_name = 'inet'
    ) THEN
        ALTER TABLE flow.nwobject
            ALTER COLUMN ip_start TYPE cidr
            USING ip_start::cidr;
    END IF;

    IF EXISTS
    (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'flow'
            AND table_name = 'nwobject'
            AND column_name = 'ip_end'
            AND udt_name = 'inet'
    ) THEN
        ALTER TABLE flow.nwobject
            ALTER COLUMN ip_end TYPE cidr
            USING ip_end::cidr;
    END IF;
END;
$$;
