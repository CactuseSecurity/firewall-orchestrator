
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
  SELECT INTO NEW.interface_id id FROM gw_interface 
    WHERE gw_interface.routing_device=NEW.routing_device AND gw_interface.name=NEW.interface AND gw_interface.ip_version=NEW.ip_version;
  RETURN NEW;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS gw_route_add ON gw_route CASCADE;
CREATE TRIGGER gw_route_add BEFORE INSERT ON gw_route FOR EACH ROW EXECUTE PROCEDURE gw_route_add();

-- -- function used during import of owner data
CREATE OR REPLACE FUNCTION recert_refresh_per_owner(i_owner_id INTEGER) RETURNS VOID AS $$
DECLARE
	r_mgm    RECORD;
BEGIN
	BEGIN
		FOR r_mgm IN
			SELECT mgm_id, mgm_name FROM management
		LOOP
			PERFORM recert_refresh_one_owner_one_mgm (i_owner_id, r_mgm.mgm_id, NULL::TIMESTAMP);
		END LOOP;

	EXCEPTION WHEN OTHERS THEN
		RAISE EXCEPTION 'Exception caught in recert_refresh_per_owner while handling management %', r_mgm.mgm_name;
	END;
	RETURN;
END;
$$ LANGUAGE plpgsql;

