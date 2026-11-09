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
    removed timestamp with time zone,
    created timestamp with time zone default now(),
    criterion_id INT,
    name TEXT,
    id BIGSERIAL CONSTRAINT ip_range_id_pkey PRIMARY KEY
);

CREATE TABLE network_zone.device_ip_range_root
(
    dev_id BIGINT NOT NULL,
    ip_range_id BIGINT NOT NULL,
    order_to_root BIGINT NOT NULL,
    PRIMARY KEY (ip_range_id, dev_id)
);

CREATE TABLE network_zone.device_ip_range_internet
(
    dev_id BIGINT NOT NULL,
    ip_range_id BIGINT NOT NULL,
    order_to_internet BIGINT NOT NULL,
    PRIMARY KEY (ip_range_id, dev_id)
);
