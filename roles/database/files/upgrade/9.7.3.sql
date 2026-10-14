-- Add the default implicit approval comment setting for existing installations.
INSERT INTO config (config_key, config_value, config_user)
VALUES ('reqImplicitApprovalComment', 'Automatically approved because the request task was promoted beyond the approval phase.', 0)
ON CONFLICT (config_key, config_user) DO NOTHING;

INSERT INTO config (config_key, config_value, config_user)
VALUES ('reqDisplayBundledTasksAsOne', 'False', 0)
ON CONFLICT (config_key, config_user) DO NOTHING;

INSERT INTO config (config_key, config_value, config_user)
VALUES ('reqTicketFieldVisibility', '{"requester":true,"priority":true,"deadline":true,"reason":true,"comments":false}', 0)
ON CONFLICT (config_key, config_user) DO NOTHING;

ALTER TABLE request.ticket
    ADD COLUMN IF NOT EXISTS additional_info varchar;
