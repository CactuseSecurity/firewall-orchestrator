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
	IF (SELECT 1 FROM ldap_connection WHERE ldap_server = serverName LIMIT 1) IS NULL 
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

-- 8.8.3
ALTER TABLE modelling.connection ADD COLUMN IF NOT EXISTS requested_on_fw boolean default false;
ALTER TABLE modelling.connection ADD COLUMN IF NOT EXISTS removed boolean default false;
ALTER TABLE modelling.connection ADD COLUMN IF NOT EXISTS removal_date timestamp;
UPDATE modelling.connection SET requested_on_fw=true WHERE NOT requested_on_fw;

-- 8.8.4
insert into stm_action (action_id,action_name) VALUES (30,'ask') ON CONFLICT DO NOTHING; -- cp

-- 8.8.5
ALTER TYPE rule_field_enum ADD VALUE IF NOT EXISTS 'modelled_source';
ALTER TYPE rule_field_enum ADD VALUE IF NOT EXISTS 'modelled_destination';

-- 8.8.6
insert into stm_track (track_id,track_name) VALUES (23,'detailed log') ON CONFLICT DO NOTHING; -- check point R8x
insert into stm_track (track_id,track_name) VALUES (24,'extended log') ON CONFLICT DO NOTHING; -- check point R8x

-- 8.8.8
DO $$ 
BEGIN
    IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'fwo_ro') THEN
        CREATE ROLE fwo_ro WITH LOGIN NOSUPERUSER INHERIT NOCREATEDB NOCREATEROLE;
    END IF;
END
$$;


GRANT CONNECT ON DATABASE fworchdb TO fwo_ro;

GRANT USAGE ON SCHEMA compliance TO fwo_ro;
GRANT SELECT ON ALL TABLES IN SCHEMA compliance TO fwo_ro;
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA compliance TO fwo_ro;
ALTER DEFAULT PRIVILEGES IN SCHEMA compliance GRANT SELECT ON TABLES TO fwo_ro;
ALTER DEFAULT PRIVILEGES IN SCHEMA compliance GRANT USAGE, SELECT ON SEQUENCES TO fwo_ro;

GRANT USAGE ON SCHEMA modelling TO fwo_ro;
GRANT SELECT ON ALL TABLES IN SCHEMA modelling TO fwo_ro;
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA modelling TO fwo_ro;
ALTER DEFAULT PRIVILEGES IN SCHEMA modelling GRANT SELECT ON TABLES TO fwo_ro;
ALTER DEFAULT PRIVILEGES IN SCHEMA modelling GRANT USAGE, SELECT ON SEQUENCES TO fwo_ro;

GRANT USAGE ON SCHEMA public TO fwo_ro;
GRANT SELECT ON ALL TABLES IN SCHEMA public TO fwo_ro;
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO fwo_ro;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT SELECT ON TABLES TO fwo_ro;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT USAGE, SELECT ON SEQUENCES TO fwo_ro;

GRANT USAGE ON SCHEMA request TO fwo_ro;
GRANT SELECT ON ALL TABLES IN SCHEMA request TO fwo_ro;
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA request TO fwo_ro;
ALTER DEFAULT PRIVILEGES IN SCHEMA request GRANT SELECT ON TABLES TO fwo_ro;
ALTER DEFAULT PRIVILEGES IN SCHEMA request GRANT USAGE, SELECT ON SEQUENCES TO fwo_ro;

-- 8.8.9

ALTER TABLE recertification ADD column IF NOT EXISTS owner_recert_id BIGINT;

create table if not exists owner_recertification
(
    id BIGSERIAL PRIMARY KEY,
    owner_id int NOT NULL,
    user_dn varchar,
    recertified boolean default false,
    recert_date Timestamp,
    comment varchar,
    next_recert_date Timestamp
);

create table if not exists notification
(
    id SERIAL PRIMARY KEY,
	notification_client Varchar,
	user_id int,
	owner_id int,
	channel Varchar,
	recipient_to Varchar,
    email_address_to Varchar,
	recipient_cc Varchar,
	email_address_cc Varchar,
	email_subject Varchar,
	layout Varchar,
	deadline Varchar,
	interval_before_deadline int,
	offset_before_deadline int,
	repeat_interval_after_deadline int,
	repeat_offset_after_deadline int,
	repetitions_after_deadline int,
	last_sent Timestamp
);

alter table notification drop constraint if exists notification_owner_foreign_key;
ALTER TABLE notification ADD CONSTRAINT notification_owner_foreign_key FOREIGN KEY (owner_id) REFERENCES owner(id) ON UPDATE RESTRICT ON DELETE CASCADE;
alter table notification drop constraint if exists notification_user_foreign_key;
ALTER TABLE notification ADD CONSTRAINT notification_user_foreign_key FOREIGN KEY (user_id) REFERENCES uiuser(uiuser_id) ON UPDATE RESTRICT ON DELETE CASCADE;

alter table owner add column if not exists last_recertified Timestamp;
alter table owner add column if not exists last_recertifier int;
alter table owner add column if not exists last_recertifier_dn Varchar;
alter table owner add column if not exists next_recert_date Timestamp;

alter table owner drop constraint if exists owner_last_recertifier_uiuser_uiuser_id_f_key;
alter table owner add constraint owner_last_recertifier_uiuser_uiuser_id_f_key foreign key (last_recertifier) references uiuser (uiuser_id) on update restrict;

insert into config (config_key, config_value, config_user) VALUES ('modDecommEmailReceiver', 'None', 0) ON CONFLICT DO NOTHING;
insert into config (config_key, config_value, config_user) VALUES ('modDecommEmailSubject', '', 0) ON CONFLICT DO NOTHING;
insert into config (config_key, config_value, config_user) VALUES ('modDecommEmailBody', '', 0) ON CONFLICT DO NOTHING;

alter table report add column if not exists read_only Boolean default FALSE;

DO $$
BEGIN
  -- recertification.owner_recert_id → owner_recertification(id)
  IF NOT EXISTS (
    SELECT 1
    FROM pg_constraint c
    JOIN pg_class t ON t.oid = c.conrelid
    JOIN pg_namespace n ON n.oid = t.relnamespace
    WHERE c.conname = 'recertification_owner_recertification_foreign_key'
      AND t.relname = 'recertification'
      AND n.nspname = current_schema()
  ) THEN
    ALTER TABLE recertification
      ADD CONSTRAINT recertification_owner_recertification_foreign_key
      FOREIGN KEY (owner_recert_id)
      REFERENCES owner_recertification(id)
      ON UPDATE RESTRICT
      ON DELETE CASCADE;
  END IF;

  -- owner_recertification.owner_id → owner(id)
  IF NOT EXISTS (
    SELECT 1
    FROM pg_constraint c
    JOIN pg_class t ON t.oid = c.conrelid
    JOIN pg_namespace n ON n.oid = t.relnamespace
    WHERE c.conname = 'owner_recertification_owner_foreign_key'
      AND t.relname = 'owner_recertification'
      AND n.nspname = current_schema()
  ) THEN
    ALTER TABLE owner_recertification
      ADD CONSTRAINT owner_recertification_owner_foreign_key
      FOREIGN KEY (owner_id)
      REFERENCES owner(id)
      ON UPDATE RESTRICT
      ON DELETE CASCADE;
  END IF;
END
$$;

-- 8.9.1

alter table report add column if not exists owner_id Integer;

alter table report drop constraint if exists report_owner_foreign_key;
ALTER TABLE report ADD CONSTRAINT report_owner_foreign_key FOREIGN KEY (owner_id) REFERENCES owner(id) ON UPDATE RESTRICT ON DELETE CASCADE;

alter table owner_recertification add column if not exists report_id bigint;
alter table owner_recertification drop constraint if exists owner_recertification_report_foreign_key;
ALTER TABLE owner_recertification ADD CONSTRAINT owner_recertification_report_foreign_key FOREIGN KEY (report_id) REFERENCES report(report_id) ON UPDATE RESTRICT ON DELETE CASCADE;

alter table owner add column if not exists recert_active boolean default false;

alter table recertification add column if not exists owner_recert_id bigint;

alter table recertification drop constraint if exists recertification_owner_recertification_foreign_key;
ALTER TABLE recertification ADD CONSTRAINT recertification_owner_recertification_foreign_key FOREIGN KEY (owner_recert_id) REFERENCES owner_recertification(id) ON UPDATE RESTRICT ON DELETE CASCADE;

-- 8.9.2
CREATE TABLE if not exists owner_lifecycle_state (
    id SERIAL PRIMARY KEY,
    name Varchar NOT NULL
);

alter table owner add column if not exists owner_lifecycle_state_id int;

alter table owner drop constraint if exists owner_owner_lifecycle_state_foreign_key;
ALTER TABLE owner ADD CONSTRAINT owner_owner_lifecycle_state_foreign_key FOREIGN KEY (owner_lifecycle_state_id)REFERENCES owner_lifecycle_state(id) ON DELETE SET NULL;

