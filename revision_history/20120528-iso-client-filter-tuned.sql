/*		

web-Zweig: komplett neu

*/

SET statement_timeout = 0;
SET client_encoding = 'UTF8';
SET standard_conforming_strings = off;
SET check_function_bodies = false;
SET client_min_messages = warning;
SET escape_string_warning = off;
SET search_path = public, pg_catalog;

CREATE OR REPLACE FUNCTION get_rule_ids_no_client_filter(int4, "timestamp", cidr, cidr, cidr, int4, int4, VARCHAR)
  RETURNS SETOF int4 AS
$BODY$
DECLARE
    i_device_id ALIAS FOR $1;
    t_in_report_time ALIAS FOR $2;
--    i_client_id ALIAS FOR $3;
    c_ip_src ALIAS FOR $3;
    c_ip_dst ALIAS FOR $4;
    c_ip_anywhere ALIAS FOR $5;
    i_proto ALIAS FOR $6;
    i_port ALIAS FOR $7;
    v_admin_view_filter ALIAS FOR $8;
    i_relevant_import_id INTEGER;					-- ID des Imports, direkt vor dem Report-Zeitpunkt
    v_client_filter_ip_list VARCHAR;				-- Filter-Liste mit allen IP-Bereichen des Clients
    v_client_filter_ip_list_negated VARCHAR;		-- Filter-Liste mit allen IP-Bereichen des Clients fuer negierte Faelle
    r_rule RECORD;									-- temp. Variable fuer Rule-ID
    t_report_time TIMESTAMP;						-- Zeitpunkt des Reports (jetzt, wenn t_in_report_time IS NULL)
    v_sql_get_rules_with_client_src_ips VARCHAR;	-- SQL-Code zum Holen der Rule-IDs mit Quellen im Client-Bereich
    v_sql_get_rules_with_client_dst_ips VARCHAR;	-- SQL-Code zum Holen der Rule-IDs mit Zielen im Client-Bereich
	v_error_str VARCHAR;
	v_dev_filter VARCHAR; 							-- filter for devices (true for all devices)
	v_import_filter VARCHAR;						-- filter for imports
	v_select_statement VARCHAR;
	v_order_statement VARCHAR;
    v_src_ip_filter VARCHAR;						-- Filter fuer source ip match
    v_dst_ip_filter VARCHAR;						-- Filter fuer destination ip match
BEGIN
	v_order_statement := '';
	IF t_in_report_time IS NULL THEN t_report_time := now(); --	no report time given, assuming now()
	ELSE t_report_time := t_in_report_time; END IF;
	-- set filter: a) import filter, b) device filter
	IF i_device_id IS NULL THEN   -- ueber alle Devices
		v_import_filter := get_previous_import_ids(t_report_time);
		IF v_import_filter = ' () ' THEN v_import_filter := ' FALSE ';
		ELSE v_import_filter := 'rule_order.control_id IN ' || get_previous_import_ids(t_report_time); END IF;
		v_dev_filter := ' TRUE ';
	ELSE 
		i_relevant_import_id := get_previous_import_id(i_device_id, t_report_time);
	    IF i_relevant_import_id IS NULL THEN
			v_error_str := 'device_id: ' || CAST(i_device_id AS VARCHAR) || ', time: ' || CAST(t_report_time AS VARCHAR);
    	    PERFORM error_handling('WARN_NO_IMP_ID_FOUND', v_error_str);
			v_import_filter := ' FALSE ';
		ELSE    	    
			v_import_filter := 'rule_order.control_id = ' || CAST(i_relevant_import_id AS VARCHAR);
		END IF;
		v_dev_filter := 'rule_order.dev_id = ' || CAST(i_device_id AS VARCHAR);
	END IF;
	IF c_ip_src IS NULL THEN v_src_ip_filter := ' TRUE ';
	ELSE v_src_ip_filter := ' (object.obj_ip <<= ' || E'\'' || CAST(c_ip_src AS VARCHAR) || E'\'' || ' OR object.obj_ip >>= ' || E'\'' || CAST(c_ip_src AS VARCHAR) || E'\'' || ') '; END IF;
	IF c_ip_dst IS NULL THEN v_dst_ip_filter := ' TRUE ';
	ELSE v_dst_ip_filter := ' (object.obj_ip <<= ' || E'\'' || CAST(c_ip_dst AS VARCHAR) || E'\'' || ' OR object.obj_ip >>= ' || E'\'' || CAST(c_ip_dst AS VARCHAR) || E'\'' || ') '; END IF;
	v_select_statement :=
		' (SELECT rule_id FROM rule_order LEFT JOIN rule USING (rule_id) LEFT JOIN rule_from USING (rule_id) LEFT JOIN objgrp_flat ON (rule_from.obj_id=objgrp_flat_member_id) ' ||
		' LEFT JOIN object ON (objgrp_flat_member_id=object.obj_id) WHERE ' || v_import_filter || ' AND ' || v_dev_filter ||
		' AND ' || v_src_ip_filter || ' AND ' || v_admin_view_filter || ' AND rule.rule_head_text IS NULL AND NOT rule_disabled AND rule_action<>' ||
		E'\'' || 'drop' || E'\'' || ' AND rule_action<>' ||
		E'\'' || 'reject' || E'\'' || ' AND rule_action<>' || E'\'' || 'deny' || E'\'' || ')' ||
		' INTERSECT ' ||
		' (SELECT rule_id FROM rule_order LEFT JOIN rule USING (rule_id) LEFT JOIN rule_to USING (rule_id) LEFT JOIN objgrp_flat ON (rule_to.obj_id=objgrp_flat_member_id) ' ||
		' LEFT JOIN object ON (objgrp_flat_member_id=object.obj_id) WHERE ' || v_import_filter || ' AND ' || v_dev_filter ||
		' AND ' || v_dst_ip_filter || ' AND ' || v_admin_view_filter  || ' AND rule.rule_head_text IS NULL AND NOT rule_disabled AND rule_action<>' ||
		E'\'' || 'drop' || E'\'' || ' AND rule_action<>' ||
		E'\'' || 'reject' || E'\'' || ' AND rule_action<>' || E'\'' || 'deny' || E'\'' 
		-- || ' GROUP BY rule_id' 
		|| ')';
	FOR r_rule IN EXECUTE v_select_statement	
	LOOP
		RETURN NEXT r_rule.rule_id;
	END LOOP;
	RETURN;
