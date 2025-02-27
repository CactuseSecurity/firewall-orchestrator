-- in preparation for performance optimization in march 2025
/* 
DROP INDEX idx_owner01;
DROP INDEX idx_reqtask_owner01;
DROP INDEX idx_reqtask_owner02;
DROP INDEX idx_request_ticket01;
DROP INDEX idx_request_ticket02;
DROP INDEX idx_request_ticket03;
DROP INDEX idx_request_ticket04;
DROP INDEX idx_request_ticket05;
DROP INDEX idx_request_reqtask01;
DROP INDEX idx_request_reqtask02;
DROP INDEX idx_request_reqtask03;
DROP INDEX idx_request_reqtask04;
DROP INDEX idx_request_reqtask05;
DROP INDEX idx_request_reqtask06;
DROP INDEX idx_request_reqtask07;
DROP INDEX idx_request_reqtask08;
DROP INDEX idx_request_reqtask09;
DROP INDEX idx_request_reqtask10;
DROP INDEX idx_request_reqelement01;
DROP INDEX idx_request_reqelement02;
DROP INDEX idx_request_reqelement03;
DROP INDEX idx_request_reqelement04;
DROP INDEX idx_request_reqelement05;
DROP INDEX idx_request_reqelement06;
DROP INDEX idx_request_reqelement07;
DROP INDEX idx_request_approval01;
DROP INDEX idx_request_approval02;
DROP INDEX idx_request_approval03;
DROP INDEX idx_request_approval04;
DROP INDEX idx_request_approval05;
DROP INDEX idx_request_comment01;
DROP INDEX idx_request_comment02;
DROP INDEX idx_owner_network01;
DROP INDEX idx_owner_network02;
DROP INDEX idx_request_ticket_comment01;
DROP INDEX idx_request_ticket_comment02;
DROP INDEX idx_request_reqtask_comment01;
DROP INDEX idx_request_reqtask_comment02;
DROP INDEX idx_request_approval_comment01;
DROP INDEX idx_request_approval_comment02;
DROP INDEX idx_request_impltask_comment01;
DROP INDEX idx_request_impltask_comment02;
DROP INDEX idx_request_implelement01;
DROP INDEX idx_request_implelement02;
DROP INDEX idx_request_implelement03;
DROP INDEX idx_request_implelement04;
DROP INDEX idx_request_implelement05;
DROP INDEX idx_request_implelement06;
DROP INDEX idx_request_impltask01;
DROP INDEX idx_request_impltask02;
DROP INDEX idx_request_impltask03;
DROP INDEX idx_request_impltask04;
DROP INDEX idx_request_impltask05;
DROP INDEX idx_request_impltask06;
DROP INDEX idx_request_impltask07;
DROP INDEX idx_request_impltask08;
DROP INDEX idx_request_impltask09;
DROP INDEX idx_modelling_nwgroup01;
DROP INDEX idx_modelling_connection01;
DROP INDEX idx_modelling_connection02;
DROP INDEX idx_modelling_connection03;
DROP INDEX idx_modelling_nwobject_nwgroup01;
DROP INDEX idx_modelling_nwobject_nwgroup02;
DROP INDEX idx_modelling_nwgroup_connection01;
DROP INDEX idx_modelling_nwgroup_connection02;
DROP INDEX idx_modelling_nwobject_connection01;
DROP INDEX idx_modelling_nwobject_connection02;
DROP INDEX idx_modelling_service01;
DROP INDEX idx_modelling_service02;
DROP INDEX idx_modelling_service_group01;
DROP INDEX idx_modelling_service_service_group01;
DROP INDEX idx_modelling_service_service_group02;
DROP INDEX idx_modelling_service_group_connection01;
DROP INDEX idx_modelling_service_group_connection02;
DROP INDEX idx_modelling_service_connection01;
DROP INDEX idx_modelling_service_connection02;
DROP INDEX idx_modelling_change_history01;
DROP INDEX idx_modelling_selected_objects01;
DROP INDEX idx_modelling_selected_objects02;
DROP INDEX idx_modelling_selected_connections01;
DROP INDEX idx_modelling_selected_connections02;
*/
--- create indices for owner & reqtask_owner ---
CREATE index if not exists idx_owner01 on owner (tenant_id);

CREATE index if not exists idx_reqtask_owner01 on reqtask_owner (reqtask_id);
CREATE index if not exists idx_reqtask_owner02 on reqtask_owner (owner_id);

