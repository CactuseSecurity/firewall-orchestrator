<?php
// $Id: db-config.php,v 1.1.2.4 2013-01-31 21:54:13 tim Exp $
// $Source: /home/cvs/iso/package/web/include/Attic/db-config.php,v $
require_once ("db-base.php");
require_once ("display-filter.php");
require_once ("db-base.php");
require_once ("PEAR.php");

class Device {
	var $dev_id;
	var $dev_name;
	var $dev_mgm_name;
	var $dev_rulebase;
	var $dev_typ;
	var $dev_typ_id;
	var $dev_do_import;
	var $dev_created;
	var $dev_updated;
	var $dev_mgm_id;
	var $error;
	var $dev_hide_in_gui;
		
	function __construct ($db_connection, $dev_id) {
		$this->error = new PEAR();
		$sql_code = "SELECT device.*, stm_dev_typ.dev_typ_id, dev_typ_name, dev_typ_manufacturer, mgm_name, mgm_id " . 
					"FROM device LEFT JOIN management USING (mgm_id) LEFT JOIN stm_dev_typ ON (device.dev_typ_id=stm_dev_typ.dev_typ_id) WHERE dev_id=$dev_id";
		if ($this->error->isError($db_connection)) $this->error->raiseError("F-RCF: Connection not initialized. " . $db_connection->getMessage());
		$dev_details = $db_connection->iso_db_query ($sql_code,0);
		if ($this->error->isError($dev_details)) $this->error->raiseError($dev_details->getMessage());
		$this->dev_id		= $dev_id;
		$this->dev_name		= $dev_details->data[0]['dev_name'];
		$this->dev_mgm_name	= $dev_details->data[0]['mgm_name'];
		$this->dev_rulebase	= $dev_details->data[0]['dev_rulebase'];
		$this->dev_typ		= $dev_details->data[0]['dev_typ_id'] . ' (' . $dev_details->data[0]['dev_typ_manufacturer'] .
								 ', ' . $dev_details->data[0]['dev_typ_name'] . ')';
		$this->dev_typ_id	= $dev_details->data[0]['dev_typ_id'];
		$this->dev_do_import= ($dev_details->data[0]['do_not_import'] == 't')?0:1;
		$this->dev_created	= $dev_details->data[0]['dev_create'];
		$this->dev_updated	= $dev_details->data[0]['dev_update'];
		$this->dev_mgm_id	= $dev_details->data[0]['mgm_id'];
		$this->dev_hide_in_gui	= ($dev_details->data[0]['hide_in_gui'] == 't')?1:0;
		
		if (!isset($this->dev_name)) $this->error->raiseError("ERROR: no matching device for ID $dev_id found!");		
	}

	function getDevMgmName() {
		return $this->dev_mgm_name;
	}
	function getDevID() {
		return $this->dev_id;
	}
	function getDevName() {
		return $this->dev_name;
	}
	function getDevRulebase() {
		return $this->dev_rulebase;
	}
	function getDevTypName() {
		return $this->dev_typ;
	}
	function getDevTypId() {
		return $this->dev_typ_id;
	}
	function getDevDoImport() {
		return $this->dev_do_import;
	}
	function getDevCreated() {
		return $this->dev_created;
	}
	function getDevLastUpdated() {
		return $this->dev_updated;
	}
	function getDevMgmId() {
		return $this->dev_mgm_id;
	}
	function getDevHideInGui() {
		return $this->dev_hide_in_gui;
	}
}

class Management {
	var $mgm_id;
	var $dev_name;
	var $dev_typ;
	var $dev_typ_id;
	var $mgm_do_import;
	var $mgm_created;
	var $mgm_updated;
	var $mgm_ssh_user;
	var $mgm_ssh_hostname;
	var $mgm_importer_hostname;
	var $mgm_ssh_pub_key;
	var $mgm_ssh_priv_key;
	var $mgm_ssh_port;
	var $mgm_config_path;
	var $error;
	var $mgm_hide_in_gui;
	
	function __construct ($db_connection, $mgm_id) {
		$this->error = new PEAR();
		$sql_code = "SELECT management.*, stm_dev_typ.dev_typ_id, dev_typ_name, dev_typ_manufacturer " . 
					"FROM management LEFT JOIN stm_dev_typ USING (dev_typ_id) WHERE mgm_id=$mgm_id";
		if ($this->error->isError($db_connection)) $this->error->raiseError("F-RCF: Connection not initialized. " . $db_connection->getMessage());
		$mgm_details = $db_connection->iso_db_query ($sql_code,0);
		if ($this->error->isError($mgm_details)) $this->error->raiseError($dev_details->getMessage());
		$this->mgm_id		= $mgm_id;
		$this->mgm_name		= $mgm_details->data[0]['mgm_name'];
		$this->dev_typ		= $mgm_details->data[0]['dev_typ_id'] . ' (' . $mgm_details->data[0]['dev_typ_manufacturer'] .
								 ', ' . $mgm_details->data[0]['dev_typ_name'] . ')';
		$this->dev_typ_id	= $mgm_details->data[0]['dev_typ_id'];
		$this->mgm_do_import= ($mgm_details->data[0]['do_not_import'] == 't')?0:1;
		$this->mgm_created	= $mgm_details->data[0]['mgm_create'];
		$this->mgm_updated	= $mgm_details->data[0]['mgm_update'];
		$this->mgm_ssh_user	= $mgm_details->data[0]['ssh_user'];
		$this->mgm_ssh_hostname	= $mgm_details->data[0]['ssh_hostname'];
		$this->mgm_importer_hostname	= $mgm_details->data[0]['importer_hostname'];
		$this->mgm_ssh_pub_key	= $mgm_details->data[0]['ssh_public_key'];
		$this->mgm_ssh_priv_key	= $mgm_details->data[0]['ssh_private_key'];
		$this->mgm_ssh_port		= $mgm_details->data[0]['ssh_port'];
		$this->mgm_config_path	= $mgm_details->data[0]['config_path'];
		$this->mgm_hide_in_gui	= ($mgm_details->data[0]['hide_in_gui'] == 't')?1:0;
		
		if (!isset($this->mgm_name)) $this->error->raiseError("ERROR: no matching management for ID $mgm_id found!");		
	}

