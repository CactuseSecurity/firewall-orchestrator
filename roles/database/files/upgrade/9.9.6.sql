-- device specific settings: named versions (snapshots) of the settings, each a portable export package
CREATE TABLE IF NOT EXISTS device_specific_config_snapshot
(
    id BIGSERIAL PRIMARY KEY,
    name VARCHAR NOT NULL,
    description VARCHAR,
    fwo_version VARCHAR NOT NULL DEFAULT '',
    created TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT now(),
    content JSONB NOT NULL,
    CONSTRAINT device_specific_config_snapshot_name_key UNIQUE (name)
);
