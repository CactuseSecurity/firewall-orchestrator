/*
----------------------------------------------------
-- Maintenance-Funktionen zum Aufraeumen der Datenbank
----------------------------------------------------

adding cascade on delete constraints for all tables
loesche alle Daten von Systemen, die zu einem Management gehören, 
das mit do_not_import=true und hide_in_gui=true markiert ist
execute: DELETE FROM management where do_not_import and hide_in_gui;

*/




/*
select mgm_name, count(control_id) from management left join import_control using (mgm_id) 
where do_not_import and hide_in_gui group by mgm_id order by (count(control_id)) limit 1;

select mgm_name,mgm_id, max(stop_time) as last_import,
 cast (now() as date) - cast(max(stop_time) as date) as days_ago from import_control 
 left join management using (mgm_id) 
 WHERE cast (now() as date) - cast((stop_time) as date)>730 
 group by mgm_id,mgm_name order by mgm_id,last_import;

select mgm_id, max(stop_time) as last_import,
 (cast (now() as date) - cast(max(stop_time) as date)) as days_ago 
 FROM import_control 
 LEFT JOIN management USING (mgm_id) 
 WHERE cast (now() as date) - cast((stop_time) as date)>730 
 -- WHERE days_ago>730 
 group by mgm_id,mgm_name order by mgm_id,last_import;


#!/bin/bash
while [ true ]
do
   mgm_to_delete=$(psql -qtAX -d fworchdb -c "select mgm_name from management left join import_control using (mgm_id) where do_not_import and hide_in_gui group by mgm_id order by count(control_id) limit 1")
   echo "next mgm to delete: $mgm_to_delete" 
   echo "executing: time psql -qtAX -d fworchdb -c delete from management where mgm_name=$mgm_to_delete"
   time psql -d fworchdb -c "delete from management where mgm_name='$mgm_to_delete'"
done

*/

ALTER TABLE changelog_object   
    DROP CONSTRAINT changelog_object_change_type_id_fkey,   
    ADD CONSTRAINT changelog_object_change_type_id_fkey FOREIGN KEY (change_type_id)
        REFERENCES public.stm_change_type (change_type_id) MATCH SIMPLE
        ON UPDATE RESTRICT
        ON DELETE CASCADE,
    DROP CONSTRAINT changelog_object_control_id_fkey,
    ADD CONSTRAINT changelog_object_control_id_fkey FOREIGN KEY (control_id)
        REFERENCES public.import_control (control_id) MATCH SIMPLE
        ON UPDATE RESTRICT
        ON DELETE CASCADE,
    DROP CONSTRAINT changelog_object_doku_admin_fkey,
    ADD CONSTRAINT changelog_object_doku_admin_fkey FOREIGN KEY (doku_admin)
        REFERENCES public.admin (admin_id) MATCH SIMPLE
        ON UPDATE RESTRICT
        ON DELETE CASCADE,
    DROP CONSTRAINT changelog_object_import_admin_fkey,
    ADD CONSTRAINT changelog_object_import_admin_fkey FOREIGN KEY (import_admin)
        REFERENCES public.admin (admin_id) MATCH SIMPLE
        ON UPDATE RESTRICT
        ON DELETE CASCADE,
    DROP CONSTRAINT changelog_object_mgm_id_fkey,
    ADD CONSTRAINT changelog_object_mgm_id_fkey FOREIGN KEY (mgm_id)
        REFERENCES public.management (mgm_id) MATCH SIMPLE
        ON UPDATE RESTRICT
        ON DELETE CASCADE,
    DROP CONSTRAINT changelog_object_new_obj_id_fkey,
    ADD CONSTRAINT changelog_object_new_obj_id_fkey FOREIGN KEY (new_obj_id)
        REFERENCES public.object (obj_id) MATCH SIMPLE
        ON UPDATE RESTRICT
        ON DELETE CASCADE,
    DROP CONSTRAINT changelog_object_old_obj_id_fkey,
    ADD CONSTRAINT changelog_object_old_obj_id_fkey FOREIGN KEY (old_obj_id)
        REFERENCES public.object (obj_id) MATCH SIMPLE
        ON UPDATE RESTRICT
        ON DELETE CASCADE;

