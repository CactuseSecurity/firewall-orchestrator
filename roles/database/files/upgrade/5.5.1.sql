
-- adding new nat actions for nat rules (xlate_rule only)
insert into stm_action (action_id,action_name) VALUES (21,'NAT src') ON CONFLICT DO NOTHING; -- source ip nat
insert into stm_action (action_id,action_name) VALUES (22,'NAT src, dst') ON CONFLICT DO NOTHING; -- source and destination ip nat
insert into stm_action (action_id,action_name) VALUES (23,'NAT src, dst, svc') ON CONFLICT DO NOTHING; -- source and destination ip nat plus port nat
insert into stm_action (action_id,action_name) VALUES (24,'NAT dst') ON CONFLICT DO NOTHING; -- destination ip nat
insert into stm_action (action_id,action_name) VALUES (25,'NAT dst, svc') ON CONFLICT DO NOTHING; -- destination ip nat plus port nat
insert into stm_action (action_id,action_name) VALUES (26,'NAT svc') ON CONFLICT DO NOTHING; -- port nat
insert into stm_action (action_id,action_name) VALUES (27,'NAT src, svc') ON CONFLICT DO NOTHING; -- source ip nat plus port nat
insert into stm_action (action_id,action_name) VALUES (28,'NAT') ON CONFLICT DO NOTHING; -- generic NAT

ALTER table "rule" ADD Column IF NOT EXISTS 
    "access_rule" BOOLEAN Default TRUE;
ALTER table "rule" ADD Column IF NOT EXISTS 
	"nat_rule" BOOLEAN Default FALSE;
ALTER table "rule" ADD Column IF NOT EXISTS 
	"xlate_rule" BIGINT;

ALTER TABLE "rule"
    DROP CONSTRAINT IF EXISTS "rule_rule_nat_rule_id_fkey" CASCADE;
ALTER TABLE "rule"
    ADD CONSTRAINT rule_rule_nat_rule_id_fkey FOREIGN KEY ("xlate_rule") REFERENCES "rule" ("rule_id") ON UPDATE RESTRICT ON DELETE CASCADE;

-- changing constraint as there can now be two rules with the same uid but one (the original) has a reference to the xlate rule
ALTER TABLE "rule"
    DROP CONSTRAINT IF EXISTS "rule_altkey" CASCADE;
Alter Table "rule" add Constraint "rule_altkey" UNIQUE ("dev_id","rule_uid","rule_create","xlate_rule");

ALTER table "import_rule" ADD Column IF NOT EXISTS
	"rule_type" Varchar Default 'access';

-- set defaults for all existing rules (only for nat-undefined rules):
UPDATE rule SET access_rule = TRUE, nat_rule = FALSE WHERE nat_rule IS NULL;

-- dropping function insert_single_rule as we changed the return value from boolean to bigint
-- DROP FUNCTION insert_single_rule(BIGINT,INTEGER,INTEGER,BIGINT,BOOLEAN);
-- todo: need to re-create fworch-rule-import.sql functions
-- a bit of an order issue here: we would need to drop first and then re-create functions afterwards
-- but this upgrade script runs too late for this
-- work-around applied by dropping function every time roles/database/files/sql/idempotent/fworch-rule-import.sql is executed 

ALTER TABLE device
    ADD COLUMN IF NOT EXISTS 
	"local_rulebase_name" Varchar;
ALTER TABLE device
    ADD COLUMN IF NOT EXISTS 
	"local_rulebase_uid" Varchar;

ALTER TABLE device
    ADD COLUMN IF NOT EXISTS 
	"global_rulebase_name" Varchar;
ALTER TABLE device
    ADD COLUMN IF NOT EXISTS 
	"global_rulebase_uid" Varchar;

ALTER TABLE device
    ADD COLUMN IF NOT EXISTS 
	"package_name" Varchar;
ALTER TABLE device
    ADD COLUMN IF NOT EXISTS 
	"package_uid" Varchar;


CREATE OR REPLACE FUNCTION transform_rulebase_names () RETURNS VOID AS $$
DECLARE
    r_dev RECORD;
BEGIN 

    BEGIN
        SELECT dev_rulebase FROM device;
    EXCEPTION
        WHEN OTHERS THEN
            RAISE NOTICE 'nothing to do - device has no field "dev_rulebase" anymore';
            RETURN;
    END;

    FOR r_dev IN
        SELECT  * FROM device
    LOOP
        IF r_dev.dev_rulebase LIKE '%/%' THEN 
            UPDATE device SET 
                global_rulebase_name = split_part(r_dev.dev_rulebase, '/', 1),
                local_rulebase_name = split_part(r_dev.dev_rulebase, '/', 2) 
            WHERE dev_id=r_dev.dev_id;
        ELSE
            UPDATE device SET 
                local_rulebase_name = r_dev.dev_rulebase
            WHERE dev_id=r_dev.dev_id;
        END IF;
    END LOOP;
	RETURN;
