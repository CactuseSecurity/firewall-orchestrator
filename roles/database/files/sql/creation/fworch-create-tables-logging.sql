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
