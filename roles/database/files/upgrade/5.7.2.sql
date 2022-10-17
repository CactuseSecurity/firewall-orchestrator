---------------------------------------------------------------------------------------
-- adding routing to start path analysis

-- drop table if exists gw_route;
-- drop table if exists gw_interface;

create table if not exists gw_interface
(
    id SERIAL PRIMARY KEY,
    routing_device INTEGER NOT NULL,
    name VARCHAR NOT NULL,
    ip CIDR,
    state_up BOOLEAN DEFAULT TRUE,
    ip_version INTEGER NOT NULL DEFAULT 4,
    netmask_bits INTEGER NOT NULL
);

create table if not exists gw_route
(
    id SERIAL PRIMARY KEY,
    routing_device INT NOT NULL,
    target_gateway CIDR NOT NULL,
    destination CIDR NOT NULL,
    source CIDR,
    interface_id INT,
    interface VARCHAR,
    static BOOLEAN DEFAULT TRUE,
    metric INT,
    distance INT,
    ip_version INTEGER NOT NULL DEFAULT 4
);

ALTER TABLE gw_route DROP CONSTRAINT IF EXISTS gw_route_routing_device_foreign_key;
ALTER TABLE gw_route ADD CONSTRAINT gw_route_routing_device_foreign_key FOREIGN KEY (routing_device) REFERENCES device(dev_id) ON UPDATE RESTRICT ON DELETE CASCADE;

ALTER TABLE gw_route DROP CONSTRAINT IF EXISTS gw_route_interface_foreign_key;
ALTER TABLE gw_route ADD CONSTRAINT gw_route_interface_foreign_key FOREIGN KEY (interface_id) REFERENCES gw_interface(id) ON UPDATE RESTRICT ON DELETE CASCADE;

ALTER TABLE gw_interface DROP CONSTRAINT IF EXISTS gw_interface_routing_device_foreign_key;
ALTER TABLE gw_interface ADD CONSTRAINT gw_interface_routing_device_foreign_key FOREIGN KEY (routing_device) REFERENCES device(dev_id) ON UPDATE RESTRICT ON DELETE CASCADE;

-- decision: we are not enforcing (at DB level) that the interface of a route belongs to the same device

CREATE OR REPLACE FUNCTION gw_interface_id_seq() RETURNS TRIGGER AS $$
BEGIN
  NEW.id = coalesce(NEW.id, nextval('gw_interface_id_seq'));
  RETURN NEW;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS gw_interface_id_seq ON gw_interface CASCADE;
CREATE TRIGGER gw_interface_id_seq BEFORE INSERT ON gw_interface FOR EACH ROW EXECUTE PROCEDURE gw_interface_id_seq();

CREATE OR REPLACE FUNCTION gw_route_add() RETURNS TRIGGER AS $$
BEGIN
  NEW.id = coalesce(NEW.id, nextval('gw_route_id_seq'));
  -- set reference to interface:
  SELECT INTO NEW.interface_id id FROM gw_interface 
    WHERE gw_interface.routing_device=NEW.routing_device AND gw_interface.name=NEW.interface AND gw_interface.ip_version=NEW.ip_version;
  RETURN NEW;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS gw_route_add ON gw_route CASCADE;
CREATE TRIGGER gw_route_add BEFORE INSERT ON gw_route FOR EACH ROW EXECUTE PROCEDURE gw_route_add();

CREATE OR REPLACE FUNCTION import_config_from_json ()
    RETURNS TRIGGER
    AS $BODY$
DECLARE
    i_mgm_id INTEGER;
