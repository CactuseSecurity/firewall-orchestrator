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

-- the area IP data conversion script was renamed from convertNwObjDataFromGit to
-- convert_area_ip_data_from_git. The installer removes the old file, so a configured import source
-- pointing at the delivered script is moved to the new name. Only paths of the delivered script are
-- changed, a copy elsewhere keeps its configured name. Repeated runs find nothing left to replace.
UPDATE config
SET config_value = regexp_replace(
        config_value,
        '/scripts/customizing/area_ip_data_import/convertNwObjDataFromGit(\.py|\.json)?"',
        '/scripts/customizing/area_ip_data_import/convert_area_ip_data_from_git\1"',
        'g')
WHERE config_key = 'importSubnetDataPath'
    AND strpos(config_value, '/scripts/customizing/area_ip_data_import/convertNwObjDataFromGit') > 0;

-- log data table settings: hide the log time column and the log time range written into import
-- files generated from CSV data, see issue #5391
INSERT INTO config (config_key, config_value, config_user)
VALUES
    ('hideLogTimeColumn', 'True', 0),
    ('showLogImportTimeInHeading', 'False', 0),
    ('defaultLogTimeRangeInSeconds', '604800', 0)
ON CONFLICT DO NOTHING;

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