-- changes to nw obj ip constraints
ALTER TABLE "object" DROP CONSTRAINT IF EXISTS "object_obj_ip_not_null" CASCADE;
ALTER TABLE "object" DROP CONSTRAINT IF EXISTS "object_obj_ip_end_not_null" CASCADE;

-- magic numbers here: 1 = host object, 3 = network object, 4 = range object
ALTER TABLE "object" ADD CONSTRAINT object_obj_ip_not_null CHECK (NOT (obj_ip IS NULL AND obj_typ_id IN (1, 3, 4)));
ALTER TABLE "object" ADD CONSTRAINT object_obj_ip_end_not_null CHECK (NOT (obj_ip_end IS NULL AND obj_typ_id IN (1, 3, 4)));

-- 8.9.2
CREATE TABLE if not exists owner_lifecycle_state (
    id SERIAL PRIMARY KEY,
    name Varchar NOT NULL
);

alter table owner add column if not exists owner_lifecycle_state_id int;

alter table owner drop constraint if exists owner_owner_lifecycle_state_foreign_key;
ALTER TABLE owner ADD CONSTRAINT owner_owner_lifecycle_state_foreign_key FOREIGN KEY (owner_lifecycle_state_id)REFERENCES owner_lifecycle_state(id) ON DELETE SET NULL;

-- v8.9.6

alter table notification add column if not exists initial_offset_after_deadline int;
alter table notification add column if not exists name Varchar;

------------------------------------------------------------------------------------

-- rename changes_found column to rule_changes_found in import_control table
DO $$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_name = 'import_control'
          AND column_name = 'changes_found'
    ) THEN
        EXECUTE 'ALTER TABLE import_control RENAME COLUMN changes_found TO rule_changes_found';
    END IF;
END
$$;

-- add any_changes_found column to import_control table
ALTER table "import_control"
    ADD COLUMN IF NOT EXISTS "any_changes_found" Boolean Default FALSE;


-- now set the any_changes_found column to true for all imports that have security relevant changes

DROP VIEW IF EXISTS view_imports_with_security_relevant_changes;

CREATE OR REPLACE VIEW view_imports_with_security_relevant_changes AS
    SELECT clr.control_id AS import_id, clr.mgm_id
    FROM changelog_rule clr
    WHERE clr.security_relevant

    UNION

    SELECT clo.control_id AS import_id, clo.mgm_id
    FROM changelog_object clo
    WHERE clo.security_relevant

    UNION

    SELECT cls.control_id AS import_id, cls.mgm_id
    FROM changelog_service cls
    WHERE cls.security_relevant

    UNION

    SELECT clu.control_id AS import_id, clu.mgm_id
    FROM changelog_user clu
    WHERE clu.security_relevant;


UPDATE import_control
SET any_changes_found = true
WHERE control_id IN (
    SELECT import_id
    FROM view_imports_with_security_relevant_changes
);

DROP VIEW IF EXISTS view_imports_with_security_relevant_changes;

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

ALTER TABLE "rulebase" DROP CONSTRAINT IF EXISTS "fk_rulebase_mgm_id" CASCADE;
Alter table "rulebase" add CONSTRAINT fk_rulebase_mgm_id foreign key ("mgm_id") references "management" ("mgm_id") on update restrict on delete cascade;

ALTER TABLE "rulebase" DROP CONSTRAINT IF EXISTS "unique_rulebase_mgm_id_name" CASCADE;
ALTER TABLE "rulebase" DROP CONSTRAINT IF EXISTS "unique_rulebase_mgm_id_uid" CASCADE;
ALTER TABLE "rulebase" DROP CONSTRAINT IF EXISTS "unique_rulebase_mgm_id_uid_removed" CASCADE;
ALTER TABLE "rulebase" DROP CONSTRAINT IF EXISTS "rulebase_uid_mgm_id_removed_key" CASCADE;
Alter table "rulebase" add CONSTRAINT rulebase_uid_mgm_id_removed_key UNIQUE ("mgm_id", "uid", "removed");
-----------------------------------------------

ALTER TABLE "management" ADD COLUMN IF NOT EXISTS "is_super_manager" BOOLEAN DEFAULT FALSE;
ALTER TABLE "rule" ADD COLUMN IF NOT EXISTS "is_global" BOOLEAN DEFAULT FALSE NOT NULL;
ALTER TABLE "rule" ADD COLUMN IF NOT EXISTS "rulebase_id" INTEGER;

-- permanent table for storing latest config to calc diffs
CREATE TABLE IF NOT EXISTS "latest_config" (
    "mgm_id" integer NOT NULL,
    "import_id" bigint NOT NULL,
    "config" jsonb NOT NULL,
    PRIMARY KEY ("mgm_id")
);


-- Drop old primary key if it exists
DO $$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conrelid = 'latest_config'::regclass
          AND contype = 'p'
    ) THEN
        ALTER TABLE latest_config DROP CONSTRAINT latest_config_pkey;
    END IF;
END $$;

-- Add new primary key on mgm_id if not already set
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conrelid = 'latest_config'::regclass
          AND contype = 'p'
          AND conkey = ARRAY[
              (SELECT attnum FROM pg_attribute
               WHERE attrelid = 'latest_config'::regclass
                 AND attname = 'mgm_id')
          ]
    ) THEN
        ALTER TABLE latest_config ADD CONSTRAINT latest_config_pkey PRIMARY KEY (mgm_id);
    END IF;
END $$;


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
      FROM rule r2
      WHERE rule_uid = current_rule_uid AND r2.mgm_id = mgmId AND active
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

-- removed logic for rule
ALTER TABLE "rule" ADD COLUMN IF NOT EXISTS "removed" BIGINT;

-----------------------------------------------
-- METADATA part
-- we are removing dev_id and rulebase_id from rule_metadata
-- even CP API does not provide this information regarding hits (the target parameter is ignored, so hits are returned per rule not per rule per gw)

Alter table "rule" drop constraint IF EXISTS "rule_metadata_dev_id_rule_uid_f_key";
Alter Table "rule_metadata" drop Constraint IF EXISTS "rule_metadata_alt_key";

ALTER TABLE rule_metadata DROP Constraint IF EXISTS "rule_metadata_rule_uid_unique" CASCADE;
Alter table "rule" DROP constraint IF EXISTS "rule_rule_metadata_rule_uid_f_key";

-- composite fk is added after rule_metadata.mgm_id is populated


-- rule_metadata add mgm_id + fk, drop constraint
ALTER TABLE rule_metadata ADD COLUMN IF NOT EXISTS mgm_id Integer;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 
        FROM pg_constraint
        WHERE conname = 'rule_metadata_mgm_id_management_id_fk'
    ) THEN
        ALTER TABLE rule_metadata
        ADD CONSTRAINT rule_metadata_mgm_id_management_id_fk
        FOREIGN KEY (mgm_id) REFERENCES management(mgm_id)
        ON UPDATE RESTRICT ON DELETE CASCADE;
    END IF;
END$$;

DO $$
DECLARE
    rec RECORD;
    v_do_not_import_true_count INT;
    v_do_not_import_false_count INT;
    missing_uids TEXT;
    too_many_mgm_ids_on_uid_and_no_resolve TEXT;
    all_errors_with_no_resolve TEXT := '';

BEGIN
--Check rule_metadata has entries in rule
    SELECT string_agg(rm.rule_uid::text, ', ')
    INTO missing_uids
    FROM rule_metadata rm
    LEFT JOIN rule r ON rm.rule_uid = r.rule_uid
    WHERE r.rule_uid IS NULL
      AND rm.mgm_id IS NULL;

    IF missing_uids IS NOT NULL THEN
        RAISE NOTICE 'Missing rule(s): %', missing_uids;
        DELETE FROM rule_metadata
            WHERE rule_uid IN (
                SELECT rm.rule_uid
                FROM rule_metadata rm
                LEFT JOIN rule r ON rm.rule_uid = r.rule_uid
                WHERE r.rule_uid IS NULL
                  AND rm.mgm_id IS NULL
        )
          AND mgm_id IS NULL;
    END IF;

    -- drop constraints
    ALTER TABLE rule DROP CONSTRAINT IF EXISTS rule_metadatum;
    ALTER TABLE rule DROP CONSTRAINT IF EXISTS rule_rule_metadata_rule_uid_f_key;
    ALTER TABLE rule_metadata DROP CONSTRAINT IF EXISTS rule_metadata_rule_uid_unique;

