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
    allowed BOOLEAN NOT NULL DEFAULT TRUE,
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
    )
);

ALTER TABLE logging.log_entry DROP CONSTRAINT IF EXISTS log_entry_service_protocol_foreign_key;
ALTER TABLE logging.log_entry ADD CONSTRAINT log_entry_service_protocol_foreign_key
    FOREIGN KEY (service_protocol) REFERENCES stm_ip_proto(ip_proto_id) ON UPDATE RESTRICT ON DELETE SET NULL;
ALTER TABLE logging.log_entry DROP CONSTRAINT IF EXISTS log_entry_owner_foreign_key;
ALTER TABLE logging.log_entry ADD CONSTRAINT log_entry_owner_foreign_key
    FOREIGN KEY (owner_id) REFERENCES owner(id) ON UPDATE RESTRICT ON DELETE CASCADE;

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
    ('logDataRetentionDays', '90', 0)
ON CONFLICT DO NOTHING;