ALTER TABLE changelog_rule   
    DROP CONSTRAINT changelog_rule_change_type_id_fkey,
    ADD CONSTRAINT changelog_rule_change_type_id_fkey FOREIGN KEY (change_type_id)
        REFERENCES public.stm_change_type (change_type_id) MATCH SIMPLE
        ON UPDATE RESTRICT
        ON DELETE CASCADE,
    DROP CONSTRAINT changelog_rule_control_id_fkey,
    ADD CONSTRAINT changelog_rule_control_id_fkey FOREIGN KEY (control_id)
        REFERENCES public.import_control (control_id) MATCH SIMPLE
        ON UPDATE RESTRICT
        ON DELETE CASCADE,
    DROP CONSTRAINT changelog_rule_dev_id_fkey,
    ADD CONSTRAINT changelog_rule_dev_id_fkey FOREIGN KEY (dev_id)
        REFERENCES public.device (dev_id) MATCH SIMPLE
        ON UPDATE RESTRICT
        ON DELETE CASCADE,
    DROP CONSTRAINT changelog_rule_doku_admin_fkey,
    ADD CONSTRAINT changelog_rule_doku_admin_fkey FOREIGN KEY (doku_admin)
        REFERENCES public.admin (admin_id) MATCH SIMPLE
        ON UPDATE RESTRICT
        ON DELETE CASCADE,
    DROP CONSTRAINT changelog_rule_import_admin_fkey,
    ADD CONSTRAINT changelog_rule_import_admin_fkey FOREIGN KEY (import_admin)
        REFERENCES public.admin (admin_id) MATCH SIMPLE
        ON UPDATE RESTRICT
        ON DELETE CASCADE,
    DROP CONSTRAINT changelog_rule_mgm_id_fkey,
    ADD CONSTRAINT changelog_rule_mgm_id_fkey FOREIGN KEY (mgm_id)
        REFERENCES public.management (mgm_id) MATCH SIMPLE
        ON UPDATE RESTRICT
        ON DELETE CASCADE,
    DROP CONSTRAINT changelog_rule_new_rule_id_fkey,
    ADD CONSTRAINT changelog_rule_new_rule_id_fkey FOREIGN KEY (new_rule_id)
        REFERENCES public.rule (rule_id) MATCH SIMPLE
        ON UPDATE RESTRICT
        ON DELETE CASCADE,
    DROP CONSTRAINT changelog_rule_old_rule_id_fkey,
    ADD CONSTRAINT changelog_rule_old_rule_id_fkey FOREIGN KEY (old_rule_id)
        REFERENCES public.rule (rule_id) MATCH SIMPLE
        ON UPDATE RESTRICT
        ON DELETE CASCADE;

    ALTER TABLE changelog_service
    DROP CONSTRAINT changelog_service_change_type_id_fkey,
    ADD CONSTRAINT changelog_service_change_type_id_fkey FOREIGN KEY (change_type_id)
        REFERENCES public.stm_change_type (change_type_id) MATCH SIMPLE
        ON UPDATE RESTRICT
        ON DELETE CASCADE,
    DROP CONSTRAINT changelog_service_control_id_fkey,
    ADD CONSTRAINT changelog_service_control_id_fkey FOREIGN KEY (control_id)
        REFERENCES public.import_control (control_id) MATCH SIMPLE
        ON UPDATE RESTRICT
        ON DELETE CASCADE,
    DROP CONSTRAINT changelog_service_doku_admin_fkey,
    ADD CONSTRAINT changelog_service_doku_admin_fkey FOREIGN KEY (doku_admin)
        REFERENCES public.admin (admin_id) MATCH SIMPLE
        ON UPDATE RESTRICT
        ON DELETE CASCADE,
    DROP CONSTRAINT changelog_service_import_admin_fkey,
    ADD CONSTRAINT changelog_service_import_admin_fkey FOREIGN KEY (import_admin)
        REFERENCES public.admin (admin_id) MATCH SIMPLE
        ON UPDATE RESTRICT
        ON DELETE CASCADE,
    DROP CONSTRAINT changelog_service_mgm_id_fkey,
    ADD CONSTRAINT changelog_service_mgm_id_fkey FOREIGN KEY (mgm_id)
        REFERENCES public.management (mgm_id) MATCH SIMPLE
        ON UPDATE RESTRICT
        ON DELETE CASCADE,
    DROP CONSTRAINT changelog_service_new_svc_id_fkey,
    ADD CONSTRAINT changelog_service_new_svc_id_fkey FOREIGN KEY (new_svc_id)
        REFERENCES public.service (svc_id) MATCH SIMPLE
        ON UPDATE RESTRICT
        ON DELETE CASCADE,
    DROP CONSTRAINT changelog_service_old_svc_id_fkey,
    ADD CONSTRAINT changelog_service_old_svc_id_fkey FOREIGN KEY (old_svc_id)
        REFERENCES public.service (svc_id) MATCH SIMPLE
        ON UPDATE RESTRICT
        ON DELETE CASCADE;

ALTER TABLE changelog_user
    DROP CONSTRAINT changelog_user_change_type_id_fkey,
    ADD CONSTRAINT changelog_user_change_type_id_fkey FOREIGN KEY (change_type_id)
        REFERENCES public.stm_change_type (change_type_id) MATCH SIMPLE
        ON UPDATE RESTRICT
        ON DELETE CASCADE,
    DROP CONSTRAINT changelog_user_control_id_fkey,
    ADD CONSTRAINT changelog_user_control_id_fkey FOREIGN KEY (control_id)
        REFERENCES public.import_control (control_id) MATCH SIMPLE
        ON UPDATE RESTRICT
        ON DELETE CASCADE,
    DROP CONSTRAINT changelog_user_doku_admin_fkey,
    ADD CONSTRAINT changelog_user_doku_admin_fkey FOREIGN KEY (doku_admin)
        REFERENCES public.admin (admin_id) MATCH SIMPLE
        ON UPDATE RESTRICT
        ON DELETE CASCADE,
    DROP CONSTRAINT changelog_user_import_admin_fkey,
    ADD CONSTRAINT changelog_user_import_admin_fkey FOREIGN KEY (import_admin)
        REFERENCES public.admin (admin_id) MATCH SIMPLE
        ON UPDATE RESTRICT
        ON DELETE CASCADE,
    DROP CONSTRAINT changelog_user_mgm_id_fkey,
    ADD CONSTRAINT changelog_user_mgm_id_fkey FOREIGN KEY (mgm_id)
        REFERENCES public.management (mgm_id) MATCH SIMPLE
        ON UPDATE RESTRICT
        ON DELETE CASCADE,
    DROP CONSTRAINT changelog_user_new_user_id_fkey,
    ADD CONSTRAINT changelog_user_new_user_id_fkey FOREIGN KEY (new_user_id)
        REFERENCES public.usr (user_id) MATCH SIMPLE
        ON UPDATE RESTRICT
        ON DELETE CASCADE,
    DROP CONSTRAINT changelog_user_old_user_id_fkey,
    ADD CONSTRAINT changelog_user_old_user_id_fkey FOREIGN KEY (old_user_id)
        REFERENCES public.usr (user_id) MATCH SIMPLE
        ON UPDATE RESTRICT
        ON DELETE CASCADE;

        
