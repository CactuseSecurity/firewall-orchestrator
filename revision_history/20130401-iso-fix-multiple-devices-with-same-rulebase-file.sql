--	Fix: Erlaube mehrere Devices mit derselben Rulebase (MDM, Check Point)

alter table "rule" drop constraint "rule_altkey";
alter table "rule" add Constraint "rule_altkey" UNIQUE ("mgm_id","rule_uid","rule_create","dev_id");

CREATE OR REPLACE FUNCTION import_rules_save_order (INTEGER,INTEGER) RETURNS VOID AS $$
DECLARE
	i_current_control_id ALIAS FOR $1; -- ID des aktiven Imports
	i_dev_id ALIAS FOR $2; -- ID des zu importierenden Devices
	i_mgm_id INTEGER; -- ID des zugehoerigen Managements
	b_existing_rulebase BOOLEAN;
BEGIN
	RAISE DEBUG 'import_rules_save_order - start';
	SELECT INTO i_mgm_id mgm_id FROM device WHERE dev_id=i_dev_id;
	IF (TRUE) THEN
		RAISE DEBUG 'import_rules_save_order - mgm_id=%, dev_id=%, before inserting', i_mgm_id, i_dev_id;
		INSERT INTO rule_order (control_id,dev_id,rule_id,rule_number)
			SELECT i_current_control_id AS control_id, i_dev_id as dev_id, rule.rule_id, import_rule.rule_num as rule_number
			FROM device, import_rule LEFT JOIN rule ON (import_rule.rule_uid=rule.rule_uid AND rule.dev_id=i_dev_id) WHERE device.dev_id=i_dev_id 
			AND rule.mgm_id = i_mgm_id AND rule.active AND import_rule.control_id=i_current_control_id 
			AND import_rule.rulebase_name=device.dev_rulebase;
	ELSE
		RAISE DEBUG 'import_rules_save_order - policy already processed for other device: skipping';	
	END IF;
	RAISE DEBUG 'import_rules_save_order - end';
	RETURN;
END;
$$ LANGUAGE plpgsql;

/* changed: importer/CACTUS/ISO/import.pm:: fill_import_tables_from_csv
 	$fields = "(" . join(',',@rule_import_fields) . ")";
	my @rulebase_ar = ();
	foreach my $d (keys %{$rulebases}) {
		my $rb = $rulebases->{$d}->{'dev_rulebase'};
		if ( !grep( /^$rb$/, @rulebase_ar ) ) {
			@rulebase_ar = (@rulebase_ar, $rb);
			$csv_rule_file = $iso_workdir . '/' . $rb . '_rulebase.csv';
			print ("\nrulebase found: $rb, rule_file: $csv_rule_file, device: $d  ");
			$sqlcode = "COPY import_rule $fields FROM STDIN DELIMITER '$CACTUS::ISO::csv_delimiter' CSV";
			if ($fehler = CACTUS::ISO::copy_file_to_db($sqlcode,$csv_rule_file)) {
				print_error("dbimport: $fehler"); print_linebreak(); $fehler_count += 1;
			}
		} else {
			print ("\nignoring another device ($d) with rulebase $rb");			
		}
	}
*/


/*
  -- Optimierung: unnoetige rule_order-Eintraege loeschen:
CREATE OR REPLACE VIEW view_imports_without_changes AS 
SELECT control_id FROM import_control WHERE (control_id, 0) IN
(SELECT control_id, COUNT(view_changes.*) 
FROM import_control LEFT JOIN view_changes ON (change_time=import_control.start_time) 
WHERE successful_import
-- AND control_id>=7588700
GROUP BY control_id )
ORDER by control_id;

-- drop VIEW view_import_stats;
CREATE OR REPLACE VIEW view_import_stats AS
SELECT import_control.control_id AS alle,
	COUNT(rule_order.control_id) AS in_rule_order, 
	COUNT(view_imports_without_changes.control_id) AS imports_ohne_aenderung, 
	COUNT(import_control_fehler.control_id) AS import_mit_fehler
FROM import_control LEFT JOIN rule_order ON (import_control.control_id=rule_order.control_id) 
LEFT JOIN view_imports_without_changes ON (import_control.control_id=view_imports_without_changes.control_id)
LEFT JOIN import_control AS import_control_fehler ON (import_control.control_id=import_control_fehler.control_id AND NOT import_control_fehler.successful_import)
GROUP BY import_control.control_id
ORDER BY import_control.control_id;

SELECT 100.0*COUNT(view_imports_without_changes.control_id)/COUNT(import_control.control_id) 
FROM import_control
LEFT JOIN view_imports_without_changes ON (import_control.control_id=view_imports_without_changes.control_id)
-- result = 23,16%

SELECT * FROM rule_order WHERE control_id IN (SELECT * FROM view_imports_without_changes);
DELETE FROM rule_order WHERE control_id IN (SELECT * FROM view_imports_without_changes);
SELECT * FROM import_control WHERE control_id IN (SELECT * FROM view_imports_without_changes);
DELETE FROM import_control WHERE control_id IN (SELECT * FROM view_imports_without_changes);
*/