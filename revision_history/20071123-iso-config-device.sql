-- $Id: 20071123-iso-config-device.sql,v 1.1.2.2 2007-12-13 10:47:31 tim Exp $
-- $Source: /home/cvs/iso/package/install/migration/Attic/20071123-iso-config-device.sql,v $
-- this adds a GUI for manipulating device infos

UPDATE stm_dev_typ SET dev_typ_version='5.x' WHERE dev_typ_version='5.0';
UPDATE stm_dev_typ SET dev_typ_version='6.x' WHERE dev_typ_version='5.1';
UPDATE stm_dev_typ SET dev_typ_version='R5x' WHERE dev_typ_version='R55';
UPDATE stm_dev_typ SET dev_typ_version='R6x' WHERE dev_typ_version='R60';
UPDATE stm_dev_typ SET dev_typ_version='3.x' WHERE dev_typ_version='3.2';
insert into stm_dev_typ	(dev_typ_id,dev_typ_name,dev_typ_version,dev_typ_manufacturer,dev_typ_predef_svc)
	VALUES (6,'phion netfence','3.x','phion','');

-- the following files need to be replaced:
-- web/*

-- check etc/gui.conf for:
--      usergroup isoadmins privileges:		admin-users admin-devices admin-clients
