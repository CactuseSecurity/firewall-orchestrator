
-- we need to modify the altkey constraint for rule from mgm_id to dev_id
-- reason: we can now have the same (global) rule (uid) more than once in different rulesets of the same management

ALTER TABLE public.rule drop CONSTRAINT rule_altkey;
Alter Table "rule" add Constraint "rule_altkey" UNIQUE ("dev_id","rule_uid","rule_create");
