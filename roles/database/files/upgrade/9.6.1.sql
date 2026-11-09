-- Seed the gateway-based global default without overwriting an existing preference.
INSERT INTO config (config_key, config_value, config_user)
VALUES ('defaultManagementRulebaseView', 'False', 0)
ON CONFLICT (config_key, config_user) DO NOTHING;

-- Incoming links of a rulebase are looked up by to_rulebase_id (rules report management rulebases view);
-- the only other index on rulebase_link starts with gw_id and cannot serve these lookups.
CREATE INDEX IF NOT EXISTS idx_rulebase_link_to_rulebase_id
ON rulebase_link (to_rulebase_id);