BEGIN
    -- networking
    IF NEW.chunk_number=0 THEN -- delete all networking data only when starting import, not for each chunk
        SELECT INTO i_mgm_id mgm_id FROM import_control WHERE control_id=NEW.import_id;
        -- before importing, delete all old interfaces and routes belonging to the current management:
        DELETE FROM gw_route WHERE routing_device IN 
            (SELECT dev_id FROM device LEFT JOIN management ON (device.mgm_id=management.mgm_id) WHERE management.mgm_id=i_mgm_id);
        DELETE FROM gw_interface WHERE routing_device IN 
            (SELECT dev_id FROM device LEFT JOIN management ON (device.mgm_id=management.mgm_id) WHERE management.mgm_id=i_mgm_id);
    END IF;

	-- now re-insert the currently found interfaces: 
    INSERT INTO gw_interface SELECT * FROM jsonb_populate_recordset(NULL::gw_interface, NEW.config -> 'interfaces');
	-- now re-insert the currently found routes: 
    INSERT INTO gw_route SELECT * FROM jsonb_populate_recordset(NULL::gw_route, NEW.config -> 'routing');

    -- firewall objects and rules

    INSERT INTO import_object
    SELECT
        *
    FROM
        jsonb_populate_recordset(NULL::import_object, NEW.config -> 'network_objects');

    INSERT INTO import_service
    SELECT
        *
    FROM
        jsonb_populate_recordset(NULL::import_service, NEW.config -> 'service_objects');

    INSERT INTO import_user
    SELECT
        *
    FROM
        jsonb_populate_recordset(NULL::import_user, NEW.config -> 'user_objects');

    INSERT INTO import_zone
    SELECT
        *
    FROM
        jsonb_populate_recordset(NULL::import_zone, NEW.config -> 'zone_objects');

    INSERT INTO import_rule
    SELECT
        *
    FROM
        jsonb_populate_recordset(NULL::import_rule, NEW.config -> 'rules');

    IF NEW.start_import_flag THEN
        -- finally start the stored procedure import
        PERFORM import_all_main(NEW.import_id, NEW.debug_mode);        
    END IF;
    RETURN NEW;
END;
$BODY$
LANGUAGE plpgsql
VOLATILE
COST 100;
ALTER FUNCTION public.import_config_from_json () OWNER TO fworch;

DROP TRIGGER IF EXISTS import_config_insert ON import_config CASCADE;
CREATE TRIGGER import_config_insert
    AFTER INSERT ON import_config
    FOR EACH ROW
    EXECUTE PROCEDURE import_config_from_json ();

ALTER TABLE import_config ADD COLUMN IF NOT EXISTS "chunk_number" integer;
ALTER TABLE stm_dev_typ ADD COLUMN IF NOT EXISTS "is_pure_routing_device" Boolean Default FALSE;

UPDATE stm_dev_typ SET dev_typ_is_mgmt=true,  is_pure_routing_device=false WHERE dev_typ_id=2;
UPDATE stm_dev_typ SET dev_typ_is_mgmt=true,  is_pure_routing_device=false WHERE dev_typ_id=4;
UPDATE stm_dev_typ SET dev_typ_is_mgmt=true,  is_pure_routing_device=false WHERE dev_typ_id=5;
UPDATE stm_dev_typ SET dev_typ_is_mgmt=false, is_pure_routing_device=false WHERE dev_typ_id=6;
UPDATE stm_dev_typ SET dev_typ_is_mgmt=true,  is_pure_routing_device=false WHERE dev_typ_id=7;
UPDATE stm_dev_typ SET dev_typ_is_mgmt=true,  is_pure_routing_device=false WHERE dev_typ_id=8;
UPDATE stm_dev_typ SET dev_typ_is_mgmt=false, is_pure_routing_device=false WHERE dev_typ_id=9;
UPDATE stm_dev_typ SET dev_typ_is_mgmt=false, is_pure_routing_device=false WHERE dev_typ_id=10;
UPDATE stm_dev_typ SET dev_typ_is_mgmt=true,  is_pure_routing_device=false WHERE dev_typ_id=11;
UPDATE stm_dev_typ SET dev_typ_is_mgmt=true,  is_pure_routing_device=false WHERE dev_typ_id=12;
UPDATE stm_dev_typ SET dev_typ_is_mgmt=true,  is_pure_routing_device=false WHERE dev_typ_id=13;
UPDATE stm_dev_typ SET dev_typ_is_mgmt=true,  is_pure_routing_device=false WHERE dev_typ_id=14;
UPDATE stm_dev_typ SET dev_typ_is_mgmt=true,  is_pure_routing_device=false WHERE dev_typ_id=15;
UPDATE stm_dev_typ SET dev_typ_is_mgmt=false, is_pure_routing_device=false WHERE dev_typ_id=16;

insert into stm_dev_typ (dev_typ_id,dev_typ_name,dev_typ_version,dev_typ_manufacturer,dev_typ_predef_svc,dev_typ_is_multi_mgmt,dev_typ_is_mgmt,is_pure_routing_device)
     VALUES (17,'DummyRouter Management','1','DummyRouter','',false,true,true) ON CONFLICT DO NOTHING;
insert into stm_dev_typ (dev_typ_id,dev_typ_name,dev_typ_version,dev_typ_manufacturer,dev_typ_predef_svc,dev_typ_is_multi_mgmt,dev_typ_is_mgmt,is_pure_routing_device)
    VALUES (18,'DummyRouter Gateway','1','DummyRouter','',false,false,true) ON CONFLICT DO NOTHING;



---- resolved rules report
