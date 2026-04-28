alter table notification add column if not exists recipient_bcc Varchar;
alter table notification add column if not exists email_address_bcc Varchar;

INSERT INTO "report_template" ("report_filter","report_template_name","report_template_comment","report_template_owner", "report_parameters")
SELECT '',
    'Last Week Approved Tickets','T0110', 0,
    '{"report_type":42,"device_filter":{"management":[]},
        "time_filter": {
            "is_shortcut": true,
            "shortcut": "now",
            "report_time": "2022-01-01T00:00:00.0000000+01:00",
            "timerange_type": "SHORTCUT",
            "shortcut_range": "last week",
            "offset": 0,
            "interval": "DAYS",
            "start_time": "2022-01-01T00:00:00.0000000+01:00",
            "end_time": "2022-01-01T00:00:00.0000000+01:00",
            "open_start": false,
            "open_end": false},
        "workflow_filter": {
            "reference_date": "Approved"}}'
WHERE NOT EXISTS
(
    SELECT 1
    FROM report_template
    WHERE report_template_name = 'Last Week Approved Tickets'
);
