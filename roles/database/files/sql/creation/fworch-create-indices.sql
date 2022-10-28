Create index IF NOT EXISTS idx_changelog_object01 on changelog_object (change_type_id);
Create index IF NOT EXISTS idx_changelog_object02 on changelog_object (mgm_id);
Create index IF NOT EXISTS idx_changelog_rule01 on changelog_rule (change_type_id);
Create index IF NOT EXISTS idx_changelog_rule02 on changelog_rule (mgm_id);
Create index IF NOT EXISTS idx_changelog_rule03 on changelog_rule (dev_id);
Create index IF NOT EXISTS idx_changelog_service01 on changelog_service (change_type_id);
Create index IF NOT EXISTS idx_changelog_service02 on changelog_service (mgm_id);
Create index IF NOT EXISTS idx_changelog_user01 on changelog_user (change_type_id);
Create index IF NOT EXISTS idx_changelog_user02 on changelog_user (mgm_id);
Create index IF NOT EXISTS idx_import_control01 on import_control (control_id);
Create index IF NOT EXISTS idx_import_object01 on import_object (control_id);
Create index IF NOT EXISTS idx_import_object02 on import_object (obj_id);
Create index IF NOT EXISTS idx_import_rule01 on import_rule (rule_id);
Create index IF NOT EXISTS idx_object01 on object (mgm_id);
Create index IF NOT EXISTS idx_object02 on object (obj_name,mgm_id,zone_id,active);
Create index IF NOT EXISTS idx_object03 on object (obj_uid,mgm_id,zone_id,active);
Create index IF NOT EXISTS idx_objgrp_flat01 on objgrp_flat (objgrp_flat_id);
Create index IF NOT EXISTS idx_objgrp_flat02 on objgrp_flat (objgrp_flat_member_id);
Create index IF NOT EXISTS idx_rule01 on rule (rule_uid,mgm_id,dev_id,active,nat_rule,xlate_rule);
Create index IF NOT EXISTS idx_rule02 on rule (mgm_id,rule_id,rule_uid,dev_id);
Create index IF NOT EXISTS idx_rule03 on rule (dev_id);
Create index IF NOT EXISTS idx_rule_from01 on rule_from (rule_id);
Create index IF NOT EXISTS idx_rule_service01 on rule_service (rule_id);
Create index IF NOT EXISTS idx_rule_service02 on rule_service (svc_id);
Create index IF NOT EXISTS idx_rule_to01 on rule_to (rule_id);
Create index IF NOT EXISTS idx_service01 on service (mgm_id);
Create index IF NOT EXISTS idx_service02 on service (svc_color_id);
Create index IF NOT EXISTS idx_svcgrp_flat01 on svcgrp_flat (svcgrp_flat_id);
Create index IF NOT EXISTS idx_svcgrp_flat02 on svcgrp_flat (svcgrp_flat_member_id);
Create index IF NOT EXISTS idx_usr01 on usr (mgm_id);
Create index IF NOT EXISTS idx_usergrp_flat01 on usergrp_flat (usergrp_flat_id);
Create index IF NOT EXISTS idx_usergrp_flat02 on usergrp_flat (usergrp_flat_member_id);
Create index IF NOT EXISTS idx_zone01 on zone (zone_name,mgm_id);
Create index IF NOT EXISTS idx_zone02 on zone (mgm_id); -- needed as mgm_id is not first column on above composite index


-- make sure a maximum of one stop_time=null entry exists per mgm_id (only one running import per mgm):
CREATE UNIQUE INDEX uidx_import_control_only_one_null_stop_time_per_mgm_when_null ON import_control (mgm_id) WHERE stop_time IS NULL;

-- probably useful:
Create index "IX_Relationship59" on "import_service" ("control_id");
Create index "IX_Relationship61" on "import_rule" ("control_id");
Create index "IX_Relationship62" on "import_user" ("control_id");
Create index "IX_Relationship132" on "import_zone" ("control_id");

Create index "IX_Relationship68" on "changelog_object" ("control_id");
Create index "IX_Relationship76" on "changelog_service" ("control_id");
Create index "IX_Relationship77" on "changelog_user" ("control_id");
Create index "IX_Relationship78" on "changelog_rule" ("control_id");

