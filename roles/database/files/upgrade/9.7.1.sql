-- Existing rows have no reliable import period; leave it unknown until reimported.
ALTER TABLE logging.log_entry
    ADD COLUMN IF NOT EXISTS import_time TIMESTAMP WITH TIME ZONE,
    ADD COLUMN IF NOT EXISTS log_time_range_in_seconds INTEGER CHECK (log_time_range_in_seconds > 0);

-- A disabled lookup must not prevent a later enabled import from resolving the address.
ALTER TABLE logging.ip_metadata
    ADD COLUMN IF NOT EXISTS dns_lookup_completed BOOLEAN NOT NULL DEFAULT FALSE;

INSERT INTO config (config_key, config_value, config_user)
VALUES ('resolveLogDataDns', 'True', 0)
ON CONFLICT DO NOTHING;