	function getMgmID() {
		return $this->mgm_id;
	}
	function getMgmName() {
		return $this->mgm_name;
	}
	function getDevTypName() {
		return $this->dev_typ;
	}
	function getDevTypId() {
		return $this->dev_typ_id;
	}
	function getMgmDoImport() {
		return $this->mgm_do_import;
	}
	function getMgmCreated() {
		return $this->mgm_created;
	}
	function getMgmLastUpdated() {
		return $this->mgm_updated;
	}
	function getMgmSshUser() {
		return $this->mgm_ssh_user;
	}
	function getMgmSshHostname() {
		return $this->mgm_ssh_hostname;
	}
	function getMgmImporterHostname() {
		return $this->mgm_importer_hostname;
	}
	function getMgmSshPubKey() {
		return $this->mgm_ssh_pub_key;
	}
	function getMgmSshPrivKey() {
		return $this->mgm_ssh_priv_key;
	}
	function getMgmSshPort() {
		return $this->mgm_ssh_port;
	}
	function getMgmConfigPath() {
		return $this->mgm_config_path;
	}
	function getMgmHideInGui() {
		return $this->mgm_hide_in_gui;
	}
}

class DevTypList {
	var $dev_typ_list;
		
	function __construct ($db) {
		$sql_code = "SELECT * FROM stm_dev_typ ORDER BY dev_typ_name"; 
		$this->dev_typ_list = $db->iso_db_query ($sql_code);
	}
	function getDevTypList() {
		return $this->dev_typ_list;
	}
}

class DisplayDevTypes {
	var $dev_typ_list;

	function __construct($dev_typ_list) {
		$this->dev_typ_list = $dev_typ_list;
	}
	function getDevTypSelection($name, $selected_dev_typ_id, $disable_string) {
		$dev_typ_list = $this->dev_typ_list;
		$anz = $dev_typ_list->rows;
//		echo "selected = $selected_dev_typ_id<br>";
		$form = "<SELECT name=\"$name\" $disable_string>";
		for ($zi=0; $zi<$anz; $zi++) {
			$dev_typ_id = $dev_typ_list->data[$zi]['dev_typ_id'];
			$dev_typ = $dev_typ_list->data[$zi]['dev_typ_name'] . ' ' . $dev_typ_list->data[$zi]['dev_typ_version'];
			if ($dev_typ_id == $selected_dev_typ_id) $selected = ' selected'; else $selected = '';
			$form .= "<OPTION$selected value=\"$dev_typ_id\">$dev_typ</OPTION>";
		} // for 		
		$form .= '</SELECT>';
 		return $form;
	}
}

class IsoadminUser {
	var $user_id;
	var $username;
	var $first_name;
	var $last_name;
	var $start_date;
	var $end_date;
	var $email;
	var $is_isoadmin;
	var $error;
		
	function __construct ($db_connection, $user_id) {
		$this->error = new PEAR();
		$sql_code = "SELECT * FROM isoadmin WHERE isoadmin_id=$user_id";
		if ($this->error->isError($db_connection)) $this->error->raiseError("F-RCF: Connection not initialized. " . $db_connection->getMessage());
		$user_details = $db_connection->iso_db_query ($sql_code,0);
		if ($this->error->isError($user_details)) $this->error->raiseError($dev_details->getMessage());
		$this->user_id		= $user_id;
		$this->username		= $user_details->data[0]['isoadmin_username'];
		$this->first_name	= $user_details->data[0]['isoadmin_first_name'];
		$this->last_name	= $user_details->data[0]['isoadmin_last_name'];
		$this->start_date	= $user_details->data[0]['isoadmin_start_date'];
		$this->end_date		= $user_details->data[0]['isoadmin_end_date'];
		$this->email		= $user_details->data[0]['isoadmin_email'];
//	TODO:		$this->is_isoadmin	= $user_details->data[0]['ssh_user'];
		
		if (!isset($this->username)) $this->error->raiseError("ERROR: no matching management for ID $user_id found!");		
	}
	function getUserName() {
		return $this->username;
	}
	function getFirstName() {
		return $this->first_name;
	}
	function getLastName() {
		return $this->last_name;
	}
	function getStartDate() {
		return $this->start_date;
	}
	function getEndDate() {
		return $this->end_date;
	}
	function getEmail() {
		return $this->email;
	}
	function getIsIsoadmin() {
		return $this->is_isoadmin;
	}
}

class IsoadminList {
	var $isoadmin_list;
	var $error;
	
	function __construct() {
		$this->error = new PEAR();
		if ($this->error->isError($db_connection))
			$this->error->raiseError("F-RCF: Connection not initialized. " . $db_connection->getMessage());
		$db = new DbList();
		$db->initSessionConnection();
		$sql_code = "SELECT * FROM isoadmin ORDER BY isoadmin_username"; 
		$this->isoadmin_list =$db->db_connection->iso_db_query($sql_code);
		if ($this->error->isError($this->isoadmin_list)) $this->error->raiseError($this->isoadmin_list->getMessage());
	}
	function GetUsers() {
		return $this->isoadmin_list;
	}
}
?>