-- next steps:
   -- add rule_to, rule_service to importer
   -- consolidate: not only first import but also subsequent imports should work
   -- improve rollback - currently if import stops in the middle, the rollback is not automatically called

-- pre 9.0 upgrade scripts

--- 8.6.3

insert into config (config_key, config_value, config_user) VALUES ('dnsLookup', 'False', 0) ON CONFLICT DO NOTHING;
insert into config (config_key, config_value, config_user) VALUES ('overwriteExistingNames', 'False', 0) ON CONFLICT DO NOTHING;
insert into config (config_key, config_value, config_user) VALUES ('autoReplaceAppServer', 'False', 0) ON CONFLICT DO NOTHING;

ALTER TABLE modelling.change_history ADD COLUMN IF NOT EXISTS change_source Varchar default 'manual';


CREATE TABLE IF NOT EXISTS refresh_log (
    id SERIAL PRIMARY KEY,
    view_name TEXT NOT NULL,
    refreshed_at TIMESTAMPTZ DEFAULT now(),
    status TEXT
);

CREATE OR REPLACE FUNCTION refresh_view_rule_with_owner()
RETURNS SETOF refresh_log AS $$
DECLARE
    status_message TEXT;
BEGIN
    -- Attempt to refresh the materialized view
    BEGIN
        REFRESH MATERIALIZED VIEW view_rule_with_owner;
        status_message := 'Materialized view refreshed successfully';
    EXCEPTION
        WHEN OTHERS THEN
            status_message := format('Failed to refresh view: %s', SQLERRM);
    END;

    -- Log the operation
    INSERT INTO refresh_log (view_name, status)
    VALUES ('view_rule_with_owner', status_message);

    -- Return the log entry
    RETURN QUERY SELECT * FROM refresh_log WHERE view_name = 'view_rule_with_owner' ORDER BY refreshed_at DESC LIMIT 1;
END;
$$ LANGUAGE plpgsql VOLATILE;

---
--- 8.7.1

Alter table "ldap_connection" ADD COLUMN IF NOT EXISTS "ldap_writepath_for_groups" Varchar;

CREATE OR REPLACE FUNCTION insertLocalLdapWithEncryptedPasswords(
    serverName TEXT, 
    port INTEGER,
    userSearchPath TEXT,
    roleSearchPath TEXT, 
    groupSearchPath TEXT,
    groupWritePath TEXT,
    tenantLevel INTEGER,
    searchUser TEXT,
    searchUserPwd TEXT,
    writeUser TEXT,
    writeUserPwd TEXT,
    ldapType INTEGER
) RETURNS VOID AS $$
DECLARE
    t_key TEXT;
    t_encryptedReadPwd TEXT;
    t_encryptedWritePwd TEXT;
BEGIN
    IF NOT EXISTS (SELECT * FROM ldap_connection WHERE ldap_server = serverName)
    THEN
        SELECT INTO t_key * FROM getMainKey();
        SELECT INTO t_encryptedReadPwd * FROM encryptText(searchUserPwd, t_key);
        SELECT INTO t_encryptedWritePwd * FROM encryptText(writeUserPwd, t_key);
        INSERT INTO ldap_connection
            (ldap_server, ldap_port, ldap_searchpath_for_users, ldap_searchpath_for_roles, ldap_searchpath_for_groups, ldap_writepath_for_groups,
            ldap_tenant_level, ldap_search_user, ldap_search_user_pwd, ldap_write_user, ldap_write_user_pwd, ldap_type)
            VALUES (serverName, port, userSearchPath, roleSearchPath, groupSearchPath, groupWritePath, tenantLevel, searchUser, t_encryptedReadPwd, writeUser, t_encryptedWritePwd, ldapType);
    END IF;
END;
$$ LANGUAGE plpgsql;


-- 8.7.2
ALTER TABLE ext_request ADD COLUMN IF NOT EXISTS attempts int DEFAULT 0;
insert into config (config_key, config_value, config_user) VALUES ('modModelledMarker', 'FWOC', 0) ON CONFLICT DO NOTHING;
insert into config (config_key, config_value, config_user) VALUES ('modModelledMarkerLocation', 'rulename', 0) ON CONFLICT DO NOTHING;
insert into config (config_key, config_value, config_user) VALUES ('ruleRecognitionOption', '{"nwRegardIp":true,"nwRegardName":false,"nwRegardGroupName":false,"nwResolveGroup":false,"svcRegardPortAndProt":true,"svcRegardName":false,"svcRegardGroupName":false,"svcResolveGroup":true}', 0) ON CONFLICT DO NOTHING;
insert into config (config_key, config_value, config_user) VALUES ('availableReportTypes', '[1,2,3,4,5,6,7,8,9,10,21,22]', 0) ON CONFLICT DO NOTHING;

-- 8.8.2
insert into config (config_key, config_value, config_user) VALUES ('varianceAnalysisSleepTime', '0', 0) ON CONFLICT DO NOTHING;
insert into config (config_key, config_value, config_user) VALUES ('varianceAnalysisStartAt', '00:00:00', 0) ON CONFLICT DO NOTHING;
insert into config (config_key, config_value, config_user) VALUES ('varianceAnalysisSync', 'false', 0) ON CONFLICT DO NOTHING;
insert into config (config_key, config_value, config_user) VALUES ('varianceAnalysisRefresh', 'false', 0) ON CONFLICT DO NOTHING;
insert into config (config_key, config_value, config_user) VALUES ('resolveNetworkAreas', 'false', 0) ON CONFLICT DO NOTHING;

--- pre 9.0 changes (old import)

DELETE FROM stm_dev_typ WHERE dev_typ_id IN (2,4,5,6,7);

DROP TRIGGER IF EXISTS gw_route_add ON gw_route CASCADE;
CREATE TRIGGER gw_route_add BEFORE INSERT ON gw_route FOR EACH ROW EXECUTE PROCEDURE gw_route_add();

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

ALTER TABLE management ADD COLUMN IF NOT EXISTS "mgm_uid" Varchar NOT NULL DEFAULT '';
ALTER TABLE management ADD COLUMN IF NOT EXISTS "rulebase_name" Varchar NOT NULL DEFAULT '';
ALTER TABLE management ADD COLUMN IF NOT EXISTS "rulebase_uid" Varchar NOT NULL DEFAULT '';
-- Alter table rule_metadata add column if not exists rulebase_id integer; -- not null;

ALTER TABLE device ADD COLUMN IF NOT EXISTS "dev_uid" Varchar NOT NULL DEFAULT '';

