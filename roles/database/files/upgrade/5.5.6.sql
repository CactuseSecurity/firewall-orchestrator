
-- if a manager belongs to another multi_device_manager, then the multi_device_manager_id points to it:
ALTER table "management" ADD Column IF NOT EXISTS 
	"multi_device_manager_id" integer;

-- a dev type is either multi domain manager or not:
ALTER table "stm_dev_typ" ADD Column IF NOT EXISTS 
	"dev_typ_is_multi_mgmt" Boolean Default FALSE;

ALTER TABLE "management"
    DROP CONSTRAINT IF EXISTS "management_multi_device_manager_id_fkey" CASCADE;
ALTER TABLE "management"
    ADD CONSTRAINT management_multi_device_manager_id_fkey FOREIGN KEY ("multi_device_manager_id") REFERENCES "management" ("mgm_id") ON UPDATE RESTRICT; -- ON DELETE CASCADE;

update stm_dev_typ set dev_typ_is_multi_mgmt = FALSE WHERE NOT(dev_typ_id=12 OR dev_typ_id=13);

-- turning original fortinet into a FortiGate:
insert into stm_dev_typ (dev_typ_id,dev_typ_name,dev_typ_version,dev_typ_manufacturer,dev_typ_predef_svc) VALUES (10,'FortiGate','5ff','Fortinet','') ON CONFLICT DO NOTHING;
update stm_dev_typ set dev_typ_is_multi_mgmt = FALSE, dev_typ_name = 'FortiGate', dev_typ_version = '5ff' WHERE dev_typ_id=10;

-- turning forti management into ADOM:
update stm_dev_typ set dev_typ_is_multi_mgmt = FALSE, dev_typ_name = 'FortiADOM', dev_typ_version = '5ff' WHERE dev_typ_id=11;

-- adding new super mangagers:
insert into stm_dev_typ (dev_typ_id,dev_typ_name,dev_typ_version,dev_typ_manufacturer,dev_typ_predef_svc,dev_typ_is_multi_mgmt) 
    VALUES (12,'FortiManager','5ff','Fortinet','',true) ON CONFLICT DO NOTHING;
insert into stm_dev_typ (dev_typ_id,dev_typ_name,dev_typ_version,dev_typ_manufacturer,dev_typ_predef_svc,dev_typ_is_multi_mgmt) 
    VALUES (13,'Check Point','MDS R8x','Check Point','',true) ON CONFLICT DO NOTHING;

-- only for incomplete upgrades up to 2021-12-30
update stm_dev_typ set dev_typ_is_multi_mgmt = TRUE WHERE (dev_typ_id=12 OR dev_typ_id=13);