-- Start loop for rule_uid und mgm_id import/transfer
    FOR rec IN
        SELECT 
            rm.rule_uid,
            COUNT(DISTINCT r.mgm_id) AS mgm_count
        FROM rule_metadata rm
        JOIN rule r ON rm.rule_uid = r.rule_uid
        WHERE rm.mgm_id IS NULL
        GROUP BY rm.rule_uid
        HAVING COUNT(DISTINCT r.mgm_id) >= 1
    LOOP
        -- Case 1: exactly one mgm_id gefunden
        IF rec.mgm_count = 1 THEN
            --
            UPDATE rule_metadata rm
            SET mgm_id = r.mgm_id
            FROM rule r
            WHERE rm.rule_uid = r.rule_uid
              AND rm.mgm_id IS NULL
              AND rm.rule_uid = rec.rule_uid;

        -- Case 2: found more then two mgm_id found
        ELSIF rec.mgm_count >= 2 THEN
            -- Count flag "do_not_import" for rule_uid 
            SELECT 
			COUNT(*) FILTER (WHERE m.do_not_import IS TRUE),
			COUNT(*) FILTER (WHERE m.do_not_import IS FALSE)
			INTO v_do_not_import_true_count, v_do_not_import_false_count
			FROM rule r
			JOIN management m ON r.mgm_id = m.mgm_id
			WHERE r.rule_uid = rec.rule_uid;

            -- check if there is just 1 "do_not_import" = false
			IF v_do_not_import_false_count = 1 THEN
				UPDATE rule_metadata rm
					SET mgm_id = r.mgm_id
					FROM rule r
					JOIN management m ON r.mgm_id = m.mgm_id
					WHERE rm.rule_uid = r.rule_uid
					AND m.do_not_import IS FALSE
					AND rm.rule_uid = rec.rule_uid
					AND rm.mgm_id IS NULL;
					
			-- Warning: Not used mgm_ids where do_not_import=true
			RAISE NOTICE 'rule_uid % has % additional mgm_id(s) marked do_not_import=true: %', 
			rec.rule_uid, v_do_not_import_true_count,
				(SELECT string_agg(format('mgm_id=%s', r.mgm_id), ', ')
					FROM rule r
					JOIN management m ON r.mgm_id = m.mgm_id
					WHERE r.rule_uid = rec.rule_uid
					AND m.do_not_import IS TRUE);
					
			ELSE
				-- No resolve
				SELECT string_agg(
                       format('rule_uid=%s → mgm_id=%s (do_not_import=%s)', 
                              r.rule_uid, r.mgm_id, m.do_not_import),
                       E'\n'
					)
				INTO too_many_mgm_ids_on_uid_and_no_resolve
				FROM rule r
				JOIN management m ON r.mgm_id = m.mgm_id
				WHERE r.rule_uid = rec.rule_uid;	

				all_errors_with_no_resolve := all_errors_with_no_resolve || format(
                    E'\n\nrule_uid %s has ambiguous mgm_id assignments:\n%s',
                    rec.rule_uid,
                    too_many_mgm_ids_on_uid_and_no_resolve
                );
				
            END IF;
        END IF;
    END LOOP;
	
    IF all_errors_with_no_resolve <> '' THEN
        RAISE EXCEPTION 'Ambiguous mgm_id assignments detected:%s', all_errors_with_no_resolve;
    END IF;

    -- redo constraints
    ALTER TABLE rule_metadata ALTER COLUMN mgm_id SET NOT NULL;
        ALTER TABLE rule_metadata DROP CONSTRAINT IF EXISTS rule_metadata_rule_uid_unique;
        IF NOT EXISTS (
            SELECT 1
            FROM pg_constraint
            WHERE conname = 'rule_metadata_mgm_id_rule_uid_unique'
        ) THEN
            ALTER TABLE rule_metadata ADD CONSTRAINT rule_metadata_mgm_id_rule_uid_unique UNIQUE (mgm_id, rule_uid);
        END IF;
        ALTER TABLE rule DROP CONSTRAINT IF EXISTS rule_rule_metadata_rule_uid_f_key;
        ALTER TABLE rule DROP CONSTRAINT IF EXISTS rule_rule_metadata_mgm_id_rule_uid_f_key;
        ALTER TABLE rule ADD CONSTRAINT rule_rule_metadata_mgm_id_rule_uid_f_key
            FOREIGN KEY (mgm_id, rule_uid) REFERENCES rule_metadata (mgm_id, rule_uid)
            ON UPDATE RESTRICT ON DELETE CASCADE;
END$$;

-- rework rule_metadata timestamps to reference import_control and drop unused columns
ALTER TABLE IF EXISTS rule_metadata DROP CONSTRAINT IF EXISTS rule_metadata_rule_last_certifier_uiuser_uiuser_id_f_key CASCADE;
ALTER TABLE IF EXISTS rule_metadata DROP CONSTRAINT IF EXISTS rule_metadata_rule_owner_uiuser_uiuser_id_f_key CASCADE;

ALTER TABLE IF EXISTS rule_metadata ADD COLUMN IF NOT EXISTS rule_created_new BIGINT;
ALTER TABLE IF EXISTS rule_metadata ADD COLUMN IF NOT EXISTS rule_last_modified_new BIGINT;

-- delete all stale rule_metadata entries
DELETE FROM rule_metadata
WHERE rule_uid IN (
    SELECT rm.rule_uid
    FROM rule_metadata rm
    LEFT JOIN rule r ON r.rule_uid = rm.rule_uid
    WHERE r.rule_uid IS NULL
);

UPDATE rule_metadata m SET
    rule_created_new = r.rule_create,
    rule_last_modified_new = r.rule_last_seen
FROM rule r
WHERE r.rule_uid = m.rule_uid;

UPDATE rule_metadata SET rule_last_modified_new = COALESCE(rule_last_modified_new, rule_created_new) WHERE TRUE;

ALTER TABLE IF EXISTS rule_metadata DROP COLUMN IF EXISTS rule_created;
ALTER TABLE IF EXISTS rule_metadata DROP COLUMN IF EXISTS rule_last_modified;

ALTER TABLE IF EXISTS rule_metadata RENAME COLUMN rule_created_new TO rule_created;
ALTER TABLE IF EXISTS rule_metadata RENAME COLUMN rule_last_modified_new TO rule_last_modified;

ALTER TABLE IF EXISTS rule_metadata
    ALTER COLUMN rule_created SET NOT NULL,
    ALTER COLUMN rule_last_modified SET NOT NULL;

-- rebuild recertification related views/materialized view
DROP MATERIALIZED VIEW IF EXISTS view_rule_with_owner CASCADE;
DROP VIEW IF EXISTS v_rule_with_ip_owner CASCADE;
DROP VIEW IF EXISTS v_rule_with_dst_owner CASCADE;
DROP VIEW IF EXISTS v_rule_with_src_owner CASCADE;
DROP VIEW IF EXISTS v_rule_with_rule_owner CASCADE;
DROP VIEW IF EXISTS v_rule_ownership_mode CASCADE;
DROP VIEW IF EXISTS v_active_access_allow_rules CASCADE;
DROP VIEW IF EXISTS v_excluded_src_ips CASCADE;
DROP VIEW IF EXISTS v_excluded_dst_ips CASCADE;

ALTER TABLE IF EXISTS rule_metadata
    DROP COLUMN IF EXISTS rule_last_certified,
    DROP COLUMN IF EXISTS rule_last_certifier,
    DROP COLUMN IF EXISTS rule_last_certifier_dn,
    DROP COLUMN IF EXISTS rule_owner,
    DROP COLUMN IF EXISTS rule_owner_dn,
    DROP COLUMN IF EXISTS rule_to_be_removed,
    DROP COLUMN IF EXISTS last_change_admin,
    DROP COLUMN IF EXISTS rule_decert_date,
    DROP COLUMN IF EXISTS rule_recertification_comment;

ALTER TABLE IF EXISTS rule_metadata DROP CONSTRAINT IF EXISTS rule_metadata_rule_created_import_control_control_id_f_key CASCADE;
ALTER TABLE IF EXISTS rule_metadata DROP CONSTRAINT IF EXISTS rule_metadata_rule_last_modified_import_control_control_id_f_key CASCADE;
ALTER TABLE IF EXISTS rule_metadata ADD CONSTRAINT rule_metadata_rule_created_import_control_control_id_f_key
  FOREIGN KEY (rule_created) REFERENCES import_control(control_id) ON UPDATE RESTRICT ON DELETE RESTRICT;
ALTER TABLE IF EXISTS rule_metadata ADD CONSTRAINT rule_metadata_rule_last_modified_import_control_control_id_f_key
  FOREIGN KEY (rule_last_modified) REFERENCES import_control(control_id) ON UPDATE RESTRICT ON DELETE RESTRICT;

CREATE OR REPLACE VIEW v_active_access_allow_rules AS 
	SELECT rule_id,
    rule_src, rule_dst, rule_svc,
    rule_svc_neg, rule_src_neg, rule_dst_neg,
    mgm_id, rule_uid,
    rule_num_numeric, rule_disabled,
    rule_src_refs, rule_dst_refs, rule_svc_refs,
    rule_from_zone, rule_to_zone,
    rule_action, rule_track, track_id, action_id,
    rule_installon, rule_comment, rule_name, rule_implied, rule_custom_fields, 
    rule_create, removed,
    is_global,
    rulebase_id
    FROM rule r
	WHERE r.active
		AND r.access_rule
		AND r.rule_head_text IS NULL
		AND NOT r.rule_disabled
		AND NOT r.action_id IN (2,3,7);

CREATE OR REPLACE VIEW v_rule_ownership_mode AS
	SELECT c.config_value as mode FROM config c
	WHERE c.config_key = 'ruleOwnershipMode';