Alter table stm_action add column if not exists allowed BOOLEAN NOT NULL DEFAULT TRUE;
UPDATE stm_action SET allowed = FALSE WHERE action_name = 'deny' OR action_name = 'drop' OR action_name = 'reject';

Create table IF NOT EXISTS "rulebase" 
(
	"id" SERIAL primary key,
	"name" Varchar NOT NULL,
	"uid" Varchar NOT NULL,
	"mgm_id" Integer NOT NULL,
	"is_global" BOOLEAN DEFAULT FALSE NOT NULL,
	"created" BIGINT,
	"removed" BIGINT
);

-- ALTER TABLE "rulebase" ADD COLUMN IF NOT EXISTS "uid" Varchar NOT NULL;

ALTER TABLE "rulebase" DROP CONSTRAINT IF EXISTS "fk_rulebase_mgm_id" CASCADE;
Alter table "rulebase" add CONSTRAINT fk_rulebase_mgm_id foreign key ("mgm_id") references "management" ("mgm_id") on update restrict on delete cascade;

ALTER TABLE "rulebase" DROP CONSTRAINT IF EXISTS "unique_rulebase_mgm_id_name" CASCADE;
Alter table "rulebase" add CONSTRAINT unique_rulebase_mgm_id_name UNIQUE ("mgm_id", "name");

-----------------------------------------------

ALTER TABLE "management" ADD COLUMN IF NOT EXISTS "is_super_manager" BOOLEAN DEFAULT FALSE;
ALTER TABLE "rule" ADD COLUMN IF NOT EXISTS "is_global" BOOLEAN DEFAULT FALSE NOT NULL;
ALTER TABLE "rule" ADD COLUMN IF NOT EXISTS "rulebase_id" INTEGER;

Alter Table "rule" DROP Constraint IF EXISTS "rule_altkey";
Alter Table "rule" DROP Constraint IF EXISTS "rule_unique_mgm_id_rule_uid_rule_create_xlate_rule";
Alter Table "rule" ADD Constraint "rule_unique_mgm_id_rule_uid_rule_create_xlate_rule" UNIQUE ("mgm_id", "rule_uid","rule_create","xlate_rule");


-- permanent table for storing latest config to calc diffs
CREATE TABLE IF NOT EXISTS "latest_config" (
    "import_id" bigint NOT NULL,
    "mgm_id" integer NOT NULL,
    "config" jsonb NOT NULL,
    PRIMARY KEY ("import_id")
);

ALTER TABLE "latest_config" DROP CONSTRAINT IF EXISTS "unique_latest_config_mgm_id" CASCADE;
Alter table "latest_config" add CONSTRAINT unique_latest_config_mgm_id UNIQUE ("mgm_id");

ALTER table "import_control" ADD COLUMN IF NOT EXISTS "is_full_import" BOOLEAN DEFAULT FALSE;

-----------------------------------------------

