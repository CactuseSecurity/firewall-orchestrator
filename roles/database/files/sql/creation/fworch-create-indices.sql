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
Create index IF NOT EXISTS idx_object01 on firewall.nw_object (mgm_id);
Create index IF NOT EXISTS idx_object02 on firewall.nw_object (obj_name,mgm_id,zone_id,active);
Create index IF NOT EXISTS idx_object03 on firewall.nw_object (obj_uid,mgm_id,zone_id,active);
Create index IF NOT EXISTS idx_object04 on firewall.nw_object (obj_ip);
Create index IF NOT EXISTS idx_objgrp_flat01 on objgrp_flat (objgrp_flat_id);
Create index IF NOT EXISTS idx_objgrp_flat02 on objgrp_flat (objgrp_flat_member_id);
Create index IF NOT EXISTS idx_rule01 on rule (rule_uid,mgm_id,dev_id,active,nat_rule,xlate_rule);
Create index IF NOT EXISTS idx_rule02 on rule (mgm_id,rule_id,rule_uid,dev_id);
Create index IF NOT EXISTS idx_rule03 on rule (dev_id);
Create index IF NOT EXISTS idx_rule04 on rule (action_id);
Create index IF NOT EXISTS idx_rule_from01 on rule_from (rule_id);
Create index IF NOT EXISTS idx_rule_service01 on rule_service (rule_id);
Create index IF NOT EXISTS idx_rule_service02 on rule_service (svc_id);
Create index IF NOT EXISTS idx_rule_to01 on rule_to (rule_id);
Create index IF NOT EXISTS idx_service01 on firewall.nw_service (mgm_id);
Create index IF NOT EXISTS idx_service02 on firewall.nw_service (svc_color_id);
Create index IF NOT EXISTS idx_svcgrp_flat01 on svcgrp_flat (svcgrp_flat_id);
Create index IF NOT EXISTS idx_svcgrp_flat02 on svcgrp_flat (svcgrp_flat_member_id);
Create index IF NOT EXISTS idx_usr01 on firewall.nw_user (mgm_id);
Create index IF NOT EXISTS idx_usergrp_flat01 on usergrp_flat (usergrp_flat_id);
Create index IF NOT EXISTS idx_usergrp_flat02 on usergrp_flat (usergrp_flat_member_id);
Create index IF NOT EXISTS idx_zone01 on zone (zone_name,mgm_id);
Create index IF NOT EXISTS idx_zone02 on zone (mgm_id); -- needed as mgm_id is not first column on above composite index

-- make sure a maximum of one stop_time=null entry exists per mgm_id (only one running import per mgm):
CREATE UNIQUE INDEX uidx_import_control_only_one_null_stop_time_per_mgm_when_null ON import_control (mgm_id) WHERE stop_time IS NULL;

CREATE UNIQUE index if not exists only_one_default_owner on owner(is_default) where is_default = true;
CREATE UNIQUE index if not exists owner_responsible_owner_dn_type_unique on owner_responsible(owner_id, dn, responsible_type);
CREATE index if not exists owner_responsible_dn_idx on owner_responsible(dn);
CREATE UNIQUE index if not exists owner_responsible_type_name_unique on owner_responsible_type(name);

Create index "IX_Relationship68" on "changelog_object" ("control_id");
Create index "IX_Relationship76" on "changelog_service" ("control_id");
Create index "IX_Relationship77" on "changelog_user" ("control_id");
Create index "IX_Relationship78" on "changelog_rule" ("control_id");

-- tbd
Create unique index "kundennetze_akey" on "tenant_network" using btree ("tenant_net_id","tenant_id");
Create unique index "rule_from_unique_index" on "rule_from" using btree ("rule_id","obj_id","user_id");
Create index "import_control_start_time_idx" on "import_control" using btree ("start_time");


Create index "IX_relationship23" on "rule" ("action_id");
Create index "IX_relationship9" on firewall."nw_object" ("obj_typ_id");
Create index "IX_relationship24" on "rule" ("track_id");
Create index "IX_Relationship33" on firewall."nw_service" ("ip_proto_id");
Create index "IX_Relationship36" on firewall."nw_service" ("svc_typ_id");
Create index "IX_Relationship37" on firewall."nw_object" ("zone_id");
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


