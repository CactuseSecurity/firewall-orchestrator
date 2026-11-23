-- Test the email priority column, its constraint and the repeatability of upgrade/9.9.4.sql.
-- The test task inserts the upgrade file at both run-upgrade markers; the transaction is rolled back.
\set ON_ERROR_STOP 1
BEGIN;
-- simulate a database before the email priority, which has no such column yet
ALTER TABLE notification DROP COLUMN IF EXISTS email_priority;

CREATE TEMP TABLE email_priority_probe (notification_id int) ON COMMIT DROP;
WITH inserted AS (
    INSERT INTO notification (notification_client, name, channel, layout)
    VALUES ('upgrade-email-priority-probe', 'existing', 'Email', 'SimpleText')
    RETURNING id
)
INSERT INTO email_priority_probe SELECT id FROM inserted;

-- @run-upgrade-9.9.4

DO $$
DECLARE
    i_notification_id int;
    i_loaded int;
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'notification' AND column_name = 'email_priority'
            AND is_nullable = 'NO' AND column_default LIKE '''Normal''%'
    ) THEN
        RAISE EXCEPTION 'upgrade 9.9.4: non-nullable email_priority column with default Normal is missing';
    END IF;
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conrelid = 'notification'::regclass
            AND conname = 'notification_email_priority_valid'
    ) THEN
        RAISE EXCEPTION 'upgrade 9.9.4: email priority check constraint is missing';
    END IF;
    SELECT notification_id INTO i_notification_id FROM email_priority_probe;
    IF (SELECT email_priority FROM notification WHERE id = i_notification_id) IS DISTINCT FROM 'Normal' THEN
        RAISE EXCEPTION 'upgrade 9.9.4: existing notification did not receive the priority Normal';
    END IF;

    -- every valid value can be written
    INSERT INTO notification (notification_client, name, channel, layout, email_priority)
    VALUES
        ('upgrade-email-priority-probe', 'low', 'Email', 'SimpleText', 'Low'),
        ('upgrade-email-priority-probe', 'high', 'Email', 'SimpleText', 'High');

    -- an out-of-domain value is rejected on insert and update
    BEGIN
        INSERT INTO notification (notification_client, name, channel, layout, email_priority)
        VALUES ('upgrade-email-priority-probe', 'rejected', 'Email', 'SimpleText', 'Urgent');
        RAISE EXCEPTION 'upgrade 9.9.4: an unknown email priority passed the constraint on insert';
    EXCEPTION
        WHEN check_violation THEN NULL;
    END;
    BEGIN
        UPDATE notification SET email_priority = 'high' WHERE id = i_notification_id;
        RAISE EXCEPTION 'upgrade 9.9.4: a wrongly cased email priority passed the constraint on update';
    EXCEPTION
        WHEN check_violation THEN NULL;
    END;

    -- the rejected writes left the notifications of the client loadable and unchanged
    SELECT count(*) INTO i_loaded
    FROM notification
    WHERE notification_client = 'upgrade-email-priority-probe'
        AND email_priority IN ('Normal', 'Low', 'High');
    IF i_loaded <> 3 THEN
        RAISE EXCEPTION 'upgrade 9.9.4: expected 3 loadable probe notifications, found %', i_loaded;
    END IF;
    UPDATE notification SET email_priority = 'Low' WHERE id = i_notification_id;
END $$;

-- @run-upgrade-9.9.4

DO $$
BEGIN
    IF (SELECT count(*) FROM pg_constraint
        WHERE conrelid = 'notification'::regclass
            AND conname = 'notification_email_priority_valid') <> 1 THEN
        RAISE EXCEPTION 'upgrade 9.9.4: rerunning the migration changed the email priority constraint';
    END IF;
    IF (SELECT email_priority FROM notification
        WHERE id = (SELECT notification_id FROM email_priority_probe)) IS DISTINCT FROM 'Low' THEN
        RAISE EXCEPTION 'upgrade 9.9.4: rerunning the migration changed an existing email priority';
    END IF;
END $$;
ROLLBACK;
\echo '9.9.4 email priority migration and idempotency checks passed'
