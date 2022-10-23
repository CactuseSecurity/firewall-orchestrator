ALTER TABLE management ADD COLUMN IF NOT EXISTS "domain_uid" varchar;

UPDATE stm_dev_typ SET dev_typ_is_mgmt = TRUE WHERE dev_typ_id=9; -- Check Point','R8x
