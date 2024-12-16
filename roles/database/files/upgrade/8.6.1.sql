ALTER TABLE owner_network DROP CONSTRAINT IF EXISTS owner_network_ip_unique;
ALTER TABLE owner_network ADD CONSTRAINT owner_network_ip_unique UNIQUE (owner_id, ip, ip_end, import_source) REFERENCES owner(id) ON UPDATE RESTRICT ON DELETE CASCADE;
