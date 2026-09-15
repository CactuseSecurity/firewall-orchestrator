-- migrate interface request notification text from legacy config to notification rows
WITH request_config AS
(
    SELECT
        MAX(CASE WHEN config_key = 'modReqEmailReceiver' THEN COALESCE(config_value, '') END) AS recipients,
        MAX(CASE WHEN config_key = 'modReqEmailSubject' THEN COALESCE(config_value, '') END) AS subject,
        MAX(CASE WHEN config_key = 'modReqEmailBody' THEN COALESCE(config_value, '') END) AS body,
        MAX(CASE WHEN config_key = 'modUnansweredReqEmailBody' THEN COALESCE(config_value, '') END) AS reminder_body
    FROM config
    WHERE config_user = 0
      AND config_key IN ('modReqEmailReceiver', 'modReqEmailSubject', 'modReqEmailBody', 'modUnansweredReqEmailBody')
),
initial_notification_seed AS
(
    SELECT COUNT(*) AS notification_count
    FROM notification
    WHERE notification_client = 'InterfaceRequest'
      AND (deadline = 'None' OR deadline IS NULL)
),
reminder_notification_seed AS
(
    SELECT COUNT(*) AS notification_count
    FROM notification
    WHERE notification_client = 'InterfaceRequest'
      AND deadline = 'RequestDate'
),
insert_initial_notification AS
(
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
        deadline,
        interval_before_deadline,
        offset_before_deadline,
        repeat_interval_after_deadline,
        initial_offset_after_deadline,
        repeat_offset_after_deadline,
        repetitions_after_deadline
    )
    SELECT
        'InterfaceRequest',
        'Interface requested',
        'Email',
        CASE
            WHEN recipients = '' THEN 'None'
            WHEN recipients LIKE '{%' THEN 'ConfiguredResponsibles'
            ELSE 'OtherAddresses'
        END,
        recipients,
        'None',
        '',
        'None',
        '',
        CASE
            WHEN LENGTH(subject) = 0 THEN 'Interface requested'
            ELSE subject
        END,
        body,
        'SimpleText',
        'None',
        NULL,
        NULL,
        NULL,
        NULL,
        NULL,
        NULL
    FROM request_config
    CROSS JOIN initial_notification_seed
    WHERE notification_count = 0
      AND recipients <> ''
    RETURNING 1
),
update_initial_bodies AS
(
    UPDATE notification n
    SET email_body = CASE
        WHEN COALESCE(n.email_body, '') = '' THEN request_config.body
        ELSE n.email_body
    END
    FROM request_config
    CROSS JOIN initial_notification_seed
    WHERE n.notification_client = 'InterfaceRequest'
      AND (n.deadline = 'None' OR n.deadline IS NULL)
      AND initial_notification_seed.notification_count > 0
    RETURNING 1
),
update_reminder_bodies AS
(
    UPDATE notification n
    SET email_body = CASE
        WHEN COALESCE(n.email_body, '') = '' THEN request_config.reminder_body
        ELSE n.email_body
    END
    FROM request_config
    CROSS JOIN reminder_notification_seed
    WHERE n.notification_client = 'InterfaceRequest'
      AND n.deadline = 'RequestDate'
      AND reminder_notification_seed.notification_count > 0
    RETURNING 1
)
SELECT 1;

-- migrate rule-by-rule recertification delivery to notifications
WITH recert_config AS
(
    SELECT
        MAX(CASE WHEN config_key = 'recCheckEmailSubject' THEN COALESCE(config_value, '') END) AS subject
    FROM config
    WHERE config_user = 0
      AND config_key = 'recCheckEmailSubject'
),
recert_notification_seed AS
(
    SELECT COUNT(*) AS notification_count
    FROM notification
    WHERE notification_client = 'RuleRecertification'
),
insert_rule_recertification_notification AS
(
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
        deadline,
        interval_before_deadline,
        offset_before_deadline,
        repeat_interval_after_deadline,
        initial_offset_after_deadline,
        repeat_offset_after_deadline,
        repetitions_after_deadline
    )
    SELECT
        'RuleRecertification',
        'Rule recertification',
        'Email',
        'AllOwnerResponsibles',
        '',
        'None',
        '',
        'None',
        '',
        CASE
            WHEN COALESCE(recert_config.subject, '') = '' THEN 'Rule recertification @@APPNAME@@'
            ELSE recert_config.subject || ' @@APPNAME@@'
        END,
        '@@CONTENT@@',
        'SimpleText',
        'None',
        NULL,
        NULL,
        NULL,
        NULL,
        NULL,
        NULL
    FROM recert_config
    CROSS JOIN recert_notification_seed
    WHERE notification_count = 0
    RETURNING 1
)
SELECT 1;

