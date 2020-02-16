/*
 web-Zweig und importer-Zweig komplett
 
 
root@itsecorg:/etc/postgresql/8.4/main# grep textreader /etc/postgresql/8.4/main/pg_hba.conf 
host    all         textreader          127.0.0.1/32       trust

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
  

GRANT DELETE ON TABLE client_network TO isoadmins;

CREATE OR REPLACE FUNCTION get_report_typ_list_eng(REFCURSOR) RETURNS REFCURSOR AS $$
DECLARE
	r_config RECORD;
BEGIN
	SELECT INTO r_config * FROM config;
	OPEN $1 FOR
		SELECT report_typ_id,report_typ_name_english as report_typ_name
			FROM stm_report_typ
			ORDER BY report_typ_id;
    RETURN $1;
END;
$$ LANGUAGE plpgsql;

CREATE OR REPLACE FUNCTION get_report_typ_list_ger(REFCURSOR) RETURNS REFCURSOR AS $$
DECLARE
	r_config RECORD;
BEGIN
	SELECT INTO r_config * FROM config;
	OPEN $1 FOR
		SELECT report_typ_id,report_typ_name_german as report_typ_name
			FROM stm_report_typ
			ORDER BY report_typ_id;
    RETURN $1;
END;
$$ LANGUAGE plpgsql;
		
CREATE ROLE textreader LOGIN
  NOSUPERUSER NOINHERIT NOCREATEDB NOCREATEROLE;
COMMENT ON ROLE textreader IS 'wird nur benutzt, um informationen aus der tabelle text_msg zu lesen';

GRANT SELECT ON TABLE text_msg TO textreader;

INSERT INTO text_msg VALUES ('INITIAL_IMPORT', 'Dieses Element ist Teil der initial importierten Konfiguration.', 'This entity is part of the initially imported configuration.');
INSERT INTO text_msg VALUES ('NON_SECURITY_RELEVANT_CHANGE', 'Keine sicherheitsrelevante Änderung', 'This was a non-security-relevant change.');
INSERT INTO text_msg VALUES ('FACTORY_SETTING', 'Dieses Element ist Teil der initialen Herstellerkonfiguration.', 'This entity is part of the initially factory settings.');
INSERT INTO text_msg VALUES ('IN_OPERATION', 'Dieses Element wurde im laufenden Betrieb veraendert.', 'This entity was changed during normal operation.');
INSERT INTO text_msg VALUES ('DOC_REQUEST_START', 'Bitte dokumentieren Sie', 'Please document');
INSERT INTO text_msg VALUES ('DOC_REQUEST_END', 'offene Änderungen', 'open changes');
INSERT INTO text_msg VALUES ('username', 'Benutzername', 'Username');
INSERT INTO text_msg VALUES ('password', 'Passwort', 'Password');
INSERT INTO text_msg VALUES ('login', 'Anmelden', 'Login');
INSERT INTO text_msg VALUES ('documentation', 'Dokumentation', 'Documentation');
INSERT INTO text_msg VALUES ('surname', 'Nachname', 'Surname');
INSERT INTO text_msg VALUES ('documentation_description', 'Dokumentieren der offenen Änderungen', 'Document open changes');
INSERT INTO text_msg VALUES ('change_documentation_description', 'Bereits vorhandene Dokumentation ändern', 'Change existing documentation');
INSERT INTO text_msg VALUES ('wrong_creds', 'Fehler bei der Anmeldung. Fehlernummer: A001.', 'Login error. Error code: A001.');
INSERT INTO text_msg VALUES ('expired', 'Fehler bei der Anmeldung. Fehlernummer: A002.', 'Login error. Error code: A002.');
INSERT INTO text_msg VALUES ('superuser_login', 'Fehler bei der Anmeldung. Fehlernummer: A003.', 'Login error. Error code: A003.');
INSERT INTO text_msg VALUES ('login_failure_generic', 'Fehler bei der Anmeldung. Fehlernummer: A000.', 'Login error. Error code: A000.');
INSERT INTO text_msg VALUES ('support_description', 'Bei Problemen mit ITSecOrg erfahren Sie hier, wie Sie den Support kontaktieren können.', 'In case of problems with ITSecOrg you can get the contact details of our support department.');
INSERT INTO text_msg VALUES ('manual', 'Handbuch', 'Manual');
INSERT INTO text_msg VALUES ('logout', 'Abmelden', 'Log Out');
INSERT INTO text_msg VALUES ('reporting_description', 'Hier können Reports über Konfigurations(-änderungen) erstellt werden.', 'You can generate various reports about configuration (changes) here.');
INSERT INTO text_msg VALUES ('manual_description', 'Die Online-Handbuch von ITSecOrg.', 'The online manual.');
INSERT INTO text_msg VALUES ('help', 'Hilfe', 'Help');
INSERT INTO text_msg VALUES ('help_description', 'Die kontextsensitive Online-Hilfe.', 'The context-sensitive online help.');
INSERT INTO text_msg VALUES ('logged_in', 'Angemeldet', 'Logged in');
INSERT INTO text_msg VALUES ('no_frames', 'Ihr Browser unterstützt entweder keine Frames oder ist so konfiguriert, dass er keine Frames unterstützt.', 'Your user agent does not support frames or is currently configured not to display frames.');
INSERT INTO text_msg VALUES ('form_send', 'Abschicken', 'Submit');
INSERT INTO text_msg VALUES ('form_reset', 'Zurücksetzen', 'Reset');
INSERT INTO text_msg VALUES ('documentation_title', 'Noch nicht dokumentierte Änderungen', 'Not yet documented changes');
INSERT INTO text_msg VALUES ('change_documentation_title', 'Bereits dokumentierte Änderungen', 'Changes already documented');
INSERT INTO text_msg VALUES ('all', 'Alle', 'all');
INSERT INTO text_msg VALUES ('sanctioned_by', 'Genehmigung durch', 'sanctioned by');
INSERT INTO text_msg VALUES ('request_type', 'Auftragsart', 'request type');
INSERT INTO text_msg VALUES ('request_number', 'Auftragsnummer', 'Request number');
INSERT INTO text_msg VALUES ('comment', 'Kommentar', 'Comment');
INSERT INTO text_msg VALUES ('request_data', 'Auftragsdaten', 'Request data');
INSERT INTO text_msg VALUES ('missing_request_number', 'Bitte mindestens eine Auftragsnummer eingeben.', 'Please enter at least one request number.');
INSERT INTO text_msg VALUES ('missing_request_type', 'Die eingetragenen Auftragsdaten passen nicht. Bitte zu jeder eingetragenen Auftragsnummer einen Auftragstyp auswählen.', 'Mismatch in request data. Please enter a request type for each request number.');
INSERT INTO text_msg VALUES ('missing_client_for_request', 'Die eingetragenen Auftragsdaten passen nicht. Bitte zu jeder eingetragenen Auftragsnummer einen Mandanten auswählen.', 'Mismatch in request data. Please enter a client name for each request number.');
INSERT INTO text_msg VALUES ('missing_comment', 'Bitte Kommentarfeld ausfüllen.', 'Please fill-in comment field.');
INSERT INTO text_msg VALUES ('exceeded_max_request_number', 'Die Anzahl der Auftragsfelder übersteigt die maximale Anzahl von 9. Bitte in der ITSecOrg-Konfiguration (gui.conf) anpassen.', 'Please set  request number to not more than 9 in gui.conf. ');
INSERT INTO text_msg VALUES ('no_change_selected', 'Bitte mindestens eine Änderung auswählen.', 'Please select at least one change.');
INSERT INTO text_msg VALUES ('all_systems', 'Alle Systeme', 'All systems');
INSERT INTO text_msg VALUES ('display_filter', 'Anzeigefilter', 'Display filter');
INSERT INTO text_msg VALUES ('refresh', 'Neu anzeigen', 'Refresh');
INSERT INTO text_msg VALUES ('from', 'von', 'from');
INSERT INTO text_msg VALUES ('to', 'bis', 'to');
INSERT INTO text_msg VALUES ('my_own', 'eigene', 'own');
INSERT INTO text_msg VALUES ('foreign', 'fremde', 'foreign');
INSERT INTO text_msg VALUES ('only_self_documented', 'nur selbstdok.', 'only self-doc.');
INSERT INTO text_msg VALUES ('total', 'insgesamt', 'total');
INSERT INTO text_msg VALUES ('document_filter', 'Dokufilter', 'Doc filter');
INSERT INTO text_msg VALUES ('request', 'Auftrag', 'Request');
INSERT INTO text_msg VALUES ('document_comment', 'Dok-Komm.', 'Doc-comm.');
INSERT INTO text_msg VALUES ('object_comment', 'Obj-Komm.', 'Obj-comm.');
INSERT INTO text_msg VALUES ('first_name', 'Vorname', 'First name');
INSERT INTO text_msg VALUES ('anonymous', 'anonym', 'anonymous');
INSERT INTO text_msg VALUES ('management_filter', 'Management-Filter', 'Management filter');
INSERT INTO text_msg VALUES ('systems', 'Systeme', 'systems');
INSERT INTO text_msg VALUES ('systems_capital', 'Systeme', 'Systems');
INSERT INTO text_msg VALUES ('no_client_selected', 'Bitte wählen Sie einen Mandanten aus.', 'Please select a client.');
INSERT INTO text_msg VALUES ('please_select', 'Bitte wählen Sie ...', 'Please select ...');
INSERT INTO text_msg VALUES ('no_device_selected', 'Bitte wählen Sie ein Device aus.', 'Please select a device.');
INSERT INTO text_msg VALUES ('select_on_left', 'Bitte links auswählen.', 'Please select on left hand side.');
INSERT INTO text_msg VALUES ('settings', 'Einstellungen', 'Settings');
INSERT INTO text_msg VALUES ('settings_description', 'Änderungen der Einstellungen von ITSecOrg.', 'Change the settings of ITSecOrg.');
INSERT INTO text_msg VALUES ('user_id', 'Benutzer-ID', 'User ID');
INSERT INTO text_msg VALUES ('client', 'Mandant', 'Client');
INSERT INTO text_msg VALUES ('change_documentation', 'Dokumentation ändern', 'Change Documentation');
INSERT INTO text_msg VALUES ('config_time', 'Konfigurationsstand', 'Configuration state');
INSERT INTO text_msg VALUES ('report_time', 'Zeitpunkt', 'Time of report');
INSERT INTO text_msg VALUES ('of', 'von', 'of');
INSERT INTO text_msg VALUES ('destination', 'Ziel', 'Destination');
INSERT INTO text_msg VALUES ('source', 'Quelle', 'Source');
INSERT INTO text_msg VALUES ('service', 'Dienst', 'Service');
INSERT INTO text_msg VALUES ('show_only_active_rules', 'Nur aktive Regeln anzeigen', 'Show only active rules');
INSERT INTO text_msg VALUES ('special_filters', 'Spezialfilter', 'Special filters');
INSERT INTO text_msg VALUES ('rule_comment', 'Regelkommentar', 'Rule comment');
INSERT INTO text_msg VALUES ('user', 'Benutzer', 'User');
INSERT INTO text_msg VALUES ('ip_protocol', 'IP-Protokoll', 'IP protocol');
INSERT INTO text_msg VALUES ('show_only_rule_objects', 'Nur im Regelsatz vorkommende Objekte anzeigen', 'Show only objects that appear in rules');
INSERT INTO text_msg VALUES ('ip_filter_shows_any_objects', 'IP-Filter zeigt Any-Objekte', 'IP filter shows ''any objects''');
INSERT INTO text_msg VALUES ('ip_filter_shows_negated_rule_parts', 'IP-Filter zeigt negierte Regelanteile', 'IP filter shows negated rule parts');
INSERT INTO text_msg VALUES ('remove_filter', 'Filter entfernen', 'Remove filter');
INSERT INTO text_msg VALUES ('generate_report', 'Report erstellen', 'Generate report');
INSERT INTO text_msg VALUES ('change_password', 'Passwort ändern', 'Change password');
INSERT INTO text_msg VALUES ('change_devices', 'Systeme ändern', 'Change devices');
INSERT INTO text_msg VALUES ('change_clients', 'Mandanten ändern', 'Change clients');
INSERT INTO text_msg VALUES ('set_password', 'Passwort setzen', 'Set password');
INSERT INTO text_msg VALUES ('old_password', 'Altes Passwort', 'Old password');
INSERT INTO text_msg VALUES ('new_password', 'Neues Passwort (min. 6 Zeichen)', 'New password (min. 6 characters)');
INSERT INTO text_msg VALUES ('new_password_repeat', 'Neues Passwort (Wiederholung)', 'New password (please repeat)');
INSERT INTO text_msg VALUES ('device_settings', 'Einstellungen Systeme', 'Device settings');
INSERT INTO text_msg VALUES ('existing_device', 'Vorhandenes Device', 'Existing device');
INSERT INTO text_msg VALUES ('create_new_device', 'Neues System anlegen', 'Create new device');
INSERT INTO text_msg VALUES ('new_device', 'Neues Device', 'New device');
INSERT INTO text_msg VALUES ('new_management', 'Neues Management', 'New management');
INSERT INTO text_msg VALUES ('device_created', 'Device angelegt am', 'Device created at');
INSERT INTO text_msg VALUES ('device_last_changed', 'Device zuletzt geändert am', 'Device last changed at');
INSERT INTO text_msg VALUES ('import_active', 'Import aktiv', 'Import active');
INSERT INTO text_msg VALUES ('device_type', 'Device Typ', 'Device type');
INSERT INTO text_msg VALUES ('rulebase_name', 'Regelwerksname', 'Rulebase name');
INSERT INTO text_msg VALUES ('yes', 'Ja', 'Yes');
INSERT INTO text_msg VALUES ('no', 'Nein', 'No');
INSERT INTO text_msg VALUES ('change', 'Bearbeiten', 'Edit');
INSERT INTO text_msg VALUES ('management_created', 'Management angelegt am', 'Management created at');
INSERT INTO text_msg VALUES ('management_last_changed', 'Management zuletzt geändert am', 'Management last changed at');
INSERT INTO text_msg VALUES ('save', 'Speichern', 'Save');
INSERT INTO text_msg VALUES ('cancel', 'Abbrechen', 'Cancel');
INSERT INTO text_msg VALUES ('client_settings', 'Einstellungen Mandanten', 'Client settings');
INSERT INTO text_msg VALUES ('existing_client_information', 'Existierende Mandanteninformation', 'Existing client information');
INSERT INTO text_msg VALUES ('new_client', 'Neuer Mandant', 'New client');
INSERT INTO text_msg VALUES ('new_ip_network', 'Neues IP-Netzwerk', 'New IP network');
INSERT INTO text_msg VALUES ('create_new_clients_or_networks', 'Neuen Mandanten oder Netzwerk anlegen', 'Create new client or network');
INSERT INTO text_msg VALUES ('client_id', 'Mandanten-ID', 'Client ID');
INSERT INTO text_msg VALUES ('client_name', 'Mandantenname', 'Client name');
INSERT INTO text_msg VALUES ('delete', 'Löschen', 'Delete');
INSERT INTO text_msg VALUES ('client_network', 'Mandanten IP-Netzwerk', 'Client IP network');
INSERT INTO text_msg VALUES ('report_headline_changes', 'Änderungsreport Sicherheitskonfiguration', 'Security Configuration Change Report');
INSERT INTO text_msg VALUES ('report_headline_usage', 'Verwendungsstatistik der Elemente der Sicherheitskonfiguration', 'Statistics Report: Usage of Security Configuration Elements');
INSERT INTO text_msg VALUES ('report_headline_rulesearch', 'Globale Liste von Regeln, die Filterkriterien erfüllen', 'Rulesearch Report');
INSERT INTO text_msg VALUES ('report_headline_configuration', 'Report der Sicherheitskonfiguration', 'Security Configuration Report');
INSERT INTO text_msg VALUES ('report_changes_start_time', 'Startzeitpunkt', 'Start time');
INSERT INTO text_msg VALUES ('report_changes_end_time', 'Endzeitpunkt', 'End time');
INSERT INTO text_msg VALUES ('report_unused_objects', 'Elemente anzeigen, die nicht im Regelwerk vorkommen', 'Display elements that do not occur in rulebase');
INSERT INTO text_msg VALUES ('report_inactive_objects', 'Anzeige von nicht aktiven Konfigurationselementen', 'Display inactive rules');
INSERT INTO text_msg VALUES ('report_generated_by', 'erzeugt mit', 'generated by');
INSERT INTO text_msg VALUES ('report_change_time_of_change', 'Änderungszeitpunkt', 'Time of change');
INSERT INTO text_msg VALUES ('report_generation', 'Reportgenerierung', 'Report generation');
INSERT INTO text_msg VALUES ('report_type', 'Typ', 'Type');
INSERT INTO text_msg VALUES ('report_generation_took', 'dauerte', 'took');
INSERT INTO text_msg VALUES ('seconds', 'Sekunden', 'seconds');
INSERT INTO text_msg VALUES ('report_changes_end_time', 'Endzeitpunkt', 'End time');

/*
CREATE OR REPLACE FUNCTION remove_spaces(varchar) RETURNS VARCHAR AS $$
DECLARE
    s ALIAS FOR $1;
BEGIN
	RETURN btrim(s);
END;
$$ LANGUAGE plpgsql;
*/