Create Table IF NOT EXISTS "rule_enforced_on_gateway" 
(
	"rule_id" Integer NOT NULL,
	"dev_id" Integer,  --  NULL if rule is available for all gateways of its management
	"created" BIGINT,
	"removed" BIGINT
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
    DROP CONSTRAINT IF EXISTS "fk_rule_enforced_on_gateway_removed_import_control_control_id" CASCADE;

-- just temp for migration purposes - will be removed later
ALTER TABLE "rule_enforced_on_gateway"
    DROP CONSTRAINT IF EXISTS "fk_rule_enforced_on_gateway_deleted_import_control_control_id" CASCADE;

Alter table "rule_enforced_on_gateway" add CONSTRAINT fk_rule_enforced_on_gateway_removed_import_control_control_id 
	foreign key ("removed") references "import_control" ("control_id") on update restrict on delete cascade;

-----------------------------------------------

CREATE OR REPLACE FUNCTION get_next_rule_number_after_uid(mgmId int, current_rule_uid text)
RETURNS NUMERIC AS $$
  SELECT r.rule_num_numeric as ruleNumber
  FROM rule r
  WHERE r.mgm_id = mgmId and active
    AND r.rule_num_numeric > (
      SELECT rule_num_numeric 
      FROM rule 
      WHERE rule_uid = current_rule_uid AND mgm_id = mgmId AND active
      LIMIT 1
    )
  ORDER BY r.rule_num_numeric ASC
  LIMIT 1;
$$ LANGUAGE sql;

ALTER table "svcgrp_flat" ALTER COLUMN "svcgrp_flat_id" TYPE BIGINT;
ALTER table "svcgrp_flat" ALTER COLUMN "svcgrp_flat_member_id" TYPE BIGINT;
ALTER table "svcgrp_flat" ALTER COLUMN "import_created" TYPE BIGINT;
ALTER table "svcgrp_flat" ALTER COLUMN "import_last_seen" TYPE BIGINT;

ALTER TABLE "rule" DROP CONSTRAINT IF EXISTS "fk_rule_rulebase_id" CASCADE;
ALTER TABLE "rule" ADD CONSTRAINT fk_rule_rulebase_id FOREIGN KEY ("rulebase_id") REFERENCES "rulebase" ("id") ON UPDATE RESTRICT ON DELETE CASCADE;

-- Alter Table "rule_metadata" ADD Constraint "rule_metadata_alt_key" UNIQUE ("rule_uid", "dev_id", "rulebase_id");
-- TODO: this needs to analysed (as dev_id will be removed from rule):
-- Alter table "rule" add constraint "rule_metadata_dev_id_rule_uid_f_key"
--   foreign key ("dev_id", "rule_uid", "rulebase_id") references "rule_metadata" ("dev_id", "rule_uid", "rulebase_id") on update restrict on delete cascade;

-- Create table IF NOT EXISTS "rule_hit" 
-- (
--     "rule_id" BIGINT NOT NULL,
--     "rule_uid" VARCHAR NOT NULL,
--     "gw_id" INTEGER NOT NULL,
--     "metadata_id" BIGINT NOT NULL,
-- 	"rule_first_hit" Timestamp,
-- 	"rule_last_hit" Timestamp,
-- 	"rule_hit_counter" BIGINT
-- );
-- Alter table "rule_hit" DROP CONSTRAINT IF EXISTS fk_rule_hit_rule_id;
-- Alter table "rule_hit" DROP CONSTRAINT IF EXISTS fk_hit_gw_id;
-- Alter table "rule_hit" DROP CONSTRAINT IF EXISTS fk_hit_metadata_id;
-- Alter table "rule_hit" add CONSTRAINT fk_hit_rule_id foreign key ("rule_id") references "rule" ("rule_id") on update restrict on delete cascade; 
-- Alter table "rule_hit" add CONSTRAINT fk_hit_gw_id foreign key ("gw_id") references "device" ("dev_id") on update restrict on delete cascade; 
-- Alter table "rule_hit" add CONSTRAINT fk_hit_metadata_id foreign key ("metadata_id") references "rule_metadata" ("dev_id") on update restrict on delete cascade; 

-----------------------------------------------
-- METADATA part
-- we are removing dev_id and rulebase_id from rule_metadata
-- even CP API does not provide this information regarding hits (the target parameter is ignored, so hits are returned per rule not per rule per gw)

Alter table "rule" drop constraint IF EXISTS "rule_metadata_dev_id_rule_uid_f_key";
Alter Table "rule_metadata" drop Constraint IF EXISTS "rule_metadata_alt_key";

    -- TODO: fix this:
    --     ALTER TABLE rule_metadata DROP Constraint IF EXISTS "rule_metadata_rule_uid_unique";
    --     ALTER TABLE rule_metadata ADD Constraint "rule_metadata_rule_uid_unique" unique ("rule_uid");
    -- causes error:
    --     None: FEHLER:  kann Constraint rule_metadata_rule_uid_unique für Tabelle rule_metadata nicht löschen, weil andere Objekte davon abhängen\nDETAIL:  
    --     Constraint rule_metadata_rule_uid_f_key für Tabelle rule hängt von Index rule_metadata_rule_uid_unique ab\nHINT:  Verwenden Sie DROP ... CASCADE, um die abhängigen Objekte ebenfalls zu löschen.\n"}

ALTER TABLE rule_metadata DROP Constraint IF EXISTS "rule_metadata_rule_uid_unique" CASCADE;
ALTER TABLE rule_metadata ADD Constraint "rule_metadata_rule_uid_unique" unique ("rule_uid");
Alter table "rule" DROP constraint IF EXISTS "rule_rule_metadata_rule_uid_f_key";
Alter table "rule" add constraint "rule_rule_metadata_rule_uid_f_key"
  foreign key ("rule_uid") references "rule_metadata" ("rule_uid") on update restrict on delete cascade;

CREATE OR REPLACE VIEW v_rule_with_rule_owner AS
	SELECT r.rule_id, ow.id as owner_id, ow.name as owner_name, 'rule' AS matches,
		ow.recert_interval, met.rule_last_certified, met.rule_last_certifier
	FROM v_active_access_allow_rules r
	LEFT JOIN rule_metadata met ON (r.rule_uid=met.rule_uid)
	LEFT JOIN rule_owner ro ON (ro.rule_metadata_id=met.rule_metadata_id)
	LEFT JOIN owner ow ON (ro.owner_id=ow.id)
	WHERE NOT ow.id IS NULL
	GROUP BY r.rule_id, ow.id, ow.name, met.rule_last_certified, met.rule_last_certifier;

CREATE OR REPLACE VIEW v_rule_with_src_owner AS 
	SELECT
		r.rule_id, ow.id as owner_id, ow.name as owner_name, 
		CASE
			WHEN onw.ip = onw.ip_end
			THEN SPLIT_PART(CAST(onw.ip AS VARCHAR), '/', 1) -- Single IP overlap, removing netmask
			ELSE
				CASE WHEN	-- range is a single network
					host(broadcast(inet_merge(onw.ip, onw.ip_end))) = host (onw.ip_end) AND
					host(inet_merge(onw.ip, onw.ip_end)) = host (onw.ip)
				THEN
					text(inet_merge(onw.ip, onw.ip_end))
				ELSE
					CONCAT(SPLIT_PART(onw.ip::VARCHAR,'/', 1), '-', SPLIT_PART(onw.ip_end::VARCHAR, '/', 1))
				END
		END AS matching_ip,
		'source' AS match_in,
		ow.recert_interval, met.rule_last_certified, met.rule_last_certifier
	FROM v_active_access_allow_rules r
	LEFT JOIN rule_from ON (r.rule_id=rule_from.rule_id)
	LEFT JOIN objgrp_flat of ON (rule_from.obj_id=of.objgrp_flat_id)
	LEFT JOIN object o ON (of.objgrp_flat_member_id=o.obj_id)
	LEFT JOIN owner_network onw ON (onw.ip_end >= o.obj_ip AND onw.ip <= o.obj_ip_end)
	LEFT JOIN owner ow ON (onw.owner_id=ow.id)
	LEFT JOIN rule_metadata met ON (r.rule_uid=met.rule_uid)
	WHERE r.rule_id NOT IN (SELECT distinct rwo.rule_id FROM v_rule_with_rule_owner rwo) AND
	CASE
		when (select mode from v_rule_ownership_mode) = 'exclusive' then (NOT o.obj_ip IS NULL) AND o.obj_ip NOT IN (select * from v_excluded_src_ips)
		else NOT o.obj_ip IS NULL
	END
	GROUP BY r.rule_id, o.obj_ip, o.obj_ip_end, onw.ip, onw.ip_end, ow.id, ow.name, met.rule_last_certified, met.rule_last_certifier;

CREATE OR REPLACE VIEW v_rule_with_dst_owner AS 
	SELECT 
		r.rule_id, ow.id as owner_id, ow.name as owner_name, 
		CASE
			WHEN onw.ip = onw.ip_end
			THEN SPLIT_PART(CAST(onw.ip AS VARCHAR), '/', 1) -- Single IP overlap, removing netmask
			ELSE
				CASE WHEN	-- range is a single network
					host(broadcast(inet_merge(onw.ip, onw.ip_end))) = host (onw.ip_end) AND
					host(inet_merge(onw.ip, onw.ip_end)) = host (onw.ip)
				THEN
					text(inet_merge(onw.ip, onw.ip_end))
				ELSE
					CONCAT(SPLIT_PART(onw.ip::VARCHAR,'/', 1), '-', SPLIT_PART(onw.ip_end::VARCHAR, '/', 1))
				END
		END AS matching_ip,
		'destination' AS match_in,
		ow.recert_interval, met.rule_last_certified, met.rule_last_certifier
	FROM v_active_access_allow_rules r
	LEFT JOIN rule_to rt ON (r.rule_id=rt.rule_id)
	LEFT JOIN objgrp_flat of ON (rt.obj_id=of.objgrp_flat_id)
	LEFT JOIN object o ON (of.objgrp_flat_member_id=o.obj_id)
	LEFT JOIN owner_network onw ON (onw.ip_end >= o.obj_ip AND onw.ip <= o.obj_ip_end)
	LEFT JOIN owner ow ON (onw.owner_id=ow.id)
	LEFT JOIN rule_metadata met ON (r.rule_uid=met.rule_uid)
	WHERE r.rule_id NOT IN (SELECT distinct rwo.rule_id FROM v_rule_with_rule_owner rwo) AND
	CASE
		when (select mode from v_rule_ownership_mode) = 'exclusive' then (NOT o.obj_ip IS NULL) AND o.obj_ip NOT IN (select * from v_excluded_dst_ips)
		else NOT o.obj_ip IS NULL
	END
	GROUP BY r.rule_id, o.obj_ip, o.obj_ip_end, onw.ip, onw.ip_end, ow.id, ow.name, met.rule_last_certified, met.rule_last_certifier;


ALTER TABLE rule_metadata DROP COLUMN IF EXISTS "rulebase_id";
ALTER TABLE rule_metadata DROP COLUMN IF EXISTS "dev_id";

-----------------------------------------------
-- bulid rule-rulebase graph
-- rules may spawn rulebase children with new link table rulebase_link

Create table IF NOT EXISTS "stm_link_type"
(
	"id" SERIAL primary key,
	"name" Varchar NOT NULL
);

Create table IF NOT EXISTS "rulebase_link"
(
	"id" SERIAL primary key,
	"gw_id" Integer,
	"from_rulebase_id" Integer, -- either from_rulebase_id or from_rule_id must be SET or the is_initial flag
	"from_rule_id" BIGINT,
	"to_rulebase_id" Integer NOT NULL,
	"link_type" Integer,
	"is_initial" BOOLEAN DEFAULT FALSE,
	"is_global" BOOLEAN DEFAULT FALSE,
	"created" BIGINT,
	"removed" BIGINT
);

-- only for developers who already have on old 9.0 database:
Alter table "rulebase_link" add column IF NOT EXISTS "is_initial" BOOLEAN;
Alter table "rulebase_link" add column IF NOT EXISTS "is_global" BOOLEAN;
Alter table "rulebase_link" add column IF NOT EXISTS "from_rulebase_id" Integer;

DO $$
BEGIN
  IF EXISTS (
    SELECT 1
    FROM information_schema.columns
    WHERE table_name = 'rulebase_link'
      AND column_name = 'from_rule_id'
      AND data_type = 'integer'
  ) THEN
    Alter table "rulebase_link" alter column "from_rule_id" TYPE bigint;
  END IF;
END
$$;
---

Alter table "rulebase_link" drop constraint IF EXISTS "fk_rulebase_link_from_rulebase_id";
Alter table "rulebase_link" add constraint "fk_rulebase_link_from_rulebase_id" foreign key ("from_rulebase_id") references "rulebase" ("id") on update restrict on delete cascade;
Alter table "rulebase_link" drop constraint IF EXISTS "fk_rulebase_link_to_rulebase_id";
Alter table "rulebase_link" add constraint "fk_rulebase_link_to_rulebase_id" foreign key ("to_rulebase_id") references "rulebase" ("id") on update restrict on delete cascade;
Alter table "rulebase_link" drop constraint IF EXISTS "fk_rulebase_link_from_rule_id";
Alter table "rulebase_link" add constraint "fk_rulebase_link_from_rule_id" foreign key ("from_rule_id") references "rule" ("rule_id") on update restrict on delete cascade;
Alter table "rulebase_link" drop constraint IF EXISTS "fk_rulebase_link_link_type";
Alter table "rulebase_link" add constraint "fk_rulebase_link_link_type" foreign key ("link_type") references "stm_link_type" ("id") on update restrict on delete cascade;
Alter table "rulebase_link" drop constraint IF EXISTS "fk_rulebase_link_gw_id";
Alter table "rulebase_link" add constraint "fk_rulebase_link_gw_id" foreign key ("gw_id") references "device" ("dev_id") on update restrict on delete cascade;
Alter table "rulebase_link" drop constraint IF EXISTS "unique_rulebase_link";
Alter table "rulebase_link" add CONSTRAINT unique_rulebase_link
	UNIQUE (
	"gw_id",
	"from_rulebase_id",
	"from_rule_id",
	"to_rulebase_id",
	"created"
	);
    
ALTER TABLE "rulebase_link"
    DROP CONSTRAINT IF EXISTS "fk_rulebase_link_created_import_control_control_id" CASCADE;
Alter table "rulebase_link" add CONSTRAINT fk_rulebase_link_created_import_control_control_id 
	foreign key ("created") references "import_control" ("control_id") on update restrict on delete cascade;
ALTER TABLE "rulebase_link"
    DROP CONSTRAINT IF EXISTS "fk_rulebase_link_removed_import_control_control_id" CASCADE;
Alter table "rulebase_link" add CONSTRAINT fk_rulebase_link_removed_import_control_control_id 
	foreign key ("removed") references "import_control" ("control_id") on update restrict on delete cascade;

insert into stm_link_type (id, name) VALUES (2, 'ordered') ON CONFLICT DO NOTHING;
insert into stm_link_type (id, name) VALUES (3, 'inline') ON CONFLICT DO NOTHING;
delete from stm_link_type where name in ('initial','global','local','section'); -- initial and global/local are additional flags now

-- TODO delete all rule.parent_rule_id and rule.parent_rule_type, always = None so far
    -- migration plan:
    -- 1) create rulebases without rules (derive from device table)
    -- 2) set rule.rulebase_id to reference the correct rulebase
    -- 3) set not null constratint for rule.rulebase_id
    -- 4) do we really dare to delete duplicate rules here? yes, we should.
    -- 5) after upgrade start import
    -- TODO: deal with global policies --> move them to the global mgm_id

