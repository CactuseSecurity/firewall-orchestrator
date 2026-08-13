-- logging schema ------------------------------------------------

CREATE SCHEMA IF NOT EXISTS logging;

CREATE TABLE IF NOT EXISTS logging.log_entry
(
    id BIGSERIAL PRIMARY KEY,
    log_count INTEGER NOT NULL DEFAULT 1,
    source CIDR NOT NULL,
    destination CIDR NOT NULL,
    service_protocol INTEGER,
    service_port INTEGER,
    -- a null value is distinct from every other value in a unique constraint, so the nullable
    -- service columns are mapped to a value that can never be a real protocol or port to keep
    -- one row per logged flow even when the service is only partly known
    service_protocol_key INTEGER GENERATED ALWAYS AS (COALESCE(service_protocol, -1)) STORED,
    service_port_key INTEGER GENERATED ALWAYS AS (COALESCE(service_port, -1)) STORED,
    allowed BOOLEAN NOT NULL DEFAULT TRUE,
    -- reserved for future use: no component sets this flag yet, it is meant to mark the
    -- flows which are already covered by a modelled connection
    modelled BOOLEAN NOT NULL DEFAULT FALSE,
    log_time TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    logging_rule_name VARCHAR(100),
    owner_id INTEGER NOT NULL,
    CONSTRAINT log_entry_source_single_ip CHECK
    (
        (family(source) = 4 AND masklen(source) = 32)
        OR (family(source) = 6 AND masklen(source) = 128)
    ),
    CONSTRAINT log_entry_destination_single_ip CHECK
    (
        (family(destination) = 4 AND masklen(destination) = 32)
        OR (family(destination) = 6 AND masklen(destination) = 128)
    ),
    -- one row per owner and logged flow, repeated imports of the same flow update that row
    CONSTRAINT log_entry_unique_flow UNIQUE
        (owner_id, source, destination, service_protocol_key, service_port_key)
);

ALTER TABLE logging.log_entry DROP CONSTRAINT IF EXISTS log_entry_service_protocol_foreign_key;
ALTER TABLE logging.log_entry ADD CONSTRAINT log_entry_service_protocol_foreign_key
    FOREIGN KEY (service_protocol) REFERENCES stm_ip_proto(ip_proto_id) ON UPDATE RESTRICT ON DELETE SET NULL;
ALTER TABLE logging.log_entry DROP CONSTRAINT IF EXISTS log_entry_owner_foreign_key;
ALTER TABLE logging.log_entry ADD CONSTRAINT log_entry_owner_foreign_key
    FOREIGN KEY (owner_id) REFERENCES owner(id) ON UPDATE RESTRICT ON DELETE CASCADE;

GRANT USAGE ON SCHEMA logging TO fwo_ro;
GRANT SELECT ON ALL TABLES IN SCHEMA logging TO fwo_ro;
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA logging TO fwo_ro;
ALTER DEFAULT PRIVILEGES IN SCHEMA logging GRANT SELECT ON TABLES TO fwo_ro;
ALTER DEFAULT PRIVILEGES IN SCHEMA logging GRANT USAGE, SELECT ON SEQUENCES TO fwo_ro;

INSERT INTO stm_import (import_type_id, import_type_name)
VALUES (4, 'log')
ON CONFLICT (import_type_id) DO NOTHING;

INSERT INTO config (config_key, config_value, config_user)
VALUES
    ('importLogDataPath', '[]', 0),
    ('importLogDataScriptArgs', '', 0),
    ('importLogDataSleepTime', '0', 0),
    ('importLogDataSleepTimeUnit', 'Hours', 0),
    ('importLogDataStartAt', '00:00:00', 0),
    ('importLogDataMaxEntries', '1000', 0),
    ('logDataRetentionDays', '90', 0),
    ('allowLogDataPortWithoutProtocol', 'False', 0),
    ('showLogDataInConnections', 'False', 0)
ON CONFLICT DO NOTHING;
