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

-- handling table object
    FOR r_obj IN SELECT obj_id, obj_ip, obj_ip_end FROM object
    LOOP
        IF NOT is_single_ip(r_obj.obj_ip) OR r_obj.obj_ip_end IS NULL THEN

            UPDATE object SET obj_ip_end = get_last_ip_of_cidr(r_obj.obj_ip) WHERE obj_id=r_obj.obj_id;
            UPDATE object SET obj_ip = get_first_ip_of_cidr(r_obj.obj_ip) WHERE obj_id=r_obj.obj_id;
        END IF;
    END LOOP;

    -- all network objects but groups must have ip addresses:
    ALTER TABLE object DROP CONSTRAINT IF EXISTS object_obj_ip_not_null;
    ALTER TABLE object DROP CONSTRAINT IF EXISTS object_obj_ip_end_not_null;
    ALTER TABLE object ADD CONSTRAINT object_obj_ip_not_null CHECK (obj_ip IS NOT NULL OR obj_typ_id=2);
    ALTER TABLE object ADD CONSTRAINT object_obj_ip_end_not_null CHECK (obj_ip_end IS NOT NULL OR obj_typ_id=2);

    ALTER TABLE object DROP CONSTRAINT IF EXISTS object_obj_ip_is_host;
    ALTER TABLE object DROP CONSTRAINT IF EXISTS object_obj_ip_end_is_host;
    ALTER TABLE object ADD CONSTRAINT object_obj_ip_is_host CHECK (is_single_ip(obj_ip));
    ALTER TABLE object ADD CONSTRAINT object_obj_ip_end_is_host CHECK (is_single_ip(obj_ip_end));

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

-- handling table tenant_network
    FOR r_obj IN SELECT tenant_net_id, tenant_net_ip, tenant_net_ip_end FROM tenant_network
    LOOP
        IF is_single_ip(r_obj.tenant_net_ip) OR r_obj.tenant_net_ip_end IS NULL THEN
            UPDATE tenant_network SET tenant_net_ip_end = inet(host(broadcast(r_obj.tenant_net_ip))) WHERE tenant_net_id=r_obj.tenant_net_id;
            UPDATE tenant_network SET tenant_net_ip = inet(abbrev(r_obj.tenant_net_ip)) WHERE tenant_net_id=r_obj.tenant_net_id;
        END IF;
    END LOOP;

    ALTER TABLE tenant_network DROP CONSTRAINT IF EXISTS tenant_network_tenant_net_ip_end_not_null;
    ALTER TABLE tenant_network ADD CONSTRAINT tenant_network_tenant_net_ip_end_not_null CHECK (tenant_net_ip_end IS NOT NULL);

    Alter Table tenant DROP Constraint IF EXISTS tenant_tenant_name_key;
    Alter Table tenant ADD Constraint tenant_tenant_name_key UNIQUE(tenant_name);

    RETURN;
END;
$$ LANGUAGE plpgsql;

SELECT * FROM turn_all_cidr_objects_into_ranges();
DROP FUNCTION turn_all_cidr_objects_into_ranges();

-- removing unused import_status views:
DROP VIEW IF EXISTS view_import_status_table_unsorted CASCADE;
DROP VIEW IF EXISTS view_import_status_table CASCADE;
DROP VIEW IF EXISTS view_import_status_errors CASCADE;
DROP VIEW IF EXISTS view_import_status_successful CASCADE;

