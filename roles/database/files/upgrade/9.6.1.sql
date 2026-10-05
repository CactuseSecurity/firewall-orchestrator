-- Seed the gateway-based global default without overwriting an existing preference.
INSERT INTO config (config_key, config_value, config_user)
VALUES ('defaultManagementRulebaseView', 'False', 0)
ON CONFLICT (config_key, config_user) DO NOTHING;

-- Incoming links of a rulebase are looked up by to_rulebase_id (rules report management rulebases view);
-- the only other index on rulebase_link starts with gw_id and cannot serve these lookups.
CREATE INDEX IF NOT EXISTS idx_rulebase_link_to_rulebase_id
ON rulebase_link (to_rulebase_id);

-- TLS certificate checking is configured per connection type: firewall connections (importCheckCertificates,
-- which keeps its value and now also covers autodiscovery and Check Point change requests), email servers and
-- external ticket systems. The new switches keep the unchecked behaviour where such a connection is configured.
INSERT INTO config (config_key, config_value, config_user)
SELECT 'emailCheckCertificates',
    CASE WHEN EXISTS (
        SELECT 1 FROM config server
        JOIN config tls ON tls.config_key = 'emailTls' AND tls.config_user = 0
        WHERE server.config_key = 'emailServerAddress' AND server.config_user = 0
          AND COALESCE(server.config_value, '') <> '' AND COALESCE(tls.config_value, 'None') <> 'None'
    ) THEN 'False' ELSE 'True' END,
    0
ON CONFLICT (config_key, config_user) DO NOTHING;

INSERT INTO config (config_key, config_value, config_user)
SELECT 'extTicketSystemsCheckCertificates',
    CASE WHEN EXISTS (
        SELECT 1 FROM config
        WHERE config_key = 'extTicketSystems' AND config_user = 0
          AND COALESCE(config_value, '') ~ '"Url"\s*:\s*"[^"]'
    ) THEN 'False' ELSE 'True' END,
    0
ON CONFLICT (config_key, config_user) DO NOTHING;