ALTER TABLE device
   DROP CONSTRAINT device_tenant_id_fkey,
   ADD CONSTRAINT device_tenant_id_fkey FOREIGN KEY (tenant_id)
        REFERENCES public.tenant (tenant_id) MATCH SIMPLE
        ON UPDATE RESTRICT
        ON DELETE CASCADE,
    DROP CONSTRAINT device_dev_typ_id_fkey,
    ADD CONSTRAINT device_dev_typ_id_fkey FOREIGN KEY (dev_typ_id)
        REFERENCES public.stm_dev_typ (dev_typ_id) MATCH SIMPLE
        ON UPDATE RESTRICT
        ON DELETE CASCADE,
    DROP CONSTRAINT device_mgm_id_fkey,
    ADD CONSTRAINT device_mgm_id_fkey FOREIGN KEY (mgm_id)
        REFERENCES public.management (mgm_id) MATCH SIMPLE
        ON UPDATE RESTRICT
        ON DELETE CASCADE;

ALTER TABLE object
    DROP CONSTRAINT object_last_change_admin_fkey,
    ADD CONSTRAINT object_last_change_admin_fkey FOREIGN KEY (last_change_admin)
        REFERENCES public.admin (admin_id) MATCH SIMPLE
        ON UPDATE RESTRICT
        ON DELETE CASCADE,
    DROP CONSTRAINT object_mgm_id_fkey,
    ADD CONSTRAINT object_mgm_id_fkey FOREIGN KEY (mgm_id)
        REFERENCES public.management (mgm_id) MATCH SIMPLE
        ON UPDATE RESTRICT
        ON DELETE CASCADE,
    DROP CONSTRAINT object_obj_color_id_fkey,
    ADD CONSTRAINT object_obj_color_id_fkey FOREIGN KEY (obj_color_id)
        REFERENCES public.stm_color (color_id) MATCH SIMPLE
        ON UPDATE RESTRICT
        ON DELETE CASCADE,
    DROP CONSTRAINT object_obj_create_fkey,
    ADD CONSTRAINT object_obj_create_fkey FOREIGN KEY (obj_create)
        REFERENCES public.import_control (control_id) MATCH SIMPLE
        ON UPDATE RESTRICT
        ON DELETE CASCADE,
    DROP CONSTRAINT object_obj_last_seen_fkey,
    ADD CONSTRAINT object_obj_last_seen_fkey FOREIGN KEY (obj_last_seen)
        REFERENCES public.import_control (control_id) MATCH SIMPLE
        ON UPDATE RESTRICT
        ON DELETE CASCADE,
    DROP CONSTRAINT object_obj_nat_install_fkey,
    ADD CONSTRAINT object_obj_nat_install_fkey FOREIGN KEY (obj_nat_install)
        REFERENCES public.device (dev_id) MATCH SIMPLE
        ON UPDATE RESTRICT
        ON DELETE CASCADE,
    DROP CONSTRAINT object_obj_typ_id_fkey,
    ADD CONSTRAINT object_obj_typ_id_fkey FOREIGN KEY (obj_typ_id)
        REFERENCES public.stm_obj_typ (obj_typ_id) MATCH SIMPLE
        ON UPDATE RESTRICT
        ON DELETE CASCADE,
    DROP CONSTRAINT object_zone_id_fkey,
    ADD CONSTRAINT object_zone_id_fkey FOREIGN KEY (zone_id)
        REFERENCES public.zone (zone_id) MATCH SIMPLE
        ON UPDATE RESTRICT
        ON DELETE CASCADE;        

