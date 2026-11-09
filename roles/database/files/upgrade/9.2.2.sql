INSERT INTO stm_dev_typ (dev_typ_id,dev_typ_name,dev_typ_version,dev_typ_manufacturer,dev_typ_predef_svc,dev_typ_is_multi_mgmt,dev_typ_is_mgmt,is_pure_routing_device)
VALUES
    (30, 'Generic Firewall Management', '1.0', null, '', false, true, false),
    (31, 'Generic Firewall Gateway', '1.0', null, '', false, false, false)
ON CONFLICT (dev_typ_id) DO NOTHING;
