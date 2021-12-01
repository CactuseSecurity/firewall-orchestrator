
-- ALTER table "management" ADD Column IF NOT EXISTS 
--     "multi_device_manager" Boolean NOT NULL Default false;

ALTER table "management" ADD Column IF NOT EXISTS 
	"multi_device_manager_id" integer;

ALTER table "stm_dev_typ" ADD Column IF NOT EXISTS 
	"dev_typ_is_multi_mgmt" Boolean Default FALSE;

-- if a manager belongs to another multi_device_manager, then the multi_device_manager_id points to it

ALTER TABLE "management"
    DROP CONSTRAINT IF EXISTS "management_multi_device_manager_id_fkey" CASCADE;
ALTER TABLE "management"
    ADD CONSTRAINT management_multi_device_manager_id_fkey FOREIGN KEY ("multi_device_manager_id") REFERENCES "management" ("mgm_id") ON UPDATE RESTRICT ON DELETE CASCADE;

update stm_dev_typ set dev_typ_is_multi_mgmt = TRUE WHERE dev_typ_name='FortiManager';

insert into stm_dev_typ (dev_typ_id,dev_typ_name,dev_typ_version,dev_typ_manufacturer,dev_typ_predef_svc,dev_typ_is_multi_mgmt) 
    VALUES (12,'Check Point','MDS R8x','Check Point','',TRUE) ON CONFLICT DO NOTHING;