ALTER TABLE usr
    DROP CONSTRAINT usr_tenant_id_fkey,
    ADD CONSTRAINT usr_tenant_id_fkey FOREIGN KEY (tenant_id)
        REFERENCES public.tenant (tenant_id) MATCH SIMPLE
        ON UPDATE RESTRICT
        ON DELETE CASCADE,
    DROP CONSTRAINT usr_last_change_admin_fkey,
    ADD CONSTRAINT usr_last_change_admin_fkey FOREIGN KEY (last_change_admin)
        REFERENCES public.admin (admin_id) MATCH SIMPLE
        ON UPDATE RESTRICT
        ON DELETE CASCADE,
    DROP CONSTRAINT usr_mgm_id_fkey,
    ADD CONSTRAINT usr_mgm_id_fkey FOREIGN KEY (mgm_id)
        REFERENCES public.management (mgm_id) MATCH SIMPLE
        ON UPDATE RESTRICT
        ON DELETE CASCADE,
    DROP CONSTRAINT usr_user_color_id_fkey,
    ADD CONSTRAINT usr_user_color_id_fkey FOREIGN KEY (user_color_id)
        REFERENCES public.stm_color (color_id) MATCH SIMPLE
        ON UPDATE RESTRICT
        ON DELETE CASCADE,
    DROP CONSTRAINT usr_usr_typ_id_fkey,
    ADD CONSTRAINT usr_usr_typ_id_fkey FOREIGN KEY (usr_typ_id)
        REFERENCES public.stm_usr_typ (usr_typ_id) MATCH SIMPLE
        ON UPDATE RESTRICT
        ON DELETE CASCADE;
       
ALTER TABLE rule
    DROP CONSTRAINT rule_action_id_fkey,
    ADD CONSTRAINT rule_action_id_fkey FOREIGN KEY (action_id)
        REFERENCES public.stm_action (action_id) MATCH SIMPLE
        ON UPDATE RESTRICT
        ON DELETE CASCADE,
    DROP CONSTRAINT rule_dev_id_fkey,
    ADD CONSTRAINT rule_dev_id_fkey FOREIGN KEY (dev_id)
        REFERENCES public.device (dev_id) MATCH SIMPLE
        ON UPDATE RESTRICT
        ON DELETE CASCADE,
    DROP CONSTRAINT rule_last_change_admin_fkey,
    ADD CONSTRAINT rule_last_change_admin_fkey FOREIGN KEY (last_change_admin)
        REFERENCES public.admin (admin_id) MATCH SIMPLE
        ON UPDATE RESTRICT
        ON DELETE CASCADE,
    DROP CONSTRAINT rule_mgm_id_fkey,
    ADD CONSTRAINT rule_mgm_id_fkey FOREIGN KEY (mgm_id)
        REFERENCES public.management (mgm_id) MATCH SIMPLE
        ON UPDATE RESTRICT
        ON DELETE CASCADE,
    DROP CONSTRAINT rule_rule_create_fkey,
    ADD CONSTRAINT rule_rule_create_fkey FOREIGN KEY (rule_create)
        REFERENCES public.import_control (control_id) MATCH SIMPLE
        ON UPDATE RESTRICT
        ON DELETE CASCADE,
    DROP CONSTRAINT rule_rule_from_zone_fkey,
    ADD CONSTRAINT rule_rule_from_zone_fkey FOREIGN KEY (rule_from_zone)
        REFERENCES public.zone (zone_id) MATCH SIMPLE
        ON UPDATE RESTRICT
        ON DELETE CASCADE,
    DROP CONSTRAINT rule_rule_last_seen_fkey,
    ADD CONSTRAINT rule_rule_last_seen_fkey FOREIGN KEY (rule_last_seen)
        REFERENCES public.import_control (control_id) MATCH SIMPLE
        ON UPDATE RESTRICT
        ON DELETE CASCADE,
    DROP CONSTRAINT rule_rule_to_zone_fkey,
    ADD CONSTRAINT rule_rule_to_zone_fkey FOREIGN KEY (rule_to_zone)
        REFERENCES public.zone (zone_id) MATCH SIMPLE
        ON UPDATE RESTRICT
        ON DELETE CASCADE,
    DROP CONSTRAINT rule_track_id_fkey,
    ADD CONSTRAINT rule_track_id_fkey FOREIGN KEY (track_id)
        REFERENCES public.stm_track (track_id) MATCH SIMPLE
        ON UPDATE RESTRICT
        ON DELETE CASCADE;

ALTER TABLE service      
    DROP CONSTRAINT service_ip_proto_id_fkey,
    ADD CONSTRAINT service_ip_proto_id_fkey FOREIGN KEY (ip_proto_id)
        REFERENCES public.stm_ip_proto (ip_proto_id) MATCH SIMPLE
        ON UPDATE RESTRICT
        ON DELETE CASCADE,
    DROP CONSTRAINT service_last_change_admin_fkey,
    ADD CONSTRAINT service_last_change_admin_fkey FOREIGN KEY (last_change_admin)
        REFERENCES public.admin (admin_id) MATCH SIMPLE
        ON UPDATE RESTRICT
        ON DELETE CASCADE,
    DROP CONSTRAINT service_mgm_id_fkey,
    ADD CONSTRAINT service_mgm_id_fkey FOREIGN KEY (mgm_id)
        REFERENCES public.management (mgm_id) MATCH SIMPLE
        ON UPDATE RESTRICT
        ON DELETE CASCADE,
    DROP CONSTRAINT service_svc_color_id_fkey,
    ADD CONSTRAINT service_svc_color_id_fkey FOREIGN KEY (svc_color_id)
        REFERENCES public.stm_color (color_id) MATCH SIMPLE
        ON UPDATE RESTRICT
        ON DELETE CASCADE,
    DROP CONSTRAINT service_svc_create_fkey,
    ADD CONSTRAINT service_svc_create_fkey FOREIGN KEY (svc_create)
        REFERENCES public.import_control (control_id) MATCH SIMPLE
        ON UPDATE RESTRICT
        ON DELETE CASCADE,
    DROP CONSTRAINT service_svc_last_seen_fkey,
    ADD CONSTRAINT service_svc_last_seen_fkey FOREIGN KEY (svc_last_seen)
        REFERENCES public.import_control (control_id) MATCH SIMPLE
        ON UPDATE RESTRICT
        ON DELETE CASCADE,
    DROP CONSTRAINT service_svc_typ_id_fkey,
    ADD CONSTRAINT service_svc_typ_id_fkey FOREIGN KEY (svc_typ_id)
        REFERENCES public.stm_svc_typ (svc_typ_id) MATCH SIMPLE
        ON UPDATE RESTRICT
        ON DELETE CASCADE;
        
