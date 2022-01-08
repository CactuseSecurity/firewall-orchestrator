Alter table "report_template" ADD COLUMN IF NOT EXISTS "report_parameters" json;

UPDATE report_template SET report_filter = 'time=now ', report_parameters = '{"report_type":1,"device_filter":{"management":[]}}'
    WHERE report_template_owner = 0 AND report_template_name = 'Current Rules';
UPDATE report_template SET report_filter = 'time="this year" ', report_parameters = '{"report_type":2,"device_filter":{"management":[]}}'
    WHERE report_template_owner = 0 AND report_template_name = 'This year''s Rule Changes';
UPDATE report_template SET report_filter = 'time=now ', report_parameters = '{"report_type":3,"device_filter":{"management":[]}}'
    WHERE report_template_owner = 0 AND report_template_name = 'Basic Statistics';
UPDATE report_template SET report_filter = 'time=now and (src=any or dst=any or svc=any or src=all or dst=all or svc=all) and not(action=drop or action=reject or action=deny) ',
                           report_parameters = '{"report_type":1,"device_filter":{"management":[]}}'
    WHERE report_template_owner = 0 AND report_template_name = 'Compliance: Pass rules with ANY';
UPDATE report_template SET report_filter = 'time=now ', report_parameters = '{"report_type":4,"device_filter":{"management":[]}}'
    WHERE report_template_owner = 0 AND report_template_name = 'Current NAT Rules';
