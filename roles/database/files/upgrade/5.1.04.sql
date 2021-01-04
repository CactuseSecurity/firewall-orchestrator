
alter table "report_template" alter column "report_template_create" SET Default now();

INSERT INTO "report_template" ("report_filter","report_template_name","report_template_comment","report_template_owner") 
    VALUES ('type=rules and time=now ','Current Rules','Show currently active rules of all gateways', NULL);
INSERT INTO "report_template" ("report_filter","report_template_name","report_template_comment","report_template_owner") 
    VALUES ('type=changes and time="this year" ','This year''s Rule Changes','Show all rule change performed in the current year', NULL);
INSERT INTO "report_template" ("report_filter","report_template_name","report_template_comment","report_template_owner") 
    VALUES ('type=statistics and time=now ','Basic Statistics','Show number of objects and rules per device', NULL);