-- ALTER TABLE temp_mgmid_importid_at_report_time
--     DROP CONSTRAINT temp_mgmid_importid_at_report_time_control_id_fkey,
--     ADD CONSTRAINT temp_mgmid_importid_at_report_time_control_id_fkey FOREIGN KEY (control_id)
--         REFERENCES public.import_control (control_id) MATCH SIMPLE
--         ON UPDATE RESTRICT
--         ON DELETE CASCADE,
--     DROP CONSTRAINT temp_mgmid_importid_at_report_time_mgm_id_fkey,
--     ADD CONSTRAINT temp_mgmid_importid_at_report_time_mgm_id_fkey FOREIGN KEY (mgm_id)
--         REFERENCES public.management (mgm_id) MATCH SIMPLE
--         ON UPDATE RESTRICT
--         ON DELETE CASCADE;

ALTER TABLE zone
    DROP CONSTRAINT zone_mgm_id_fkey,
    ADD CONSTRAINT zone_mgm_id_fkey FOREIGN KEY (mgm_id)
        REFERENCES public.management (mgm_id) MATCH SIMPLE
        ON UPDATE RESTRICT
        ON DELETE CASCADE,
    DROP CONSTRAINT zone_zone_create_fkey,
    ADD CONSTRAINT zone_zone_create_fkey FOREIGN KEY (zone_create)
        REFERENCES public.import_control (control_id) MATCH SIMPLE
        ON UPDATE RESTRICT
        ON DELETE CASCADE,
    DROP CONSTRAINT zone_zone_last_seen_fkey,
    ADD CONSTRAINT zone_zone_last_seen_fkey FOREIGN KEY (zone_last_seen)
        REFERENCES public.import_control (control_id) MATCH SIMPLE
        ON UPDATE RESTRICT
        ON DELETE CASCADE;

-- ALTER TABLE rule_order
--     DROP CONSTRAINT rule_order_control_id_fkey,
--     ADD CONSTRAINT rule_order_control_id_fkey FOREIGN KEY (control_id)
--         REFERENCES public.import_control (control_id) MATCH SIMPLE
--         ON UPDATE RESTRICT
--         ON DELETE CASCADE,
--     DROP CONSTRAINT rule_order_dev_id_fkey,
--     ADD CONSTRAINT rule_order_dev_id_fkey FOREIGN KEY (dev_id)
--         REFERENCES public.device (dev_id) MATCH SIMPLE
--         ON UPDATE RESTRICT
--         ON DELETE CASCADE,
--     DROP CONSTRAINT rule_order_rule_id_fkey,
--     ADD CONSTRAINT rule_order_rule_id_fkey FOREIGN KEY (rule_id)
--         REFERENCES public.rule (rule_id) MATCH SIMPLE
--         ON UPDATE RESTRICT
--         ON DELETE CASCADE;
        
ALTER TABLE objgrp
    DROP CONSTRAINT objgrp_import_created_fkey,
    ADD CONSTRAINT objgrp_import_created_fkey FOREIGN KEY (import_created)
        REFERENCES public.import_control (control_id) MATCH SIMPLE
        ON UPDATE RESTRICT
        ON DELETE CASCADE,
    DROP CONSTRAINT objgrp_import_last_seen_fkey,
    ADD CONSTRAINT objgrp_import_last_seen_fkey FOREIGN KEY (import_last_seen)
        REFERENCES public.import_control (control_id) MATCH SIMPLE
        ON UPDATE RESTRICT
        ON DELETE CASCADE,
    DROP CONSTRAINT objgrp_objgrp_id_fkey,
    ADD CONSTRAINT objgrp_objgrp_id_fkey FOREIGN KEY (objgrp_id)
        REFERENCES public.object (obj_id) MATCH SIMPLE
        ON UPDATE RESTRICT
        ON DELETE CASCADE,
    DROP CONSTRAINT objgrp_objgrp_member_id_fkey,
    ADD CONSTRAINT objgrp_objgrp_member_id_fkey FOREIGN KEY (objgrp_member_id)
        REFERENCES public.object (obj_id) MATCH SIMPLE
        ON UPDATE RESTRICT
        ON DELETE CASCADE;

