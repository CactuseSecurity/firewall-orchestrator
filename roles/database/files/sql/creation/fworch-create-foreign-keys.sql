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
Alter table "device" add  foreign key ("tenant_id") references "tenant" ("tenant_id") on update restrict on delete cascade;
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
Alter table "management" add  foreign key ("tenant_id") references "tenant" ("tenant_id") on update restrict on delete cascade;
ALTER TABLE "management" ADD CONSTRAINT management_multi_device_manager_id_fkey FOREIGN KEY ("multi_device_manager_id") REFERENCES "management" ("mgm_id") ON UPDATE RESTRICT; --ON DELETE CASCADE;
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
Alter table "txt" add foreign key ("language") references "language" ("name") on update restrict on delete cascade;
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

--- request.req_task ---
ALTER TABLE request.req_task ADD CONSTRAINT request_req_task_request_ticket_foreign_key FOREIGN KEY (ticket_id) REFERENCES request.ticket(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.req_task ADD CONSTRAINT request_req_task_request_state_foreign_key FOREIGN KEY (state_id) REFERENCES request.state(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.req_task ADD CONSTRAINT request_req_task_stm_action_foreign_key FOREIGN KEY (rule_action) REFERENCES stm_action(action_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.req_task ADD CONSTRAINT request_req_task_stm_track_foreign_key FOREIGN KEY (rule_tracking) REFERENCES stm_track(track_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.req_task ADD CONSTRAINT request_req_task_service_foreign_key FOREIGN KEY (svc_grp_id) REFERENCES service(svc_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.req_task ADD CONSTRAINT request_req_task_object_foreign_key FOREIGN KEY (nw_obj_grp_id) REFERENCES object(obj_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.req_task ADD CONSTRAINT request_req_task_usergrp_foreign_key FOREIGN KEY (user_grp_id) REFERENCES usr(user_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.req_task ADD CONSTRAINT request_req_task_current_handler_foreign_key FOREIGN KEY (current_handler) REFERENCES uiuser(uiuser_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.req_task ADD CONSTRAINT request_req_task_recent_handler_foreign_key FOREIGN KEY (recent_handler) REFERENCES uiuser(uiuser_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.req_task ADD CONSTRAINT request_req_task_device_foreign_key FOREIGN KEY (device_id) REFERENCES device(dev_id) ON UPDATE RESTRICT ON DELETE CASCADE;
--- request.req_element ---
ALTER TABLE request.req_element ADD CONSTRAINT request_req_element_request_req_task_foreign_key FOREIGN KEY (task_id) REFERENCES request.req_task(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.req_element ADD CONSTRAINT request_req_element_proto_foreign_key FOREIGN KEY (ip_proto_id) REFERENCES stm_ip_proto(ip_proto_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.req_element ADD CONSTRAINT request_req_element_service_foreign_key FOREIGN KEY (service_id) REFERENCES service(svc_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.req_element ADD CONSTRAINT request_req_element_object_foreign_key FOREIGN KEY (network_object_id) REFERENCES object(obj_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.req_element ADD CONSTRAINT request_req_element_request_req_element_foreign_key FOREIGN KEY (original_nat_id) REFERENCES request.req_element(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.req_element ADD CONSTRAINT request_req_element_usr_foreign_key FOREIGN KEY (user_id) REFERENCES usr(user_id) ON UPDATE RESTRICT ON DELETE CASCADE;
--- request.approval ---
ALTER TABLE request.approval ADD CONSTRAINT request_approval_request_req_task_foreign_key FOREIGN KEY (task_id) REFERENCES request.req_task(id) ON UPDATE RESTRICT ON DELETE CASCADE;
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
ALTER TABLE request.comment ADD CONSTRAINT comment_uiuser_foreign_key FOREIGN KEY (creator_id) REFERENCES uiuser(uiuser_id) ON UPDATE RESTRICT ON DELETE CASCADE;
--- owner_network ---
ALTER TABLE owner_network ADD CONSTRAINT owner_network_proto_foreign_key FOREIGN KEY (ip_proto_id) REFERENCES stm_ip_proto(ip_proto_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE owner_network ADD CONSTRAINT owner_network_owner_foreign_key FOREIGN KEY (owner_id) REFERENCES owner(id) ON UPDATE RESTRICT ON DELETE CASCADE;
--- rule_owner ---
ALTER TABLE rule_owner ADD CONSTRAINT rule_owner_rule_metadata_foreign_key FOREIGN KEY (rule_metadata_id) REFERENCES rule_metadata(rule_metadata_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE rule_owner ADD CONSTRAINT rule_owner_owner_foreign_key FOREIGN KEY (owner_id) REFERENCES owner(id) ON UPDATE RESTRICT ON DELETE CASCADE;
--- request_owner ---
ALTER TABLE request_owner ADD CONSTRAINT request_owner_request_req_task_foreign_key FOREIGN KEY (request_task_id) REFERENCES request.req_task(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request_owner ADD CONSTRAINT request_owner_owner_foreign_key FOREIGN KEY (owner_id) REFERENCES owner(id) ON UPDATE RESTRICT ON DELETE CASCADE;
--- ticket_comment ---
ALTER TABLE request.ticket_comment ADD CONSTRAINT ticket_comment_ticket_foreign_key FOREIGN KEY (ticket_id) REFERENCES request.ticket(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.ticket_comment ADD CONSTRAINT ticket_comment_comment_foreign_key FOREIGN KEY (comment_id) REFERENCES request.comment(id) ON UPDATE RESTRICT ON DELETE CASCADE;
--- reqtask_comment ---
ALTER TABLE request.reqtask_comment ADD CONSTRAINT reqtask_comment_task_foreign_key FOREIGN KEY (task_id) REFERENCES request.req_task(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.reqtask_comment ADD CONSTRAINT reqtask_comment_comment_foreign_key FOREIGN KEY (comment_id) REFERENCES request.comment(id) ON UPDATE RESTRICT ON DELETE CASCADE;
--- approval_comment ---
ALTER TABLE request.approval_comment ADD CONSTRAINT approval_comment_task_foreign_key FOREIGN KEY (approval_id) REFERENCES request.approval(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.approval_comment ADD CONSTRAINT approval_comment_comment_foreign_key FOREIGN KEY (comment_id) REFERENCES request.comment(id) ON UPDATE RESTRICT ON DELETE CASCADE;
--- impltask_comment ---
ALTER TABLE request.impltask_comment ADD CONSTRAINT impltask_comment_task_foreign_key FOREIGN KEY (task_id) REFERENCES request.impl_task(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.impltask_comment ADD CONSTRAINT impltask_comment_comment_foreign_key FOREIGN KEY (comment_id) REFERENCES request.comment(id) ON UPDATE RESTRICT ON DELETE CASCADE;
--- state_action ---
ALTER TABLE request.state_action ADD CONSTRAINT state_action_state_foreign_key FOREIGN KEY (state_id) REFERENCES request.state(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.state_action ADD CONSTRAINT state_action_action_foreign_key FOREIGN KEY (action_id) REFERENCES request.action(id) ON UPDATE RESTRICT ON DELETE CASCADE;
--- request.impl_element ---
ALTER TABLE request.impl_element ADD CONSTRAINT request_impl_element_request_impl_element_foreign_key FOREIGN KEY (original_nat_id) REFERENCES request.impl_element(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.impl_element ADD CONSTRAINT request_impl_element_service_foreign_key FOREIGN KEY (service_id) REFERENCES service(svc_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.impl_element ADD CONSTRAINT request_impl_element_object_foreign_key FOREIGN KEY (network_object_id) REFERENCES object(obj_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.impl_element ADD CONSTRAINT request_impl_element_proto_foreign_key FOREIGN KEY (ip_proto_id) REFERENCES stm_ip_proto(ip_proto_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.impl_element ADD CONSTRAINT request_impl_element_request_impl_task_foreign_key FOREIGN KEY (implementation_task_id) REFERENCES request.impl_task(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.impl_element ADD CONSTRAINT request_impl_element_usr_foreign_key FOREIGN KEY (user_id) REFERENCES usr(user_id) ON UPDATE RESTRICT ON DELETE CASCADE;
--- request.impl_task
ALTER TABLE request.impl_task ADD CONSTRAINT request_impl_task_request_req_task_foreign_key FOREIGN KEY (request_task_id) REFERENCES request.req_task(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.impl_task ADD CONSTRAINT request_impl_task_request_state_foreign_key FOREIGN KEY (state_id) REFERENCES request.state(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.impl_task ADD CONSTRAINT request_impl_task_device_foreign_key FOREIGN KEY (device_id) REFERENCES device(dev_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.impl_task ADD CONSTRAINT request_impl_task_stm_action_foreign_key FOREIGN KEY (rule_action) REFERENCES stm_action(action_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.impl_task ADD CONSTRAINT request_impl_task_stm_tracking_foreign_key FOREIGN KEY (rule_tracking) REFERENCES stm_track(track_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.impl_task ADD CONSTRAINT request_impl_task_service_foreign_key FOREIGN KEY (svc_grp_id) REFERENCES service(svc_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.impl_task ADD CONSTRAINT request_impl_task_object_foreign_key FOREIGN KEY (nw_obj_grp_id) REFERENCES object(obj_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.impl_task ADD CONSTRAINT request_impl_task_usergrp_foreign_key FOREIGN KEY (user_grp_id) REFERENCES usr(user_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.impl_task ADD CONSTRAINT request_impl_task_current_handler_foreign_key FOREIGN KEY (current_handler) REFERENCES uiuser(uiuser_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.impl_task ADD CONSTRAINT request_impl_task_recent_handler_foreign_key FOREIGN KEY (recent_handler) REFERENCES uiuser(uiuser_id) ON UPDATE RESTRICT ON DELETE CASCADE;

