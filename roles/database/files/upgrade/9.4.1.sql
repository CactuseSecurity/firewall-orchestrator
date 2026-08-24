-- update fqdn, dynamic and access-role network objects to have NULL ip and ip_end values
UPDATE nw_object
SET
    obj_ip = NULL,
    obj_ip_end = NULL
WHERE obj_typ_id IN (5, 10, 21);

-- Recalculate ownership matches after removing the previously imported full address ranges.
REFRESH MATERIALIZED VIEW view_rule_with_owner;