END;
$BODY$ LANGUAGE plpgsql;

CREATE OR REPLACE FUNCTION get_rule_ids_no_client_filter(int4, "timestamp", VARCHAR) RETURNS SETOF int4 AS
$BODY$
DECLARE
    i_device_id ALIAS FOR $1;
    t_in_report_time ALIAS FOR $2;
    v_admin_view_filter ALIAS FOR $3;
    i_relevant_import_id INTEGER;					-- ID des Imports, direkt vor dem Report-Zeitpunkt
    r_rule RECORD;									-- temp. Variable fuer Rule-ID
    t_report_time TIMESTAMP;						-- Zeitpunkt des Reports (jetzt, wenn t_in_report_time IS NULL)
 	v_error_str VARCHAR;
	v_dev_filter VARCHAR; 							-- filter for devices (true for all devices)
	v_import_filter VARCHAR;						-- filter for imports
	v_select_statement VARCHAR;
	v_order_statement VARCHAR;
BEGIN
	v_order_statement := '';
	IF t_in_report_time IS NULL THEN --	no report time given, assuming now()
		t_report_time := now();
	ELSE
		t_report_time := t_in_report_time;
	END IF;
	-- set filter: a) import filter, b) device filter
	IF i_device_id IS NULL THEN   -- ueber alle Devices
		v_import_filter := get_previous_import_ids(t_report_time);
		IF v_import_filter = ' () ' THEN
			v_import_filter := ' FALSE ';
		ELSE
			v_import_filter := 'rule_order.control_id IN ' ||  get_previous_import_ids(t_report_time);
		END IF;
		v_dev_filter := ' TRUE ';
	ELSE 
		i_relevant_import_id := get_previous_import_id(i_device_id, t_report_time);
		IF i_relevant_import_id IS NULL THEN
			v_error_str := 'device_id: ' || CAST(i_device_id AS VARCHAR) || ', time: ' || CAST(t_report_time AS VARCHAR);
			PERFORM error_handling('WARN_NO_IMP_ID_FOUND', v_error_str);
			v_import_filter := ' FALSE ';
		ELSE    	    
			v_import_filter := 'rule_order.control_id = ' || CAST(i_relevant_import_id AS VARCHAR);
		END IF;
		v_dev_filter := 'rule_order.dev_id = ' || CAST(i_device_id AS VARCHAR);
	END IF;
	v_select_statement := 'SELECT rule_id FROM rule_order INNER JOIN device USING (dev_id) INNER JOIN management USING (mgm_id) WHERE ' || v_import_filter
		|| ' AND ' || v_dev_filter || ' AND ' || v_admin_view_filter || v_order_statement;
	FOR r_rule IN EXECUTE v_select_statement	
	LOOP
		RETURN NEXT r_rule.rule_id;
	END LOOP;
	RETURN;
END;
$BODY$
  LANGUAGE 'plpgsql' VOLATILE;