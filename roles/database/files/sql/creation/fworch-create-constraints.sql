
/* Create Alternate Keys */
Alter Table "changelog_object" add Constraint "alt_key_changelog_object" UNIQUE ("abs_change_id");
Alter Table "changelog_rule" add Constraint "alt_key_changelog_rule" UNIQUE ("abs_change_id");
Alter Table "changelog_service" add Constraint "alt_key_changelog_service" UNIQUE ("abs_change_id");
Alter Table "changelog_user" add Constraint "alt_key_changelog_user" UNIQUE ("abs_change_id");
Alter Table "import_changelog" add Constraint "Alter_Key14" UNIQUE ("import_changelog_nr","control_id");
Alter Table "import_control" add Constraint "control_id_stop_time_unique" UNIQUE ("stop_time","control_id");
Alter Table "object" add Constraint "obj_altkey" UNIQUE ("mgm_id","zone_id","obj_uid","obj_create");
-- Alter Table "rule" add Constraint "rule_altkey" UNIQUE ("mgm_id","rule_uid","rule_create");
Alter Table "rule" add Constraint "rule_altkey" UNIQUE ("dev_id","rule_uid","rule_create",xlate_rule);
Alter Table "rule_metadata" add Constraint "rule_metadata_alt_key" UNIQUE ("rule_uid","dev_id");
Alter Table "service" add Constraint "svc_altkey" UNIQUE ("mgm_id","svc_uid","svc_create");
Alter Table "stm_dev_typ" add Constraint "Alter_Key1" UNIQUE ("dev_typ_name","dev_typ_version");
Alter Table "usr" add Constraint "usr_altkey" UNIQUE ("mgm_id","user_name","user_create");
Alter Table "zone" add Constraint "Alter_Key10" UNIQUE ("mgm_id","zone_name");

--- owner_network ---
ALTER TABLE owner_network ADD CONSTRAINT port_in_valid_range CHECK port > 0 and port <= 65535;

--- request.element ---
ALTER TABLE request.element ADD CONSTRAINT port_in_valid_range CHECK port > 0 and port <= 65535;

--- implementation.element ---
ALTER TABLE implementation.element ADD CONSTRAINT port_in_valid_range CHECK port > 0 and port <= 65535;