CREATE OR REPLACE FUNCTION deleteDuplicateRulebases() RETURNS VOID
    LANGUAGE plpgsql
    VOLATILE
AS $function$
    BEGIN
        -- TODO: make sure that we have at least one rulebase for each device
        DELETE FROM rule WHERE rulebase_id IS NULL;
    END;
$function$;

-- get latest import id for this management

-- needs to be rewritten to rulebase_link
CREATE OR REPLACE FUNCTION addRuleEnforcedOnGatewayEntries() RETURNS VOID
    LANGUAGE plpgsql
    VOLATILE
AS $function$
    DECLARE
        r_rulebase RECORD;
        r_rule RECORD;
        r_dev_null RECORD;
        i_dev_id INTEGER;
        a_all_dev_ids_of_rulebase INTEGER[];
        a_target_gateways VARCHAR[];
        v_gw_name VARCHAR;
    BEGIN
        FOR r_rulebase IN 
            SELECT * FROM rulebase
        LOOP
            -- collect all device ids for this rulebase
            SELECT ARRAY(
                SELECT gw_id FROM rulebase_link
                WHERE to_rulebase_id=r_rulebase.id
            ) INTO a_all_dev_ids_of_rulebase;

            FOR r_rule IN 
                SELECT rule_installon, rule_id FROM rule
            LOOP
                -- depending on install_on field:
                --     add enry for all gateways of the management
                --     or just add specific gateway entries
                IF r_rule.rule_installon='Policy Targets' THEN
                    -- need to find out other platforms equivivalent keywords
                    FOREACH i_dev_id IN ARRAY a_all_dev_ids_of_rulebase 
                    LOOP
                        INSERT INTO rule_enforced_on_gateway (rule_id, dev_id, created) 
                        VALUES (r_rule.rule_id, i_dev_id, (SELECT * FROM get_last_import_id_for_mgmt(r_rulebase.mgm_id)));
                    END LOOP;
                ELSE
                    -- need to deal with entries separately - split rule_installon field by '|'
                    IF r_rule.rule_installon IS NULL THEN
                        r_rule.rule_installon := 'Policy Targets';
                    END IF;
                    SELECT ARRAY(
                        SELECT string_to_array(r_rule.rule_installon, '|')
                    ) INTO a_target_gateways;
                    FOREACH v_gw_name IN ARRAY a_target_gateways 
                    LOOP
                        -- get dev_id for gw_name
                        SELECT INTO i_dev_id dev_id FROM device WHERE dev_name=v_gw_name;
                        IF FOUND THEN
                            INSERT INTO rule_enforced_on_gateway (rule_id, dev_id, created) 
                            VALUES (r_rule.rule_id, i_dev_id, (SELECT * FROM get_last_import_id_for_mgmt(r_rulebase.mgm_id))); 
                        ELSE
                            -- decide what to do with misses
                        END IF;
                    END LOOP;
                END IF;
            END LOOP;
        END LOOP;
    END;
