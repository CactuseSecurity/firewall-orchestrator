
-- setting indices on view_rule_change to improve performance
-- DROP index if exists idx_changelog_rule04;
-- Create index IF NOT EXISTS idx_changelog_rule04 on changelog_rule (change_action);

-- DROP index if exists idx_changelog_rule05;
-- Create index IF NOT EXISTS idx_changelog_rule05 on changelog_rule (new_rule_id);

-- DROP index if exists idx_changelog_rule06;
-- Create index IF NOT EXISTS idx_changelog_rule06 on changelog_rule (old_rule_id);

-- DROP index if exists idx_rule04;
-- Create index IF NOT EXISTS idx_rule04 on rule (access_rule);


-- alter table report add column if not exists report_schedule_id bigint;
-- ALTER TABLE report DROP CONSTRAINT IF EXISTS "report_report_schedule_report_schedule_id_fkey" CASCADE;
-- Alter table report add CONSTRAINT report_report_schedule_report_schedule_id_fkey foreign key (report_schedule_id) references report_schedule (report_schedule_id) on update restrict on delete cascade;

insert into config (config_key, config_value, config_user) VALUES ('importCheckCertificates', 'False', 0) ON CONFLICT DO NOTHING;
insert into config (config_key, config_value, config_user) VALUES ('importSuppressCertificateWarnings', 'True', 0) ON CONFLICT DO NOTHING;
insert into config (config_key, config_value, config_user) VALUES ('importFwProxy', '', 0) ON CONFLICT DO NOTHING;
