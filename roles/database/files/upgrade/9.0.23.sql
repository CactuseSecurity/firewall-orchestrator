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


-- migrate import change notifier to notification service
WITH imp_change_config AS
(
    SELECT
        MAX(CASE WHEN config_key = 'impChangeNotifyRecipients' THEN COALESCE(config_value, '') END) AS recipients,
        MAX(CASE WHEN config_key = 'impChangeNotifySubject' THEN COALESCE(config_value, '') END) AS subject,
        MAX(CASE WHEN config_key = 'impChangeNotifyBody' THEN COALESCE(config_value, '') END) AS body,
        MAX(CASE WHEN config_key = 'impChangeNotifyType' THEN COALESCE(config_value, '0') END) AS layout_id
    FROM config
    WHERE config_user = 0
      AND config_key IN ('impChangeNotifyRecipients', 'impChangeNotifySubject', 'impChangeNotifyBody', 'impChangeNotifyType')
)
INSERT INTO notification
(
    notification_client,
    name,
    channel,
    recipient_to,
    email_address_to,
    recipient_cc,
    email_address_cc,
    recipient_bcc,
    email_address_bcc,
    email_subject,
    email_body,
    layout,
    deadline
)
SELECT
    'ImportChange',
    'Import Change',
    'Email',
    'OtherAddresses',
    recipients,
    'None',
    '',
    'None',
    '',
    CASE WHEN subject = '' THEN 'Import Change Notification' ELSE subject END,
    CASE WHEN body = '' THEN '@@CONTENT@@' ELSE body || '@@CONTENT@@' END,
    CASE layout_id
        WHEN '1' THEN 'HtmlInBody'
        WHEN '10' THEN 'PdfAsAttachment'
        WHEN '11' THEN 'HtmlAsAttachment'
        WHEN '12' THEN 'CsvAsAttachment'
        WHEN '13' THEN 'JsonAsAttachment'
        ELSE 'SimpleText'
    END,
    'None'
FROM imp_change_config
WHERE recipients <> ''
  AND NOT EXISTS
  (
      SELECT 1
      FROM notification
      WHERE notification_client = 'ImportChange'
  );
