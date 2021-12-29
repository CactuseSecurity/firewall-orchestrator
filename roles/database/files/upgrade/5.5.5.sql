
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

update stm_dev_typ set dev_typ_is_multi_mgmt = FALSE;
-- turning fortimanager into a single adom:
update stm_dev_typ set dev_typ_is_multi_mgmt = FALSE, dev_typ_name = 'fortiADOM'  WHERE dev_typ_id=11;

-- adding new super mangagers:
insert into stm_dev_typ (dev_typ_id,dev_typ_name,dev_typ_version,dev_typ_manufacturer,dev_typ_predef_svc,dev_typ_is_multi_mgmt) 
    VALUES (12,'FortiManager','5ff','Fortinet','',true) ON CONFLICT DO NOTHING;
insert into stm_dev_typ (dev_typ_id,dev_typ_name,dev_typ_version,dev_typ_manufacturer,dev_typ_predef_svc,dev_typ_is_multi_mgmt) 
    VALUES (13,'Check Point','MDS R8x','Check Point','',true) ON CONFLICT DO NOTHING;
