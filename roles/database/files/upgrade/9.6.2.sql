-- Metadata calculated during the log data import for every logged address, see issue #5269.
-- Keyed by the address itself, because the same address is reported by several owners and its
-- applications, network areas and name do not depend on the owner which logged it.
CREATE TABLE IF NOT EXISTS logging.ip_metadata
(
    ip_address CIDR PRIMARY KEY,
    app_ids TEXT[] NOT NULL DEFAULT '{}',
    area_ids TEXT[] NOT NULL DEFAULT '{}',
    dns TEXT NOT NULL DEFAULT ''
);

-- support the orphan check which removes metadata of addresses no log entry refers to any more.
-- The unique constraint of log_entry leads with owner_id and cannot answer it.
CREATE INDEX IF NOT EXISTS idx_log_entry_source ON logging.log_entry (source);
CREATE INDEX IF NOT EXISTS idx_log_entry_destination ON logging.log_entry (destination);

GRANT SELECT ON logging.ip_metadata TO fwo_ro;
