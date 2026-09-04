-- Flow network-object ranges store individual endpoints. Keep the existing
-- paired-null rule for FQDN objects, but require any populated endpoint to be
-- an IPv4 /32 or IPv6 /128 address.

-- Installations upgraded from an earlier version can already hold network masks here:
-- FlowDbCreatorObjectResolution.InsertNetworkObject writes request.reqelement.ip through
-- unchanged, and that column carries no host constraint of its own. A plain ADD CONSTRAINT
-- validates every existing row, so a single such row would abort the whole upgrade play.
-- The endpoints are therefore normalized to the first and the last host address of their
-- network first - the same treatment upgrade/7.2.2.sql gave object, owner_network and
-- tenant_network. host() and broadcast() leave an already-host value and a NULL endpoint
-- unchanged, so this is a no-op on a clean database.
-- nwobj_hash is derived from both endpoints and goes stale for every row changed here. It is
-- deliberately not rewritten: FlowSync.GetConsistentFlowDataAsync detects the mismatch and
-- repairs it through FlowHashRecalculator on the next flow sync. Should two flow network
-- objects end up on the same range here, that recalculation reports the collision instead of
-- writing it, and the two entries have to be merged manually.
DO $$
DECLARE
    normalized_rows INTEGER;
BEGIN
    UPDATE flow.nwobject
        SET ip_start = host(ip_start)::cidr,
            ip_end = host(broadcast(ip_end))::cidr
        WHERE (ip_start IS NOT NULL AND NOT (
                  (family(ip_start) = 4 AND masklen(ip_start) = 32)
                  OR (family(ip_start) = 6 AND masklen(ip_start) = 128)))
           OR (ip_end IS NOT NULL AND NOT (
                  (family(ip_end) = 4 AND masklen(ip_end) = 32)
                  OR (family(ip_end) = 6 AND masklen(ip_end) = 128)));

    GET DIAGNOSTICS normalized_rows = ROW_COUNT;

    IF normalized_rows > 0 THEN
        RAISE NOTICE 'flow.nwobject: normalized % row(s) with network endpoints to their first/last host address, their nwobj_hash is recalculated by the next flow sync', normalized_rows;
    END IF;
END $$;

ALTER TABLE flow.nwobject DROP CONSTRAINT IF EXISTS flow_nwobject_ip_start_is_host;
ALTER TABLE flow.nwobject ADD CONSTRAINT flow_nwobject_ip_start_is_host CHECK
(
    (family(ip_start) = 4 AND masklen(ip_start) = 32)
    OR (family(ip_start) = 6 AND masklen(ip_start) = 128)
);

ALTER TABLE flow.nwobject DROP CONSTRAINT IF EXISTS flow_nwobject_ip_end_is_host;
ALTER TABLE flow.nwobject ADD CONSTRAINT flow_nwobject_ip_end_is_host CHECK
(
    (family(ip_end) = 4 AND masklen(ip_end) = 32)
    OR (family(ip_end) = 6 AND masklen(ip_end) = 128)
);