CREATE OR REPLACE VIEW v_rule_with_rule_owner AS
	SELECT r.rule_id, ow.id as owner_id, ow.name as owner_name, 'rule' AS matches,
		ow.recert_interval, max(rec.recert_date) AS rule_last_certified
	FROM v_active_access_allow_rules r
	LEFT JOIN rule_metadata met ON (r.rule_uid=met.rule_uid)
	LEFT JOIN rule_owner ro ON (ro.rule_metadata_id=met.rule_metadata_id)
	LEFT JOIN owner ow ON (ro.owner_id=ow.id)
	LEFT JOIN recertification rec ON (rec.rule_metadata_id = met.rule_metadata_id AND rec.owner_id = ow.id AND rec.recertified IS TRUE)
	WHERE NOT ow.id IS NULL
	GROUP BY r.rule_id, ow.id, ow.name, ow.recert_interval;

CREATE OR REPLACE VIEW v_excluded_src_ips AS
	SELECT distinct o.obj_ip
	FROM v_rule_with_rule_owner r
	LEFT JOIN rule_from rf ON (r.rule_id=rf.rule_id)
	LEFT JOIN objgrp_flat of ON (rf.obj_id=of.objgrp_flat_id)
	LEFT JOIN object o ON (of.objgrp_flat_member_id=o.obj_id)
	WHERE NOT o.obj_ip='0.0.0.0/0';

CREATE OR REPLACE VIEW v_excluded_dst_ips AS
	SELECT distinct o.obj_ip
	FROM v_rule_with_rule_owner r
	LEFT JOIN rule_to rt ON (r.rule_id=rt.rule_id)
	LEFT JOIN objgrp_flat of ON (rt.obj_id=of.objgrp_flat_id)
	LEFT JOIN object o ON (of.objgrp_flat_member_id=o.obj_id)
	WHERE NOT o.obj_ip='0.0.0.0/0';

CREATE OR REPLACE VIEW v_rule_with_rule_owner_1 AS
	SELECT r.rule_id, r.rule_uid, r.rule_name, r.mgm_id, r.rulebase_id, ow.id as owner_id, met.rule_metadata_id
	FROM v_active_access_allow_rules r
	LEFT JOIN rule_metadata met ON (r.rule_uid=met.rule_uid)
	LEFT JOIN rule_owner ro ON (ro.rule_metadata_id=met.rule_metadata_id)
	LEFT JOIN owner ow ON (ro.owner_id=ow.id)
	WHERE NOT ow.id IS NULL
	GROUP BY r.rule_id, r.rule_uid, r.rule_name, r.mgm_id, r.rulebase_id, ow.id, met.rule_metadata_id;

CREATE OR REPLACE VIEW v_rule_with_src_owner AS 
	SELECT
		r.rule_id, ow.id as owner_id, ow.name as owner_name, 
		CASE
			WHEN onw.ip = onw.ip_end
			THEN SPLIT_PART(CAST(onw.ip AS VARCHAR), '/', 1)
			ELSE
				CASE WHEN
					host(broadcast(inet_merge(onw.ip, onw.ip_end))) = host (onw.ip_end) AND
					host(inet_merge(onw.ip, onw.ip_end)) = host (onw.ip)
				THEN
					text(inet_merge(onw.ip, onw.ip_end))
				ELSE
					CONCAT(SPLIT_PART(onw.ip::VARCHAR,'/', 1), '-', SPLIT_PART(onw.ip_end::VARCHAR, '/', 1))
				END
		END AS matching_ip,
		'source' AS match_in,
		ow.recert_interval, max(rec.recert_date) AS rule_last_certified
	FROM v_active_access_allow_rules r
	LEFT JOIN rule_from ON (r.rule_id=rule_from.rule_id)
	LEFT JOIN objgrp_flat of ON (rule_from.obj_id=of.objgrp_flat_id)
	LEFT JOIN object o ON (of.objgrp_flat_member_id=o.obj_id)
	LEFT JOIN owner_network onw ON (onw.ip_end >= o.obj_ip AND onw.ip <= o.obj_ip_end)
	LEFT JOIN owner ow ON (onw.owner_id=ow.id)
	LEFT JOIN rule_metadata met ON (r.rule_uid=met.rule_uid)
	LEFT JOIN recertification rec ON (rec.rule_metadata_id = met.rule_metadata_id AND rec.owner_id = ow.id AND rec.recertified IS TRUE)
	WHERE r.rule_id NOT IN (SELECT distinct rwo.rule_id FROM v_rule_with_rule_owner rwo) AND
	CASE
		when (select mode from v_rule_ownership_mode) = 'exclusive' then (NOT o.obj_ip IS NULL) AND o.obj_ip NOT IN (select * from v_excluded_src_ips)
		else NOT o.obj_ip IS NULL
	END
	GROUP BY r.rule_id, o.obj_ip, o.obj_ip_end, onw.ip, onw.ip_end, ow.id, ow.name, ow.recert_interval;

CREATE OR REPLACE VIEW v_rule_with_dst_owner AS 
	SELECT 
		r.rule_id, ow.id as owner_id, ow.name as owner_name, 
		CASE
			WHEN onw.ip = onw.ip_end
			THEN SPLIT_PART(CAST(onw.ip AS VARCHAR), '/', 1)
			ELSE
				CASE WHEN
					host(broadcast(inet_merge(onw.ip, onw.ip_end))) = host (onw.ip_end) AND
					host(inet_merge(onw.ip, onw.ip_end)) = host (onw.ip)
				THEN
					text(inet_merge(onw.ip, onw.ip_end))
				ELSE
					CONCAT(SPLIT_PART(onw.ip::VARCHAR,'/', 1), '-', SPLIT_PART(onw.ip_end::VARCHAR, '/', 1))
				END
		END AS matching_ip,
		'destination' AS match_in,
		ow.recert_interval, max(rec.recert_date) AS rule_last_certified
	FROM v_active_access_allow_rules r
	LEFT JOIN rule_to rt ON (r.rule_id=rt.rule_id)
	LEFT JOIN objgrp_flat of ON (rt.obj_id=of.objgrp_flat_id)
	LEFT JOIN object o ON (of.objgrp_flat_member_id=o.obj_id)
	LEFT JOIN owner_network onw ON (onw.ip_end >= o.obj_ip AND onw.ip <= o.obj_ip_end)
	LEFT JOIN owner ow ON (onw.owner_id=ow.id)
	LEFT JOIN rule_metadata met ON (r.rule_uid=met.rule_uid)
	LEFT JOIN recertification rec ON (rec.rule_metadata_id = met.rule_metadata_id AND rec.owner_id = ow.id AND rec.recertified IS TRUE)
	WHERE r.rule_id NOT IN (SELECT distinct rwo.rule_id FROM v_rule_with_rule_owner rwo) AND
	CASE
		when (select mode from v_rule_ownership_mode) = 'exclusive' then (NOT o.obj_ip IS NULL) AND o.obj_ip NOT IN (select * from v_excluded_dst_ips)
		else NOT o.obj_ip IS NULL
	END
	GROUP BY r.rule_id, o.obj_ip, o.obj_ip_end, onw.ip, onw.ip_end, ow.id, ow.name, ow.recert_interval;

CREATE OR REPLACE VIEW v_rule_with_ip_owner AS
	SELECT DISTINCT	uno.rule_id, uno.owner_id, uno.owner_name,
		string_agg(DISTINCT match_in || ':' || matching_ip::VARCHAR, '; ' order by match_in || ':' || matching_ip::VARCHAR desc) as matches,
		uno.recert_interval, uno.rule_last_certified
	FROM ( SELECT DISTINCT * FROM v_rule_with_src_owner AS src UNION SELECT DISTINCT * FROM v_rule_with_dst_owner AS dst) AS uno
	GROUP BY uno.rule_id, uno.owner_id, uno.owner_name, uno.recert_interval, uno.rule_last_certified;

CREATE MATERIALIZED VIEW view_rule_with_owner AS
	SELECT DISTINCT ar.rule_id, ar.owner_id, ar.owner_name, ar.matches, ar.recert_interval, ar.rule_last_certified,
	r.rule_num_numeric, r.track_id, r.action_id, r.rule_from_zone, r.rule_to_zone, r.mgm_id, r.rule_uid,
	r.rule_action, r.rule_name, r.rule_comment, r.rule_track, r.rule_src_neg, r.rule_dst_neg, r.rule_svc_neg,
	r.rule_head_text, r.rule_disabled, r.access_rule, r.xlate_rule, r.nat_rule
	FROM ( SELECT DISTINCT * FROM v_rule_with_rule_owner AS rul UNION SELECT DISTINCT * FROM v_rule_with_ip_owner AS ips) AS ar
	LEFT JOIN rule AS r USING (rule_id)
	GROUP BY ar.rule_id, ar.owner_id, ar.owner_name, ar.matches, ar.recert_interval, ar.rule_last_certified,
		r.rule_num_numeric, r.track_id, r.action_id, r.rule_from_zone, r.rule_to_zone, r.mgm_id, r.rule_uid,
		r.rule_action, r.rule_name, r.rule_comment, r.rule_track, r.rule_src_neg, r.rule_dst_neg, r.rule_svc_neg,
		r.rule_head_text, r.rule_disabled, r.access_rule, r.xlate_rule, r.nat_rule;

