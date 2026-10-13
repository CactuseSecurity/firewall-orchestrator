-- Implementation tasks may be implemented on a different management than their request task, e.g. an object
-- task on every sub-management of a super-management. NULL keeps the previous behaviour: the task is implemented
-- on the management of its request task.
ALTER TABLE request.impltask ADD COLUMN IF NOT EXISTS mgm_id int;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname = 'request_impltask_management_foreign_key'
        AND conrelid = 'request.impltask'::regclass
    ) THEN
        ALTER TABLE request.impltask
        ADD CONSTRAINT request_impltask_management_foreign_key
        FOREIGN KEY (mgm_id) REFERENCES management (mgm_id)
        ON UPDATE RESTRICT ON DELETE CASCADE;
    END IF;
END $$;
