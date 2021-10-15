
-- adding new nat actions for nat rules (xlate_rule only)
insert into stm_action (action_id,action_name) VALUES (21,'NAT src') ON CONFLICT DO NOTHING; -- source ip nat
insert into stm_action (action_id,action_name) VALUES (22,'NAT src, dst') ON CONFLICT DO NOTHING; -- source and destination ip nat
insert into stm_action (action_id,action_name) VALUES (23,'NAT src, dst, svc') ON CONFLICT DO NOTHING; -- source and destination ip nat plus port nat
insert into stm_action (action_id,action_name) VALUES (24,'NAT dst') ON CONFLICT DO NOTHING; -- destination ip nat
insert into stm_action (action_id,action_name) VALUES (25,'NAT dst, svc') ON CONFLICT DO NOTHING; -- destination ip nat plus port nat
insert into stm_action (action_id,action_name) VALUES (26,'NAT svc') ON CONFLICT DO NOTHING; -- port nat
insert into stm_action (action_id,action_name) VALUES (27,'NAT src, svc') ON CONFLICT DO NOTHING; -- source ip nat plus port nat

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
DROP FUNCTION insert_single_rule(BIGINT,INTEGER,INTEGER,BIGINT,BOOLEAN);
-- todo: need to re-create fworch-rule-import.sql functions
-- todo: need to re-create import_config_from_jsonb (removed import_all_main for testing)