GRANT SELECT ON TABLE view_rule_with_owner TO GROUP secuadmins, reporters, configimporters;

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
    "is_section" BOOLEAN DEFAULT TRUE,
	"created" BIGINT,
	"removed" BIGINT
);

-- only for developers who already have on old 9.0 database:
Alter table "rulebase_link" add column IF NOT EXISTS "is_initial" BOOLEAN;
Alter table "rulebase_link" add column IF NOT EXISTS "is_global" BOOLEAN;
Alter table "rulebase_link" add column IF NOT EXISTS "from_rulebase_id" Integer;
Alter table "rulebase_link" add column IF NOT EXISTS "is_section" BOOLEAN DEFAULT TRUE;

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
insert into stm_link_type (id, name) VALUES (4, 'concatenated') ON CONFLICT DO NOTHING;
insert into stm_link_type (id, name) VALUES (5, 'domain') ON CONFLICT DO NOTHING;
delete from stm_link_type where name in ('initial','global','local','section'); -- initial and global/local are additional flags now

CREATE OR REPLACE FUNCTION deleteDuplicateRulebases() RETURNS VOID
    LANGUAGE plpgsql
    VOLATILE
AS $function$
    BEGIN
        -- TODO: make sure that we have at least one rulebase for each device
        DELETE FROM rule WHERE rulebase_id IS NULL;
    END;
$function$;

-- TODO: needs to be rewritten to rulebase_link
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
                    IF r_rule.rule_installon IS NULL OR btrim(r_rule.rule_installon) = '' THEN
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
    BEGIN
        IF NOT EXISTS (
            SELECT 1
            FROM information_schema.columns
            WHERE table_name = 'rule_metadata'
            AND column_name = 'rulebase_id'
        ) THEN
            RETURN;
        END IF;

        WITH rulebase_per_rule AS (
            SELECT rule_uid, mgm_id, MIN(rulebase_id) AS rulebase_id
            FROM rule
            WHERE rulebase_id IS NOT NULL
            GROUP BY rule_uid, mgm_id
        )
        UPDATE rule_metadata rm
        SET rulebase_id = rbr.rulebase_id
        FROM rulebase_per_rule rbr
        WHERE rm.rule_uid = rbr.rule_uid
            AND rm.mgm_id = rbr.mgm_id
            AND rm.rulebase_id IS NULL;
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
        PERFORM addMetadataRulebaseEntries();
        -- add entries in rule_enforced_on_gateway
        PERFORM addRuleEnforcedOnGatewayEntries();
    END;
$function$;

-- end of rule_metadata migration

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM rulebase_link LIMIT 1
    ) THEN
        PERFORM migrateToRulebases();
    END IF;
END
$$;

-- now we can set the new constraint on rule_uid:
Alter Table "rule" DROP Constraint IF EXISTS "rule_altkey";
Alter Table "rule" DROP Constraint IF EXISTS "rule_unique_mgm_id_rule_uid_rule_create_xlate_rule";
Alter Table "rule" ADD Constraint "rule_unique_mgm_id_rule_uid_rule_create_xlate_rule" UNIQUE ("mgm_id", "rule_uid","rule_create","xlate_rule");


-- drop only after migration

ALTER TABLE rule DROP CONSTRAINT IF EXISTS rule_dev_id_fkey;

ALTER TABLE "rule_metadata" DROP CONSTRAINT IF EXISTS "rule_metadata_rulebase_id_f_key" CASCADE;
ALTER TABLE "rule_metadata" DROP CONSTRAINT IF EXISTS "unique_rule_metadata_rule_uid_mgm_id";
ALTER TABLE "rule_metadata" ADD CONSTRAINT "unique_rule_metadata_rule_uid_mgm_id" UNIQUE ("rule_uid","mgm_id");

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
ALTER TABLE "rule_from" ADD COLUMN IF NOT EXISTS "removed" BIGINT;
ALTER TABLE "rule_to" ADD COLUMN IF NOT EXISTS "removed" BIGINT;
ALTER TABLE "rule_service" ADD COLUMN IF NOT EXISTS "removed" BIGINT;
ALTER TABLE "rule_enforced_on_gateway" ADD COLUMN IF NOT EXISTS "removed" BIGINT;
ALTER TABLE "rulebase" ADD COLUMN IF NOT EXISTS "removed" BIGINT;
ALTER TABLE "rulebase_link" ADD COLUMN IF NOT EXISTS "removed" BIGINT;

-- add obj type access-role for cp import
INSERT INTO stm_obj_typ (obj_typ_id,obj_typ_name) VALUES (21,'access-role') ON CONFLICT DO NOTHING;

-- remove dev_id fk and set nullable if column exists
ALTER TABLE changelog_rule DROP CONSTRAINT IF EXISTS changelog_rule_dev_id_fkey;

DO $$
BEGIN
  IF EXISTS (
    SELECT 1
    FROM information_schema.columns
    WHERE table_name = 'changelog_rule'
      AND column_name = 'dev_id'
  ) THEN
    ALTER TABLE changelog_rule ALTER COLUMN dev_id DROP NOT NULL;
  END IF;
END
$$;

-- add new compliance tables

CREATE TABLE IF NOT EXISTS compliance.policy  
(
    id SERIAL PRIMARY KEY,
	name TEXT,
	created_date timestamp default now(),
	disabled bool
);

CREATE TABLE IF NOT EXISTS compliance.policy_criterion
(
    policy_id INT NOT NULL,
	criterion_id INT NOT NULL,
    removed timestamp with time zone,
	created timestamp with time zone default now()
);

CREATE TABLE IF NOT EXISTS compliance.criterion
(
    id SERIAL PRIMARY KEY,
	name TEXT,
	comment TEXT,
	criterion_type TEXT,
	content TEXT,
	removed timestamp with time zone,
	created timestamp with time zone default now(),
	import_source TEXT
);

CREATE TABLE IF NOT EXISTS compliance.violation
(
    id BIGSERIAL PRIMARY KEY,
	rule_id bigint NOT NULL,
	found_date timestamp with time zone default now(),
	removed_date timestamp with time zone,
	details TEXT,
	risk_score real,
	policy_id INT NOT NULL,
	criterion_id INT NOT NULL
);

-- add columns in existing compliance tables

ALTER TABLE compliance.network_zone ADD COLUMN IF NOT EXISTS "created" TIMESTAMP WITH TIME ZONE DEFAULT NOW();
ALTER TABLE compliance.network_zone ADD COLUMN IF NOT EXISTS "removed" TIMESTAMP WITH TIME ZONE;
ALTER TABLE compliance.network_zone ADD COLUMN IF NOT EXISTS "criterion_id" INT;
ALTER TABLE compliance.network_zone ADD COLUMN IF NOT EXISTS "id_string" TEXT;
ALTER TABLE compliance.network_zone_communication ADD COLUMN IF NOT EXISTS "created" TIMESTAMP WITH TIME ZONE DEFAULT NOW();
ALTER TABLE compliance.network_zone_communication ADD COLUMN IF NOT EXISTS "removed" TIMESTAMP WITH TIME ZONE;
ALTER TABLE compliance.network_zone_communication ADD COLUMN IF NOT EXISTS "criterion_id" INT;
ALTER TABLE compliance.ip_range ADD COLUMN IF NOT EXISTS "created" TIMESTAMP WITH TIME ZONE DEFAULT NOW();
ALTER TABLE compliance.ip_range ADD COLUMN IF NOT EXISTS "removed" TIMESTAMP WITH TIME ZONE;
ALTER TABLE compliance.ip_range ADD COLUMN IF NOT EXISTS "criterion_id" INT;
ALTER TABLE compliance.ip_range ADD COLUMN IF NOT EXISTS "name" TEXT;

-- tables altered inside this version

ALTER TABLE compliance.criterion ADD COLUMN IF NOT EXISTS "created" TIMESTAMP WITH TIME ZONE DEFAULT NOW();
ALTER TABLE compliance.criterion ADD COLUMN IF NOT EXISTS "removed" TIMESTAMP WITH TIME ZONE;
ALTER TABLE compliance.criterion ADD COLUMN IF NOT EXISTS "import_source" TEXT;
ALTER TABLE compliance.criterion ADD COLUMN IF NOT EXISTS "comment" TEXT;
ALTER TABLE compliance.network_zone ALTER COLUMN "removed" DROP DEFAULT;
ALTER TABLE compliance.network_zone ADD COLUMN IF NOT EXISTS "is_auto_calculated_internet_zone" BOOLEAN DEFAULT FALSE;
ALTER TABLE compliance.network_zone ADD COLUMN IF NOT EXISTS "is_auto_calculated_undefined_internal_zone" BOOLEAN DEFAULT FALSE;
ALTER TABLE compliance.network_zone_communication ALTER COLUMN "removed" DROP DEFAULT;
ALTER TABLE compliance.ip_range ALTER COLUMN "removed" DROP DEFAULT;
ALTER TABLE compliance.policy_criterion ALTER COLUMN "removed" DROP DEFAULT;
ALTER TABLE compliance.violation ALTER COLUMN "removed_date" DROP DEFAULT;
ALTER TABLE compliance.violation ALTER COLUMN "found_date" TYPE TIMESTAMP WITH TIME ZONE; -- takes local timezone. can be set wxplicitly by "USING found_date AT TIME ZONE 'UTC'";

