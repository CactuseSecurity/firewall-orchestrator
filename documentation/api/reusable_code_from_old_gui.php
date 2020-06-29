# from db-import-ids.php:
	function setImportIdMgmList() {
		$mgm_filter = $this->filter->getMgmFilter();
		$report_timestamp = $this->filter->getReportTime();
		$sqlcmd = "SELECT max(control_id) AS import_id, import_control.mgm_id AS mgm_id FROM import_control" .
				" INNER JOIN management USING (mgm_id)" .
				" INNER JOIN device ON (management.mgm_id=device.mgm_id)" .
				" WHERE start_time<='$report_timestamp' AND NOT stop_time IS NULL AND $mgm_filter AND successful_import" .
				" AND NOT device.hide_in_gui AND NOT management.hide_in_gui " . 
				" GROUP BY import_control.mgm_id";
		$import_id_table = $this->db_connection->iso_db_query($sqlcmd);
		if (!$this->error->isError($import_id_table)) {
			$this->import_id_mgm_id_table = $import_id_table;
		} else {
			$err = $import_id_table;
			$this->error->raiseError("E-NWL1: Could not query DB: $sqlcmd" . $err->getMessage());
		}
		return;
	}


# from db-rule.php:
	function getRules($report_timestamp, $report_id) {
		if (empty ($this->rule_list)) {
			if ($this->error->isError($this->rule_ids)) {
				$this->error->raiseError("E-RL1: Rule Ids not loaded properly. ".$this->rule_ids->getMessage());
			}
			$import_id_mgm_id_str = $this->import_ids->getImportIdMgmStringList();
			$sqlcmd = "SELECT rule_order.dev_id, rule_order.rule_number, rule.*, " . 
					" from_zone.zone_name AS rule_from_zone_name, to_zone.zone_name AS rule_to_zone_name" .
					" FROM rule_order" .
					" INNER JOIN device ON (rule_order.dev_id=device.dev_id) " .
					" INNER JOIN management ON (device.mgm_id=management.mgm_id) " .
					" INNER JOIN rule USING (rule_id)" .
					" LEFT JOIN zone as from_zone ON rule.rule_from_zone=from_zone.zone_id" .  // does not work with INNER JOIN
					" LEFT JOIN zone as to_zone ON rule.rule_to_zone=to_zone.zone_id" . // does not work with INNER JOIN
					" INNER JOIN import_control ON (rule_order.control_id=import_control.control_id)" .
					" INNER JOIN stm_track ON (stm_track.track_id=rule.track_id)" .
					" INNER JOIN stm_action ON (stm_action.action_id=rule.action_id)" .
					" INNER JOIN temp_filtered_rule_ids ON (rule.rule_id=temp_filtered_rule_ids.rule_id)" .
					" WHERE temp_filtered_rule_ids.report_id=$report_id AND successful_import AND (rule_order.control_id, management.mgm_id) IN $import_id_mgm_id_str" .
					" ORDER BY rule_order.dev_id,rule_from_zone_name,rule_to_zone_name,rule_order.rule_number";
