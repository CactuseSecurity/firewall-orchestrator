DROP INDEX IF EXISTS import_control_only_one_null_stop_time_per_mgm_when_null;

CREATE UNIQUE INDEX import_control_only_one_null_stop_time_per_mgm_when_null
    ON import_control
       (mgm_id)
 WHERE stop_time IS NULL;
 