-- alter ip_range's PK

ALTER TABLE compliance.ip_range DROP CONSTRAINT IF EXISTS ip_range_pkey;
ALTER TABLE compliance.ip_range
ADD CONSTRAINT ip_range_pkey
PRIMARY KEY (network_zone_id, ip_range_start, ip_range_end, created);

-- add FKs

ALTER TABLE compliance.network_zone 
DROP CONSTRAINT IF EXISTS compliance_criterion_network_zone_foreign_key;
ALTER TABLE compliance.network_zone 
ADD CONSTRAINT compliance_criterion_network_zone_foreign_key 
FOREIGN KEY (criterion_id) REFERENCES compliance.criterion(id) 
ON UPDATE RESTRICT ON DELETE CASCADE;

ALTER TABLE compliance.ip_range 
DROP CONSTRAINT IF EXISTS compliance_criterion_ip_range_foreign_key;
ALTER TABLE compliance.ip_range 
ADD CONSTRAINT compliance_criterion_ip_range_foreign_key 
FOREIGN KEY (criterion_id) REFERENCES compliance.criterion(id) 
ON UPDATE RESTRICT ON DELETE CASCADE;

ALTER TABLE compliance.network_zone_communication 
DROP CONSTRAINT IF EXISTS compliance_criterion_network_zone_communication_foreign_key;
ALTER TABLE compliance.network_zone_communication 
ADD CONSTRAINT compliance_criterion_network_zone_communication_foreign_key 
FOREIGN KEY (criterion_id) REFERENCES compliance.criterion(id) 
ON UPDATE RESTRICT ON DELETE CASCADE;

ALTER TABLE compliance.policy_criterion 
DROP CONSTRAINT IF EXISTS compliance_policy_policy_criterion_foreign_key;
ALTER TABLE compliance.policy_criterion 
ADD CONSTRAINT compliance_policy_policy_criterion_foreign_key 
FOREIGN KEY (policy_id) REFERENCES compliance.policy(id) 
ON UPDATE RESTRICT ON DELETE CASCADE;

ALTER TABLE compliance.policy_criterion 
DROP CONSTRAINT IF EXISTS compliance_criterion_policy_criterion_foreign_key;
ALTER TABLE compliance.policy_criterion 
ADD CONSTRAINT compliance_criterion_policy_criterion_foreign_key 
FOREIGN KEY (criterion_id) REFERENCES compliance.criterion(id) 
ON UPDATE RESTRICT ON DELETE CASCADE;

ALTER TABLE compliance.violation 
DROP CONSTRAINT IF EXISTS compliance_policy_violation_foreign_key;
ALTER TABLE compliance.violation 
ADD CONSTRAINT compliance_policy_violation_foreign_key 
FOREIGN KEY (policy_id) REFERENCES compliance.policy(id) 
ON UPDATE RESTRICT ON DELETE CASCADE;

ALTER TABLE compliance.violation 
DROP CONSTRAINT IF EXISTS compliance_criterion_violation_foreign_key;
ALTER TABLE compliance.violation 
ADD CONSTRAINT compliance_criterion_violation_foreign_key 
FOREIGN KEY (criterion_id) REFERENCES compliance.criterion(id) 
ON UPDATE RESTRICT ON DELETE CASCADE;

ALTER TABLE compliance.violation 
DROP CONSTRAINT IF EXISTS compliance_rule_violation_foreign_key;
ALTER TABLE compliance.violation 
ADD CONSTRAINT compliance_rule_violation_foreign_key 
FOREIGN KEY (rule_id) REFERENCES public.rule(rule_id) 
ON UPDATE RESTRICT ON DELETE CASCADE;

-- add report type Compliance

UPDATE config SET config_value = '[1,2,3,4,5,6,7,8,9,10,21,22,31,32]' WHERE config_key = 'availableReportTypes';

--- prevent overlapping active ip address ranges in the same zone
ALTER TABLE compliance.ip_range DROP CONSTRAINT IF EXISTS exclude_overlapping_ip_ranges;
ALTER TABLE compliance.ip_range ADD CONSTRAINT exclude_overlapping_ip_ranges
EXCLUDE USING gist (
    network_zone_id WITH =,
    numrange(ip_range_start - '0.0.0.0'::inet, ip_range_end - '0.0.0.0'::inet, '[]') WITH &&
)
WHERE (removed IS NULL);

-- add config parameter debugConfig if not exists

INSERT INTO config (config_key, config_value, config_user) 
VALUES ('debugConfig', '{"debugLevel":8, "extendedLogComplianceCheck":true, "extendedLogReportGeneration":true, "extendedLogScheduler":true}', 0)
ON CONFLICT (config_key, config_user) DO NOTHING;

-- add config parameter complianceCheckPolicy if not exists

INSERT INTO config (config_key, config_value, config_user) 
VALUES ('complianceCheckPolicy', '0', 0)
ON CONFLICT (config_key, config_user) DO NOTHING;

-- add management and rule uids to table violation

ALTER TABLE compliance.violation ADD COLUMN IF NOT EXISTS rule_uid TEXT;
ALTER TABLE compliance.violation ADD COLUMN IF NOT EXISTS mgmt_uid TEXT;
ALTER TABLE compliance.violation ADD COLUMN IF NOT EXISTS is_initial BOOLEAN;

-- add unique constraint for report_template_name

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'unique_report_template_name'
    ) THEN
        ALTER TABLE report_template
        ADD CONSTRAINT unique_report_template_name UNIQUE (report_template_name);
    END IF;
END$$;

-- add new report template for compliance: unresolved violations

INSERT INTO "report_template" ("report_filter","report_template_name","report_template_comment","report_template_owner", "report_parameters") 
    VALUES ('action=accept',
        'Compliance: Unresolved violations','T0108', 0, 
        '{"report_type":31,"device_filter":{"management":[]},
            "time_filter": {
                "is_shortcut": true,
                "shortcut": "now",
                "report_time": "2022-01-01T00:00:00.0000000+01:00",
                "timerange_type": "SHORTCUT",
                "shortcut_range": "this year",
                "offset": 0,
                "interval": "DAYS",
                "start_time": "2022-01-01T00:00:00.0000000+01:00",
                "end_time": "2022-01-01T00:00:00.0000000+01:00",
                "open_start": false,
                "open_end": false},
            "compliance_filter": {
                "diff_reference_in_days": 0,
                "show_non_impact_rules": true}}')
ON CONFLICT (report_template_name) DO NOTHING;

-- add new report template for compliance: diffs

INSERT INTO "report_template" ("report_filter","report_template_name","report_template_comment","report_template_owner", "report_parameters") 
    VALUES ('action=accept',
        'Compliance: Diffs','T0109', 0, 
        '{"report_type":32,"device_filter":{"management":[]},
            "time_filter": {
                "is_shortcut": true,
                "shortcut": "now",
                "report_time": "2022-01-01T00:00:00.0000000+01:00",
                "timerange_type": "SHORTCUT",
                "shortcut_range": "this year",
                "offset": 0,
                "interval": "DAYS",
                "start_time": "2022-01-01T00:00:00.0000000+01:00",
                "end_time": "2022-01-01T00:00:00.0000000+01:00",
                "open_start": false,
                "open_end": false},
            "compliance_filter": {
                "diff_reference_in_days": 7,
                "show_non_impact_rules": false}}')
ON CONFLICT (report_template_name) DO NOTHING;

-- add parameter to limit number of printed violations in compliance report to config

INSERT INTO config (config_key, config_value, config_user)
VALUES ('complianceCheckMaxPrintedViolations', '0', 0)
ON CONFLICT (config_key, config_user) DO NOTHING;

-- add parameter to persist report scheduler configs to config

INSERT INTO config (config_key, config_value, config_user) 
VALUES ('reportSchedulerConfig', '', 0)
ON CONFLICT (config_key, config_user) DO NOTHING;

-- add parameter to choose order by column of network matrix between name and id

INSERT INTO config (config_key, config_value, config_user) 
VALUES ('complianceCheckSortMatrixByID', 'false', 0)
ON CONFLICT (config_key, config_user) DO NOTHING;

-- internal zone parameters

