-- Flow network-object ranges store individual endpoints. Keep the existing
-- paired-null rule for FQDN objects, but require any populated endpoint to be
-- an IPv4 /32 or IPv6 /128 address, with both endpoints in the same address family.

-- IPv4 addresses sort before IPv6 ones, so ip_start <= ip_end alone still admits a range which starts in
-- one address family and ends in the other. Such a range describes nothing and cannot be repaired without
-- guessing which of the two endpoints was meant, so the upgrade names the rows and stops instead. This runs
-- before the normalization below, so an upgrade which stops here has not changed any data yet.
DO $$
DECLARE
    mixed_family_objects TEXT;
BEGIN
    SELECT string_agg(nwobj_id::text, ', ' ORDER BY nwobj_id) INTO mixed_family_objects
        FROM flow.nwobject
        WHERE ip_start IS NOT NULL
          AND ip_end IS NOT NULL
          AND family(ip_start) <> family(ip_end);

    IF mixed_family_objects IS NOT NULL THEN
        RAISE EXCEPTION 'flow.nwobject holds range endpoints of different address families in nwobj_id(s) %. Correct or remove these flow network objects, then run the upgrade again.', mixed_family_objects;
    END IF;
END $$;

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
        WHERE (ip_start IS NOT NULL AND NOT is_single_ip(ip_start))
           OR (ip_end IS NOT NULL AND NOT is_single_ip(ip_end));

    GET DIAGNOSTICS normalized_rows = ROW_COUNT;

    IF normalized_rows > 0 THEN
        RAISE NOTICE 'flow.nwobject: normalized % row(s) with network endpoints to their first/last host address, their nwobj_hash is recalculated by the next flow sync', normalized_rows;
    END IF;
END $$;

-- is_single_ip() is the /32-or-/128 predicate above, and is what the host constraints of nw_object
-- and owner_network use. fworch-create-tables-flow.sql has to spell it out because install-database.yml
-- creates the flow tables before fworch-create-constraints.sql defines the function, while an upgrade
-- always runs against a database which already has it.
ALTER TABLE flow.nwobject DROP CONSTRAINT IF EXISTS flow_nwobject_ip_start_is_host;
ALTER TABLE flow.nwobject ADD CONSTRAINT flow_nwobject_ip_start_is_host CHECK (is_single_ip(ip_start));

ALTER TABLE flow.nwobject DROP CONSTRAINT IF EXISTS flow_nwobject_ip_end_is_host;
ALTER TABLE flow.nwobject ADD CONSTRAINT flow_nwobject_ip_end_is_host CHECK (is_single_ip(ip_end));

ALTER TABLE flow.nwobject DROP CONSTRAINT IF EXISTS flow_nwobject_ip_same_family;
ALTER TABLE flow.nwobject ADD CONSTRAINT flow_nwobject_ip_same_family CHECK (family(ip_start) = family(ip_end));
