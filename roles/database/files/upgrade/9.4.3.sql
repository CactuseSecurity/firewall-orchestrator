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

-- Delete the legacy dummy Flow network object if it is no longer in use. A DELETE (rather than
-- a soft-retire) is required here: flow_nwobject rows are matched by nwobj_hash on every flow
-- sync, so a row left behind in a removed state would permanently shadow that hash and could
-- never be reactivated or recreated - the UI has no path to clear a removed state, and FlowSync
-- only writes state/removed_date on insert. The NOT EXISTS guards below prove no other row still
-- references it, so the DELETE cannot cascade or leave dangling foreign keys; if this object is
-- still in use, it is left untouched and simply recreated as needed by the flow sync.
-- Caveat: this also deletes an administrator-created custom Flow object at 0.0.0.0/32 that has
-- not yet been referenced by any request, firewall object, or access entry. That is considered
-- an acceptable, unlikely edge case.
DELETE FROM flow.nwobject AS flow_object
WHERE flow_object.ip_start = '0.0.0.0/32'::cidr
    AND flow_object.ip_end = '0.0.0.0/32'::cidr
    AND NOT EXISTS (SELECT 1 FROM firewall.nw_object WHERE flow_nwobj_id = flow_object.nwobj_id)
    AND NOT EXISTS (SELECT 1 FROM request.reqelement WHERE flow_nwobj_id = flow_object.nwobj_id)
    AND NOT EXISTS (SELECT 1 FROM flow.access_source WHERE nwobj_id = flow_object.nwobj_id)
    AND NOT EXISTS (SELECT 1 FROM flow.access_destination WHERE nwobj_id = flow_object.nwobj_id)
    AND NOT EXISTS (SELECT 1 FROM flow.nwgroup_member WHERE nwobj_id = flow_object.nwobj_id);

-- Recalculate ownership matches after removing the previously imported full address ranges.
REFRESH MATERIALIZED VIEW view_rule_with_owner;