-- adding indices for workflow (request schema)
CREATE index if not exists idx_request_ticket01 on request.ticket(state_id);
CREATE index if not exists idx_request_ticket02 on request.ticket(tenant_id);
CREATE index if not exists idx_request_ticket03 on request.ticket(requester_id);
CREATE index if not exists idx_request_ticket04 on request.ticket(current_handler);
CREATE index if not exists idx_request_ticket05 on request.ticket(recent_handler);

CREATE index if not exists idx_request_reqtask01 on request.reqtask (ticket_id);
CREATE index if not exists idx_request_reqtask02 on request.reqtask (state_id);
CREATE index if not exists idx_request_reqtask03 on request.reqtask (rule_action);
CREATE index if not exists idx_request_reqtask04 on request.reqtask (rule_tracking);
CREATE index if not exists idx_request_reqtask05 on request.reqtask (svc_grp_id);
CREATE index if not exists idx_request_reqtask06 on request.reqtask (nw_obj_grp_id);
CREATE index if not exists idx_request_reqtask07 on request.reqtask (user_grp_id);
CREATE index if not exists idx_request_reqtask08 on request.reqtask (current_handler);
CREATE index if not exists idx_request_reqtask09 on request.reqtask (recent_handler);
CREATE index if not exists idx_request_reqtask10 on request.reqtask (mgm_id);

CREATE index if not exists idx_request_reqelement01 on request.reqelement (task_id);
CREATE index if not exists idx_request_reqelement02 on request.reqelement (ip_proto_id);
CREATE index if not exists idx_request_reqelement03 on request.reqelement (service_id);
CREATE index if not exists idx_request_reqelement04 on request.reqelement (network_object_id);
CREATE index if not exists idx_request_reqelement05 on request.reqelement (original_nat_id);
CREATE index if not exists idx_request_reqelement06 on request.reqelement (user_id);
CREATE index if not exists idx_request_reqelement07 on request.reqelement (device_id);

CREATE index if not exists idx_request_approval01 on request.approval (task_id);
CREATE index if not exists idx_request_approval02 on request.approval (tenant_id);
CREATE index if not exists idx_request_approval03 on request.approval (state_id);
CREATE index if not exists idx_request_approval04 on request.approval (current_handler);
CREATE index if not exists idx_request_approval05 on request.approval (recent_handler);

CREATE index if not exists idx_request_comment01 on request.comment (creator_id);
CREATE index if not exists idx_request_comment02 on request.comment (ref_id);

CREATE index if not exists idx_owner_network01 on owner_network (ip_proto_id);
CREATE index if not exists idx_owner_network02 on owner_network (owner_id);

CREATE index if not exists idx_request_ticket_comment01 on request.ticket_comment (ticket_id);
CREATE index if not exists idx_request_ticket_comment02 on request.ticket_comment (comment_id);

CREATE index if not exists idx_request_reqtask_comment01 on request.reqtask_comment (comment_id);
CREATE index if not exists idx_request_reqtask_comment02 on request.reqtask_comment (task_id);

CREATE index if not exists idx_request_approval_comment01 on request.approval_comment (comment_id);
CREATE index if not exists idx_request_approval_comment02 on request.approval_comment (approval_id);

CREATE index if not exists idx_request_impltask_comment01 on request.impltask_comment (comment_id);
CREATE index if not exists idx_request_impltask_comment02 on request.impltask_comment (task_id);

CREATE index if not exists idx_request_implelement01 on request.implelement (original_nat_id);
CREATE index if not exists idx_request_implelement02 on request.implelement (service_id);
CREATE index if not exists idx_request_implelement03 on request.implelement (network_object_id);
CREATE index if not exists idx_request_implelement04 on request.implelement (ip_proto_id);
CREATE index if not exists idx_request_implelement05 on request.implelement (implementation_task_id);
CREATE index if not exists idx_request_implelement06 on request.implelement (user_id);

CREATE index if not exists idx_request_impltask01 on request.impltask (reqtask_id);
CREATE index if not exists idx_request_impltask02 on request.impltask (state_id);
CREATE index if not exists idx_request_impltask03 on request.impltask (device_id);
CREATE index if not exists idx_request_impltask04 on request.impltask (rule_action);
CREATE index if not exists idx_request_impltask05 on request.impltask (rule_tracking);
CREATE index if not exists idx_request_impltask06 on request.impltask (svc_grp_id);
CREATE index if not exists idx_request_impltask07 on request.impltask (user_grp_id);
CREATE index if not exists idx_request_impltask08 on request.impltask (current_handler);
CREATE index if not exists idx_request_impltask09 on request.impltask (recent_handler);

