Alter table "alert" add CONSTRAINT alert_ref_log_id_log_data_issue_data_issue_id_fkey foreign key ("ref_log_id") references "log_data_issue" ("data_issue_id") on update restrict on delete cascade;
Alter table "alert" add CONSTRAINT alert_user_id_uiuser_uiuser_id_fkey foreign key ("user_id") references "uiuser" ("uiuser_id") on update restrict on delete cascade;
Alter table "alert" add CONSTRAINT alert_ack_by_uiuser_uiuser_id_fkey foreign key ("ack_by") references "uiuser" ("uiuser_id") on update restrict on delete cascade;
Alter table "alert" add CONSTRAINT alert_alert_mgm_id_management_mgm_id_fkey foreign key ("alert_mgm_id") references "management" ("mgm_id") on update restrict on delete cascade;
Alter table "alert" add CONSTRAINT alert_alert_dev_id_device_dev_id_fkey foreign key ("alert_dev_id") references "device" ("dev_id") on update restrict on delete cascade;
ALTER TABLE "log_data_issue" ADD CONSTRAINT log_data_issue_import_control_control_id_fkey FOREIGN KEY ("import_id") REFERENCES "import_control" ("control_id") ON UPDATE RESTRICT ON DELETE CASCADE;
Alter table "log_data_issue" add CONSTRAINT log_data_issue_uiuser_uiuser_id_fkey foreign key ("user_id") references "uiuser" ("uiuser_id") on update restrict on delete cascade;
Alter table "changelog_object" add  foreign key ("change_type_id") references "stm_change_type" ("change_type_id") on update restrict on delete cascade;
Alter table "changelog_object" add  foreign key ("control_id") references "import_control" ("control_id") on update restrict on delete cascade;
Alter table "changelog_object" add  foreign key ("doku_admin") references "uiuser" ("uiuser_id") on update restrict on delete cascade;
Alter table "changelog_object" add  foreign key ("import_admin") references "uiuser" ("uiuser_id") on update restrict on delete cascade;
Alter table "changelog_object" add  foreign key ("mgm_id") references "management" ("mgm_id") on update restrict on delete cascade;
Alter table "changelog_object" add  foreign key ("new_obj_id") references "object" ("obj_id") on update restrict on delete cascade;
Alter table "changelog_object" add  foreign key ("old_obj_id") references "object" ("obj_id") on update restrict on delete cascade;
Alter table "changelog_rule" add  foreign key ("change_type_id") references "stm_change_type" ("change_type_id") on update restrict on delete cascade;
Alter table "changelog_rule" add  foreign key ("control_id") references "import_control" ("control_id") on update restrict on delete cascade;
Alter table "changelog_rule" add  foreign key ("dev_id") references "device" ("dev_id") on update restrict on delete cascade;
Alter table "changelog_rule" add  foreign key ("doku_admin") references "uiuser" ("uiuser_id") on update restrict on delete cascade;
Alter table "changelog_rule" add  foreign key ("import_admin") references "uiuser" ("uiuser_id") on update restrict on delete cascade;
Alter table "changelog_rule" add  foreign key ("mgm_id") references "management" ("mgm_id") on update restrict on delete cascade;
Alter table "changelog_rule" add  foreign key ("new_rule_id") references "rule" ("rule_id") on update restrict on delete cascade;
Alter table "changelog_rule" add  foreign key ("old_rule_id") references "rule" ("rule_id") on update restrict on delete cascade;
Alter table "changelog_service" add  foreign key ("change_type_id") references "stm_change_type" ("change_type_id") on update restrict on delete cascade;
Alter table "changelog_service" add  foreign key ("control_id") references "import_control" ("control_id") on update restrict on delete cascade;
Alter table "changelog_service" add  foreign key ("doku_admin") references "uiuser" ("uiuser_id") on update restrict on delete cascade;
Alter table "changelog_service" add  foreign key ("import_admin") references "uiuser" ("uiuser_id") on update restrict on delete cascade;
Alter table "changelog_service" add  foreign key ("mgm_id") references "management" ("mgm_id") on update restrict on delete cascade;
Alter table "changelog_service" add  foreign key ("new_svc_id") references "service" ("svc_id") on update restrict on delete cascade;
Alter table "changelog_service" add  foreign key ("old_svc_id") references "service" ("svc_id") on update restrict on delete cascade;
Alter table "changelog_user" add  foreign key ("change_type_id") references "stm_change_type" ("change_type_id") on update restrict on delete cascade;
Alter table "changelog_user" add  foreign key ("control_id") references "import_control" ("control_id") on update restrict on delete cascade;
Alter table "changelog_user" add  foreign key ("doku_admin") references "uiuser" ("uiuser_id") on update restrict on delete cascade;
Alter table "changelog_user" add  foreign key ("import_admin") references "uiuser" ("uiuser_id") on update restrict on delete cascade;
Alter table "changelog_user" add  foreign key ("mgm_id") references "management" ("mgm_id") on update restrict on delete cascade;
Alter table "changelog_user" add  foreign key ("new_user_id") references "usr" ("user_id") on update restrict on delete cascade;
Alter table "changelog_user" add  foreign key ("old_user_id") references "usr" ("user_id") on update restrict on delete cascade;
Alter table "config" add  foreign key ("config_user") references "uiuser" ("uiuser_id") on update restrict on delete cascade;
Alter table "device" add  foreign key ("dev_typ_id") references "stm_dev_typ" ("dev_typ_id") on update restrict on delete cascade;
Alter table "device" add  foreign key ("mgm_id") references "management" ("mgm_id") on update restrict on delete cascade;
ALTER TABLE gw_route ADD CONSTRAINT gw_route_routing_device_foreign_key FOREIGN KEY (routing_device) REFERENCES device(dev_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE gw_route ADD CONSTRAINT gw_route_interface_foreign_key FOREIGN KEY (interface_id) REFERENCES gw_interface(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE gw_interface ADD CONSTRAINT gw_interface_routing_device_foreign_key FOREIGN KEY (routing_device) REFERENCES device(dev_id) ON UPDATE RESTRICT ON DELETE CASCADE;
Alter table "import_changelog" add  foreign key ("control_id") references "import_control" ("control_id") on update restrict on delete cascade;
Alter table "import_config" add constraint "import_config_import_id_f_key"  foreign key ("import_id") references "import_control" ("control_id") on update restrict on delete cascade;
Alter table "import_config" add constraint "import_config_mgm_id_f_key"  foreign key ("mgm_id") references "management" ("mgm_id") on update restrict on delete cascade;
Alter table "import_control" add  foreign key ("mgm_id") references "management" ("mgm_id") on update restrict on delete cascade;
Alter table "import_full_config" add constraint "import_full_config_import_id_f_key"  foreign key ("import_id") references "import_control" ("control_id") on update restrict on delete cascade;
Alter table "import_full_config" add constraint "import_full_config_mgm_id_f_key"  foreign key ("mgm_id") references "management" ("mgm_id") on update restrict on delete cascade;
Alter table "import_object" add  foreign key ("control_id") references "import_control" ("control_id") on update restrict on delete cascade;
Alter table "import_rule" add  foreign key ("control_id") references "import_control" ("control_id") on update restrict on delete cascade;
Alter table "import_service" add  foreign key ("control_id") references "import_control" ("control_id") on update restrict on delete cascade;
Alter table "import_user" add  foreign key ("control_id") references "import_control" ("control_id") on update restrict on delete cascade;
Alter table "import_zone" add  foreign key ("control_id") references "import_control" ("control_id") on update restrict on delete cascade;
Alter table "ldap_connection" add foreign key ("tenant_id") references "tenant" ("tenant_id") on update restrict on delete cascade;
Alter table "management" add  foreign key ("dev_typ_id") references "stm_dev_typ" ("dev_typ_id") on update restrict on delete cascade;
ALTER TABLE "management" ADD CONSTRAINT management_multi_device_manager_id_fkey FOREIGN KEY ("multi_device_manager_id") REFERENCES "management" ("mgm_id") ON UPDATE RESTRICT; --ON DELETE CASCADE;
ALTER TABLE "management" ADD CONSTRAINT management_import_credential_id_foreign_key FOREIGN KEY (import_credential_id) REFERENCES import_credential(id) ON UPDATE RESTRICT ON DELETE CASCADE;
Alter table "object" add  foreign key ("last_change_admin") references "uiuser" ("uiuser_id") on update restrict on delete cascade;
Alter table "object" add  foreign key ("mgm_id") references "management" ("mgm_id") on update restrict on delete cascade;
Alter table "object" add  foreign key ("obj_color_id") references "stm_color" ("color_id") on update restrict on delete cascade;
Alter table "object" add  foreign key ("obj_create") references "import_control" ("control_id") on update restrict on delete cascade;
Alter table "object" add  foreign key ("obj_last_seen") references "import_control" ("control_id") on update restrict on delete cascade;
Alter table "object" add  foreign key ("obj_nat_install") references "device" ("dev_id") on update restrict on delete cascade;
Alter table "object" add  foreign key ("obj_typ_id") references "stm_obj_typ" ("obj_typ_id") on update restrict on delete cascade;
Alter table "object" add  foreign key ("zone_id") references "zone" ("zone_id") on update restrict on delete cascade;
Alter table "objgrp" add  foreign key ("import_created") references "import_control" ("control_id") on update restrict on delete cascade;
Alter table "objgrp" add  foreign key ("import_last_seen") references "import_control" ("control_id") on update restrict on delete cascade;
Alter table "objgrp" add  foreign key ("objgrp_id") references "object" ("obj_id") on update restrict on delete cascade;
Alter table "objgrp" add  foreign key ("objgrp_member_id") references "object" ("obj_id") on update restrict on delete cascade;
Alter table "objgrp_flat" add  foreign key ("import_created") references "import_control" ("control_id") on update restrict on delete cascade;
Alter table "objgrp_flat" add  foreign key ("import_last_seen") references "import_control" ("control_id") on update restrict on delete cascade;
Alter table "objgrp_flat" add  foreign key ("objgrp_flat_id") references "object" ("obj_id") on update restrict on delete cascade;
Alter table "objgrp_flat" add  foreign key ("objgrp_flat_member_id") references "object" ("obj_id") on update restrict on delete cascade;
Alter table "report" add foreign key ("report_template_id") references "report_template" ("report_template_id") on update restrict on delete cascade;
Alter table "report" add foreign key ("report_owner_id") references "uiuser" ("uiuser_id") on update restrict on delete cascade;
Alter table "report" add foreign key ("tenant_wide_visible") references "tenant" ("tenant_id") on update restrict on delete cascade;
Alter table "report_schedule" add foreign key ("report_template_id") references "report_template" ("report_template_id") on update restrict on delete cascade;
Alter table "report_schedule" add foreign key ("report_schedule_owner") references "uiuser" ("uiuser_id") on update restrict on delete cascade;
Alter table "report_schedule_format" add foreign key ("report_schedule_id") references "report_schedule" ("report_schedule_id") on update restrict on delete cascade;
Alter table "report_schedule_format" add foreign key ("report_schedule_format_name") references "report_format" ("report_format_name") on update restrict on delete cascade;
Alter table "report_template" add foreign key ("report_template_owner") references "uiuser" ("uiuser_id") on update restrict on delete cascade;
Alter table "report_template_viewable_by_user" add foreign key ("report_template_id") references "report_template" ("report_template_id") on update restrict on delete cascade;
Alter table "report_template_viewable_by_user" add foreign key ("uiuser_id") references "uiuser" ("uiuser_id") on update restrict on delete cascade;
Alter table "rule" add  foreign key ("action_id") references "stm_action" ("action_id") on update restrict on delete cascade;
Alter table "rule" add  foreign key ("dev_id") references "device" ("dev_id") on update restrict on delete cascade;
Alter table "rule" add  foreign key ("last_change_admin") references "uiuser" ("uiuser_id") on update restrict on delete cascade;
Alter table "rule" add  foreign key ("mgm_id") references "management" ("mgm_id") on update restrict on delete cascade;
Alter table "rule" add  foreign key ("rule_create") references "import_control" ("control_id") on update restrict on delete cascade;
Alter table "rule" add  foreign key ("rule_from_zone") references "zone" ("zone_id") on update restrict on delete cascade;
Alter table "rule" add  foreign key ("rule_last_seen") references "import_control" ("control_id") on update restrict on delete cascade;
Alter table "rule" add  foreign key ("rule_to_zone") references "zone" ("zone_id") on update restrict on delete cascade;
Alter table "rule" add  foreign key ("track_id") references "stm_track" ("track_id") on update restrict on delete cascade;
ALTER TABLE "rule"
    ADD CONSTRAINT rule_rule_nat_rule_id_fkey FOREIGN KEY ("xlate_rule") REFERENCES "rule" ("rule_id") ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE "rule"
    ADD CONSTRAINT rule_rule_parent_rule_id_fkey FOREIGN KEY ("parent_rule_id") REFERENCES "rule" ("rule_id") ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE "rule"
    ADD CONSTRAINT rule_parent_rule_type_id_fkey FOREIGN KEY ("parent_rule_type") REFERENCES "parent_rule_type" ("id") ON UPDATE RESTRICT ON DELETE CASCADE;
Alter table "rule" add constraint "rule_metadata_dev_id_rule_uid_f_key"
  foreign key ("dev_id", "rule_uid") references "rule_metadata" ("dev_id", "rule_uid") on update restrict on delete cascade;

Alter table "rule_from" add  foreign key ("obj_id") references "object" ("obj_id") on update restrict on delete cascade;
Alter table "rule_from" add  foreign key ("rf_create") references "import_control" ("control_id") on update restrict on delete cascade;
Alter table "rule_from" add  foreign key ("rf_last_seen") references "import_control" ("control_id") on update restrict on delete cascade;
Alter table "rule_from" add  foreign key ("rule_id") references "rule" ("rule_id") on update restrict on delete cascade;
Alter table "rule_from" add  foreign key ("user_id") references "usr" ("user_id") on update restrict on delete cascade;

Alter table "rule_metadata" add constraint "rule_metadata_device_dev_id_f_key"
    foreign key ("dev_id") references "device" ("dev_id") on update restrict on delete cascade;
Alter table "rule_metadata" add constraint "rule_metadata_rule_last_certifier_uiuser_uiuser_id_f_key"
  foreign key ("rule_last_certifier") references "uiuser" ("uiuser_id") on update restrict on delete cascade;
Alter table "rule_metadata" add constraint "rule_metadata_rule_owner_uiuser_uiuser_id_f_key"
  foreign key ("rule_owner") references "uiuser" ("uiuser_id") on update restrict on delete cascade;

Alter table "rule_enforced_on_gateway" add CONSTRAINT fk_rule_enforced_on_gateway_rule_rule_id foreign key ("rule_id") references "rule" ("rule_id") on update restrict on delete cascade;
Alter table "rule_enforced_on_gateway" add CONSTRAINT fk_rule_enforced_on_gateway_device_dev_id foreign key ("dev_id") references "device" ("dev_id") on update restrict on delete cascade;
Alter table "rule_enforced_on_gateway" add CONSTRAINT fk_rule_enforced_on_gateway_created_import_control_control_id 	foreign key ("created") references "import_control" ("control_id") on update restrict on delete cascade;
Alter table "rule_enforced_on_gateway" add CONSTRAINT fk_rule_enforced_on_gateway_deleted_import_control_control_id foreign key ("deleted") references "import_control" ("control_id") on update restrict on delete cascade;
Alter table "rulebase" add CONSTRAINT fk_rulebase_mgm_id foreign key ("mgm_id") references "management" ("mgm_id") on update restrict on delete cascade;
Alter table "rulebase_on_gateway" add CONSTRAINT fk_rulebase_on_gateway_dev_id foreign key ("dev_id") references "device" ("dev_id") on update restrict on delete cascade;
Alter TABLE "rulebase_on_gateway" add CONSTRAINT fk_rulebase_on_gateway_rulebase_id foreign key ("rulebase_id") references "rulebase" ("id") on update restrict on delete cascade;

Alter table "rule_nwobj_resolved" add foreign key ("obj_id") references "object" ("obj_id") on update restrict on delete cascade;
Alter table "rule_nwobj_resolved" add foreign key ("rule_id") references "rule" ("rule_id") on update restrict on delete cascade;
Alter table "rule_nwobj_resolved" add foreign key ("mgm_id") references "management" ("mgm_id") on update restrict on delete cascade;
Alter table "rule_nwobj_resolved" add CONSTRAINT fk_rule_nwobj_resolved_created foreign key ("created") references "import_control" ("control_id") on update restrict on delete cascade;
Alter table "rule_nwobj_resolved" add CONSTRAINT fk_rule_nwobj_resolved_removed foreign key ("removed") references "import_control" ("control_id") on update restrict on delete cascade;

Alter table "rule_service" add  foreign key ("rs_create") references "import_control" ("control_id") on update restrict on delete cascade;
Alter table "rule_service" add  foreign key ("rs_last_seen") references "import_control" ("control_id") on update restrict on delete cascade;
Alter table "rule_service" add  foreign key ("rule_id") references "rule" ("rule_id") on update restrict on delete cascade;
Alter table "rule_service" add  foreign key ("svc_id") references "service" ("svc_id") on update restrict on delete cascade;

Alter table "rule_svc_resolved" add foreign key ("svc_id") references "service" ("svc_id") on update restrict on delete cascade;
Alter table "rule_svc_resolved" add foreign key ("rule_id") references "rule" ("rule_id") on update restrict on delete cascade;
Alter table "rule_svc_resolved" add foreign key ("mgm_id") references "management" ("mgm_id") on update restrict on delete cascade;
Alter table "rule_svc_resolved" add CONSTRAINT fk_rule_svcobj_resolved_created foreign key ("created") references "import_control" ("control_id") on update restrict on delete cascade;
Alter table "rule_svc_resolved" add CONSTRAINT fk_rule_svcobj_resolved_removed foreign key ("removed") references "import_control" ("control_id") on update restrict on delete cascade;

Alter table "rule_to" add  foreign key ("obj_id") references "object" ("obj_id") on update restrict on delete cascade;
Alter table "rule_to" add  foreign key ("rt_create") references "import_control" ("control_id") on update restrict on delete cascade;
Alter table "rule_to" add  foreign key ("rt_last_seen") references "import_control" ("control_id") on update restrict on delete cascade;
Alter table "rule_to" add  foreign key ("rule_id") references "rule" ("rule_id") on update restrict on delete cascade;
Alter table "rule_to" add constraint rule_to_user_id_usr_user_id FOREIGN KEY ("user_id") references "usr" ("user_id") on update restrict on delete cascade;

Alter table "rule_to_rulebase" add CONSTRAINT fk_rule_to_rulebase_rule_id foreign key ("rule_id") references "rule" ("rule_id") on update restrict on delete cascade;
Alter table "rule_to_rulebase" add CONSTRAINT fk_rule_to_rulebase_rulebase_id foreign key ("rulebase_id") references "rulebase" ("id") on update restrict on delete cascade;

Alter table "rule_user_resolved" add foreign key ("user_id") references "usr" ("user_id") on update restrict on delete cascade;
Alter table "rule_user_resolved" add foreign key ("rule_id") references "rule" ("rule_id") on update restrict on delete cascade;
Alter table "rule_user_resolved" add foreign key ("mgm_id") references "management" ("mgm_id") on update restrict on delete cascade;
Alter table "rule_user_resolved" add CONSTRAINT fk_rule_userobj_resolved_created foreign key ("created") references "import_control" ("control_id") on update restrict on delete cascade;
Alter table "rule_user_resolved" add CONSTRAINT fk_rule_userobj_resolved_removed foreign key ("removed") references "import_control" ("control_id") on update restrict on delete cascade;

Alter table "service" add  foreign key ("ip_proto_id") references "stm_ip_proto" ("ip_proto_id") on update restrict on delete cascade;
Alter table "service" add  foreign key ("last_change_admin") references "uiuser" ("uiuser_id") on update restrict on delete cascade;
Alter table "service" add  foreign key ("mgm_id") references "management" ("mgm_id") on update restrict on delete cascade;
Alter table "service" add  foreign key ("svc_color_id") references "stm_color" ("color_id") on update restrict on delete cascade;
Alter table "service" add  foreign key ("svc_create") references "import_control" ("control_id") on update restrict on delete cascade;
Alter table "service" add  foreign key ("svc_last_seen") references "import_control" ("control_id") on update restrict on delete cascade;
Alter table "service" add  foreign key ("svc_typ_id") references "stm_svc_typ" ("svc_typ_id") on update restrict on delete cascade;
Alter table "svcgrp" add  foreign key ("import_created") references "import_control" ("control_id") on update restrict on delete cascade;
Alter table "svcgrp" add  foreign key ("import_last_seen") references "import_control" ("control_id") on update restrict on delete cascade;
Alter table "svcgrp" add  foreign key ("svcgrp_id") references "service" ("svc_id") on update restrict on delete cascade;
Alter table "svcgrp" add  foreign key ("svcgrp_member_id") references "service" ("svc_id") on update restrict on delete cascade;
Alter table "svcgrp_flat" add  foreign key ("import_created") references "import_control" ("control_id") on update restrict on delete cascade;
Alter table "svcgrp_flat" add  foreign key ("import_last_seen") references "import_control" ("control_id") on update restrict on delete cascade;
Alter table "svcgrp_flat" add  foreign key ("svcgrp_flat_id") references "service" ("svc_id") on update restrict on delete cascade;
Alter table "svcgrp_flat" add  foreign key ("svcgrp_flat_member_id") references "service" ("svc_id") on update restrict on delete cascade;
-- Alter table "temp_filtered_rule_ids" add  foreign key ("rule_id") references "rule" ("rule_id") on update restrict on delete cascade;
-- Alter table "temp_mgmid_importid_at_report_time" add  foreign key ("control_id") references "import_control" ("control_id") on update restrict on delete cascade;
-- Alter table "temp_mgmid_importid_at_report_time" add  foreign key ("mgm_id") references "management" ("mgm_id") on update restrict on delete cascade;
Alter table "tenant_network" add  foreign key ("tenant_id") references "tenant" ("tenant_id") on update restrict on delete cascade;
Alter table "tenant_to_device" add  foreign key ("device_id") references "device" ("dev_id") on update restrict on delete cascade;
Alter table "tenant_to_device" add  foreign key ("tenant_id") references "tenant" ("tenant_id") on update restrict on delete cascade;
Alter table "tenant_to_management" add  foreign key ("management_id") references "management" ("mgm_id") on update restrict on delete cascade;
Alter table "tenant_to_management" add  foreign key ("tenant_id") references "tenant" ("tenant_id") on update restrict on delete cascade;
Alter table "txt" add foreign key ("language") references "language" ("name") on update restrict on delete cascade;
Alter table "customtxt" add foreign key ("language") references "language" ("name") on update restrict on delete cascade;
Alter table "uiuser" add  foreign key ("tenant_id") references "tenant" ("tenant_id") on update restrict on delete cascade;
Alter table "uiuser" add  foreign key ("uiuser_language") references "language" ("name") on update restrict on delete cascade;
Alter table "uiuser" add  foreign key ("ldap_connection_id") references "ldap_connection" ("ldap_connection_id") on update restrict on delete cascade;
Alter table "usergrp" add  foreign key ("import_created") references "import_control" ("control_id") on update restrict on delete cascade;
Alter table "usergrp" add  foreign key ("import_last_seen") references "import_control" ("control_id") on update restrict on delete cascade;
Alter table "usergrp" add  foreign key ("usergrp_id") references "usr" ("user_id") on update restrict on delete cascade;
Alter table "usergrp" add  foreign key ("usergrp_member_id") references "usr" ("user_id") on update restrict on delete cascade;
Alter table "usergrp_flat" add  foreign key ("import_created") references "import_control" ("control_id") on update restrict on delete cascade;
Alter table "usergrp_flat" add  foreign key ("import_last_seen") references "import_control" ("control_id") on update restrict on delete cascade;
Alter table "usergrp_flat" add  foreign key ("usergrp_flat_id") references "usr" ("user_id") on update restrict on delete cascade;
Alter table "usergrp_flat" add  foreign key ("usergrp_flat_member_id") references "usr" ("user_id") on update restrict on delete cascade;
Alter table "usr" add  foreign key ("last_change_admin") references "uiuser" ("uiuser_id") on update restrict on delete cascade;
Alter table "usr" add  foreign key ("mgm_id") references "management" ("mgm_id") on update restrict on delete cascade;
Alter table "usr" add  foreign key ("tenant_id") references "tenant" ("tenant_id") on update restrict on delete cascade;
Alter table "usr" add  foreign key ("user_color_id") references "stm_color" ("color_id") on update restrict on delete cascade;
Alter table "usr" add  foreign key ("usr_typ_id") references "stm_usr_typ" ("usr_typ_id") on update restrict on delete cascade;

Alter table "usr" add  foreign key ("user_create") references "import_control" ("control_id") on update restrict on delete cascade;
Alter table "usr" add  foreign key ("user_last_seen") references "import_control" ("control_id") on update restrict on delete cascade;

Alter table "zone" add  foreign key ("mgm_id") references "management" ("mgm_id") on update restrict on delete cascade;
Alter table "zone" add  foreign key ("zone_create") references "import_control" ("control_id") on update restrict on delete cascade;
Alter table "zone" add  foreign key ("zone_last_seen") references "import_control" ("control_id") on update restrict on delete cascade;

--- request.reqtask ---
ALTER TABLE request.reqtask ADD CONSTRAINT request_reqtask_request_ticket_foreign_key FOREIGN KEY (ticket_id) REFERENCES request.ticket(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.reqtask ADD CONSTRAINT request_reqtask_request_state_foreign_key FOREIGN KEY (state_id) REFERENCES request.state(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.reqtask ADD CONSTRAINT request_reqtask_stm_action_foreign_key FOREIGN KEY (rule_action) REFERENCES stm_action(action_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.reqtask ADD CONSTRAINT request_reqtask_stm_track_foreign_key FOREIGN KEY (rule_tracking) REFERENCES stm_track(track_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.reqtask ADD CONSTRAINT request_reqtask_service_foreign_key FOREIGN KEY (svc_grp_id) REFERENCES service(svc_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.reqtask ADD CONSTRAINT request_reqtask_object_foreign_key FOREIGN KEY (nw_obj_grp_id) REFERENCES object(obj_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.reqtask ADD CONSTRAINT request_reqtask_usergrp_foreign_key FOREIGN KEY (user_grp_id) REFERENCES usr(user_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.reqtask ADD CONSTRAINT request_reqtask_current_handler_foreign_key FOREIGN KEY (current_handler) REFERENCES uiuser(uiuser_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.reqtask ADD CONSTRAINT request_reqtask_recent_handler_foreign_key FOREIGN KEY (recent_handler) REFERENCES uiuser(uiuser_id) ON UPDATE RESTRICT ON DELETE CASCADE;
--- request.reqelement ---
ALTER TABLE request.reqelement ADD CONSTRAINT request_reqelement_request_reqtask_foreign_key FOREIGN KEY (task_id) REFERENCES request.reqtask(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.reqelement ADD CONSTRAINT request_reqelement_proto_foreign_key FOREIGN KEY (ip_proto_id) REFERENCES stm_ip_proto(ip_proto_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.reqelement ADD CONSTRAINT request_reqelement_service_foreign_key FOREIGN KEY (service_id) REFERENCES service(svc_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.reqelement ADD CONSTRAINT request_reqelement_object_foreign_key FOREIGN KEY (network_object_id) REFERENCES object(obj_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.reqelement ADD CONSTRAINT request_reqelement_request_reqelement_foreign_key FOREIGN KEY (original_nat_id) REFERENCES request.reqelement(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.reqelement ADD CONSTRAINT request_reqelement_usr_foreign_key FOREIGN KEY (user_id) REFERENCES usr(user_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.reqelement ADD CONSTRAINT request_reqelement_device_foreign_key FOREIGN KEY (device_id) REFERENCES device(dev_id) ON UPDATE RESTRICT ON DELETE CASCADE;
--- request.approval ---
ALTER TABLE request.approval ADD CONSTRAINT request_approval_request_reqtask_foreign_key FOREIGN KEY (task_id) REFERENCES request.reqtask(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.approval ADD CONSTRAINT request_approval_tenant_foreign_key FOREIGN KEY (tenant_id) REFERENCES tenant(tenant_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.approval ADD CONSTRAINT request_approval_request_state_foreign_key FOREIGN KEY (state_id) REFERENCES request.state(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.approval ADD CONSTRAINT request_approval_current_handler_foreign_key FOREIGN KEY (current_handler) REFERENCES uiuser(uiuser_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.approval ADD CONSTRAINT request_approval_recent_handler_foreign_key FOREIGN KEY (recent_handler) REFERENCES uiuser(uiuser_id) ON UPDATE RESTRICT ON DELETE CASCADE;
--- request.ticket ---
ALTER TABLE request.ticket ADD CONSTRAINT request_ticket_request_state_foreign_key FOREIGN KEY (state_id) REFERENCES request.state(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.ticket ADD CONSTRAINT request_ticket_tenant_foreign_key FOREIGN KEY (tenant_id) REFERENCES tenant(tenant_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.ticket ADD CONSTRAINT request_ticket_uiuser_foreign_key FOREIGN KEY (requester_id) REFERENCES uiuser(uiuser_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.ticket ADD CONSTRAINT request_ticket_current_handler_foreign_key FOREIGN KEY (current_handler) REFERENCES uiuser(uiuser_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.ticket ADD CONSTRAINT request_ticket_recent_handler_foreign_key FOREIGN KEY (recent_handler) REFERENCES uiuser(uiuser_id) ON UPDATE RESTRICT ON DELETE CASCADE;
--- owner ---
ALTER TABLE owner ADD CONSTRAINT owner_tenant_foreign_key FOREIGN KEY (tenant_id) REFERENCES tenant(tenant_id) ON UPDATE RESTRICT ON DELETE CASCADE;
--- comment ---
ALTER TABLE request.comment ADD CONSTRAINT request_comment_uiuser_foreign_key FOREIGN KEY (creator_id) REFERENCES uiuser(uiuser_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.comment ADD CONSTRAINT request_comment_request_comment_foreign_key FOREIGN KEY (ref_id) REFERENCES request.comment(id) ON UPDATE RESTRICT ON DELETE CASCADE;
--- owner_network ---
ALTER TABLE owner_network ADD CONSTRAINT owner_network_proto_foreign_key FOREIGN KEY (ip_proto_id) REFERENCES stm_ip_proto(ip_proto_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE owner_network ADD CONSTRAINT owner_network_owner_foreign_key FOREIGN KEY (owner_id) REFERENCES owner(id) ON UPDATE RESTRICT ON DELETE CASCADE;
--- rule_owner ---
ALTER TABLE rule_owner ADD CONSTRAINT rule_owner_rule_metadata_foreign_key FOREIGN KEY (rule_metadata_id) REFERENCES rule_metadata(rule_metadata_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE rule_owner ADD CONSTRAINT rule_owner_owner_foreign_key FOREIGN KEY (owner_id) REFERENCES owner(id) ON UPDATE RESTRICT ON DELETE CASCADE;
--- reqtask_owner ---
ALTER TABLE reqtask_owner ADD CONSTRAINT reqtask_owner_request_reqtask_foreign_key FOREIGN KEY (reqtask_id) REFERENCES request.reqtask(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE reqtask_owner ADD CONSTRAINT reqtask_owner_owner_foreign_key FOREIGN KEY (owner_id) REFERENCES owner(id) ON UPDATE RESTRICT ON DELETE CASCADE;
--- ticket_comment ---
ALTER TABLE request.ticket_comment ADD CONSTRAINT request_ticket_comment_ticket_foreign_key FOREIGN KEY (ticket_id) REFERENCES request.ticket(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.ticket_comment ADD CONSTRAINT request_ticket_comment_comment_foreign_key FOREIGN KEY (comment_id) REFERENCES request.comment(id) ON UPDATE RESTRICT ON DELETE CASCADE;
--- reqtask_comment ---
ALTER TABLE request.reqtask_comment ADD CONSTRAINT request_reqtask_comment_reqtask_foreign_key FOREIGN KEY (task_id) REFERENCES request.reqtask(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.reqtask_comment ADD CONSTRAINT request_reqtask_comment_comment_foreign_key FOREIGN KEY (comment_id) REFERENCES request.comment(id) ON UPDATE RESTRICT ON DELETE CASCADE;
--- approval_comment ---
ALTER TABLE request.approval_comment ADD CONSTRAINT request_approval_comment_approval_foreign_key FOREIGN KEY (approval_id) REFERENCES request.approval(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.approval_comment ADD CONSTRAINT request_approval_comment_comment_foreign_key FOREIGN KEY (comment_id) REFERENCES request.comment(id) ON UPDATE RESTRICT ON DELETE CASCADE;
--- impltask_comment ---
ALTER TABLE request.impltask_comment ADD CONSTRAINT request_impltask_comment_impltask_foreign_key FOREIGN KEY (task_id) REFERENCES request.impltask(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.impltask_comment ADD CONSTRAINT request_impltask_comment_comment_foreign_key FOREIGN KEY (comment_id) REFERENCES request.comment(id) ON UPDATE RESTRICT ON DELETE CASCADE;
--- state_action ---
ALTER TABLE request.state_action ADD CONSTRAINT request_state_action_state_foreign_key FOREIGN KEY (state_id) REFERENCES request.state(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.state_action ADD CONSTRAINT request_state_action_action_foreign_key FOREIGN KEY (action_id) REFERENCES request.action(id) ON UPDATE RESTRICT ON DELETE CASCADE;
--- ext_state ---
ALTER TABLE request.ext_state ADD CONSTRAINT request_ext_state_state_foreign_key FOREIGN KEY (state_id) REFERENCES request.state(id) ON UPDATE RESTRICT ON DELETE CASCADE;
--- request.implelement ---
ALTER TABLE request.implelement ADD CONSTRAINT request_implelement_request_implelement_foreign_key FOREIGN KEY (original_nat_id) REFERENCES request.implelement(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.implelement ADD CONSTRAINT request_implelement_service_foreign_key FOREIGN KEY (service_id) REFERENCES service(svc_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.implelement ADD CONSTRAINT request_implelement_object_foreign_key FOREIGN KEY (network_object_id) REFERENCES object(obj_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.implelement ADD CONSTRAINT request_implelement_proto_foreign_key FOREIGN KEY (ip_proto_id) REFERENCES stm_ip_proto(ip_proto_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.implelement ADD CONSTRAINT request_implelement_request_impltask_foreign_key FOREIGN KEY (implementation_task_id) REFERENCES request.impltask(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.implelement ADD CONSTRAINT request_implelement_usr_foreign_key FOREIGN KEY (user_id) REFERENCES usr(user_id) ON UPDATE RESTRICT ON DELETE CASCADE;
--- request.impltask
ALTER TABLE request.impltask ADD CONSTRAINT request_impltask_request_reqtask_foreign_key FOREIGN KEY (reqtask_id) REFERENCES request.reqtask(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.impltask ADD CONSTRAINT request_impltask_request_state_foreign_key FOREIGN KEY (state_id) REFERENCES request.state(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.impltask ADD CONSTRAINT request_impltask_device_foreign_key FOREIGN KEY (device_id) REFERENCES device(dev_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.impltask ADD CONSTRAINT request_impltask_stm_action_foreign_key FOREIGN KEY (rule_action) REFERENCES stm_action(action_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.impltask ADD CONSTRAINT request_impltask_stm_tracking_foreign_key FOREIGN KEY (rule_tracking) REFERENCES stm_track(track_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.impltask ADD CONSTRAINT request_impltask_service_foreign_key FOREIGN KEY (svc_grp_id) REFERENCES service(svc_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.impltask ADD CONSTRAINT request_impltask_object_foreign_key FOREIGN KEY (nw_obj_grp_id) REFERENCES object(obj_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.impltask ADD CONSTRAINT request_impltask_usergrp_foreign_key FOREIGN KEY (user_grp_id) REFERENCES usr(user_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.impltask ADD CONSTRAINT request_impltask_current_handler_foreign_key FOREIGN KEY (current_handler) REFERENCES uiuser(uiuser_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.impltask ADD CONSTRAINT request_impltask_recent_handler_foreign_key FOREIGN KEY (recent_handler) REFERENCES uiuser(uiuser_id) ON UPDATE RESTRICT ON DELETE CASCADE;

--- recertification ---
ALTER TABLE recertification ADD CONSTRAINT recertification_rule_metadata_foreign_key FOREIGN KEY (rule_metadata_id) REFERENCES rule_metadata(rule_metadata_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE recertification ADD CONSTRAINT recertification_owner_foreign_key FOREIGN KEY (owner_id) REFERENCES owner(id) ON UPDATE RESTRICT ON DELETE CASCADE;

--- compliance.ip_range ---
ALTER TABLE compliance.ip_range ADD CONSTRAINT compliance_ip_range_network_zone_foreign_key FOREIGN KEY (network_zone_id) REFERENCES compliance.network_zone(id) ON UPDATE RESTRICT ON DELETE CASCADE;

--- compliance.network_zone ---
ALTER TABLE compliance.network_zone ADD CONSTRAINT compliance_super_zone_foreign_key FOREIGN KEY (super_network_zone_id) REFERENCES compliance.network_zone(id) ON UPDATE RESTRICT ON DELETE CASCADE;

--- compliance.network_zone_communication ---
ALTER TABLE compliance.network_zone_communication ADD CONSTRAINT compliance_from_network_zone_communication_foreign_key FOREIGN KEY (from_network_zone_id) REFERENCES compliance.network_zone(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE compliance.network_zone_communication ADD CONSTRAINT compliance_to_network_zone_communication_foreign_key FOREIGN KEY (to_network_zone_id) REFERENCES compliance.network_zone(id) ON UPDATE RESTRICT ON DELETE CASCADE;

-- modelling
ALTER TABLE modelling.nwgroup ADD CONSTRAINT modelling_nwgroup_owner_foreign_key FOREIGN KEY (app_id) REFERENCES owner(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE modelling.connection ADD CONSTRAINT modelling_connection_owner_foreign_key FOREIGN KEY (app_id) REFERENCES owner(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE modelling.connection ADD CONSTRAINT modelling_connection_used_interface_foreign_key FOREIGN KEY (used_interface_id) REFERENCES modelling.connection(id) ON UPDATE RESTRICT;
ALTER TABLE modelling.nwobject_nwgroup ADD CONSTRAINT modelling_nwobject_nwgroup_nwobject_foreign_key FOREIGN KEY (nwobject_id) REFERENCES owner_network(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE modelling.nwobject_nwgroup ADD CONSTRAINT modelling_nwobject_nwgroup_nwgroup_foreign_key FOREIGN KEY (nwgroup_id) REFERENCES modelling.nwgroup(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE modelling.nwgroup_connection ADD CONSTRAINT modelling_nwgroup_connection_nwgroup_foreign_key FOREIGN KEY (nwgroup_id) REFERENCES modelling.nwgroup(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE modelling.nwgroup_connection ADD CONSTRAINT modelling_nwgroup_connection_connection_foreign_key FOREIGN KEY (connection_id) REFERENCES modelling.connection(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE modelling.nwobject_connection ADD CONSTRAINT modelling_nwobject_connection_nwobject_foreign_key FOREIGN KEY (nwobject_id) REFERENCES owner_network(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE modelling.nwobject_connection ADD CONSTRAINT modelling_nwobject_connection_connection_foreign_key FOREIGN KEY (connection_id) REFERENCES modelling.connection(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE modelling.service ADD CONSTRAINT modelling_service_owner_foreign_key FOREIGN KEY (app_id) REFERENCES owner(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE modelling.service ADD CONSTRAINT modelling_service_protocol_foreign_key FOREIGN KEY (proto_id) REFERENCES stm_ip_proto(ip_proto_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE modelling.service_group ADD CONSTRAINT modelling_service_group_owner_foreign_key FOREIGN KEY (app_id) REFERENCES owner(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE modelling.service_service_group ADD CONSTRAINT modelling_service_service_group_service_foreign_key FOREIGN KEY (service_id) REFERENCES modelling.service(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE modelling.service_service_group ADD CONSTRAINT modelling_service_service_group_service_group_foreign_key FOREIGN KEY (service_group_id) REFERENCES modelling.service_group(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE modelling.service_group_connection ADD CONSTRAINT modelling_service_group_connection_service_group_foreign_key FOREIGN KEY (service_group_id) REFERENCES modelling.service_group(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE modelling.service_group_connection ADD CONSTRAINT modelling_service_group_connection_connection_foreign_key FOREIGN KEY (connection_id) REFERENCES modelling.connection(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE modelling.service_connection ADD CONSTRAINT modelling_service_connection_service_foreign_key FOREIGN KEY (service_id) REFERENCES modelling.service(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE modelling.service_connection ADD CONSTRAINT modelling_service_connection_connection_foreign_key FOREIGN KEY (connection_id) REFERENCES modelling.connection(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE modelling.change_history ADD CONSTRAINT modelling_change_history_owner_foreign_key FOREIGN KEY (app_id) REFERENCES owner(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE modelling.selected_objects ADD CONSTRAINT modelling_selected_objects_owner_foreign_key FOREIGN KEY (app_id) REFERENCES owner(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE modelling.selected_objects ADD CONSTRAINT modelling_selected_objects_nwgroup_foreign_key FOREIGN KEY (nwgroup_id) REFERENCES modelling.nwgroup(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE modelling.selected_connections ADD CONSTRAINT modelling_selected_connections_owner_foreign_key FOREIGN KEY (app_id) REFERENCES owner(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE modelling.selected_connections ADD CONSTRAINT modelling_selected_connections_connection_foreign_key FOREIGN KEY (connection_id) REFERENCES modelling.connection(id) ON UPDATE RESTRICT ON DELETE CASCADE;

