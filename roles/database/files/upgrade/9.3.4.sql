-- Insert default configuration for compliance diff filter
INSERT INTO config (config_key, config_value, config_user)
VALUES ('complianceDiffFilterExistingViolations', 'false', 0)
ON CONFLICT (config_key, config_user) DO NOTHING;
