-- email priority of a notification: Normal (no priority headers), Low or High;
-- only the values of the C# enum EmailPriority may be stored, otherwise loading the notifications fails
ALTER TABLE notification
    ADD COLUMN IF NOT EXISTS email_priority Varchar NOT NULL DEFAULT 'Normal'
    CONSTRAINT notification_email_priority_valid CHECK (email_priority IN ('Normal', 'Low', 'High'));