-- Create indices for modelling schema foreign keys
CREATE index if not exists idx_modelling_nwgroup01 on modelling.nwgroup (app_id);

CREATE INDEX IF NOT EXISTS idx_modelling_connection01 ON modelling.connection (app_id);
CREATE INDEX IF NOT EXISTS idx_modelling_connection02 ON modelling.connection (used_interface_id);
CREATE INDEX IF NOT EXISTS idx_modelling_connection03 ON modelling.connection (proposed_app_id);

CREATE INDEX IF NOT EXISTS idx_modelling_nwobject_nwgroup01 ON modelling.nwobject_nwgroup (nwobject_id);
CREATE INDEX IF NOT EXISTS idx_modelling_nwobject_nwgroup02 ON modelling.nwobject_nwgroup (nwgroup_id);

CREATE INDEX IF NOT EXISTS idx_modelling_nwgroup_connection01 ON modelling.nwgroup_connection (nwgroup_id);
CREATE INDEX IF NOT EXISTS idx_modelling_nwgroup_connection02 ON modelling.nwgroup_connection (connection_id);

CREATE INDEX IF NOT EXISTS idx_modelling_nwobject_connection01 ON modelling.nwobject_connection (nwobject_id);
CREATE INDEX IF NOT EXISTS idx_modelling_nwobject_connection02 ON modelling.nwobject_connection (connection_id);

CREATE INDEX IF NOT EXISTS idx_modelling_service01 ON modelling.service (app_id);
CREATE INDEX IF NOT EXISTS idx_modelling_service02 ON modelling.service (proto_id);

CREATE INDEX IF NOT EXISTS idx_modelling_service_group01 ON modelling.service_group (app_id);

CREATE INDEX IF NOT EXISTS idx_modelling_service_service_group01 ON modelling.service_service_group (service_id);
CREATE INDEX IF NOT EXISTS idx_modelling_service_service_group02 ON modelling.service_service_group (service_group_id);

CREATE INDEX IF NOT EXISTS idx_modelling_service_group_connection01 ON modelling.service_group_connection (service_group_id);
CREATE INDEX IF NOT EXISTS idx_modelling_service_group_connection02 ON modelling.service_group_connection (connection_id);

CREATE INDEX IF NOT EXISTS idx_modelling_service_connection01 ON modelling.service_connection (service_id);
CREATE INDEX IF NOT EXISTS idx_modelling_service_connection02 ON modelling.service_connection (connection_id);

CREATE INDEX IF NOT EXISTS idx_modelling_change_history01 ON modelling.change_history (app_id);

CREATE INDEX IF NOT EXISTS idx_modelling_selected_objects01 ON modelling.selected_objects (app_id);
CREATE INDEX IF NOT EXISTS idx_modelling_selected_objects02 ON modelling.selected_objects (nwgroup_id);

CREATE INDEX IF NOT EXISTS idx_modelling_selected_connections01 ON modelling.selected_connections (app_id);
CREATE INDEX IF NOT EXISTS idx_modelling_selected_connections02 ON modelling.selected_connections (connection_id);

/*
    some other candidates:
    
    --- rule_owner ---
    ALTER TABLE rule_owner ADD CONSTRAINT rule_owner_rule_metadata_foreign_key FOREIGN KEY (rule_metadata_id) REFERENCES rule_metadata(rule_metadata_id) ON UPDATE RESTRICT ON DELETE CASCADE;
    ALTER TABLE rule_owner ADD CONSTRAINT rule_owner_owner_foreign_key FOREIGN KEY (owner_id) REFERENCES owner(id) ON UPDATE RESTRICT ON DELETE CASCADE;

    --- state_action ---
    ALTER TABLE request.state_action ADD CONSTRAINT request_state_action_state_foreign_key FOREIGN KEY (state_id) REFERENCES request.state(id) ON UPDATE RESTRICT ON DELETE CASCADE;
    ALTER TABLE request.state_action ADD CONSTRAINT request_state_action_action_foreign_key FOREIGN KEY (action_id) REFERENCES request.action(id) ON UPDATE RESTRICT ON DELETE CASCADE;
    --- ext_state ---
    ALTER TABLE request.ext_state ADD CONSTRAINT request_ext_state_state_foreign_key FOREIGN KEY (state_id) REFERENCES request.state(id) ON UPDATE RESTRICT ON DELETE CASCADE;

*/
