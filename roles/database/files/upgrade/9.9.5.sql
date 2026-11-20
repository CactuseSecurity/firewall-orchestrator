-- email priority of a notification: Normal (no priority headers), Low or High
ALTER TABLE notification
    ADD COLUMN IF NOT EXISTS email_priority Varchar NOT NULL DEFAULT 'Normal';