END;
$$ LANGUAGE plpgsql;

-- need to get UIDs later by using auto-discovery

SELECT * FROM transform_rulebase_names ();
DROP FUNCTION transform_rulebase_names();

-- DROP VIEW view_rule_changes; -- CASCADE;

CREATE OR REPLACE VIEW view_rule_changes AS
	SELECT     -- first select for deleted rules (join over old_rule_id)
		abs_change_id,
		log_rule_id AS local_change_id,
		change_request_info,
		CAST('rule' AS VARCHAR) as change_element,
		CAST('rule_element' AS VARCHAR) as change_element_order,
		changelog_rule.old_rule_id AS old_id,	
		changelog_rule.new_rule_id AS new_id,	
		changelog_rule.documented as change_documented,
		changelog_rule.change_type_id as change_type_id,
		change_action as change_type,
		changelog_rule_comment as change_comment,
		rule_comment as obj_comment,
		import_control.start_time AS change_time, 
		management.mgm_name AS mgm_name, 
		management.mgm_id AS mgm_id,
		device.dev_name,		
		device.dev_id,		
		CAST(t_change_admin.uiuser_first_name || ' ' || t_change_admin.uiuser_last_name AS VARCHAR) AS change_admin,
		t_change_admin.uiuser_id AS change_admin_id,
		CAST (t_doku_admin.uiuser_first_name || ' ' || t_doku_admin.uiuser_last_name AS VARCHAR) AS doku_admin,
		t_doku_admin.uiuser_id AS doku_admin_id,
		security_relevant,
		CAST((COALESCE (rule.rule_ruleid, rule.rule_uid) || ', Rulebase: ' || device.local_rulebase_name) AS VARCHAR) AS unique_name,
		CAST (NULL AS VARCHAR) AS change_diffs,
		CAST (NULL AS VARCHAR) AS change_new_element
	FROM
		changelog_rule
		LEFT JOIN (import_control LEFT JOIN management using (mgm_id)) using (control_id)
		LEFT JOIN rule ON (old_rule_id=rule_id)
		LEFT JOIN device ON (changelog_rule.dev_id=device.dev_id)
		LEFT JOIN uiuser AS t_change_admin ON (t_change_admin.uiuser_id=changelog_rule.import_admin)
		LEFT JOIN uiuser AS t_doku_admin ON (changelog_rule.doku_admin=t_doku_admin.uiuser_id)
	WHERE changelog_rule.change_action='D' AND change_type_id = 3 AND security_relevant AND successful_import

	UNION

	SELECT   -- second select for changed or inserted rules (join over new_rule_id)
		abs_change_id,
		log_rule_id AS local_change_id,
		change_request_info,
		CAST('rule' AS VARCHAR) as change_element,
		CAST('rule_element' AS VARCHAR) as change_element_order,
		changelog_rule.old_rule_id AS old_id,	
		changelog_rule.new_rule_id AS new_id,	
		changelog_rule.documented as change_documented,
		changelog_rule.change_type_id as change_type_id,
		change_action as change_type,
		changelog_rule_comment as change_comment,
		rule_comment as obj_comment,
		import_control.start_time AS change_time, 
		management.mgm_name AS mgm_name, 
		management.mgm_id AS mgm_id,
		device.dev_name,		
		device.dev_id,		
		CAST(t_change_admin.uiuser_first_name || ' ' || t_change_admin.uiuser_last_name AS VARCHAR) AS change_admin,
		t_change_admin.uiuser_id AS change_admin_id,
		CAST (t_doku_admin.uiuser_first_name || ' ' || t_doku_admin.uiuser_last_name AS VARCHAR) AS doku_admin,
		t_doku_admin.uiuser_id AS doku_admin_id,
		security_relevant,
		CAST((COALESCE (rule.rule_ruleid, rule.rule_uid) || ', Rulebase: ' || device.local_rulebase_name) AS VARCHAR) AS unique_name,
		CAST (NULL AS VARCHAR) AS change_diffs,
		CAST (NULL AS VARCHAR) AS change_new_element
	FROM
		changelog_rule
		LEFT JOIN (import_control LEFT JOIN management using (mgm_id)) using (control_id)
		LEFT JOIN rule ON (new_rule_id=rule_id)
		LEFT JOIN device ON (changelog_rule.dev_id=device.dev_id)
		LEFT JOIN uiuser AS t_change_admin ON (t_change_admin.uiuser_id=changelog_rule.import_admin)
		LEFT JOIN uiuser AS t_doku_admin ON (changelog_rule.doku_admin=t_doku_admin.uiuser_id)
	WHERE changelog_rule.change_action<>'D' AND change_type_id = 3 AND security_relevant AND successful_import;


ALTER TABLE device
    DROP COLUMN IF EXISTS 
	"dev_rulebase";