INSERT INTO config (config_key, config_value, config_user) VALUES ('internalZoneRange_10_0_0_0_8', 'true', 0) ON CONFLICT (config_key, config_user) DO NOTHING;
INSERT INTO config (config_key, config_value, config_user) VALUES ('internalZoneRange_172_16_0_0_12', 'true', 0) ON CONFLICT (config_key, config_user) DO NOTHING;
INSERT INTO config (config_key, config_value, config_user) VALUES ('internalZoneRange_192_168_0_0_16', 'true', 0) ON CONFLICT (config_key, config_user) DO NOTHING;
INSERT INTO config (config_key, config_value, config_user) VALUES ('internalZoneRange_0_0_0_0_8', 'true', 0) ON CONFLICT (config_key, config_user) DO NOTHING;
INSERT INTO config (config_key, config_value, config_user) VALUES ('internalZoneRange_127_0_0_0_8', 'true', 0) ON CONFLICT (config_key, config_user) DO NOTHING;
INSERT INTO config (config_key, config_value, config_user) VALUES ('internalZoneRange_169_254_0_0_16', 'true', 0) ON CONFLICT (config_key, config_user) DO NOTHING;
INSERT INTO config (config_key, config_value, config_user) VALUES ('internalZoneRange_224_0_0_0_4', 'true', 0) ON CONFLICT (config_key, config_user) DO NOTHING;
INSERT INTO config (config_key, config_value, config_user) VALUES ('internalZoneRange_240_0_0_0_4', 'true', 0) ON CONFLICT (config_key, config_user) DO NOTHING;
INSERT INTO config (config_key, config_value, config_user) VALUES ('internalZoneRange_255_255_255_255_32', 'true', 0) ON CONFLICT (config_key, config_user) DO NOTHING;
INSERT INTO config (config_key, config_value, config_user) VALUES ('internalZoneRange_192_0_2_0_24', 'true', 0) ON CONFLICT (config_key, config_user) DO NOTHING;
INSERT INTO config (config_key, config_value, config_user) VALUES ('internalZoneRange_198_51_100_0_24', 'true', 0) ON CONFLICT (config_key, config_user) DO NOTHING;
INSERT INTO config (config_key, config_value, config_user) VALUES ('internalZoneRange_203_0_113_0_24', 'true', 0) ON CONFLICT (config_key, config_user) DO NOTHING;
INSERT INTO config (config_key, config_value, config_user) VALUES ('internalZoneRange_100_64_0_0_10', 'true', 0) ON CONFLICT (config_key, config_user) DO NOTHING;
INSERT INTO config (config_key, config_value, config_user) VALUES ('internalZoneRange_192_0_0_0_24', 'true', 0) ON CONFLICT (config_key, config_user) DO NOTHING;
INSERT INTO config (config_key, config_value, config_user) VALUES ('internalZoneRange_192_88_99_0_24', 'true', 0) ON CONFLICT (config_key, config_user) DO NOTHING;
INSERT INTO config (config_key, config_value, config_user) VALUES ('internalZoneRange_198_18_0_0_15', 'true', 0) ON CONFLICT (config_key, config_user) DO NOTHING;

-- auto calculate special zone parameters

INSERT INTO config (config_key, config_value, config_user) 
VALUES ('autoCalculateInternetZone', 'true', 0)
ON CONFLICT (config_key, config_user) DO NOTHING;

INSERT INTO config (config_key, config_value, config_user) 
VALUES ('autoCalculateUndefinedInternalZone', 'true', 0)
ON CONFLICT (config_key, config_user) DO NOTHING;

INSERT INTO config (config_key, config_value, config_user) 
VALUES ('autoCalculatedZonesAtTheEnd', 'true', 0)
ON CONFLICT (config_key, config_user) DO NOTHING;

INSERT INTO config (config_key, config_value, config_user) 
VALUES ('treatDynamicAndDomainObjectsAsInternet', 'true', 0)
ON CONFLICT (config_key, config_user) DO NOTHING;

INSERT INTO config (config_key, config_value, config_user) 
VALUES ('showShortColumnsInComplianceReports', 'true', 0)
ON CONFLICT (config_key, config_user) DO NOTHING;

INSERT INTO config (config_key, config_value, config_user) 
VALUES ('autoCalculatedZonesAtTheEnd', 'true', 0)
ON CONFLICT (config_key, config_user) DO NOTHING;

INSERT INTO config (config_key, config_value, config_user) 
VALUES ('treatDynamicAndDomainObjectsAsInternet', 'true', 0)
ON CONFLICT (config_key, config_user) DO NOTHING;

INSERT INTO config (config_key, config_value, config_user) 
VALUES ('showShortColumnsInComplianceReports', 'true', 0)
ON CONFLICT (config_key, config_user) DO NOTHING;

-- set deprecated field rule_num to 0 for all rules to avoid inconsistencies
UPDATE rule SET rule_num = 0 WHERE rule_num <> 0;;

-- add config value to make imported matrices editable

INSERT INTO config (config_key, config_value, config_user) 
VALUES ('importedMatrixReadOnly', 'true', 0)
ON CONFLICT (config_key, config_user) DO NOTHING;

-- add config values to make parallelization in compliance check configurable

INSERT INTO config (config_key, config_value, config_user) 
VALUES ('complianceCheckElementsPerFetch', '500', 0)
ON CONFLICT (config_key, config_user) DO NOTHING;

INSERT INTO config (config_key, config_value, config_user) 
VALUES ('complianceCheckAvailableProcessors', '4', 0)
ON CONFLICT (config_key, config_user) DO NOTHING;

-- add crosstabulations rules with zone for source and destination

--crosstabulation rule zone for source
Create table IF NOT EXISTS "rule_from_zone"
(
	"rule_id" BIGINT NOT NULL,
	"zone_id" Integer NOT NULL,
	"created" BIGINT NOT NULL,
	"removed" BIGINT,
	primary key (rule_id, zone_id, created)
);

--crosstabulation rule zone for destination
Create table IF NOT EXISTS "rule_to_zone"
(
	"rule_id" BIGINT NOT NULL,
	"zone_id" Integer NOT NULL,
	"created" BIGINT NOT NULL,
	"removed" BIGINT,
	primary key (rule_id, zone_id, created)
);

--crosstabulation rule zone for destination FKs
ALTER TABLE "rule_to_zone" 
DROP CONSTRAINT IF EXISTS fk_rule_to_zone_rule_id_rule_rule_id;
ALTER TABLE "rule_to_zone"
DROP CONSTRAINT IF EXISTS fk_rule_to_zone_zone_id_zone_zone_id;

ALTER TABLE "rule_to_zone"
ADD CONSTRAINT fk_rule_to_zone_rule_id_rule_rule_id FOREIGN KEY ("rule_id") REFERENCES "rule" ("rule_id") ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE "rule_to_zone"
ADD CONSTRAINT fk_rule_to_zone_zone_id_zone_zone_id FOREIGN KEY ("zone_id") REFERENCES "zone" ("zone_id") ON UPDATE RESTRICT ON DELETE CASCADE;

--crosstabulation rule zone for source FKs
ALTER TABLE "rule_from_zone" 
DROP CONSTRAINT IF EXISTS fk_rule_from_zone_rule_id_rule_rule_id;
ALTER TABLE "rule_from_zone"
DROP CONSTRAINT IF EXISTS fk_rule_from_zone_zone_id_zone_zone_id;

ALTER TABLE "rule_from_zone"
ADD CONSTRAINT fk_rule_from_zone_rule_id_rule_rule_id FOREIGN KEY ("rule_id") REFERENCES "rule" ("rule_id") ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE "rule_from_zone"
ADD CONSTRAINT fk_rule_from_zone_zone_id_zone_zone_id FOREIGN KEY ("zone_id") REFERENCES "zone" ("zone_id") ON UPDATE RESTRICT ON DELETE CASCADE;


-- initial fill script for rule_from_zones and rule_to_zones
DO $$
DECLARE
    inserted_source INT := 0;
    inserted_destination INT := 0;
    remaining_source INT:= 0;
    remaining_destination INT:= 0;
	col_exists_source BOOLEAN;
    col_exists_destination BOOLEAN;
	count_from_zone_in_rule_after_update INT:= 0;
    count_to_zone_in_rule_after_update INT:= 0;
	
BEGIN
	-- Check column rule_from_zone exists
    SELECT EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_name='rule'
          AND column_name='rule_from_zone'
    ) INTO col_exists_source;

    -- Check column rule_to_zone exists
    SELECT EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_name='rule'
          AND column_name='rule_to_zone'
    ) INTO col_exists_destination;

    IF col_exists_source AND NOT EXISTS (SELECT 1 FROM rule_from_zone) THEN
		INSERT INTO rule_from_zone (rule_id, zone_id, created, removed)
		SELECT rule_id, rule_from_zone, rule_create, removed
		FROM rule
		WHERE rule_from_zone IS NOT NULL;					
		GET DIAGNOSTICS inserted_source = ROW_COUNT;
		
		-- Count the existing rule_from_zone and rule_to_zone
		SELECT COUNT(*) INTO remaining_source
		FROM rule
		WHERE rule_from_zone IS NOT NULL;
		
    ELSE
       -- RAISE NOTICE 'Table does not exist or is not empty';
    END IF;
	
	IF col_exists_destination AND NOT EXISTS (SELECT 1 FROM rule_to_zone) THEN
		INSERT INTO rule_to_zone (rule_id, zone_id, created, removed)
		SELECT rule_id, rule_to_zone, rule_create, removed
		FROM rule
		WHERE rule_to_zone IS NOT NULL;				
		GET DIAGNOSTICS inserted_destination = ROW_COUNT;
		
		-- Count the existing rule_from_zone and rule_to_zone
		SELECT COUNT(*) INTO remaining_destination
		FROM rule
		WHERE rule_to_zone IS NOT NULL;	
		
    ELSE
       -- RAISE NOTICE 'Table does not exist or is not empty';	  
    END IF;
				
	IF (col_exists_source OR col_exists_destination) AND
		(remaining_source + remaining_destination = inserted_source + inserted_destination) Then
			UPDATE rule
			SET rule_from_zone = NULL,
				rule_to_zone = NULL
			WHERE rule_from_zone IS NOT NULL
			OR rule_to_zone IS NOT NULL;			
	END IF;
	
	IF (col_exists_source OR col_exists_destination) Then
		SELECT COUNT(*) INTO count_from_zone_in_rule_after_update FROM rule WHERE rule_from_zone IS NOT NULL;
		SELECT COUNT(*) INTO count_to_zone_in_rule_after_update FROM rule WHERE rule_to_zone IS NOT NULL;

         IF count_from_zone_in_rule_after_update > 0 OR count_to_zone_in_rule_after_update > 0 THEN
            RAISE EXCEPTION 'Cannot drop columns: non-null values remain (from_zone: %, to_zone: %)', count_from_zone_in_rule_after_update, count_to_zone_in_rule_after_update;
        END IF;

        END IF;
