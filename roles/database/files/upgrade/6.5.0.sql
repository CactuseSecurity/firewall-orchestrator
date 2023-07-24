--- Compliance Tables ---
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


--- Compliance Foreign Keys ---

--- compliance.ip_range ---
ALTER TABLE compliance.ip_range DROP CONSTRAINT IF EXISTS compliance_ip_range_network_zone_foreign_key;
ALTER TABLE compliance.ip_range ADD CONSTRAINT compliance_ip_range_network_zone_foreign_key FOREIGN KEY (network_zone_id) REFERENCES compliance.network_zone(id) ON UPDATE RESTRICT ON DELETE CASCADE;

--- compliance.network_zone ---
ALTER TABLE compliance.network_zone DROP CONSTRAINT IF EXISTS compliance_super_zone_foreign_key;
ALTER TABLE compliance.network_zone ADD CONSTRAINT compliance_super_zone_foreign_key FOREIGN KEY (super_network_zone_id) REFERENCES compliance.network_zone(id) ON UPDATE RESTRICT ON DELETE CASCADE;

--- compliance.network_zone_communication ---
ALTER TABLE compliance.network_zone_communication DROP CONSTRAINT IF EXISTS compliance_from_network_zone_communication_foreign_key;
ALTER TABLE compliance.network_zone_communication DROP CONSTRAINT IF EXISTS compliance_to_network_zone_communication_foreign_key;
ALTER TABLE compliance.network_zone_communication ADD CONSTRAINT compliance_from_network_zone_communication_foreign_key FOREIGN KEY (from_network_zone_id) REFERENCES compliance.network_zone(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE compliance.network_zone_communication ADD CONSTRAINT compliance_to_network_zone_communication_foreign_key FOREIGN KEY (to_network_zone_id) REFERENCES compliance.network_zone(id) ON UPDATE RESTRICT ON DELETE CASCADE;


--- Compliance Constraints ---
CREATE EXTENSION IF NOT EXISTS btree_gist;
--- prevent overlapping ip address ranges in the same zone
ALTER TABLE compliance.ip_range DROP CONSTRAINT IF EXISTS exclude_overlapping_ip_ranges;
ALTER TABLE compliance.ip_range ADD CONSTRAINT exclude_overlapping_ip_ranges
EXCLUDE USING gist (
    network_zone_id WITH =,
    numrange(ip_range_start - '0.0.0.0'::inet, ip_range_end - '0.0.0.0'::inet, '[]') WITH &&
);