$function$;

CREATE OR REPLACE FUNCTION addMetadataRulebaseEntries() RETURNS VOID
    LANGUAGE plpgsql
    VOLATILE
AS $function$
    DECLARE
        r_dev RECORD;
    BEGIN
        FOR r_dev IN 
            -- TODO: deal with global rulebases here
            SELECT d.dev_id, rb.id as rulebase_id FROM device d LEFT JOIN rulebase rb ON (d.local_rulebase_name=rb.name)
        LOOP
            UPDATE rule_metadata SET rulebase_id=r_dev.rulebase_id WHERE rule_metadata.dev_id=r_dev.dev_id;
        END LOOP;
        -- now we can add the "not null" constraint for rule_metadata.rulebase_id
        IF EXISTS (
            SELECT 1 
            FROM information_schema.columns
            WHERE table_name = 'rule_metadata' 
            AND column_name = 'rulebase_id'
            AND is_nullable = 'YES'
        ) THEN
            ALTER TABLE rule_metadata
            ALTER COLUMN rulebase_id SET NOT NULL;
        END IF;
    END;
$function$;

-- TODO: set created to current import id
CREATE OR REPLACE FUNCTION addRulebaseLinkEntries() RETURNS VOID
    LANGUAGE plpgsql
    VOLATILE
AS $function$
    DECLARE
        r_dev RECORD;
        r_dev_null RECORD;
        i_rulebase_id INTEGER;
        i_initial_rulebase_id INTEGER;
    BEGIN
        FOR r_dev IN 
            SELECT * FROM device
        LOOP
            -- find the id of the matching rulebase
            SELECT INTO i_rulebase_id id FROM rulebase WHERE name=r_dev.local_rulebase_name AND mgm_id=r_dev.mgm_id;
            -- check if rulebase_link already exists
            IF i_rulebase_id IS NOT NULL THEN
                SELECT INTO r_dev_null * FROM rulebase_link WHERE to_rulebase_id=i_rulebase_id AND gw_id=r_dev.dev_id AND removed IS NULL;
                IF NOT FOUND THEN
                    INSERT INTO rulebase_link (gw_id, from_rule_id, to_rulebase_id, created, link_type, is_initial) 
                    VALUES (r_dev.dev_id, NULL, i_rulebase_id, (SELECT * FROM get_last_import_id_for_mgmt(r_dev.mgm_id)), 2, True)
                    RETURNING id INTO i_initial_rulebase_id; -- when migrating, there cannot be more than one (the initial) rb per device
                END IF;
            END IF;

            -- global rulebase:
            -- find the id of the matching rulebase
            IF r_dev.global_rulebase_name IS NOT NULL THEN
                SELECT INTO i_rulebase_id id FROM rulebase WHERE name=r_dev.global_rulebase_name AND mgm_id=r_dev.mgm_id;
                -- check if rulebase_link already exists
                IF i_rulebase_id IS NOT NULL THEN
                    SELECT INTO r_dev_null * FROM rulebase_link WHERE to_rulebase_id=i_rulebase_id AND gw_id=r_dev.dev_id;
                    IF NOT FOUND THEN
                        INSERT INTO rulebase_link (gw_id, from_rule_id, to_rulebase_id, created, link_type, is_initial)
                        VALUES (r_dev.dev_id, NULL, i_rulebase_id, (SELECT * FROM get_last_import_id_for_mgmt(r_dev.mgm_id)), 2, TRUE); 
                    END IF;
                END IF;
            END IF;
        END LOOP;
    END;
$function$;

CREATE OR REPLACE FUNCTION addRulebaseEntriesAndItsRuleRefs() RETURNS VOID
    LANGUAGE plpgsql
    VOLATILE
