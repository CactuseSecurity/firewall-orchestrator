-- update fqdn and dynamic ip network objects to have NULL ip and ip_end values
UPDATE nw_object
SET
    obj_ip = NULL,
    obj_ip_end = NULL
WHERE obj_typ_id IN (5, 10);