ALTER TABLE objgrp_flat
	DROP CONSTRAINT objgrp_flat_import_created_fkey,
	ADD CONSTRAINT objgrp_flat_import_created_fkey FOREIGN KEY (import_created)
        REFERENCES public.import_control (control_id) MATCH SIMPLE
        ON UPDATE RESTRICT
        ON DELETE CASCADE,
    DROP CONSTRAINT objgrp_flat_import_last_seen_fkey,
    ADD CONSTRAINT objgrp_flat_import_last_seen_fkey FOREIGN KEY (import_last_seen)
        REFERENCES public.import_control (control_id) MATCH SIMPLE
        ON UPDATE RESTRICT
        ON DELETE CASCADE,
    DROP CONSTRAINT objgrp_flat_objgrp_flat_id_fkey,
    ADD CONSTRAINT objgrp_flat_objgrp_flat_id_fkey FOREIGN KEY (objgrp_flat_id)
        REFERENCES public.object (obj_id) MATCH SIMPLE
        ON UPDATE RESTRICT
        ON DELETE CASCADE,
    DROP CONSTRAINT objgrp_flat_objgrp_flat_member_id_fkey,
    ADD CONSTRAINT objgrp_flat_objgrp_flat_member_id_fkey FOREIGN KEY (objgrp_flat_member_id)
        REFERENCES public.object (obj_id) MATCH SIMPLE
        ON UPDATE RESTRICT
        ON DELETE CASCADE;
        
ALTER TABLE svcgrp    
    DROP CONSTRAINT svcgrp_import_created_fkey,
    ADD CONSTRAINT svcgrp_import_created_fkey FOREIGN KEY (import_created)
        REFERENCES public.import_control (control_id) MATCH SIMPLE
        ON UPDATE RESTRICT
        ON DELETE CASCADE,
    DROP CONSTRAINT svcgrp_import_last_seen_fkey,
    ADD CONSTRAINT svcgrp_import_last_seen_fkey FOREIGN KEY (import_last_seen)
        REFERENCES public.import_control (control_id) MATCH SIMPLE
        ON UPDATE RESTRICT
        ON DELETE CASCADE,
    DROP CONSTRAINT svcgrp_svcgrp_id_fkey,
    ADD CONSTRAINT svcgrp_svcgrp_id_fkey FOREIGN KEY (svcgrp_id)
        REFERENCES public.service (svc_id) MATCH SIMPLE
        ON UPDATE RESTRICT
        ON DELETE CASCADE,
    DROP CONSTRAINT svcgrp_svcgrp_member_id_fkey,
    ADD CONSTRAINT svcgrp_svcgrp_member_id_fkey FOREIGN KEY (svcgrp_member_id)
        REFERENCES public.service (svc_id) MATCH SIMPLE
        ON UPDATE RESTRICT
        ON DELETE CASCADE;
       
ALTER TABLE svcgrp_flat
    DROP CONSTRAINT svcgrp_flat_import_created_fkey,
    ADD CONSTRAINT svcgrp_flat_import_created_fkey FOREIGN KEY (import_created)
        REFERENCES public.import_control (control_id) MATCH SIMPLE
        ON UPDATE RESTRICT
        ON DELETE CASCADE,
    DROP CONSTRAINT svcgrp_flat_import_last_seen_fkey,
    ADD CONSTRAINT svcgrp_flat_import_last_seen_fkey FOREIGN KEY (import_last_seen)
        REFERENCES public.import_control (control_id) MATCH SIMPLE
        ON UPDATE RESTRICT
        ON DELETE CASCADE,
    DROP CONSTRAINT svcgrp_flat_svcgrp_flat_id_fkey,
    ADD CONSTRAINT svcgrp_flat_svcgrp_flat_id_fkey FOREIGN KEY (svcgrp_flat_id)
        REFERENCES public.service (svc_id) MATCH SIMPLE
        ON UPDATE RESTRICT
        ON DELETE CASCADE,
    DROP CONSTRAINT svcgrp_flat_svcgrp_flat_member_id_fkey,
    ADD CONSTRAINT svcgrp_flat_svcgrp_flat_member_id_fkey FOREIGN KEY (svcgrp_flat_member_id)
        REFERENCES public.service (svc_id) MATCH SIMPLE
        ON UPDATE RESTRICT
        ON DELETE CASCADE;
        
ALTER TABLE usergrp
    DROP CONSTRAINT usergrp_import_created_fkey,
    ADD CONSTRAINT usergrp_import_created_fkey FOREIGN KEY (import_created)
        REFERENCES public.import_control (control_id) MATCH SIMPLE
        ON UPDATE RESTRICT
        ON DELETE CASCADE,
    DROP CONSTRAINT usergrp_import_last_seen_fkey,
    ADD CONSTRAINT usergrp_import_last_seen_fkey FOREIGN KEY (import_last_seen)
        REFERENCES public.import_control (control_id) MATCH SIMPLE
        ON UPDATE RESTRICT
        ON DELETE CASCADE,
    DROP CONSTRAINT usergrp_usergrp_id_fkey,
    ADD CONSTRAINT usergrp_usergrp_id_fkey FOREIGN KEY (usergrp_id)
        REFERENCES public.usr (user_id) MATCH SIMPLE
        ON UPDATE RESTRICT
        ON DELETE CASCADE,
    DROP CONSTRAINT usergrp_usergrp_member_id_fkey,
    ADD CONSTRAINT usergrp_usergrp_member_id_fkey FOREIGN KEY (usergrp_member_id)
        REFERENCES public.usr (user_id) MATCH SIMPLE
        ON UPDATE RESTRICT
        ON DELETE CASCADE;
        