-- tbd
Create unique index "kundennetze_akey" on "tenant_network" using btree ("tenant_net_id","tenant_id");
Create unique index "rule_from_unique_index" on "rule_from" using btree ("rule_id","obj_id","user_id");
Create index "import_control_start_time_idx" on "import_control" using btree ("start_time");


Create index "IX_relationship23" on "rule" ("action_id");
Create index "IX_relationship9" on "object" ("obj_typ_id");
Create index "IX_relationship24" on "rule" ("track_id");
Create index "IX_Relationship33" on "service" ("ip_proto_id");
Create index "IX_Relationship36" on "service" ("svc_typ_id");
Create index "IX_Relationship37" on "object" ("zone_id");
Create index "IX_Relationship90" on "rule" ("rule_from_zone");
Create index "IX_Relationship95" on "rule_from" ("user_id");
Create index "IX_relationship26" on "rule_from" ("obj_id");
Create index "IX_relationship28" on "rule_to" ("obj_id");
Create index "IX_Relationship91" on "rule" ("rule_to_zone");

Create index "IX_Relationship65" on "changelog_object" ("old_obj_id");
Create index "IX_Relationship66" on "changelog_object" ("new_obj_id");
Create index "IX_Relationship72" on "changelog_rule" ("new_rule_id");
Create index "IX_Relationship73" on "changelog_rule" ("old_rule_id");
Create index "IX_Relationship74" on "changelog_service" ("new_svc_id");
Create index "IX_Relationship75" on "changelog_service" ("old_svc_id");
Create index "IX_Relationship79" on "changelog_user" ("new_user_id");
Create index "IX_Relationship80" on "changelog_user" ("old_user_id");


Create index "IX_Relationship120" on "objgrp" ("import_created");
Create index "IX_Relationship121" on "objgrp" ("import_last_seen");
Create index "IX_Relationship122" on "svcgrp" ("import_created");
Create index "IX_Relationship123" on "svcgrp" ("import_last_seen");
Create index "IX_Relationship50" on "usergrp" ("usergrp_id");
Create index "IX_Relationship51" on "usergrp" ("usergrp_member_id");
Create index "IX_Relationship153" on "usergrp" ("import_created");
Create index "IX_Relationship154" on "usergrp" ("import_last_seen");


Create index "IX_relationship20" on "svcgrp" ("svcgrp_member_id");
Create index "IX_relationship14" on "objgrp" ("objgrp_member_id");

Create index "IX_Relationship107" on "objgrp_flat" ("import_created");
Create index "IX_Relationship108" on "objgrp_flat" ("import_last_seen");
Create index "IX_Relationship124" on "svcgrp_flat" ("import_created");
Create index "IX_Relationship125" on "svcgrp_flat" ("import_last_seen");
Create index "IX_Relationship151" on "usergrp_flat" ("import_created");
Create index "IX_Relationship152" on "usergrp_flat" ("import_last_seen");

Create index "IX_Relationship166" on "rule" ("rule_create");
Create index "IX_Relationship167" on "rule" ("rule_last_seen");
Create index "IX_Relationship168" on "rule_from" ("rf_create");
Create index "IX_Relationship169" on "rule_from" ("rf_last_seen");
Create index "IX_Relationship170" on "rule_to" ("rt_create");
Create index "IX_Relationship171" on "rule_to" ("rt_last_seen");
Create index "IX_Relationship172" on "rule_service" ("rs_create");
Create index "IX_Relationship173" on "rule_service" ("rs_last_seen");
Create index "IX_Relationship174" on "object" ("obj_create");
Create index "IX_Relationship175" on "object" ("obj_last_seen");
Create index "IX_Relationship176" on "service" ("svc_create");
Create index "IX_Relationship177" on "service" ("svc_last_seen");
Create index "IX_Relationship178" on "zone" ("zone_create");
Create index "IX_Relationship179" on "zone" ("zone_last_seen");

create unique index if not exists only_one_default_owner on owner(is_default) 
where is_default = true;