END
$$;


insert into stm_dev_typ (dev_typ_id,dev_typ_name,dev_typ_version,dev_typ_manufacturer,dev_typ_predef_svc,dev_typ_is_multi_mgmt,dev_typ_is_mgmt,is_pure_routing_device)
    VALUES (28,'Cisco Asa','9','Cisco','',false,true,false)
    ON CONFLICT (dev_typ_id) DO NOTHING;

insert into stm_dev_typ (dev_typ_id,dev_typ_name,dev_typ_version,dev_typ_manufacturer,dev_typ_predef_svc,dev_typ_is_multi_mgmt,dev_typ_is_mgmt,is_pure_routing_device)
    VALUES (29,'Cisco Asa on FirePower','9','Cisco','',false,true,false)
    ON CONFLICT (dev_typ_id) DO NOTHING;

-- Set stm* tables hardcoded only - no Serial - stm_color filled via csv
ALTER TABLE stm_link_type ALTER COLUMN id DROP DEFAULT;
ALTER TABLE stm_track ALTER COLUMN track_id DROP DEFAULT;
ALTER TABLE stm_obj_typ ALTER COLUMN obj_typ_id DROP DEFAULT;
ALTER TABLE stm_change_type ALTER COLUMN change_type_id DROP DEFAULT;
ALTER TABLE stm_action ALTER COLUMN action_id DROP DEFAULT;
ALTER TABLE stm_dev_typ ALTER COLUMN dev_typ_id DROP DEFAULT;
ALTER TABLE parent_rule_type ALTER COLUMN id DROP DEFAULT;

-- Drop Sequence
DROP SEQUENCE IF EXISTS public.stm_link_type_id_seq;
DROP SEQUENCE IF EXISTS public.stm_track_track_id_seq;
DROP SEQUENCE IF EXISTS public.stm_obj_typ_obj_typ_id_seq;
DROP SEQUENCE IF EXISTS public.stm_change_type_change_type_id_seq;
DROP SEQUENCE IF EXISTS public.stm_action_action_id_seq;
DROP SEQUENCE IF EXISTS public.stm_dev_typ_dev_typ_id_seq;
DROP SEQUENCE IF EXISTS public.parent_rule_type_id_seq;

-- drop old mgm_id, zone_name constraint
ALTER TABLE zone DROP CONSTRAINT IF EXISTS "Alter_Key10";

-- add new mgm_id, zone_name constraint where just one with removed is null allowed
CREATE UNIQUE INDEX if not exists "zone_mgm_id_zone_name_removed_is_null_unique" ON zone (mgm_id, zone_name) WHERE removed IS NULL;

-- normalize owner responsibles into separate table and enhance it
CREATE TABLE IF NOT EXISTS owner_responsible
(
    id SERIAL PRIMARY KEY,
    owner_id int NOT NULL,
    dn Varchar NOT NULL,
    responsible_type int NOT NULL
);

-- revert older first throw
ALTER TABLE owner_responsible DROP COLUMN IF EXISTS roles;

DO $$
BEGIN
    IF (
        SELECT COUNT(*)
        FROM information_schema.columns
        WHERE table_name='owner' AND column_name='owner_responsible1'
    ) > 0 THEN
        INSERT INTO owner_responsible (owner_id, dn, responsible_type)
        SELECT owner.id, dn, 1
        FROM owner, unnest(owner_responsible1) AS dn
        LEFT JOIN owner_responsible r
            ON r.owner_id = owner.id AND r.dn = dn AND r.responsible_type = 1
        WHERE NULLIF(dn, '') IS NOT NULL
          AND r.owner_id IS NULL;
        ALTER TABLE owner DROP COLUMN owner_responsible1;
    END IF;

    IF (
        SELECT COUNT(*)
        FROM information_schema.columns
        WHERE table_name='owner' AND column_name='owner_responsible2'
    ) > 0 THEN
        INSERT INTO owner_responsible (owner_id, dn, responsible_type)
        SELECT owner.id, dn, 2
        FROM owner, unnest(owner_responsible2) AS dn
        LEFT JOIN owner_responsible r
            ON r.owner_id = owner.id AND r.dn = dn AND r.responsible_type = 2
        WHERE NULLIF(dn, '') IS NOT NULL
          AND r.owner_id IS NULL;
        ALTER TABLE owner DROP COLUMN owner_responsible2;
    END IF;

    IF (
        SELECT COUNT(*)
        FROM information_schema.columns
        WHERE table_name='owner' AND column_name='owner_responsible3'
    ) > 0 THEN
        INSERT INTO owner_responsible (owner_id, dn, responsible_type)
        SELECT owner.id, dn, 3
        FROM owner, unnest(owner_responsible3) AS dn
        LEFT JOIN owner_responsible r
            ON r.owner_id = owner.id AND r.dn = dn AND r.responsible_type = 3
        WHERE NULLIF(dn, '') IS NOT NULL
          AND r.owner_id IS NULL;
        ALTER TABLE owner DROP COLUMN owner_responsible3;
    END IF;

    IF (
        SELECT COUNT(*)
        FROM information_schema.columns
        WHERE table_name='owner' AND column_name='dn'
    ) > 0 THEN
        INSERT INTO owner_responsible (owner_id, dn, responsible_type)
        SELECT owner.id, owner.dn, 1
        FROM owner
        LEFT JOIN owner_responsible r
            ON r.owner_id = owner.id AND r.dn = owner.dn AND r.responsible_type = 1
        WHERE NULLIF(owner.dn, '') IS NOT NULL
          AND r.owner_id IS NULL;
        ALTER TABLE owner DROP COLUMN dn;
    END IF;

    IF (
        SELECT COUNT(*)
        FROM information_schema.columns
        WHERE table_name='owner' AND column_name='group_dn'
    ) > 0 THEN
        INSERT INTO owner_responsible (owner_id, dn, responsible_type)
        SELECT owner.id, owner.group_dn, 2
        FROM owner
        LEFT JOIN owner_responsible r
            ON r.owner_id = owner.id AND r.dn = owner.group_dn AND r.responsible_type = 2
        WHERE NULLIF(owner.group_dn, '') IS NOT NULL
          AND r.owner_id IS NULL;
        ALTER TABLE owner DROP COLUMN group_dn;
    END IF;
END$$;

DO $$
BEGIN
    IF (
        SELECT COUNT(*)
        FROM pg_constraint
        WHERE conname = 'owner_responsible_owner_foreign_key'
    ) = 0 THEN
        ALTER TABLE owner_responsible
            ADD CONSTRAINT owner_responsible_owner_foreign_key
            FOREIGN KEY (owner_id) REFERENCES owner(id) ON UPDATE RESTRICT ON DELETE CASCADE;
    END IF;
END$$;

CREATE UNIQUE INDEX IF NOT EXISTS owner_responsible_owner_dn_type_unique ON owner_responsible(owner_id, dn, responsible_type);
CREATE INDEX IF NOT EXISTS owner_responsible_dn_idx ON owner_responsible(dn);

-- Changing primary key of *_resolved tables to include created timestamp
-- This is necessary to store multiple resolutions for the same object/rule over time
ALTER TABLE "rule_nwobj_resolved" DROP CONSTRAINT IF EXISTS "rule_nwobj_resolved_pkey";
ALTER TABLE "rule_nwobj_resolved" ADD PRIMARY KEY ("mgm_id", "rule_id", "obj_id", "created");
ALTER TABLE "rule_svc_resolved" DROP CONSTRAINT IF EXISTS "rule_svc_resolved_pkey";
ALTER TABLE "rule_svc_resolved" ADD PRIMARY KEY ("mgm_id", "rule_id", "svc_id", "created");
ALTER TABLE "rule_user_resolved" DROP CONSTRAINT IF EXISTS "rule_user_resolved_pkey";
ALTER TABLE "rule_user_resolved" ADD PRIMARY KEY ("mgm_id", "rule_id", "user_id", "created");