AS $function$
    DECLARE
        r_dev RECORD;
        r_rule RECORD;
        r_dev_null RECORD;
        i_new_rulebase_id INTEGER;
    BEGIN

        FOR r_dev IN 
            SELECT * FROM device
        LOOP
            -- if rulebase does not exist yet: insert it
            SELECT INTO r_dev_null * FROM rulebase WHERE name=r_dev.local_rulebase_name;
            IF NOT FOUND AND r_dev.local_rulebase_name IS NOT NULL THEN
                -- first create rulebase entries
                INSERT INTO rulebase (name, uid, mgm_id, is_global, created) 
                VALUES (r_dev.local_rulebase_name, r_dev.local_rulebase_name, r_dev.mgm_id, FALSE, 1) 
                RETURNING id INTO i_new_rulebase_id;
                -- now update references in all rules to the newly created rulebase
                UPDATE rule SET rulebase_id=i_new_rulebase_id WHERE dev_id=r_dev.dev_id;
            END IF;

            SELECT INTO r_dev_null * FROM rulebase WHERE name=r_dev.global_rulebase_name;
            IF NOT FOUND AND r_dev.global_rulebase_name IS NOT NULL THEN
                INSERT INTO rulebase (name, uid, mgm_id, is_global, created) 
                VALUES (r_dev.global_rulebase_name, r_dev.global_rulebase_name, r_dev.mgm_id, TRUE, 1) 
                RETURNING id INTO i_new_rulebase_id;
                -- now update references in all rules to the newly created rulebase
                UPDATE rule SET rulebase_id=i_new_rulebase_id WHERE dev_id=r_dev.dev_id;
                -- add entries in rule_enforced_on_gateway
            END IF;
        END LOOP;

        -- now check for remaining rules without rulebase_id 
        -- TODO: decide how to deal with this - ONLY DUMMY SOLUTION FOR NOW
        FOR r_rule IN 
            SELECT * FROM rule WHERE rulebase_id IS NULL
            -- how do we deal with this? we simply pick the smallest rulebase id for now
        LOOP
            SELECT INTO i_new_rulebase_id id FROM rulebase ORDER BY id LIMIT 1;
            UPDATE rule SET rulebase_id=i_new_rulebase_id WHERE rule_id=r_rule.rule_id;
        END LOOP;

        -- now we can add the "not null" constraint for rule.rulebase_id
        IF EXISTS (
            SELECT 1 
            FROM information_schema.columns
            WHERE table_name = 'rule' 
            AND column_name = 'rulebase_id'
            AND is_nullable = 'YES'
        ) THEN
            ALTER TABLE rule
            ALTER COLUMN rulebase_id SET NOT NULL;
        END IF;
    END;
$function$;

-- in this migration, in scenarios where a rulebase is used on more than one gateway, 
-- only the rules of the first gw get a rulebase_id, the others (copies) will be deleted
CREATE OR REPLACE FUNCTION migrateToRulebases() RETURNS VOID
    LANGUAGE plpgsql
    VOLATILE
AS $function$
    BEGIN

        PERFORM addRulebaseEntriesAndItsRuleRefs();
        PERFORM addRulebaseLinkEntries();
        -- danger zone: delete all rules that have no rulebase_id
        -- the deletion might take some time
        PERFORM deleteDuplicateRulebases();
        -- PERFORM addMetadataRulebaseEntries(); -- this does not work as we just removed the dev_id column from rule_metadata
        -- add entries in rule_enforced_on_gateway
        PERFORM addRuleEnforcedOnGatewayEntries();
    END;
$function$;

SELECT * FROM migrateToRulebases();

-- rewrite get_rulebase_for_owner to work with rulebase instead of device
CREATE OR REPLACE FUNCTION public.get_rulebase_for_owner(rulebase_row rulebase, ownerid integer)
 RETURNS SETOF rule
 LANGUAGE plpgsql
 STABLE
AS 
$function$
    BEGIN
        RETURN QUERY
        SELECT r.* FROM rule r
            LEFT JOIN rule_from rf ON (r.rule_id=rf.rule_id)
            LEFT JOIN objgrp_flat rf_of ON (rf.obj_id=rf_of.objgrp_flat_id)
            LEFT JOIN object rf_o ON (rf_of.objgrp_flat_member_id=rf_o.obj_id)
            LEFT JOIN owner_network ON
            (ip_ranges_overlap(rf_o.obj_ip, rf_o.obj_ip_end, ip, ip_end, rf.negated != r.rule_src_neg))
        WHERE r.rulebase_id = rulebase_row.id AND owner_id = ownerid AND rule_head_text IS NULL
        UNION
        SELECT r.* FROM rule r
            LEFT JOIN rule_to rt ON (r.rule_id=rt.rule_id)
            LEFT JOIN objgrp_flat rt_of ON (rt.obj_id=rt_of.objgrp_flat_id)
            LEFT JOIN object rt_o ON (rt_of.objgrp_flat_member_id=rt_o.obj_id)
            LEFT JOIN owner_network ON
            (ip_ranges_overlap(rt_o.obj_ip, rt_o.obj_ip_end, ip, ip_end, rt.negated != r.rule_dst_neg))
        WHERE r.rulebase_id = rulebase_row.id AND owner_id = ownerid AND rule_head_text IS NULL
        ORDER BY rule_name;
    END;
$function$;

-- drop only after migration

ALTER TABLE rule DROP CONSTRAINT IF EXISTS rule_dev_id_fkey;

ALTER TABLE "rule_metadata" DROP CONSTRAINT IF EXISTS "rule_metadata_rulebase_id_f_key" CASCADE;
-- Alter table "rule_metadata" add constraint "rule_metadata_rulebase_id_f_key"
--   foreign key ("rulebase_id") references "rulebase" ("id") on update restrict on delete cascade;
-- ALTER TABLE "rule_metadata" DROP CONSTRAINT IF EXISTS "rule_metadata_alt_key" CASCADE;
-- Alter Table "rule_metadata" add Constraint "rule_metadata_alt_key" UNIQUE ("rule_uid","dev_id","rulebase_id");

-- reverse last_seen / removed logic for objects
ALTER TABLE "object" ADD COLUMN IF NOT EXISTS "removed" BIGINT;
ALTER TABLE "objgrp" ADD COLUMN IF NOT EXISTS "removed" BIGINT;
ALTER TABLE "objgrp_flat" ADD COLUMN IF NOT EXISTS "removed" BIGINT;
ALTER TABLE "service" ADD COLUMN IF NOT EXISTS "removed" BIGINT;
ALTER TABLE "svcgrp" ADD COLUMN IF NOT EXISTS "removed" BIGINT;
ALTER TABLE "svcgrp_flat" ADD COLUMN IF NOT EXISTS "removed" BIGINT;
ALTER TABLE "zone" ADD COLUMN IF NOT EXISTS "removed" BIGINT;
ALTER TABLE "usr" ADD COLUMN IF NOT EXISTS "removed" BIGINT;
ALTER TABLE "usergrp" ADD COLUMN IF NOT EXISTS "removed" BIGINT;
ALTER TABLE "usergrp_flat" ADD COLUMN IF NOT EXISTS "removed" BIGINT;
ALTER TABLE "rule" ADD COLUMN IF NOT EXISTS "removed" BIGINT;
ALTER TABLE "rule_from" ADD COLUMN IF NOT EXISTS "removed" BIGINT;
ALTER TABLE "rule_to" ADD COLUMN IF NOT EXISTS "removed" BIGINT;
ALTER TABLE "rule_service" ADD COLUMN IF NOT EXISTS "removed" BIGINT;
ALTER TABLE "rule_enforced_on_gateway" ADD COLUMN IF NOT EXISTS "removed" BIGINT;
ALTER TABLE "rulebase" ADD COLUMN IF NOT EXISTS "removed" BIGINT;
ALTER TABLE "rulebase_link" ADD COLUMN IF NOT EXISTS "removed" BIGINT;

