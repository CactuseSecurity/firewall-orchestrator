INSERT INTO "report_template" ("report_filter","report_template_name","report_template_comment","report_template_owner") 
    VALUES ('type=rules and time=now and (src=any or dst=any or svc=any or src=all or dst=all or svc=all) and not(action=drop or action=reject or action=deny) ',
        'Compliance: Rules with ANY','Show all rules that contain any as source, destination or service', NULL);
