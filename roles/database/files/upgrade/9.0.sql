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

-- 8.8.3
ALTER TABLE modelling.connection ADD COLUMN IF NOT EXISTS requested_on_fw boolean default false;
ALTER TABLE modelling.connection ADD COLUMN IF NOT EXISTS removed boolean default false;
ALTER TABLE modelling.connection ADD COLUMN IF NOT EXISTS removal_date timestamp;
UPDATE modelling.connection SET requested_on_fw=true WHERE requested_on_fw=false;

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

-- ALTER TABLE "rulebase" ADD COLUMN IF NOT EXISTS "uid" Varchar NOT NULL;

ALTER TABLE "rulebase" DROP CONSTRAINT IF EXISTS "fk_rulebase_mgm_id" CASCADE;
Alter table "rulebase" add CONSTRAINT fk_rulebase_mgm_id foreign key ("mgm_id") references "management" ("mgm_id") on update restrict on delete cascade;

ALTER TABLE "rulebase" DROP CONSTRAINT IF EXISTS "unique_rulebase_mgm_id_name" CASCADE;
ALTER TABLE "rulebase" DROP CONSTRAINT IF EXISTS "unique_rulebase_mgm_id_uid" CASCADE;
Alter table "rulebase" add CONSTRAINT unique_rulebase_mgm_id_uid UNIQUE ("mgm_id", "uid");
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

-- wenn es regeln mit derselben uid gibt, funktioniert der folgende constraint nicht
-- koennen wir dafuer sorgen, dass jede rule_uid nur exakt 1x in der datenbank steht?
-- brauchen wir zusaetzlich die einschraenkung auf das mgm?
-- mindestens gibt es ein Problem mit den (implicit) NAT Regeln: CP_default_Office_Mode_addresses_pool
--  rule_id | last_change_admin | rule_name | mgm_id | parent_rule_id | parent_rule_type | active | rule_num | rule_num_numeric | rule_ruleid |               rule_uid               | rule_disabled | rule_src_neg | rule_dst_neg | rule_svc_neg | action_id | track_id |               rule_src                | rule_dst | rule_svc |            rule_src_refs             |            rule_dst_refs             |            rule_svc_refs             | rule_from_zone | rule_to_zone | rule_action | rule_track | rule_installon | rule_time | rule_comment | rule_head_text | rule_implied | rule_create | rule_last_seen | dev_id | rule_custom_fields | access_rule | nat_rule | xlate_rule
-----------+-------------------+-----------+--------+----------------+------------------+--------+----------+------------------+-------------+--------------------------------------+---------------+--------------+--------------+--------------+-----------+----------+---------------------------------------+----------+----------+--------------------------------------+--------------------------------------+--------------------------------------+----------------+--------------+-------------+------------+----------------+-----------+--------------+----------------+--------------+-------------+----------------+--------+--------------------+-------------+----------+------------
--     274 |                   |           |     19 |                |                  | t      |       10 |   17000.00000000 |             | dc1a7110-e431-4f56-a84a-31b17acf7ee7 | f             | f            | f            | f            |         2 |        2 | CP_default_Office_Mode_addresses_pool | Any      | Any      | e7c5a3b6-e20f-4756-bb56-f2b394baf7a9 | 97aeb369-9aea-11d5-bd16-0090272ccb30 | 97aeb369-9aea-11d5-bd16-0090272ccb30 |                |              | drop        | None       | Policy Targets | Any       |              |                | f            |          19 |             19 |     19 |                    | f           | t        |        261

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

-- now we can set the new constraint on rule_uid:
Alter Table "rule" DROP Constraint IF EXISTS "rule_altkey";
Alter Table "rule" DROP Constraint IF EXISTS "rule_unique_mgm_id_rule_uid_rule_create_xlate_rule";
Alter Table "rule" ADD Constraint "rule_unique_mgm_id_rule_uid_rule_create_xlate_rule" UNIQUE ("mgm_id", "rule_uid","rule_create","xlate_rule");


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

-- add assessability issue

-- create table if not exists compliance.assessability_issue
-- (
--     violation_id BIGINT NOT NULL,
-- 	type_id INT NOT NULL,
-- 	PRIMARY KEY(violation_id, type_id)
-- );

-- create table if not exists compliance.assessability_issue_type
-- (
-- 	type_id INT PRIMARY KEY,
--     type_name VARCHAR(50) NOT NULL
-- );


-- ALTER TABLE compliance.assessability_issue 
-- DROP CONSTRAINT IF EXISTS compliance_assessability_issue_type_foreign_key;
-- ALTER TABLE compliance.assessability_issue ADD CONSTRAINT compliance_assessability_issue_type_foreign_key FOREIGN KEY (type_id) REFERENCES compliance.assessability_issue_type(type_id) ON UPDATE RESTRICT ON DELETE CASCADE;
-- ALTER TABLE compliance.assessability_issue 
-- DROP CONSTRAINT IF EXISTS compliance_assessability_issue_violation_foreign_key;
-- ALTER TABLE compliance.assessability_issue ADD CONSTRAINT compliance_assessability_issue_violation_foreign_key FOREIGN KEY (violation_id) REFERENCES compliance.violation(id) ON UPDATE RESTRICT ON DELETE CASCADE;

-- insert into compliance.assessability_issue_type (type_id, type_name) VALUES (1, 'empty group') ON CONFLICT DO NOTHING;
-- insert into compliance.assessability_issue_type (type_id, type_name) VALUES (2, 'broadcast address') ON CONFLICT DO NOTHING;
-- insert into compliance.assessability_issue_type (type_id, type_name) VALUES (3, 'DHCP IP undefined address') ON CONFLICT DO NOTHING;
-- insert into compliance.assessability_issue_type (type_id, type_name) VALUES (4, 'dynamic internet address') ON CONFLICT DO NOTHING;

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
