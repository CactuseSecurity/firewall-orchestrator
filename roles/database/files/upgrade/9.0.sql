
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

ALTER table "import_config" DROP COLUMN IF EXISTS "chunk_number";

DROP TRIGGER IF EXISTS gw_route_add ON gw_route CASCADE;
CREATE TRIGGER gw_route_add BEFORE INSERT ON gw_route FOR EACH ROW EXECUTE PROCEDURE gw_route_add();

-------------------

CREATE OR REPLACE FUNCTION import_config_from_json ()
    RETURNS TRIGGER
    AS $BODY$
DECLARE
    i_mgm_id INTEGER;
    i_count INTEGER;
BEGIN
    -- networking
    SELECT INTO i_mgm_id mgm_id FROM import_control WHERE control_id=NEW.import_id;
    -- before importing, delete all old interfaces and routes belonging to the current management:

	-- now re-insert the currently found interfaces: 
    SELECT INTO i_count COUNT(*) FROM  jsonb_populate_recordset(NULL::gw_interface, NEW.config -> 'interfaces');
    IF i_count>0 THEN
        DELETE FROM gw_interface WHERE routing_device IN 
            (SELECT dev_id FROM device LEFT JOIN management ON (device.mgm_id=management.mgm_id) WHERE management.mgm_id=i_mgm_id);
        INSERT INTO gw_interface SELECT * FROM jsonb_populate_recordset(NULL::gw_interface, NEW.config -> 'interfaces');
    END IF;

    SELECT INTO i_count COUNT(*) FROM  jsonb_populate_recordset(NULL::gw_route, NEW.config -> 'routing');
    IF i_count>0 THEN
        DELETE FROM gw_route WHERE routing_device IN 
            (SELECT dev_id FROM device LEFT JOIN management ON (device.mgm_id=management.mgm_id) WHERE management.mgm_id=i_mgm_id);
        -- now re-insert the currently found routes: 
        INSERT INTO gw_route SELECT * FROM jsonb_populate_recordset(NULL::gw_route, NEW.config -> 'routing');
    END IF;

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
VOLATILE;
ALTER FUNCTION public.import_config_from_json () OWNER TO fworch;

DROP TRIGGER IF EXISTS import_config_insert ON import_config CASCADE;

CREATE TRIGGER import_config_insert
    BEFORE INSERT ON import_config
    FOR EACH ROW
    EXECUTE PROCEDURE import_config_from_json ();


---------------------------------------------------------------------------------------------
-- new import

--DROP TABLE "rule_enforced_on_gateway" CASCADE;
--DROP TABLE "rulebase_on_gateway" CASCADE;


Create Table IF NOT EXISTS "rule_enforced_on_gateway" 
(
	"rule_id" Integer NOT NULL,
	"dev_id" Integer,  --  NULL if rule is available for all gateways of its management
	"created" BIGINT,
	"deleted" BIGINT
);

ALTER TABLE "rule_enforced_on_gateway"
    DROP CONSTRAINT IF EXISTS "fk_rule_enforced_on_gateway_rule_rule_id" CASCADE;
Alter table "rule_enforced_on_gateway" add CONSTRAINT fk_rule_enforced_on_gateway_rule_rule_id foreign key ("rule_id") references "rule" ("rule_id") on update restrict on delete cascade;

ALTER TABLE "rule_enforced_on_gateway"
    DROP CONSTRAINT IF EXISTS "fk_rule_enforced_on_gateway_device_dev_id" CASCADE;
Alter table "rule_enforced_on_gateway" add CONSTRAINT fk_rule_enforced_on_gateway_device_dev_id foreign key ("dev_id") references "device" ("dev_id") on update restrict on delete cascade;

ALTER TABLE "rule_enforced_on_gateway"
    DROP CONSTRAINT IF EXISTS "fk_rule_enforced_on_gateway_created_import_control_control_id" CASCADE;

Alter table "rule_enforced_on_gateway" add CONSTRAINT fk_rule_enforced_on_gateway_created_import_control_control_id 
	foreign key ("created") references "import_control" ("control_id") on update restrict on delete cascade;

ALTER TABLE "rule_enforced_on_gateway"
    DROP CONSTRAINT IF EXISTS "fk_rule_enforced_on_gateway_deleted_import_control_control_id" CASCADE;
Alter table "rule_enforced_on_gateway" add CONSTRAINT fk_rule_enforced_on_gateway_deleted_import_control_control_id 
	foreign key ("deleted") references "import_control" ("control_id") on update restrict on delete cascade;

Create table IF NOT EXISTS "rulebase" 
(
	"id" SERIAL primary key,
	"name" Varchar NOT NULL,
	"mgm_id" Integer NOT NULL,
	"is_global" BOOLEAN DEFAULT FALSE NOT NULL,
	"created" BIGINT,
	"deleted" BIGINT
);

ALTER TABLE "rulebase" DROP CONSTRAINT IF EXISTS "fk_rulebase_mgm_id" CASCADE;
Alter table "rulebase" add CONSTRAINT fk_rulebase_mgm_id foreign key ("mgm_id") references "management" ("mgm_id") on update restrict on delete cascade;

ALTER TABLE "rulebase" DROP CONSTRAINT IF EXISTS "unique_rulebase_mgm_id_name" CASCADE;
Alter table "rulebase" add CONSTRAINT unique_rulebase_mgm_id_name UNIQUE ("mgm_id", "name");


Create table IF NOT EXISTS "rulebase_on_gateway" 
(
	"dev_id" Integer,
	"rulebase_id" Integer NOT NULL,
	"order_no" Integer
--	"layer_guard_rule" bigint -- if no layer: null --> will be implemented by direct link from layer guard rule to rulebase table
);

