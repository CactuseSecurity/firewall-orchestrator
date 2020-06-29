-- Ersetzen des kompletten web-Zweigs wg. nicht sauberer Verwendung von PEAR::error-hanlding
-- Ersetzen von install/database/iso-rule-refs.sql, die folgende Funktion wurde geaendert:

CREATE OR REPLACE FUNCTION resolve_rule_list (integer,varchar,varchar,integer,integer,varchar,integer) RETURNS VOID AS $$
DECLARE
	i_rule_id ALIAS FOR $1;
	v_dst_table ALIAS FOR $2;
	v_member_string ALIAS FOR $3;
    i_mgm_id ALIAS FOR $4;
	i_zone_id ALIAS FOR $5;
	v_delimiter ALIAS FOR $6;
	i_current_import_id ALIAS FOR $7;
	v_current_member varchar;
BEGIN
	RAISE DEBUG 'import_svc_refhandler_svcgrp_add_group - 1 starting, v_member_string=%', v_member_string;
	RAISE DEBUG 'import_svc_refhandler_svcgrp_add_group - 2 dst_table=%', v_dst_table;
	IF v_member_string IS NULL OR v_member_string='' THEN RETURN; END IF;
	FOR v_current_member IN SELECT member FROM regexp_split_to_table(v_member_string, E'\\' || v_delimiter) AS member LOOP
		IF NOT (v_current_member IS NULL OR v_current_member='') THEN 
			RAISE DEBUG 'resolve_rule_list - 2 adding list refs for %.', v_current_member;
			IF v_dst_table = 'rule_from' THEN
				PERFORM f_add_single_rule_from_element(i_rule_id, v_current_member, i_mgm_id, i_zone_id, i_current_import_id);
			ELSIF v_dst_table = 'rule_to' THEN
				PERFORM f_add_single_rule_to_element(i_rule_id, v_current_member, i_mgm_id, i_zone_id, i_current_import_id);
			ELSIF v_dst_table = 'rule_service' THEN
				PERFORM f_add_single_rule_svc_element(i_rule_id, v_current_member, i_mgm_id, i_current_import_id);
			END IF;
		END IF;
	END LOOP;
	RETURN;
END; 
$$ LANGUAGE plpgsql;