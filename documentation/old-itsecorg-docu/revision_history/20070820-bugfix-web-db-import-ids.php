<?php
// $Id: 20070820-bugfix-web-db-import-ids.php,v 1.1.2.2 2007-12-13 10:47:31 tim Exp $
// $Source: /home/cvs/iso/package/install/migration/Attic/20070820-bugfix-web-db-import-ids.php,v $

require_once ("db-base.php");

class ImportIds extends DbItem {
	var $filter;
	var $db_connection;
	var $import_id_mgm_id_table;
	var $mgm_id_import_id_lookup_table;
	var $import_id_per_dev_lookup_table;
	var $obj_mgm_id_lookup_table;
	var $svc_mgm_id_lookup_table;
	var $usr_mgm_id_lookup_table;
			
	function ImportIds($filter) {
		$this->filter = $filter;
		$this->db_connection = new DbConnection(new DbConfig($this->filter->dbuser, $this->filter->dbpw));
		$this->setImportIdMgmList();
		$this->setImportIdMgmLookupTable();	
		$this->setRelevantImportIdsPerDevice();
		$this->setMgmId4ObjLookupTable();
		$this->setMgmId4SvcLookupTable();
		$this->setMgmId4UsrLookupTable();
		$this->insertTempRelevantImportList();
	}
	// temp table writers
	function insertTempRelevantImportList () {
		$report_id = $this->filter->getReportId();
//		$log = new LogConnection(); $log->log_debug("insertTempRelevantImportList called for report_id $report_id");
		$import_id_mgm_id_str = $this->getImportIdMgmStringList();
		$import_id_mgm_id_str = substr($import_id_mgm_id_str, 2, strlen($import_id_mgm_id_str) - 4); // cut of surrounding brackets 
		$mgm_id_import_id_ar = explode('),(', $import_id_mgm_id_str);
		$sqlcmd = '';
		foreach ($mgm_id_import_id_ar as $ids) {
			list ($import_id, $mgm_id) = explode(',', $ids);
			$sqlcmd .= "INSERT INTO temp_mgmid_importid_at_report_time (control_id, mgm_id, report_id) VALUES ($import_id, $mgm_id, $report_id); ";
		}
		$this->db_connection->iso_db_query($sqlcmd);
	}	
	function delete_relevant_import_times_from_temp_table() {
		$report_id = $this->filter->getReportId();
		$sqlcmd = "DELETE FROM temp_mgmid_importid_at_report_time WHERE report_id=$report_id";
		$this->db_connection->iso_db_query($sqlcmd);
	}
// setters (only these access the DB directly - just once at report start)
	function setImportIdMgmList() {
		$mgm_filter = $this->filter->getMgmFilter();
		$report_timestamp = $this->filter->getReportTime();
		$sqlcmd = "SELECT max(control_id) AS import_id, import_control.mgm_id AS mgm_id FROM import_control" .
				" INNER JOIN management USING (mgm_id)" .
				" INNER JOIN device ON (management.mgm_id=device.mgm_id)" .
				" WHERE start_time<='$report_timestamp' AND NOT stop_time IS NULL AND $mgm_filter" .
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
	function setRelevantImportIdsPerDevice() {
		$report_timestamp = $this->filter->getReportTime();
		$mgm_filter = $this->filter->getMgmFilter();
		$sqlcmd = "SELECT max(control_id) AS import_id, device.dev_id AS dev_id FROM import_control" .
				" INNER JOIN management USING (mgm_id)" .
				" INNER JOIN device ON (management.mgm_id=device.mgm_id)" .
				" WHERE start_time<='$report_timestamp' AND NOT stop_time IS NULL AND $mgm_filter" .
				" GROUP BY device.dev_id";
		$import_id_dev_id_table = $this->db_connection->iso_db_query($sqlcmd);
		if (!$this->error->isError($import_id_dev_id_table)) {
			for ($idx = 0; $idx < $import_id_dev_id_table->rows; ++$idx) {
				$dev_id = $import_id_dev_id_table->data[$idx]['dev_id'];
				$import_id = $import_id_dev_id_table->data[$idx]['import_id']; 
				$this->import_id_per_dev_lookup_table[$dev_id] = $import_id;
			}
		} else {
			$err = $import_id_dev_id_table;
			$this->error->raiseError("E-NWL2: Could not query DB: $sqlcmd" . $err->getMessage());
		}
		return;
	}
	function setImportIdMgmLookupTable() {
		for ($idx = 0; $idx < $this->import_id_mgm_id_table->rows; ++$idx) {
			$this->mgm_id_import_id_lookup_table[$this->import_id_mgm_id_table->data[$idx]['mgm_id']] = 
				$this->import_id_mgm_id_table->data[$idx]['import_id'];
		}
	}
	function setMgmId4ObjLookupTable() {
		$mgm_filter = $this->filter->getMgmFilter();
		$mgm_id = $this->filter->getManagementId();
		if (isset($mgm_id) and $mgm_id != '') {
			$where_clause = "object.mgm_id=$mgm_id AND ";
		} else {
			$where_clause = '';
		}
		$where_clause .= $mgm_filter;
		$sqlcmd = "SELECT obj_id,object.mgm_id FROM object" .
				" INNER JOIN management ON (object.mgm_id=management.mgm_id)" .
				" INNER JOIN device ON (management.mgm_id=device.mgm_id)" .		
				" WHERE $where_clause";
		$obj_mgm_id_table = $this->db_connection->iso_db_query($sqlcmd);
		if (!$this->error->isError($obj_mgm_id_table)) {
			for ($idx = 0; $idx < $obj_mgm_id_table->rows; ++$idx) {
				$obj_id = $obj_mgm_id_table->data[$idx]['obj_id'];
				$mgm_id = $obj_mgm_id_table->data[$idx]['mgm_id']; 
				$this->obj_mgm_id_lookup_table[$obj_id] = $mgm_id;
			}
		} else {
			$err = $obj_mgm_id_table;
			$this->error->raiseError("E-NWL2: Could not query DB: $sqlcmd" . $err->getMessage());
		}
		return;
	}
	function setMgmId4SvcLookupTable() {
		$mgm_filter = $this->filter->getMgmFilter();
		$mgm_id = $this->filter->getManagementId();
		if (isset($mgm_id) and $mgm_id != '') {
			$where_clause = "service.mgm_id=$mgm_id AND ";
		} else {
			$where_clause = '';
		}
		$where_clause .= $mgm_filter;
		$sqlcmd = "SELECT svc_id,service.mgm_id FROM service" .
				" INNER JOIN management ON (service.mgm_id=management.mgm_id)" .
				" INNER JOIN device ON (management.mgm_id=device.mgm_id)" .		
				" WHERE $where_clause";
		$svc_mgm_id_table = $this->db_connection->iso_db_query($sqlcmd);
		if (!$this->error->isError($svc_mgm_id_table)) {
			for ($idx = 0; $idx < $svc_mgm_id_table->rows; ++$idx) {
				$svc_id = $svc_mgm_id_table->data[$idx]['svc_id'];
				$mgm_id = $svc_mgm_id_table->data[$idx]['mgm_id']; 
				$this->svc_mgm_id_lookup_table[$svc_id] = $mgm_id;
			}
		} else {
			$err = $svc_mgm_id_table;
			$this->error->raiseError("E-NWL2: Could not query DB: $sqlcmd" . $err->getMessage());
		}
		return;
	}
	function setMgmId4UsrLookupTable() {
		$mgm_filter = $this->filter->getMgmFilter();
		$mgm_id = $this->filter->getManagementId();
		if (isset($mgm_id) and $mgm_id != '') {
			$where_clause = "usr.mgm_id=$mgm_id AND ";
		} else {
			$where_clause = '';
		}
		$where_clause .= $mgm_filter;
		$sqlcmd = "SELECT user_id,usr.mgm_id FROM usr" .
				" INNER JOIN management ON (usr.mgm_id=management.mgm_id)" .
				" INNER JOIN device ON (management.mgm_id=device.mgm_id)" .		
				" WHERE $where_clause";
		$usr_mgm_id_table = $this->db_connection->iso_db_query($sqlcmd);
		if (!$this->error->isError($usr_mgm_id_table)) {
			for ($idx = 0; $idx < $usr_mgm_id_table->rows; ++$idx) {
				$usr_id = $usr_mgm_id_table->data[$idx]['user_id'];
				$mgm_id = $usr_mgm_id_table->data[$idx]['mgm_id']; 
				$this->usr_mgm_id_lookup_table[$usr_id] = $mgm_id;
			}
		} else {
			$err = $usr_mgm_id_table;
			$this->error->raiseError("E-NWL2: Could not query DB: $sqlcmd" . $err->getMessage());
		}
		return;
	}

// getters
	function getImportIdMgmList() {
		return $this->import_id_mgm_id_table;
	}
	function getImportId4Mgm($mgm_id) {
		return $this->mgm_id_import_id_lookup_table[$mgm_id];
	}
	function getImportIdMgmStringList() {
		$import_id_table = $this->getImportIdMgmList($this->filter->getMgmFilter());
		$import_id_mgm_id_str = '(';
		$mgm_anz = $import_id_table->rows;
		for ($zi = 0; $zi < $mgm_anz; ++ $zi) {
			$import_id_mgm_id_str .=  "(" . $import_id_table->data[$zi]['import_id'] . "," . $import_id_table->data[$zi]['mgm_id'] . ")";
			if ($zi + 1 < $mgm_anz) $import_id_mgm_id_str .= ',';
		}
		$import_id_mgm_id_str .= ')';
		return $import_id_mgm_id_str;
	}
	function getRelevantImportIdForDevice($dev_id) {
		return $this->import_id_per_dev_lookup_table[$dev_id];
	}
	function getMgmId4Obj($obj_id) {
		return $this->obj_mgm_id_lookup_table[$obj_id];
	}
	function getMgmId4Svc($svc_id) {
		return $this->svc_mgm_id_lookup_table[$svc_id];
	}
	function getMgmId4Usr($usr_id) {
		return $this->usr_mgm_id_lookup_table[$usr_id];
	}
	function getRelevantImportIdForObject($obj_id) {
		if (!is_null($obj_id))
			$import_id = $this->getImportId4Mgm($this->getMgmId4Obj($obj_id));
		else
			$import_id = NULL;
		return $import_id;
	}
	function getRelevantImportIdForService($svc_id) {
		if (!is_null($svc_id))
			$import_id = $this->getImportId4Mgm($this->getMgmId4Svc($svc_id));
		else
			$import_id = NULL;
		return $import_id;
	}
	function getRelevantImportIdForUser($usr_id) {
		if (!is_null($usr_id))
			$import_id = $this->getImportId4Mgm($this->getMgmId4Usr($usr_id));
		else
			$import_id = NULL;
		return $import_id;
	}
}
?>