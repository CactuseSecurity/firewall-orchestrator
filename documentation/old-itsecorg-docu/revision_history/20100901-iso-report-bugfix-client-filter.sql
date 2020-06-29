/* in /usr/local/itsecorg/web/htdocs/inctxt/reporting_filter_1.inc.php folgende Änderung in Zeile 54+55 vornehmen:

echo $clist->get_simple_client_menue_string($clist->filter_is_mandatory($client_filter));
//				echo $clist->get_simple_client_menue_string(0);	
 
Also Zeile 54 reinnehmen und Zeile 55 auskommentieren.
*/

----------------------------------------------------
-- FUNCTION:	get_rule_ids
-- Zweck:		liefert Tabelle mit Regel-IDs zurueck, die den Filterkriterien entsprechen
-- Parameter1:	Device-ID dessen Regelsatz untersucht wird (erforderlich)
-- Parameter2:	Zeitpunkt zu dem das Regelwerk angezeigt werden soll
-- Parameter2:	wenn NULL: Zeitpunkt = jetzt (also hoechste vorhandene Import-ID, des Devices)
-- Parameter3:	Client-ID des Kunden, fuer den der Report generiert werden soll
-- Parameter3:	wenn NULL: keine Kunden-Filterung: liefere alle Regeln
-- Parameter4:	Filter resultierend aus Einschraenkungen des angemeldeten Benutzers (SQL as Text)
-- RETURNS:		Tabelle mit einer Spalte (rule_id)

CREATE OR REPLACE FUNCTION get_rule_ids(int4, "timestamp", int4, VARCHAR) RETURNS SETOF int4 AS
$BODY$
DECLARE
    i_device_id ALIAS FOR $1;
    t_in_report_time ALIAS FOR $2;
    i_client_id ALIAS FOR $3;
    v_admin_view_filter ALIAS FOR $4;
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
BEGIN
--	RAISE NOTICE 'get_rule_ids parameter device_id: %', i_device_id;
--	RAISE NOTICE 'get_rule_ids parameter in_report_time: %', t_in_report_time;
--	v_order_statement := ' ORDER BY dev_id, rule_number ';
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
	IF i_client_id IS NULL THEN -- einfacher Fall ohne Client-Filter
		v_select_statement := 'SELECT rule_id FROM rule_order INNER JOIN device USING (dev_id) INNER JOIN management USING (mgm_id) WHERE ' || v_import_filter
			|| ' AND ' || v_dev_filter || ' AND ' || v_admin_view_filter || v_order_statement;
	ELSE -- Client-Filter
		v_client_filter_ip_list := get_client_ip_filter(i_client_id);
		v_client_filter_ip_list_negated := get_negated_client_ip_filter(i_client_id);	
		v_sql_get_rules_with_client_src_ips :=
			'(SELECT rule.rule_id FROM rule, rule_order, object,rule_from
			WHERE rule.rule_id = rule_from.rule_id
				AND ' || v_import_filter || ' AND ' || v_dev_filter ||
				 ' AND rule_order.rule_id=rule.rule_id
				AND (((' || v_client_filter_ip_list || ') AND NOT rule.rule_src_neg) OR ((' ||
				v_client_filter_ip_list_negated || ') AND rule.rule_src_neg))' ||
				' AND (rule.rule_id,object.obj_id) IN
				(
					SELECT rule.rule_id,object.obj_id FROM rule_order,rule,rule_from,object
					LEFT JOIN objgrp_flat ON objgrp_flat_id=object.obj_id
					WHERE rule.rule_id = rule_from.rule_id
					AND ' || v_import_filter || ' AND  ' || v_dev_filter || 
					' AND rule_order.rule_id=rule.rule_id AND object.obj_id=rule_from.obj_id
				UNION
					SELECT rule.rule_id,objgrp_flat.objgrp_flat_member_id FROM rule_order,rule,rule_from,object
					LEFT JOIN objgrp_flat ON objgrp_flat_id=object.obj_id
					WHERE rule.rule_id = rule_from.rule_id
					AND ' || v_import_filter || ' AND  ' || v_dev_filter || 
					' AND rule_order.rule_id=rule.rule_id AND object.obj_id=rule_from.obj_id
				)
			)';
		v_sql_get_rules_with_client_dst_ips :=
			'(SELECT rule.rule_id FROM rule,rule_order,object,rule_to WHERE rule.rule_id = rule_to.rule_id
				AND ' || v_import_filter || ' AND  ' || v_dev_filter || ' AND rule_order.rule_id=rule.rule_id
	            AND (((' || v_client_filter_ip_list || ') AND NOT rule.rule_dst_neg) OR ((' ||
    	        v_client_filter_ip_list_negated || ') AND rule.rule_dst_neg))' ||
				' AND (rule.rule_id,object.obj_id) in
				(
					SELECT rule.rule_id,object.obj_id FROM rule_order,rule,rule_to,object
					LEFT JOIN objgrp_flat ON objgrp_flat_id=object.obj_id
					WHERE rule.rule_id = rule_to.rule_id
					AND ' || v_import_filter || ' AND  ' || v_dev_filter || 
					' AND rule_order.rule_id=rule.rule_id AND object.obj_id=rule_to.obj_id
					UNION
					SELECT rule.rule_id,objgrp_flat.objgrp_flat_member_id FROM rule_order,rule,rule_to,object
					LEFT JOIN objgrp_flat ON objgrp_flat_id=object.obj_id
					WHERE rule.rule_id = rule_to.rule_id
					AND ' || v_import_filter || ' AND  ' || v_dev_filter ||
					' AND rule_order.rule_id=rule.rule_id AND object.obj_id=rule_to.obj_id
				)	
			)';
		v_select_statement := 'SELECT rule_id FROM rule_order LEFT JOIN device USING (dev_id) LEFT JOIN management USING (mgm_id) WHERE rule_id IN (' || v_sql_get_rules_with_client_src_ips 
			|| ' UNION ' ||	v_sql_get_rules_with_client_dst_ips || ')' || ' AND ' || v_admin_view_filter || v_order_statement
			|| ' GROUP BY rule_order.rule_id ';
	END IF; -- client_filter set

--	RAISE NOTICE 'get_rule_ids select: %', v_select_statement;
	FOR r_rule IN EXECUTE v_select_statement	
	LOOP
		RETURN NEXT r_rule.rule_id;
	END LOOP;
	RETURN;
END;
$BODY$
  LANGUAGE 'plpgsql' VOLATILE;
ALTER FUNCTION get_rule_ids(int4, "timestamp", int4, varchar) OWNER TO itsecorg;

