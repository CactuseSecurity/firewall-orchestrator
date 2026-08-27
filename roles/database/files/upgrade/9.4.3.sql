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

-- Retire the legacy dummy Flow network object if it is no longer in use.
UPDATE flow.nwobject AS flow_object
SET
    state = 'removed',
    removed_date = NOW(),
    show_in_request_module = FALSE
WHERE flow_object.ip_start = '0.0.0.0/32'::cidr
    AND flow_object.ip_end = '0.0.0.0/32'::cidr
    AND flow_object.state <> 'removed'
    AND NOT EXISTS (SELECT 1 FROM firewall.nw_object WHERE flow_nwobj_id = flow_object.nwobj_id)
    AND NOT EXISTS (SELECT 1 FROM request.reqelement WHERE flow_nwobj_id = flow_object.nwobj_id)
    AND NOT EXISTS (SELECT 1 FROM flow.access_source WHERE nwobj_id = flow_object.nwobj_id)
    AND NOT EXISTS (SELECT 1 FROM flow.access_destination WHERE nwobj_id = flow_object.nwobj_id)
    AND NOT EXISTS (SELECT 1 FROM flow.nwgroup_member WHERE nwobj_id = flow_object.nwobj_id);

-- Recalculate ownership matches after removing the previously imported full address ranges.
REFRESH MATERIALIZED VIEW view_rule_with_owner;
