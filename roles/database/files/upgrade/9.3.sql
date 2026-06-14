-- add OPNsense standalone (25ff) device type for the new OPNsense import module
insert into stm_dev_typ (dev_typ_id,dev_typ_name,dev_typ_version,dev_typ_manufacturer,dev_typ_predef_svc,dev_typ_is_multi_mgmt,dev_typ_is_mgmt,is_pure_routing_device)
    VALUES (30,'OPNsense standalone','25ff','Deciso','',false,true,false) ON CONFLICT DO NOTHING;
