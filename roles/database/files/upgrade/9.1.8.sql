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

ALTER TABLE flow.access
    ADD COLUMN IF NOT EXISTS allows_traffic boolean NOT NULL DEFAULT TRUE;

-- ! RESET flow data ! --
UPDATE import_control
SET flow_sync_done = FALSE
WHERE flow_sync_done = TRUE;

UPDATE rule
SET flow_access_id = NULL
WHERE flow_access_id IS NOT NULL;

UPDATE object
SET flow_nwgrp_id = NULL,
    flow_nwobj_id = NULL,
    flow_active = FALSE
WHERE flow_nwgrp_id IS NOT NULL
    OR flow_nwobj_id IS NOT NULL
    OR flow_active = TRUE;

UPDATE service
SET flow_svcgrp_id = NULL,
    flow_svcobj_id = NULL,
    flow_active = FALSE
WHERE flow_svcgrp_id IS NOT NULL
    OR flow_svcobj_id IS NOT NULL
    OR flow_active = TRUE;

UPDATE time_object
SET flow_timeobj_id = NULL,
    flow_active = FALSE
WHERE flow_timeobj_id IS NOT NULL
    OR flow_active = TRUE;

DELETE FROM flow.access;
DELETE FROM flow.nwobject;
DELETE FROM flow.nwgroup;
DELETE FROM flow.svcobject;
DELETE FROM flow.svcgroup;
DELETE FROM flow.timeobject;
