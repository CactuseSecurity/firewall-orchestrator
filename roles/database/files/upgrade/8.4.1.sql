ALTER TABLE modelling.connection DROP CONSTRAINT IF EXISTS modelling_connection_proposed_app_id_foreign_key;
ALTER TABLE modelling.connection ADD CONSTRAINT modelling_connection_proposed_app_id_foreign_key FOREIGN KEY (proposed_app_id) REFERENCES owner(id) ON UPDATE RESTRICT ON DELETE CASCADE;
insert into config (config_key, config_value, config_user) VALUES ('[]', '', 0) ON CONFLICT DO NOTHING;

-- turning all CIDR objects into ranges
-- see https://github.com/CactuseSecurity/firewall-orchestrator/issues/2238
-- defining helper functions:

CREATE OR REPLACE FUNCTION get_first_ip_of_cidr (ip CIDR)
	RETURNS CIDR
	LANGUAGE 'plpgsql' IMMUTABLE COST 1
	AS
$BODY$
	BEGIN
		IF is_single_ip(ip) THEN
			RETURN ip;
		ELSE
			RETURN host(abbrev(ip)::cidr);
		END IF;
	END;
$BODY$;

CREATE OR REPLACE FUNCTION get_last_ip_of_cidr (ip CIDR)
	RETURNS CIDR
	LANGUAGE 'plpgsql' IMMUTABLE COST 1
	AS
$BODY$
	BEGIN
		IF is_single_ip(ip) THEN
			RETURN ip;
		ELSE
			RETURN inet(host(broadcast(ip)));
		END IF;
	END;
$BODY$;

CREATE OR REPLACE FUNCTION is_single_ip (ip CIDR)
	RETURNS BOOLEAN
	LANGUAGE 'plpgsql' IMMUTABLE COST 1
	AS
$BODY$
	BEGIN
		RETURN masklen(ip)=32 AND family(ip)=4 OR masklen(ip)=128 AND family(ip)=6;
	END;
$BODY$;

CREATE OR REPLACE FUNCTION turn_all_cidr_objects_into_ranges () RETURNS VOID AS $$
DECLARE
    i_obj_id BIGINT;
    r_obj RECORD;
BEGIN
-- handling table owner_network
    ALTER TABLE owner_network ADD COLUMN IF NOT EXISTS ip_end CIDR;

    FOR r_obj IN SELECT id, ip, ip_end FROM owner_network
    LOOP
        IF NOT is_single_ip(r_obj.ip) OR r_obj.ip_end IS NULL THEN
            UPDATE owner_network SET ip_end = get_last_ip_of_cidr(r_obj.ip) WHERE id=r_obj.id;
            UPDATE owner_network SET ip = get_first_ip_of_cidr(r_obj.ip) WHERE id=r_obj.id;
        END IF;
    END LOOP;

    ALTER TABLE owner_network DROP CONSTRAINT IF EXISTS owner_network_ip_end_not_null;
    ALTER TABLE owner_network ADD CONSTRAINT owner_network_ip_end_not_null CHECK (ip_end IS NOT NULL);

    RETURN;
END;
$$ LANGUAGE plpgsql;

SELECT * FROM turn_all_cidr_objects_into_ranges();
DROP FUNCTION turn_all_cidr_objects_into_ranges();

ALTER TABLE owner_network DROP CONSTRAINT IF EXISTS owner_network_ip_is_host;
ALTER TABLE owner_network DROP CONSTRAINT IF EXISTS owner_network_ip_end_is_host;
ALTER TABLE owner_network ADD CONSTRAINT owner_network_ip_is_host CHECK (is_single_ip(ip));
ALTER TABLE owner_network ADD CONSTRAINT owner_network_ip_end_is_host CHECK (is_single_ip(ip_end));
