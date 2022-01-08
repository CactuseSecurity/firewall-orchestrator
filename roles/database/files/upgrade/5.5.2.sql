INSERT INTO "report_template" ("report_filter","report_template_name","report_template_comment","report_template_owner") 
    VALUES ('type=natrules and time=now ','Current NAT Rules','T0105', 0) ON CONFLICT DO NOTHING;
