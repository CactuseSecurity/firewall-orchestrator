-- logging -----------------------------------------------------

CREATE SCHEMA logging;

CREATE TABLE logging.log_entry
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
