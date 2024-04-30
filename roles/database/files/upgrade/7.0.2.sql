INSERT INTO "report_template" ("report_filter","report_template_name","report_template_comment","report_template_owner", "report_parameters") 
SELECT '','Last year''s Unused Rules','T0106', 0,
        '{"report_type":10,"device_filter":{"management":[]},
            "time_filter": {
                "is_shortcut": true,
                "shortcut": "now",
                "report_time": "2022-01-01T00:00:00.0000000+01:00",
                "timerange_type": "SHORTCUT",
                "shortcut_range": "this year",
                "offset": 0,
                "interval": "DAYS",
                "start_time": "2022-01-01T00:00:00.0000000+01:00",
                "end_time": "2022-01-01T00:00:00.0000000+01:00",
                "open_start": false,
                "open_end": false},
            "unused_filter": {
                "creationTolerance": 0,
                "unusedForDays": 365}}'
WHERE NOT EXISTS (SELECT * FROM report_template WHERE report_template_owner = 0 AND report_template_comment = 'T0106');

INSERT INTO "report_template" ("report_filter","report_template_name","report_template_comment","report_template_owner", "report_parameters") 
SELECT '','Next Month''s Recertifications','T0107', 0,
        '{"report_type":7,"device_filter":{"management":[]},
            "time_filter": {
                "is_shortcut": true,
                "shortcut": "now",
                "report_time": "2022-01-01T00:00:00.0000000+01:00",
                "timerange_type": "SHORTCUT",
                "shortcut_range": "this year",
                "offset": 0,
                "interval": "DAYS",
                "start_time": "2022-01-01T00:00:00.0000000+01:00",
                "end_time": "2022-01-01T00:00:00.0000000+01:00",
                "open_start": false,
                "open_end": false},
            "recert_filter": {
                "recertOwnerList": [],
                "recertShowAnyMatch": true,
                "recertificationDisplayPeriod": 30}}'
WHERE NOT EXISTS (SELECT * FROM report_template WHERE report_template_owner = 0 AND report_template_comment = 'T0107');

