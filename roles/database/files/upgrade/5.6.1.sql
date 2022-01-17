UPDATE report_template SET report_filter = '',
    report_parameters = '{"report_type":1,
                          "device_filter":{"management":[]},
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
                            "open_end": false
                          }}'
    WHERE report_template_owner = 0 AND report_template_name = 'Current Rules';

UPDATE report_template SET report_filter = '', 
    report_parameters = '{"report_type":2,
                          "device_filter":{"management":[]},
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
                            "open_end": false
                          }}'
    WHERE report_template_owner = 0 AND report_template_name = 'This year''s Rule Changes';

UPDATE report_template SET report_filter = '',
    report_parameters = '{"report_type":3,
                          "device_filter":{"management":[]},
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
                            "open_end": false
                          }}'
    WHERE report_template_owner = 0 AND report_template_name = 'Basic Statistics';

UPDATE report_template SET report_filter = '(src=any or dst=any or svc=any or src=all or dst=all or svc=all) and not(action=drop or action=reject or action=deny) ',
    report_parameters = '{"report_type":1,
                          "device_filter":{"management":[]},
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
                            "open_end": false
                          }}'
    WHERE report_template_owner = 0 AND report_template_name = 'Compliance: Pass rules with ANY';

UPDATE report_template SET report_filter = '',
    report_parameters = '{"report_type":4,
                          "device_filter":{"management":[]},
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
                            "open_end": false
                          }}'
    WHERE report_template_owner = 0 AND report_template_name = 'Current NAT Rules';
