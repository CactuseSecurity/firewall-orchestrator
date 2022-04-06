
-- former indexes:
-- drop index if exists "firewall_akey"; -- on "device" using btree ("mgm_id","dev_id");
-- drop index if exists "kunden_akey"; -- on "tenant" using btree ("tenant_name");
-- drop index if exists "management_akey"; -- on "management" using btree ("mgm_name","tenant_id");
-- drop index if exists "stm_color_akey"; -- on "stm_color" using btree ("color_name");
-- drop index if exists "stm_fw_typ_a2key"; -- on "stm_dev_typ" using btree ("dev_typ_name");
-- drop index if exists "stm_fw_typ_akey"; -- on "stm_dev_typ" using btree ("dev_typ_name","dev_typ_version");
-- drop index if exists "stm_obj_typ_akey"; -- on "stm_obj_typ" using btree ("obj_typ_name");
-- drop index if exists "IX_relationship4"; -- on "management" ("tenant_id");
-- drop index if exists "IX_relationship6"; -- on "device" ("dev_typ_id");
-- drop index if exists "IX_Relationship93"; -- on "usr" ("usr_typ_id");
-- drop index if exists "IX_relationship11"; -- on "object" ("obj_nat_install");
-- drop index if exists "IX_relationship7"; -- on "device" ("tenant_id");
-- drop index if exists "IX_Relationship165"; -- on "usr" ("tenant_id");
-- drop index if exists "IX_Relationship188"; -- on "uiuser" ("tenant_id");
-- drop index if exists "IX_relationship10"; -- on "object" ("nattyp_id");
-- drop index if exists "IX_Relationship52"; -- on "usr" ("user_color_id");
-- drop index if exists "IX_Relationship63"; -- on "changelog_object" ("import_admin");
-- drop index if exists "IX_Relationship69"; -- on "changelog_service" ("import_admin");
-- drop index if exists "IX_Relationship70"; -- on "changelog_user" ("import_admin");
-- drop index if exists "IX_Relationship71"; -- on "changelog_rule" ("import_admin");
-- drop index if exists "IX_Relationship109"; -- on "changelog_object" ("doku_admin");
-- drop index if exists "IX_Relationship110"; -- on "changelog_service" ("doku_admin");
-- drop index if exists "IX_Relationship111"; -- on "changelog_user" ("doku_admin");
-- drop index if exists "IX_Relationship112"; -- on "changelog_rule" ("doku_admin");
-- drop index if exists "IX_Relationship159"; -- on "object" ("last_change_admin");
-- drop index if exists "IX_Relationship161"; -- on "rule" ("last_change_admin");
-- drop index if exists "IX_Relationship162"; -- on "service" ("last_change_admin"); 
-- drop index if exists "IX_Relationship163"; -- on "usr" ("last_change_admin");
-- drop index if exists IX_relationship19; -- on "svcgrp" ("svcgrp_id");
-- drop index if exists IX_relationship13; -- on "objgrp" ("objgrp_id");
-- drop index if exists IX_Relationship118; -- on "svcgrp_flat" ("svcgrp_flat_id");
-- drop index if exists IX_Relationship155; -- on "changelog_object" ("change_type_id");

-- DROP index if exists IX_Relationship185; -- on "import_changelog" ("control_id");
-- DROP index if exists IX_Relationship149; -- on "usergrp_flat" ("usergrp_flat_id");
-- DROP index if exists IX_relationship12; -- on "object" ("obj_color_id");
-- DROP index if exists IX_relationship18; -- on "service" ("svc_color_id");
-- DROP index if exists IX_Relationship83; -- on "management" ("dev_typ_id");

DROP index if exists IX_Relationship60; -- on "import_object" ("control_id");
DROP index if exists idx_import_control01;
DROP index if exists idx_import_object01;
DROP index if exists idx_import_object02;
DROP index if exists idx_object02;
DROP index if exists idx_object03;
Create index IF NOT EXISTS idx_import_control01 on import_control (control_id);
Create index IF NOT EXISTS idx_import_object01 on import_object (control_id);
Create index IF NOT EXISTS idx_import_object02 on import_object (obj_id);
Create index IF NOT EXISTS idx_object02 on object (obj_name,mgm_id,zone_id,active);
Create index IF NOT EXISTS idx_object03 on object (obj_uid,mgm_id,zone_id,active);

insert into config (config_key, config_value, config_user) VALUES ('maxImportDuration', '4', 0) ON CONFLICT DO NOTHING;
insert into config (config_key, config_value, config_user) VALUES ('maxImportInterval', '12', 0) ON CONFLICT DO NOTHING;
