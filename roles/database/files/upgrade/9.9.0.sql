-- Add the importer setting whether rules with unsupported features (e.g. Azure geo-location filters) are skipped
-- with a warning instead of failing the import. An existing value is kept, so running this again changes nothing.
INSERT INTO config (config_key, config_value, config_user)
VALUES ('importSkipUnsupportedRules', 'True', 0)
ON CONFLICT (config_key, config_user) DO NOTHING;