ALTER TABLE usergrp_flat
    DROP CONSTRAINT usergrp_flat_import_created_fkey,
    ADD CONSTRAINT usergrp_flat_import_created_fkey FOREIGN KEY (import_created)
        REFERENCES public.import_control (control_id) MATCH SIMPLE
        ON UPDATE RESTRICT
        ON DELETE CASCADE,
    DROP CONSTRAINT usergrp_flat_import_last_seen_fkey,
    ADD CONSTRAINT usergrp_flat_import_last_seen_fkey FOREIGN KEY (import_last_seen)
        REFERENCES public.import_control (control_id) MATCH SIMPLE
        ON UPDATE RESTRICT
        ON DELETE CASCADE,
    DROP CONSTRAINT usergrp_flat_usergrp_flat_id_fkey,
    ADD CONSTRAINT usergrp_flat_usergrp_flat_id_fkey FOREIGN KEY (usergrp_flat_id)
        REFERENCES public.usr (user_id) MATCH SIMPLE
        ON UPDATE RESTRICT
        ON DELETE CASCADE,
    DROP CONSTRAINT usergrp_flat_usergrp_flat_member_id_fkey,
    ADD CONSTRAINT usergrp_flat_usergrp_flat_member_id_fkey FOREIGN KEY (usergrp_flat_member_id)
        REFERENCES public.usr (user_id) MATCH SIMPLE
        ON UPDATE RESTRICT
        ON DELETE CASCADE;
        
ALTER TABLE rule_from        
    DROP CONSTRAINT rule_from_obj_id_fkey,
    ADD CONSTRAINT rule_from_obj_id_fkey FOREIGN KEY (obj_id)
        REFERENCES public.object (obj_id) MATCH SIMPLE
        ON UPDATE RESTRICT
        ON DELETE CASCADE,
    DROP CONSTRAINT rule_from_rf_create_fkey,
    ADD CONSTRAINT rule_from_rf_create_fkey FOREIGN KEY (rf_create)
        REFERENCES public.import_control (control_id) MATCH SIMPLE
        ON UPDATE RESTRICT
        ON DELETE CASCADE,
    DROP CONSTRAINT rule_from_rf_last_seen_fkey,
    ADD CONSTRAINT rule_from_rf_last_seen_fkey FOREIGN KEY (rf_last_seen)
        REFERENCES public.import_control (control_id) MATCH SIMPLE
        ON UPDATE RESTRICT
        ON DELETE CASCADE,
    DROP CONSTRAINT rule_from_rule_id_fkey,
    ADD CONSTRAINT rule_from_rule_id_fkey FOREIGN KEY (rule_id)
        REFERENCES public.rule (rule_id) MATCH SIMPLE
        ON UPDATE RESTRICT
        ON DELETE CASCADE,
    DROP CONSTRAINT rule_from_user_id_fkey,
    ADD CONSTRAINT rule_from_user_id_fkey FOREIGN KEY (user_id)
        REFERENCES public.usr (user_id) MATCH SIMPLE
        ON UPDATE RESTRICT
        ON DELETE CASCADE;

ALTER TABLE rule_to
    DROP CONSTRAINT rule_to_obj_id_fkey,
    ADD CONSTRAINT rule_to_obj_id_fkey FOREIGN KEY (obj_id)
        REFERENCES public.object (obj_id) MATCH SIMPLE
        ON UPDATE RESTRICT
        ON DELETE CASCADE,
    DROP CONSTRAINT rule_to_rt_create_fkey,
    ADD CONSTRAINT rule_to_rt_create_fkey FOREIGN KEY (rt_create)
        REFERENCES public.import_control (control_id) MATCH SIMPLE
        ON UPDATE RESTRICT
        ON DELETE CASCADE,
    DROP CONSTRAINT rule_to_rt_last_seen_fkey,
    ADD CONSTRAINT rule_to_rt_last_seen_fkey FOREIGN KEY (rt_last_seen)
        REFERENCES public.import_control (control_id) MATCH SIMPLE
        ON UPDATE RESTRICT
        ON DELETE CASCADE,
    DROP CONSTRAINT rule_to_rule_id_fkey,
    ADD CONSTRAINT rule_to_rule_id_fkey FOREIGN KEY (rule_id)
        REFERENCES public.rule (rule_id) MATCH SIMPLE
        ON UPDATE RESTRICT
        ON DELETE CASCADE;

ALTER TABLE rule_service
	DROP CONSTRAINT rule_service_rs_create_fkey,
	ADD CONSTRAINT rule_service_rs_create_fkey FOREIGN KEY (rs_create)
        REFERENCES public.import_control (control_id) MATCH SIMPLE
        ON UPDATE RESTRICT
        ON DELETE CASCADE,
    DROP CONSTRAINT rule_service_rs_last_seen_fkey,
    ADD CONSTRAINT rule_service_rs_last_seen_fkey FOREIGN KEY (rs_last_seen)
        REFERENCES public.import_control (control_id) MATCH SIMPLE
        ON UPDATE RESTRICT
        ON DELETE CASCADE,
    DROP CONSTRAINT rule_service_rule_id_fkey,
    ADD CONSTRAINT rule_service_rule_id_fkey FOREIGN KEY (rule_id)
        REFERENCES public.rule (rule_id) MATCH SIMPLE
        ON UPDATE RESTRICT
        ON DELETE CASCADE,
    DROP CONSTRAINT rule_service_svc_id_fkey,
    ADD CONSTRAINT rule_service_svc_id_fkey FOREIGN KEY (svc_id)
        REFERENCES public.service (svc_id) MATCH SIMPLE
        ON UPDATE RESTRICT
        ON DELETE CASCADE;  
        
