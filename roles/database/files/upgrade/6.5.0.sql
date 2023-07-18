--- Compliance ---
create schema if not exists compliance;

create table if not exists compliance.network_zone
(
    id BIGSERIAL PRIMARY KEY,
	name VARCHAR NOT NULL,
	description VARCHAR NOT NULL,
	super_network_zone_id bigint,
	owner_id bigint
);

create table if not exists compliance.network_zone_communication
(
    from_network_zone_id bigint NOT NULL,
	to_network_zone_id bigint NOT NULL
);

create table if not exists compliance.ip_range
(
    network_zone_id bigint NOT NULL,
	ip_range_start inet NOT NULL,
	ip_range_end inet NOT NULL,
	PRIMARY KEY(network_zone_id, ip_range_start, ip_range_end)
);