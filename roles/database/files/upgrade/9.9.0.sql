-- Add the importer setting whether rules with unsupported features (e.g. Azure geo-location filters) are skipped
-- with a warning instead of failing the import. An existing value is kept, so running this again changes nothing.
INSERT INTO config (config_key, config_value, config_user)
VALUES ('importSkipUnsupportedRules', 'True', 0)
ON CONFLICT (config_key, config_user) DO NOTHING;

-- issue #5644: indices for the foreign key columns that the workflow and modelling relationships filter on.
-- Plain CREATE INDEX (no CONCURRENTLY, the upgrade runs as one batch) blocks writes to the table while it builds.
-- request schema: PostgreSQL does not index the referencing side of a foreign key, but Hasura filters the child
-- table on that column for every array relationship (ticket -> reqtasks -> elements/impltasks/approvals/comments).
-- Without these indices every nested array of a ticket query scans the whole child table once per parent row.
CREATE INDEX IF NOT EXISTS idx_request_reqtask_ticket_id ON request.reqtask (ticket_id);
CREATE INDEX IF NOT EXISTS idx_request_reqelement_task_id ON request.reqelement (task_id);
CREATE INDEX IF NOT EXISTS idx_request_impltask_reqtask_id ON request.impltask (reqtask_id);
CREATE INDEX IF NOT EXISTS idx_request_implelement_impltask_id ON request.implelement (implementation_task_id);
CREATE INDEX IF NOT EXISTS idx_request_approval_task_id ON request.approval (task_id);
CREATE INDEX IF NOT EXISTS idx_request_ticket_comment_ticket_id ON request.ticket_comment (ticket_id);
CREATE INDEX IF NOT EXISTS idx_request_ticket_comment_comment_id ON request.ticket_comment (comment_id);
CREATE INDEX IF NOT EXISTS idx_request_reqtask_comment_task_id ON request.reqtask_comment (task_id);
CREATE INDEX IF NOT EXISTS idx_request_reqtask_comment_comment_id ON request.reqtask_comment (comment_id);
CREATE INDEX IF NOT EXISTS idx_request_approval_comment_approval_id ON request.approval_comment (approval_id);
CREATE INDEX IF NOT EXISTS idx_request_approval_comment_comment_id ON request.approval_comment (comment_id);
CREATE INDEX IF NOT EXISTS idx_request_impltask_comment_task_id ON request.impltask_comment (task_id);
CREATE INDEX IF NOT EXISTS idx_request_impltask_comment_comment_id ON request.impltask_comment (comment_id);
CREATE INDEX IF NOT EXISTS idx_reqtask_owner_reqtask_id ON reqtask_owner (reqtask_id);
CREATE INDEX IF NOT EXISTS idx_reqtask_owner_owner_id ON reqtask_owner (owner_id);
CREATE INDEX IF NOT EXISTS idx_owner_ticket_ticket_id ON owner_ticket (ticket_id);
CREATE INDEX IF NOT EXISTS idx_owner_ticket_owner_id ON owner_ticket (owner_id);
-- state filters of ticket lists (getFullTicketsByTicketState etc.)
CREATE INDEX IF NOT EXISTS idx_request_ticket_state_id ON request.ticket (state_id);

-- modelling schema: connection lookups by app, interface usage and ticket, and the requested interfaces list.
-- The PKs of the junction tables lead with the other column, so the connection -> *_connections relationships,
-- which filter on connection_id, cannot use them.
CREATE INDEX IF NOT EXISTS idx_modelling_connection_used_interface_id ON modelling.connection (used_interface_id) WHERE used_interface_id IS NOT NULL;
CREATE INDEX IF NOT EXISTS idx_modelling_connection_app_id ON modelling.connection (app_id);
CREATE INDEX IF NOT EXISTS idx_modelling_connection_proposed_app_id ON modelling.connection (proposed_app_id) WHERE proposed_app_id IS NOT NULL;
CREATE INDEX IF NOT EXISTS idx_modelling_connection_ticket_id ON modelling.connection (ticket_id) WHERE ticket_id IS NOT NULL;
CREATE INDEX IF NOT EXISTS idx_modelling_connection_requested_interface ON modelling.connection (removed) WHERE is_interface AND is_requested;
CREATE INDEX IF NOT EXISTS idx_modelling_nwobject_connection_connection_id ON modelling.nwobject_connection (connection_id);
CREATE INDEX IF NOT EXISTS idx_modelling_nwgroup_connection_connection_id ON modelling.nwgroup_connection (connection_id);
CREATE INDEX IF NOT EXISTS idx_modelling_service_connection_connection_id ON modelling.service_connection (connection_id);
CREATE INDEX IF NOT EXISTS idx_modelling_service_group_connection_connection_id ON modelling.service_group_connection (connection_id);
CREATE INDEX IF NOT EXISTS idx_modelling_selected_connections_connection_id ON modelling.selected_connections (connection_id);
