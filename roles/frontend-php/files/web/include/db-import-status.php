<?php

/*
 * $Id: db-import-status.php,v 1.1.2.2 2007-12-13 10:47:31 tim Exp $
 * $Source: /home/cvs/iso/package/web/include/Attic/db-import-status.php,v $
 * Created on 10.11.2007
 */
 
require_once ("db-base.php");
require_once ("db-gui-config.php");

class ImportStatusList extends DbList {
	var $imports;
	
	function __construct() {
		$this->error = new PEAR();
		$this->db_connection = $this->initConnection('fworch', '');
		if (!$this->error->isError($this->db_connection)) {
			$sqlcmd = "SELECT * FROM view_import_status_table";

			$db_imports = $this->db_connection->fworch_db_query($sqlcmd);
			if (!$this->error->isError($db_imports)) {
				$import_status_array = array ();
				for ($zi = 0; $zi < $db_imports->rows; ++ $zi)
					$import_status_array[] = new ImportStatusElement ($db_imports->data[$zi]);
				$this->imports = $import_status_array;
			} else {
				$this->imports = $db_imports;
			}
		}
	}
	function getStati() {
		return $this->imports;
	}
}

class ImportStatusElement extends DbItem {
	var $mgm_id;
	var $mgm_name;
	var $device_type;
	var $import_active;
	var $import_time_last_successful;
	var $import_time_last_error;
	var $import_count_24h_successful;
	var $import_count_24h_error;
	var $status;
	var $last_import_error;

	function __construct($import_status_table_data) {
		$db_import_status_keys = array_keys($import_status_table_data);
		$this->mgm_id =  $this->getValue($import_status_table_data, "mgm_id", $db_import_status_keys);
		$this->mgm_name =  $this->getValue($import_status_table_data, "management_name", $db_import_status_keys);
		$this->device_type =  $this->getValue($import_status_table_data, "device_type", $db_import_status_keys);
		$this->import_active =  $this->getValue($import_status_table_data, "import_is_active", $db_import_status_keys);
		$this->import_time_last_successful =  $this->getValue($import_status_table_data, "last_successful_import", $db_import_status_keys);
		$this->import_time_last_error =  $this->getValue($import_status_table_data, "last_import_with_errors", $db_import_status_keys);
		$this->import_count_24h_successful =  $this->getValue($import_status_table_data, "import_count_successful", $db_import_status_keys);
		$this->import_count_24h_error =  $this->getValue($import_status_table_data, "import_count_errors", $db_import_status_keys);
		$this->last_import_error =  $this->getValue($import_status_table_data, "last_import_error", $db_import_status_keys);
		$this->status =  $this->getValue($import_status_table_data, "status", $db_import_status_keys);
	}
}
?>