-- add obj type access-role for cp import
INSERT INTO stm_obj_typ (obj_typ_id,obj_typ_name) VALUES (21,'access-role') ON CONFLICT DO NOTHING;

-- adding labels (simple version without mapping tables and without foreign keys)

-- CREATE TABLE label (
--     id SERIAL PRIMARY KEY,
--     name TEXT NOT NULL
-- );

-- ALTER TABLE "rule" ADD COLUMN IF NOT EXISTS "labels" INT[];
-- ALTER TABLE "service" ADD COLUMN IF NOT EXISTS "labels" INT[];
-- ALTER TABLE "object" ADD COLUMN IF NOT EXISTS "labels" INT[];
-- ALTER TABLE "usr" ADD COLUMN IF NOT EXISTS "labels" INT[];


-- ALTER TABLE "object" DROP COLUMN IF EXISTS "deleted" ;
-- ALTER TABLE "objgrp" DROP COLUMN IF  EXISTS "deleted" ;
-- ALTER TABLE "objgrp_flat" DROP COLUMN IF EXISTS "deleted" ;
-- ALTER TABLE "service" DROP COLUMN IF EXISTS "deleted" ;
-- ALTER TABLE "svcgrp" DROP COLUMN IF  EXISTS "deleted" ;
-- ALTER TABLE "svcgrp_flat" DROP  COLUMN IF EXISTS "deleted" ;
-- ALTER TABLE "zone" DROP COLUMN IF EXISTS "deleted" ;
-- ALTER TABLE "usr" DROP COLUMN IF EXISTS  "deleted" ;
-- ALTER TABLE "usergrp" DROP COLUMN IF EXISTS "deleted" ;
-- ALTER TABLE "usergrp_flat" DROP COLUMN IF EXISTS "deleted" ;
-- ALTER TABLE "rule" DROP COLUMN IF EXISTS "deleted" ;
-- ALTER TABLE "rule_from" DROP COLUMN IF EXISTS "deleted" ;
-- ALTER TABLE "rule_to" DROP COLUMN IF EXISTS "deleted" ;
-- ALTER TABLE "rule_service" DROP COLUMN IF EXISTS "deleted" ;
-- ALTER TABLE "rule_enforced_on_gateway" DROP COLUMN IF EXISTS "deleted";
-- ALTER TABLE "rulebase" DROP COLUMN IF EXISTS "deleted";
-- ALTER TABLE "rulebase_link" DROP COLUMN IF EXISTS "deleted";
-- TODO: fill all rulebase_id s and then add not null constraint


--   TODOs 

-- Rename table rulebase_on_gateways to gateway_rulebase to get correct plural gateway_rulebases in hasura

-- REPORTING:
--     - RulesReportRazor line 23: deal with multiple ordered rulebases (later)
--         style="font-size:small" TableClass="table table-bordered table-sm th-bg-secondary table-responsive overflow-auto sticky-header" TableItem="Rule" Items="device.Rules" ShowSearchBar="false"
--     - ObjectGroup.Razor line 431:
--         Rule? ruleUpdated = managementsUpdate.ManagementData.SelectMany(m => m.Devices).SelectMany(d => d.Rulebases[0].Rulebase.Rules ?? new Rule[0]).FirstOrDefault();
--     - Statistics Report #rules per device are 0 (device report is null)
--     - recertification: without rule.dev_id we have lost all rule information in report!!!
--       - certification information should be aimed at a rule on a gateway
--       - this might lead to a rulebase which is enforced on multiple gateways to be changed only in
--         the rule_enforced_on_gateway field
--       - how do we get the link between rule_metadata (cert) and the actual rule details?
--         --> can we add a fk from rule_metadatum to rulebase to fix this?
--             query rulesReport($limit: Int, $offset: Int, $mgmId: [Int!], $relevantImportId: bigint, $cut: timestamp, $tolerance: timestamp) {
--             management(where: {hide_in_gui: {_eq: false}, mgm_id: {_in: $mgmId}, stm_dev_typ: {dev_typ_is_multi_mgmt: {_eq: false}, is_pure_routing_device: {_eq: false}}}, order_by: {mgm_name: asc}) {
--                 id: mgm_id
--                 name: mgm_name
--                 devices(where: {hide_in_gui: {_eq: false}}) {
--                 id: dev_id
--                 rule_metadata {
--                     rule_last_hit
--                     rule_uid
--                     dev_id
--                     # here we do not have any rule details 
--                 }
--                 name: dev_name
--                 rulebase_on_gateways(order_by: {order_no: asc}) {
--                     rulebase_id
--                     order_no
--                     rulebase {
--                     id
--                     name
--                     rules {
--                         mgm_id: mgm_id
--                         rule_metadatum {
--                             # here, the rule_metadata is always empty! 
--                             rule_last_hit
--                         }
--                         ...ruleOverview
--                     }
--                     }
--                 }
--                 }
--             }
--             }
--     - need to enhance certifications (add dev_id?)

-- - make sure that xlate rules get unique UIDs
-- - with each major version released:
--     add fwo version to demo config files on fwodemo to ensure all versions can be served

-- - add install on column to the following reports:
--     - recert
--     - change (all 3)
--     - statistics (optional: only count rules per gw which are active on gw)

--  - adjust report tests (add column)
--  import install on information (need to find out, where it is encoded) from 
--  - fortimanger - simply add name of current gw?
--  - fortios - simply add name of current gw?
--  - others? - simply add name of current gw?

