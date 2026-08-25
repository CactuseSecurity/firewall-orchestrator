CREATE INDEX IF NOT EXISTS idx_rule_standard_report_page
ON rule (mgm_id, rulebase_id, rule_num_numeric, rule_id)
WHERE access_rule = TRUE;