Create index "IX_Relationship120" on firewall."nw_object_group" ("import_created");
Create index "IX_Relationship122" on firewall."nw_service_group" ("import_created");
Create index "IX_Relationship50" on firewall."nw_user_group" ("usergrp_id");
Create index "IX_Relationship51" on firewall."nw_user_group" ("usergrp_member_id");
Create index "IX_Relationship153" on firewall."nw_user_group" ("import_created");


Create index "IX_relationship20" on firewall."nw_service_group" ("svcgrp_member_id");
Create index "IX_relationship14" on firewall."nw_object_group" ("objgrp_member_id");

Create index "IX_Relationship107" on "objgrp_flat" ("import_created");
Create index "IX_Relationship124" on "svcgrp_flat" ("import_created");
Create index "IX_Relationship151" on "usergrp_flat" ("import_created");

Create index "IX_Relationship166" on "rule" ("rule_create");
CREATE INDEX IF NOT EXISTS idx_rule_removed ON "rule" ("removed");
Create index "IX_Relationship168" on "rule_from" ("rf_create");
Create index "IX_Relationship170" on "rule_to" ("rt_create");
Create index "IX_Relationship172" on "rule_service" ("rs_create");
Create index "IX_Relationship174" on firewall."nw_object" ("obj_create");
Create index "IX_Relationship176" on firewall."nw_service" ("svc_create");
Create index "IX_Relationship178" on "zone" ("zone_create");

create unique index if not exists only_one_default_owner on owner(is_default) 
where is_default = true;

-- compliance
Create index IF NOT EXISTS idx_fkey_network_zone_id on compliance.ip_range USING HASH (network_zone_id);
Create index IF NOT EXISTS idx_fkey_network_zone_from on compliance.network_zone_communication USING HASH (from_network_zone_id);
Create index IF NOT EXISTS idx_fkey_network_zone_to on compliance.network_zone_communication USING HASH (to_network_zone_id);

-- rule_owner
CREATE UNIQUE INDEX IF NOT EXISTS idx_rule_owner_removed_is_null_unique ON rule_owner (rule_id, owner_id) WHERE removed IS NULL;

-- flow
Create index idx_flow_access_hash on flow.access (access_hash);
Create index idx_flow_access_active on flow.access (access_hash) WHERE state IN ('requested', 'implemented');
Create index idx_flow_access_source_nwobj on flow.access_source (nwobj_id);
Create index idx_flow_access_destination_nwobj on flow.access_destination (nwobj_id);
Create index idx_flow_access_service_svcobj on flow.access_service (svcobj_id);
Create index idx_flow_access_timeobject on flow.access_timeobject (timeobj_id);

Create index idx_flow_access_source_grp_nwgrp on flow.access_source_grp (nwgrp_id);
Create index idx_flow_access_destination_grp_nwgrp on flow.access_destination_grp (nwgrp_id);
Create index idx_flow_access_service_grp_svcgrp on flow.access_service_grp (svcgrp_id);

Create index idx_flow_nwgroup_member_nwobj on flow.nwgroup_member (nwobj_id);

Create index idx_flow_svcgroup_member_svcobj on flow.svcgroup_member (svcobj_id);

Create unique index if not exists service_flow_svcobj_id_active_only_one_per_mgm on firewall.nw_service (mgm_id, flow_svcobj_id) where flow_active = true;
Create unique index if not exists service_flow_svcgrp_id_active_only_one_per_mgm on firewall.nw_service (mgm_id, flow_svcgrp_id) where flow_active = true;
Create unique index if not exists time_object_flow_timeobj_id_active_only_one_per_mgm on time_object (mgm_id, flow_timeobj_id) where flow_active = true;
Create unique index if not exists object_flow_nwobj_id_active_only_one_per_mgm on firewall.nw_object (mgm_id, flow_nwobj_id) where flow_active = true;
Create unique index if not exists object_flow_nwgrp_id_active_only_one_per_mgm on firewall.nw_object (mgm_id, flow_nwgrp_id) where flow_active = true;