-- importer cp get changes:
--     {'uid': 'cf8c7582-fd95-464c-81a0-7297df3c5ad9', 'type': 'access-rule', 'domain': {'uid': '41e821a0-3720-11e3-aa6e-0800200c9fde', 'name': 'SMC User', 'domain-type': 'domain'}, 'position': 7, 'track': {'type': {...}, 'per-session': False, 'per-connection': False, 'accounting': False, 'enable-firewall-session': False, 'alert': 'none'}, 'layer': '0f45100c-e4ea-4dc1-bf22-74d9d98a4811', 'source': [{...}], 'source-negate': False, 'destination': [{...}], 'destination-negate': False, 'service': [{...}], 'service-negate': False, 'service-resource': '', 'vpn': [{...}], 'action': {'uid': '6c488338-8eec-4103-ad21-cd461ac2c472', 'name': 'Accept', 'type': 'RulebaseAction', 'domain': {...}, 'color': 'none', 'meta-info': {...}, 'tags': [...], 'icon': 'Actions/actionsAccept', 'comments': 'Accept', 'display-name': 'Accept', 'customFields': None}, 'action-settings': {'enable-identity-captive-portal': False}, 'content': [{...}], 'content-negate': False, 'content-direction': 'any', 'time': [{...}], 'custom-fields': {'field-1': '', 'field-2': '', 'field-3': ''}, 'meta-info': {'lock': 'unlocked', 'validation-state': 'ok', 'last-modify-time': {...}, 'last-modifier': 'tim-admin', 'creation-time': {...}, 'creator': 'tim-admin'}, 'comments': '', 'enabled': True, 'install-on': [{...}], 'available-actions': {'clone': 'not_supported'}, 'tags': []}

-- - change (cp) importer to read rulebases and mappings from rulebase to device
--   - each rule is only stored once
--   - each rulebase is only stored once
-- --- global changes ----
-- - allow conversion from new to old format (would lose information when working with rulebases)
-- - allow conversion from old to new format (only for simple setups with 1:1 gw to rulebase matches

-- Cleanups (after cp importer works with all config variants):
-- - re-add users (cp),check ida rules - do we have networks here?
--         #parse_users_from_rulebases(full_config, full_config['rulebases'], full_config['users'], config2import, current_import_id)
--         --> replace by api call?
-- - re-add config splits
-- - add the following functions to all modules:
--     - getNativeConfig
--     - normalizeConfig
--     - getNormalizedConfig (a combination of the two above)
-- - re-add global / domain policies
-- - update all importers:
--    - fortimanager
--    - azure
--    - cisco firepower
--    - Palo
--    - NSX
--    - Azure
--    - legacy?
--      - netscreen?!
--      - barracuda

-- can we get everything working with old config format? no!

-- optimization: add mgm_id to all tables like objgrp, ... ?

-- disabled in UI:
--     recertification.razor
--     in report.razor:
--     - RSB 
--     - TicketCreate Komponente

-- 2024-10-09 planning
-- - calculate rule_num_numeric
-- - config mapping gateway to rulebase(s)
--     - do not store/use any rulebase names in device table
--     - instead get current config with every import
--     - id for gateway needs to be fixated:

--     - check point: 
--         - read interface information from show-gateways-and-servers details-level=full
--         - where to get routing infos?
--         - optional: also get publish time per policy (push):
--             "publish-time" : {
--                     "posix" : 1727978692716,
--                     "iso-8601" : "2024-10-03T20:04+0200"
--                 },
--             filter out vswitches?

--     - goal:
--         - in device table:
--             - for CP only save policy-name per gateway (gotten from show-gateways-and-servers
--         - in config file storage: 
--             - store all policies with the management rathen than with the gateway?
--             - per gateway only store the ordered mapping gw --> policies
--                 - also allow for mapping a gateway to a policy from the manager's super-manager

--     - TODO: set is_super_manager flag = true for MDS 

-- {
--   "ConfigFormat": "NORMALIZED",
--   "ManagerSet": [ 
--     {
--       "ManagerUid": "6ae3760206b9bfbd2282b5964f6ea07869374f427533c72faa7418c28f7a77f2",
--       "ManagerName": "schting2",
--       "IsGlobal": false,
--       "DependantManagerUids": [],
--       "Configs": [
--         {
--           "ConfigFormat": "NORMALIZED_LEGACY",
--           "action": "INSERT",
--           "rules": [
--             {
--               "Uid": "FirstLayer shared with inline layer",
--               "Name": "FirstLayer shared with inline layer",
--               "Rules": {
--                 "828b0f42-4b18-4352-8bdf-c9c864d692eb": {
--             }
--           ],
--           "gateways": [
--                 Uid: str
--                 Name: str
--                 Routing: List[dict] = []
--                 Interfaces: List[dict]  = []
--                 # GlobalPolicyUid: Optional[str] = None
--                 "EnforcedPolicyUids": [
--                     "<super-manager-UID>:<super-manager-start-policy-UID>",
--                     "FirstLayer shared with inline layer",
--                     "second-layer",
--                     "<super-manager-UID>:<super-manager-final-policy-UID>",
--                 ]
--                 EnforcedNatPolicyUids: List[str] = []          
--           ]
--         }
--       ]
--     }
--   ]
-- }


-- - config mapping global start rb, local rb, global end rb
-- - import inline layers
-- - get reports working
-- - valentin: open issues for k01 UI problems
-- - decide how to implement ordered layer (all must match) vs. e.g. global policies (first match)
-- - allow for also importing native configs from file 


-- TODOs after full importer migration
-- -- ALTER table "import_config" DROP COLUMN IF EXISTS "chunk_number";
-- -- ALTER TABLE "rule" DROP COLUMN IF EXISTS "rule_installon"; -- here we would need to rebuild views
-- -- ALTER TABLE "rule" DROP COLUMN IF EXISTS "rule_ruleid"; -- here we would need to rebuild views
-- -- ALTER TABLE "rule" DROP COLUMN IF EXISTS "dev_id"; -- final step when the new structure works
-- -- ALTER TABLE "import_rule" DROP COLUMN IF EXISTS "rulebase_name";

-- ALTER TABLE "object" DROP COLUMN IF EXISTS "obj_last_seen";
-- ALTER TABLE "objgrp" DROP COLUMN IF EXISTS "objgrp_last_seen";
-- ALTER TABLE "objgrp_flat" DROP COLUMN IF EXISTS "objgrp_flat_last_seen";
-- ALTER TABLE "service" DROP COLUMN IF EXISTS "svc_last_seen";
-- ALTER TABLE "svcgrp" DROP COLUMN IF EXISTS "svcgrp_last_seen";
-- ALTER TABLE "svcgrp_flat" DROP COLUMN IF EXISTS "svcgrp_flat_last_seen";
-- ALTER TABLE "zone" DROP COLUMN IF EXISTS "zone_last_seen";
-- ALTER TABLE "usr" DROP COLUMN IF EXISTS "user_last_seen";
-- ALTER TABLE "rule" DROP COLUMN IF EXISTS "rule_last_seen";
-- ALTER TABLE "rule_from" DROP COLUMN IF EXISTS "rf_last_seen";
-- ALTER TABLE "rule_to" DROP COLUMN IF EXISTS "rt_last_seen";
-- ALTER TABLE "rule_service" DROP COLUMN IF EXISTS "rs_last_seen";
