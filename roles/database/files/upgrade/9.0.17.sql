ALTER TABLE ext_request
    ADD COLUMN IF NOT EXISTS lock_owner varchar,
    ADD COLUMN IF NOT EXISTS lock_acquired_at Timestamp with time zone,
    ADD COLUMN IF NOT EXISTS lock_expires_at Timestamp with time zone;

UPDATE ext_request
SET lock_expires_at = NOW() - interval '1 second'
WHERE locked = true
  AND lock_expires_at IS NULL;