ALTER TABLE "rulebase_on_gateway" DROP CONSTRAINT IF EXISTS "fk_rulebase_on_gateway_dev_id" CASCADE;
Alter table "rulebase_on_gateway" add CONSTRAINT fk_rulebase_on_gateway_dev_id foreign key ("dev_id") references "device" ("dev_id") on update restrict on delete cascade;

ALTER TABLE "rulebase_on_gateway" DROP CONSTRAINT IF EXISTS "fk_rulebase_on_gateway_rulebase_id" CASCADE;
Alter TABLE "rulebase_on_gateway" add CONSTRAINT fk_rulebase_on_gateway_rulebase_id foreign key ("rulebase_id") references "rulebase" ("id") on update restrict on delete cascade;

-- ALTER TABLE "rulebase_on_gateway" DROP CONSTRAINT IF EXISTS "fk_rulebase_on_gateway_layer_guard_rule" CASCADE;
-- Alter TABLE "rulebase_on_gateway" add CONSTRAINT fk_rulebase_on_gateway_layer_guard_rule foreign key ("layer_guard_rule") references "rule" ("rule_id") on update restrict on delete cascade;

ALTER TABLE "rulebase_on_gateway" DROP CONSTRAINT IF EXISTS "unique_rulebase_on_gateway_dev_id_order_no" CASCADE;
Alter table "rulebase_on_gateway" add CONSTRAINT unique_rulebase_on_gateway_dev_id_order_no UNIQUE ("dev_id", "order_no");

ALTER TABLE "management" ADD COLUMN IF NOT EXISTS "is_super_manager" BOOLEAN DEFAULT FALSE;
ALTER TABLE "rule" ADD COLUMN IF NOT EXISTS "is_global" BOOLEAN DEFAULT FALSE NOT NULL;

-- ALTER TABLE "rule" DROP COLUMN IF EXISTS "rule_installon"; -- here we would need to rebuild views
-- ALTER TABLE "rule" DROP COLUMN IF EXISTS "rule_ruleid"; -- here we would need to rebuild views
-- ALTER TABLE "rule" DROP COLUMN IF EXISTS "dev_id"; -- final step when the new structure works
-- ALTER TABLE "import_rule" DROP COLUMN IF EXISTS "rulebase_name";

-- permanent table for storing latest config to calc diffs
CREATE TABLE IF NOT EXISTS "latest_config" (
    "import_id" bigint NOT NULL,
    "mgm_id" integer NOT NULL,
    "config" jsonb NOT NULL,
    PRIMARY KEY ("import_id")
);

ALTER TABLE "latest_config" DROP CONSTRAINT IF EXISTS "unique_latest_config_mgm_id" CASCADE;
Alter table "latest_config" add CONSTRAINT unique_latest_config_mgm_id UNIQUE ("mgm_id");


/*  TODOs 
    
- with each major version released:
    add fwo version to demo config files on fwodemo to ensure all versions can be served

- add install on column to the following reports:
    - recert
    - change (all 3)
    - statistics (optional: only count rules per gw which are active on gw)

 - adjust report tests (add column)
 import install on information (need to find out, where it is encoded) from 
 - fortimanger - simply add name of current gw?
 - fortios - simply add name of current gw?
 - others? - simply add name of current gw?

importer cp get changes:
    {'uid': 'cf8c7582-fd95-464c-81a0-7297df3c5ad9', 'type': 'access-rule', 'domain': {'uid': '41e821a0-3720-11e3-aa6e-0800200c9fde', 'name': 'SMC User', 'domain-type': 'domain'}, 'position': 7, 'track': {'type': {...}, 'per-session': False, 'per-connection': False, 'accounting': False, 'enable-firewall-session': False, 'alert': 'none'}, 'layer': '0f45100c-e4ea-4dc1-bf22-74d9d98a4811', 'source': [{...}], 'source-negate': False, 'destination': [{...}], 'destination-negate': False, 'service': [{...}], 'service-negate': False, 'service-resource': '', 'vpn': [{...}], 'action': {'uid': '6c488338-8eec-4103-ad21-cd461ac2c472', 'name': 'Accept', 'type': 'RulebaseAction', 'domain': {...}, 'color': 'none', 'meta-info': {...}, 'tags': [...], 'icon': 'Actions/actionsAccept', 'comments': 'Accept', 'display-name': 'Accept', 'customFields': None}, 'action-settings': {'enable-identity-captive-portal': False}, 'content': [{...}], 'content-negate': False, 'content-direction': 'any', 'time': [{...}], 'custom-fields': {'field-1': '', 'field-2': '', 'field-3': ''}, 'meta-info': {'lock': 'unlocked', 'validation-state': 'ok', 'last-modify-time': {...}, 'last-modifier': 'tim-admin', 'creation-time': {...}, 'creator': 'tim-admin'}, 'comments': '', 'enabled': True, 'install-on': [{...}], 'available-actions': {'clone': 'not_supported'}, 'tags': []}

- change (cp) importer to read rulebases and mappings from rulebase to device
  - each rule is only stored once
  - each rulebase is only stored once
--- global changes ----
- write changes into new normalized (class-based) format
- allow conversion from new to old format (would lose information when working with rulebases)
- allow conversion from old to new format (only for simple setups with 1:1 gw to rulebase matches

Cleanups (after cp importer works with all config variants):
- re-add users (cp),check ida rules - do we have networks here?
        #parse_users_from_rulebases(full_config, full_config['rulebases'], full_config['users'], config2import, current_import_id)
        --> replace by api call?
- re-add config splits
- update all importers:
   - fortimanager
   - azure
   - cisco firepower
   - Palo
   - NSX
   - Azure
   - legacy?
     - netscreen?!
     - barracuda

can we get everything working with old config format? no!

*/