-- ALTER TABLE temp_filtered_rule_ids
-- 	DROP CONSTRAINT temp_filtered_rule_ids_rule_id_fkey,
-- 	ADD CONSTRAINT temp_filtered_rule_ids_rule_id_fkey FOREIGN KEY (rule_id)
--         REFERENCES public.rule (rule_id) MATCH SIMPLE
--         ON UPDATE RESTRICT
--         ON DELETE CASCADE;

ALTER TABLE report        
    DROP CONSTRAINT report_tenant_id_fkey,
    ADD CONSTRAINT report_tenant_id_fkey FOREIGN KEY (tenant_id)
        REFERENCES public.tenant (tenant_id) MATCH SIMPLE
        ON UPDATE RESTRICT
        ON DELETE CASCADE,
    DROP CONSTRAINT report_dev_id_fkey,
    ADD CONSTRAINT report_dev_id_fkey FOREIGN KEY (dev_id)
        REFERENCES public.device (dev_id) MATCH SIMPLE
        ON UPDATE RESTRICT
        ON DELETE CASCADE,
    -- DROP CONSTRAINT report_report_typ_id_fkey,
    -- ADD CONSTRAINT report_report_typ_id_fkey FOREIGN KEY (report_typ_id)
    --     REFERENCES public.stm_report_typ (report_typ_id) MATCH SIMPLE
    --     ON UPDATE RESTRICT
    --     ON DELETE CASCADE,
    DROP CONSTRAINT report_start_import_id_fkey,
    ADD CONSTRAINT report_start_import_id_fkey FOREIGN KEY (start_import_id)
        REFERENCES public.import_control (control_id) MATCH SIMPLE
        ON UPDATE RESTRICT
        ON DELETE CASCADE,
    DROP CONSTRAINT report_stop_import_id_fkey,
    ADD CONSTRAINT report_stop_import_id_fkey FOREIGN KEY (stop_import_id)
        REFERENCES public.import_control (control_id) MATCH SIMPLE
        ON UPDATE RESTRICT
        ON DELETE CASCADE; 
        
ALTER TABLE request_object_change        
    DROP CONSTRAINT request_object_change_log_obj_id_fkey,
    ADD CONSTRAINT request_object_change_log_obj_id_fkey FOREIGN KEY (log_obj_id)
        REFERENCES public.changelog_object (log_obj_id) MATCH SIMPLE
        ON UPDATE RESTRICT
        ON DELETE CASCADE,
    DROP CONSTRAINT request_object_change_request_id_fkey,
    ADD CONSTRAINT request_object_change_request_id_fkey FOREIGN KEY (request_id)
        REFERENCES public.request (request_id) MATCH SIMPLE
        ON UPDATE RESTRICT
        ON DELETE CASCADE;

ALTER TABLE request_rule_change
    DROP CONSTRAINT request_rule_change_log_rule_id_fkey,
    ADD CONSTRAINT request_rule_change_log_rule_id_fkey FOREIGN KEY (log_rule_id)
        REFERENCES public.changelog_rule (log_rule_id) MATCH SIMPLE
        ON UPDATE RESTRICT
        ON DELETE CASCADE,
    DROP CONSTRAINT request_rule_change_request_id_fkey,
    ADD CONSTRAINT request_rule_change_request_id_fkey FOREIGN KEY (request_id)
        REFERENCES public.request (request_id) MATCH SIMPLE
        ON UPDATE RESTRICT
        ON DELETE CASCADE;

ALTER TABLE request_user_change        
    DROP CONSTRAINT request_user_change_log_usr_id_fkey,
    ADD CONSTRAINT request_user_change_log_usr_id_fkey FOREIGN KEY (log_usr_id)
        REFERENCES public.changelog_user (log_usr_id) MATCH SIMPLE
        ON UPDATE RESTRICT
        ON DELETE CASCADE,
    DROP CONSTRAINT request_user_change_request_id_fkey,
    ADD CONSTRAINT request_user_change_request_id_fkey FOREIGN KEY (request_id)
        REFERENCES public.request (request_id) MATCH SIMPLE
        ON UPDATE RESTRICT
        ON DELETE CASCADE;
        
ALTER TABLE request_service_change
    DROP CONSTRAINT request_service_change_log_svc_id_fkey,
    ADD CONSTRAINT request_service_change_log_svc_id_fkey FOREIGN KEY (log_svc_id)
        REFERENCES public.changelog_service (log_svc_id) MATCH SIMPLE
        ON UPDATE RESTRICT
        ON DELETE CASCADE,
    DROP CONSTRAINT request_service_change_request_id_fkey,
    ADD CONSTRAINT request_service_change_request_id_fkey FOREIGN KEY (request_id)
        REFERENCES public.request (request_id) MATCH SIMPLE
        ON UPDATE RESTRICT
        ON DELETE CASCADE;
        