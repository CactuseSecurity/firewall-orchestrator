insert into stm_dev_typ (dev_typ_id,dev_typ_name,dev_typ_version,dev_typ_manufacturer,dev_typ_predef_svc) VALUES (4,'FortiGateStandalone','5ff','Fortinet','') ON Conflict Do Nothing;

update management set dev_typ_id=4 where mgm_name='fortigate_demo';
