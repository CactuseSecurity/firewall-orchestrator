-- Update legacy fqdn, dynamic and access-role network objects to have NULL ip and
-- ip_end values. The non-null address check is the migration sentinel: addressless
-- objects are valid custom Flow candidates and must survive repeated upgrades.
UPDATE nw_object
SET
    obj_ip = NULL,
    obj_ip_end = NULL,
    flow_nwobj_id = NULL,
    flow_active = FALSE
WHERE obj_typ_id IN (5, 10, 21)
    AND (obj_ip IS NOT NULL OR obj_ip_end IS NOT NULL);

-- Remove the legacy dummy Flow network object used for access roles.
-- May be recreated by the Flow sync process later if there are other objects with dummy addresses.
DELETE FROM flow.nwobject
WHERE ip_start = '0.0.0.0/32'::cidr
    AND ip_end = '0.0.0.0/32'::cidr;

-- Recalculate ownership matches after removing the previously imported full address ranges.
REFRESH MATERIALIZED VIEW view_rule_with_owner;