ALTER TABLE notification
    ADD COLUMN IF NOT EXISTS logging Varchar NOT NULL DEFAULT 'send_only';

ALTER TABLE notification
    ADD COLUMN IF NOT EXISTS active Boolean NOT NULL DEFAULT TRUE;

UPDATE notification
SET logging = 'send_only'
WHERE NULLIF(logging, '') IS NULL;

CREATE TABLE IF NOT EXISTS notification_log
(
    id SERIAL PRIMARY KEY,
    "timestamp" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    notification_id INTEGER NOT NULL,
    notification_type Varchar NOT NULL,
    "to" Varchar NOT NULL DEFAULT '',
    cc Varchar NOT NULL DEFAULT '',
    bcc Varchar NOT NULL DEFAULT '',
    subject Varchar NOT NULL DEFAULT '',
    deadline_type Varchar NOT NULL DEFAULT 'None',
    deadline TIMESTAMP WITH TIME ZONE,
    status Varchar NOT NULL DEFAULT 'Pending',
    error Varchar NOT NULL DEFAULT ''
);

WITH decomm_config AS
(
    SELECT
        MAX(CASE WHEN config_key = 'modDecommEmailReceiver' THEN COALESCE(config_value, '') END) AS recipients,
        MAX(CASE WHEN config_key = 'modDecommEmailOtherAddresses' THEN COALESCE(config_value, '') END) AS other_addresses,
        MAX(CASE WHEN config_key = 'modDecommEmailSubject' THEN COALESCE(config_value, '') END) AS subject,
        MAX(CASE WHEN config_key = 'modDecommEmailBody' THEN COALESCE(config_value, '') END) AS body
    FROM config
    WHERE config_user = 0
      AND config_key IN ('modDecommEmailReceiver', 'modDecommEmailOtherAddresses', 'modDecommEmailSubject', 'modDecommEmailBody')
),
decomm_notification_seed AS
(
    SELECT COUNT(*) AS notification_count
    FROM notification
    WHERE notification_client = 'InterfaceDecomm'
      AND (deadline = 'None' OR deadline IS NULL)
),
insert_decomm_notification AS
(
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
        deadline,
        interval_before_deadline,
        offset_before_deadline,
        repeat_interval_after_deadline,
        initial_offset_after_deadline,
        repeat_offset_after_deadline,
        repetitions_after_deadline
    )
    SELECT
        'InterfaceDecomm',
        'Interface decommissioned',
        'Email',
        CASE
            WHEN recipients = '' AND other_addresses <> '' THEN 'OtherAddresses'
            WHEN recipients = '' THEN 'None'
            ELSE recipients
        END,
        other_addresses,
        'None',
        '',
        'None',
        '',
        CASE
            WHEN LENGTH(subject) = 0 THEN 'Interface decommissioned'
            ELSE subject
        END,
        body,
        'SimpleText',
        'None',
        NULL,
        NULL,
        NULL,
        NULL,
        NULL,
        NULL
    FROM decomm_config
    CROSS JOIN decomm_notification_seed
    WHERE notification_count = 0
      AND (
          LENGTH(recipients) > 0
          OR LENGTH(other_addresses) > 0
          OR LENGTH(subject) > 0
          OR LENGTH(body) > 0
      )
    RETURNING 1
),
update_decomm_subject_bodies AS
(
    UPDATE notification n
    SET
        email_subject = CASE
            WHEN COALESCE(n.email_subject, '') = '' THEN CASE WHEN LENGTH(decomm_config.subject) = 0 THEN 'Interface decommissioned' ELSE decomm_config.subject END
            ELSE n.email_subject
        END,
        email_body = CASE
            WHEN COALESCE(n.email_body, '') = '' THEN decomm_config.body
            ELSE n.email_body
        END
    FROM decomm_config
    CROSS JOIN decomm_notification_seed
    WHERE n.notification_client = 'InterfaceDecomm'
      AND (n.deadline = 'None' OR n.deadline IS NULL)
      AND decomm_notification_seed.notification_count > 0
    RETURNING 1
)
SELECT 1;
