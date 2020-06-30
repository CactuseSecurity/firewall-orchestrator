/*		
	minor db changes
*/

UPDATE text_msg SET text_msg_eng = 'IP filter shows negates rule parts'
WHERE text_msg_id = 'ip_filter_shows_negated_rule_parts';

UPDATE text_msg SET text_msg_eng = 'End time'
WHERE text_msg_id = 'report_changes_end_time';

INSERT INTO text_msg VALUES ('report_changes_end_time', 'Endzeitpunkt', 'End timet');


CREATE OR REPLACE FUNCTION remove_spaces(varchar) RETURNS VARCHAR AS $$
DECLARE
    s ALIAS FOR $1;
BEGIN
	RETURN btrim(s);
END;
$$ LANGUAGE plpgsql;


/*
 * Aenderung in web/include/db-nwobject.php, um NW_Gruppen bei Client-Filterung um Client-fremde Objekte zu bereinigen 
 * 
 * alt:
			$sql_code =
				"SELECT object.*,zone_name,stm_obj_typ.obj_typ_name" .
				" FROM object LEFT JOIN zone ON object.zone_id=zone.zone_id" .
				" LEFT JOIN stm_obj_typ ON object.obj_typ_id=stm_obj_typ.obj_typ_id" .
				" WHERE obj_id IN" .
					" (" .
							"SELECT obj_id AS id FROM object $object_rule_filter" .
						" UNION" .
							" (" .
								"SELECT objgrp_flat_member_id as id FROM objgrp_flat" .
								" INNER JOIN object ON (objgrp_flat.objgrp_flat_id=object.obj_id)" .
								" INNER JOIN temp_mgmid_importid_at_report_time ON (temp_mgmid_importid_at_report_time.mgm_id=object.mgm_id" .
									" AND temp_mgmid_importid_at_report_time.control_id>=objgrp_flat.import_created" .
									" AND temp_mgmid_importid_at_report_time.control_id<=objgrp_flat.import_last_seen)" .
								" WHERE $grp_flat_rule_filter temp_mgmid_importid_at_report_time.report_id=$report_id" .
							")" .
					" )" .
					" ORDER BY $order_str";
 * neu:
	 				 			$sql_code =
				"SELECT object.*,zone_name,stm_obj_typ.obj_typ_name" .
				" FROM object LEFT JOIN zone ON object.zone_id=zone.zone_id" .
				" LEFT JOIN stm_obj_typ ON object.obj_typ_id=stm_obj_typ.obj_typ_id" .
				" WHERE obj_id IN (SELECT obj_id AS id FROM object $object_rule_filter) ORDER BY $order_str";
						$log = new LogConnection(); $log->log_debug("NetworkObjectList: sql = $sql_code");
*/
