insert into config (config_key, config_value, config_user) VALUES ('modIntegrationMode', 'FullyIntegrated', 0) ON CONFLICT DO NOTHING;
insert into config (config_key, config_value, config_user) VALUES ('modIntegrationStates', '[]', 0) ON CONFLICT DO NOTHING;
insert into config (config_key, config_value, config_user) VALUES ('modIntegrationStateMarker', 'ImplementationState', 0) ON CONFLICT DO NOTHING;

ALTER TABLE request.state_action ADD COLUMN IF NOT EXISTS sort_order int default 0;

WITH ordered_state_actions AS (
    SELECT
        state_id,
        action_id,
        ROW_NUMBER() OVER (PARTITION BY state_id ORDER BY action_id) AS sort_order
    FROM request.state_action
)
UPDATE request.state_action state_action
SET sort_order = ordered_state_actions.sort_order
FROM ordered_state_actions
WHERE state_action.state_id = ordered_state_actions.state_id
  AND state_action.action_id = ordered_state_actions.action_id
  AND (state_action.sort_order IS NULL OR state_action.sort_order = 0);
