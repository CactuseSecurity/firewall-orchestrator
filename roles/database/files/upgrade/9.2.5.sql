-- Reset flow data affected by timezone-invariant time object hashes.
-- Deleting flow.access cascades to all flow.access_* membership tables.
UPDATE import_control
SET flow_sync_done = FALSE
WHERE flow_sync_done = TRUE;

UPDATE public.time_object
SET flow_timeobj_id = NULL,
    flow_active = FALSE
WHERE flow_timeobj_id IS NOT NULL
    OR flow_active = TRUE;

DELETE FROM flow.access;
DELETE FROM flow.timeobject;
