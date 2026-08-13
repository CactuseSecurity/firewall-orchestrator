--- Network Zone ---
create schema network_zone;
 
create table network_zone.zone
(
    id BIGSERIAL PRIMARY KEY,
    name VARCHAR NOT NULL,
    description VARCHAR NOT NULL,
    super_network_zone_id bigint,
    owner_id bigint,
    removed timestamp with time zone,
    created timestamp with time zone default now(),
    criterion_id INT,
    id_string TEXT,
    is_auto_calculated_internet_zone BOOLEAN DEFAULT FALSE,
    is_auto_calculated_undefined_internal_zone BOOLEAN DEFAULT FALSE
);
 
create table network_zone.ip_range
(
    network_zone_id bigint NOT NULL,
    ip_range_start inet NOT NULL,
    ip_range_end inet NOT NULL,
    PRIMARY KEY(network_zone_id, ip_range_start, ip_range_end, created),
    removed timestamp with time zone,
    created timestamp with time zone default now(),
    criterion_id INT,
    name TEXT
);