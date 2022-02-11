

ALTER TABLE "log_data_issue" ADD COLUMN IF NOT EXISTS "user_id" INTEGER DEFAULT 0;
ALTER TABLE "log_data_issue" ADD COLUMN IF NOT EXISTS "ack_by" INTEGER;
ALTER TABLE "log_data_issue" ADD COLUMN IF NOT EXISTS "ack_timestamp" TIMESTAMP;

ALTER TABLE "log_data_issue" DROP CONSTRAINT IF EXISTS "log_data_issue_uiuser_uiuser_id_fkey" CASCADE;
Alter table "log_data_issue" add CONSTRAINT log_data_issue_uiuser_uiuser_id_fkey foreign key ("user_id") references "uiuser" ("uiuser_id") on update restrict on delete cascade;


DROP FUNCTION IF EXISTS add_data_issue(BIGINT,varchar,varchar,varchar,BIGINT,INT,varchar,varchar,varchar, varchar, int, int, int, timestamp);

CREATE OR REPLACE FUNCTION add_data_issue(BIGINT,varchar,varchar,varchar,BIGINT,INT,INT,varchar,varchar,varchar, varchar, int, timestamp, int) RETURNS VOID AS $$
DECLARE
	i_current_import_id ALIAS FOR $1;
	v_obj_name ALIAS FOR $2;
	v_obj_uid ALIAS FOR $3;
	v_rule_uid ALIAS FOR $4;
    i_rule_id  ALIAS FOR $5;
    i_mgm_id ALIAS FOR $6;
    i_dev_id   ALIAS FOR $7;
	v_obj_type ALIAS FOR $8;
	v_suspected_cause ALIAS FOR $9;
	v_description ALIAS FOR $10;
    v_source ALIAS FOR $11;
    i_severity ALIAS FOR $12;
    t_timestamp ALIAS FOR $13;
    i_user_id ALIAS FOR $14;
    v_log_string VARCHAR;
BEGIN
	INSERT INTO log_data_issue (
        import_id, object_name, object_uid, rule_uid, rule_id, issue_mgm_id, issue_dev_id, object_type, suspected_cause, 
        description, source, severity, issue_mgm_id, issue_dev_id, issue_timestamp, user_id) 
	VALUES (i_current_import_id, v_obj_name, v_obj_uid, v_rule_uid, i_rule_id, i_mgm_id, i_dev_id, v_obj_type, v_suspected_cause, 
        v_description, v_source, i_severity, t_timestamp, i_user_id);
	RETURN;
    v_log_string := 'src=' || v_source || ', sev=' || v_severity;
    IF t_timestamp IS NOT NULL  THEN
        v_log_string := v_log_string || ', time=' || t_timestamp; 
    END IF;
    -- todo: add more issue information
    RAISE INFO '%', v_log_string; -- send the log to syslog as well
END;
$$ LANGUAGE plpgsql;
