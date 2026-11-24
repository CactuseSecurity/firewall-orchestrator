-- A repeating report schedule needs a positive offset, otherwise the report job cannot advance its start time and
-- loops endlessly, which stops all scheduled reports (GHSA-6mh5-c5hj-qjwp). Existing schedules with an offset below 1
-- are set to offset 1 and deactivated, so their owner reviews them before they run again.
UPDATE report_schedule
SET report_schedule_repeat = 1, report_schedule_active = FALSE
WHERE report_schedule_repeat < 1 AND report_schedule_every <> 0;

ALTER TABLE report_schedule ALTER COLUMN report_schedule_repeat SET DEFAULT 1;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conrelid = 'public.report_schedule'::regclass
            AND conname = 'report_schedule_repeat_positive'
    ) THEN
        ALTER TABLE report_schedule
            ADD CONSTRAINT report_schedule_repeat_positive CHECK (report_schedule_repeat > 0 OR report_schedule_every = 0);
    END IF;
END $$;

-- email priority of a notification: Normal (no priority headers), Low or High;
-- only the values of the C# enum EmailPriority may be stored, otherwise loading the notifications fails
ALTER TABLE notification
    ADD COLUMN IF NOT EXISTS email_priority Varchar NOT NULL DEFAULT 'Normal'
    CONSTRAINT notification_email_priority_valid CHECK (email_priority IN ('Normal', 'Low', 'High'));
