-- remove deprecated, unused rule.rule_num column (ordering is handled by rule_num_numeric)
-- the rule_api view and get_rulebase_for_owner function depend on rule_num and are
-- recreated (without rule_num) afterwards by the idempotent fworch-api-funcs.sql script.
DROP FUNCTION IF EXISTS public.get_rulebase_for_owner(rulebase, integer);
DROP VIEW IF EXISTS public.rule_api CASCADE;

ALTER TABLE rule DROP COLUMN IF EXISTS rule_num;
