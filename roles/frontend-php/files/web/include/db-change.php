<?php

/*
 * $Id: db-change.php,v 1.1.2.5 2012-04-30 17:21:17 tim Exp $
 * $Source: /home/cvs/iso/package/web/include/Attic/db-change.php,v $
 * Created on 27.10.2005
 */
 
require_once ("db-base.php");
require_once ("display-filter.php");
require_once ("db-gui-config.php");
require_once ('multi-language.php');

class ChangeList extends DbList {
	var $changes;
	var $change_number;
		
	function __construct($filter, $additional_sql_filter, $change_table) {
		$this->error = new PEAR();
		$this->filter = $filter;
		$this->db_connection = $this->initConnection($this->filter->getSessionUser(), $this->filter->getSessionSecret());
		if (!$this->error->isError($this->db_connection)) {
			$display_config = new DisplayConfig();
			$sqlcmd = "SELECT * FROM $change_table WHERE security_relevant ";
			if (!$filter->show_other_admins_changes() and isset($filter->db_user_id) and $filter->db_user_id<>'') {
				$sqlcmd .= " AND (change_admin_id IS NULL OR change_admin_id=" . $filter->db_user_id . ") ";
			}
			if ($filter->show_only_selfdoc() and isset($filter->db_user_id) and $filter->db_user_id<>'') {
				$sqlcmd .= " AND (doku_admin_id=" . $filter->db_user_id . ") ";
			}
			if (isset($additional_sql_filter)) $sqlcmd .= " AND $additional_sql_filter";
			if (isset($this->filter->dev_id) AND $this->filter->dev_id<>'NULL') {
				$sqlcmd_get_mgm_id = "SELECT mgm_id FROM device WHERE dev_id=" . $this->filter->dev_id;
				$mgm_data = $this->db_connection->fworch_db_query($sqlcmd_get_mgm_id);
				if (!$this->error->isError($mgm_data)) {
					$mgm_id = $mgm_data->data[0]['mgm_id'];
				} 
				$sqlcmd .=	" AND ((change_element_order='rule_element' AND dev_id=" . $this->filter->dev_id .
							"))"; // BBC
//								") OR (change_element_order='basic_element' AND mgm_id=" . $mgm_id . "))";
			}
			if (isset($this->filter->report_date1)) $sqlcmd .= " AND change_time>=CAST('" . $this->filter->report_date1 . "' AS TIMESTAMP) "; 
			if (isset($this->filter->report_date2)) $sqlcmd .= " AND change_time<=CAST('" . $this->filter->report_date2 . "' AS TIMESTAMP) ";

			if (isset($this->filter->tenant_id)) {
				$tenant_id = $this->filter->tenant_id;
				$start_time = 'NULL'; $stop_time = 'NULL'; $mgm_filter = 'NULL'; $dev_filter = 'NULL';
				$sqlcmd .= " AND abs_change_id IN (SELECT abs_change_id FROM $change_table " .
					"LEFT JOIN rule ON (change_element='rule' AND old_id=rule_id or new_id=rule_id) " .
					"LEFT JOIN view_rule_source_or_destination USING (rule_id) " .
					"LEFT JOIN object USING (obj_id) " .
					"LEFT JOIN tenant_network ON NOT rule_neg AND " .
					"(obj_ip::inet << tenant_network.tenant_net_ip::inet OR " .
					"obj_ip::inet >> tenant_network.tenant_net_ip::inet OR " .
					"obj_ip::inet = tenant_network.tenant_net_ip::inet ".
					") OR " .
					"rule_neg AND NOT " .
					"obj_ip::inet << tenant_network.tenant_net_ip::inet AND NOT " .
					"obj_ip::inet >> tenant_network.tenant_net_ip::inet AND NOT " .
					"obj_ip::inet = tenant_network.tenant_net_ip::inet WHERE tenant_id=$tenant_id ";
					if (isset($this->filter->report_date1)) $sqlcmd .= "AND change_time>=CAST('" . $this->filter->report_date1 . "' AS TIMESTAMP) "; 
					if (isset($this->filter->report_date2)) $sqlcmd .= "AND change_time<=CAST('" . $this->filter->report_date2 . "' AS TIMESTAMP) ";
					if (isset($mgm_id) && !$mgm_id==='NULL') $mgm_filter =  " AND mgm_id=$mgm_id "; else $mgm_filter = "";
					if (isset($this->filter->dev_id) && !$this->filter->dev_id==='NULL') $dev_filter = " AND dev_id=" . $this->filter->dev_id . " "; else $dev_filter="";
					$sqlcmd .= "$mgm_filter $dev_filter";
					$sqlcmd .= ')';
			}
			$sqlcmd .= ' ORDER BY change_time';	// BBC	
//			$sqlcmd .= ' ORDER BY abs_change_id';	//BBC
			if ($filter->TypeOfChangeTable()=='undocumented' and $display_config->display_undoc_changes>0)
				$sqlcmd .= " LIMIT " . $display_config->display_undoc_changes;
// BBC
			$log = new LogConnection(); $log->log_debug("ChangeList sql debug: $sqlcmd");
			$db_changes = $this->db_connection->fworch_db_query($sqlcmd);
			if (!$this->error->isError($db_changes)) {
				$this->change_number = $db_changes->rows;
				$change_array = array ();
				for ($zi = 0; $zi < $this->change_number; ++ $zi) {
					$change_array[] = new ChangedElement ($db_changes->data[$zi],$this->filter,$this->db_connection);
				}
				$this->changes = $change_array;
			} else {
				$this->changes = $db_changes;
			}
		}
	}
	function getChanges() {
		if ($this->error->isError($this->changes)) {
			$this->error->raiseError("E-RCL1: Changes not loaded properly. ".$this->changes->getMessage());
		}
		return $this->changes;
	}
	function get_displayed_change_number() {
//		return (count($this->getChanges()));
		return $this->change_number;
	}
	function total_undocumented_change_number($additional_sql_filter) {
		$data = $this->db_connection->fworch_db_query ("SELECT COUNT(*) FROM view_undocumented_change_counter WHERE $additional_sql_filter");
		$result = $data->data[0]['count'];
//		echo "found total undoc changes $result<br>";
		return $result;
	}
	function undocumented_change_number_per_user($user_id,$additional_sql_filter) {
//		echo "add-Filter: $additional_sql_filter, user_id: $user_id<br>";
		$user_filter = (isset($user_id) and $user_id<>'')?'(NOT import_admin IS NULL AND import_admin = ' . $user_id .')':'(TRUE)';
		$data = $this->db_connection->fworch_db_query
			("SELECT COUNT(*) FROM view_undocumented_change_counter WHERE $user_filter AND $additional_sql_filter");
		$result = $data->data[0]['count'];
//		echo "found undoc changes per user $user_id: $result<br>";
		return $result;
	}
	function undocumented_change_number_anonymous($additional_sql_filter) {
		$data = $this->db_connection->fworch_db_query
			("SELECT COUNT(*) FROM view_undocumented_change_counter WHERE import_admin IS NULL AND $additional_sql_filter");
		$result = $data->data[0]['count'];
		return $result;
	}
	function total_documented_change_number($additional_sql_filter) {
		$data = $this->db_connection->fworch_db_query ("SELECT COUNT(*) FROM view_documented_change_counter WHERE $additional_sql_filter");
		$result = $data->data[0]['count'];
//		echo "found total doc changes $result<br>";
		return $result;
	}
	function documented_change_number_per_user($user_id,$additional_sql_filter) {
//		echo "add-Filter: $additional_sql_filter, user_id: $user_id<br>";
		$user_filter = (isset($user_id) and $user_id<>'')?'(NOT import_admin IS NULL AND import_admin = ' . $user_id .')':'(TRUE)';
		$data = $this->db_connection->fworch_db_query
			("SELECT COUNT(*) FROM view_documented_change_counter WHERE $user_filter AND $additional_sql_filter");
		$result = $data->data[0]['count'];
//		echo "found undoc changes per user $user_id: $result<br>";
		return $result;
	}
	function documented_change_number_anonymous($additional_sql_filter) {
		$data = $this->db_connection->fworch_db_query
			("SELECT COUNT(*) FROM view_documented_change_counter WHERE import_admin IS NULL AND $additional_sql_filter");
		$result = $data->data[0]['count'];
		return $result;
	}
}

class ChangedElement extends DbItem {
	var $abs_change_id;
	var $local_change_id;
	var $change_action;
	var $change_admin;
	var $change_admin_str;
	var $doku_admin;
	var $old_id;
	var $new_id;
	var $control_id;
	var $change_comment;
	var $change_time;
	var $tenant_request_str;
	var $mgm_name;
	var $dev_name;
	var $dev_id;
	var $mgm_id;
	var $diffStr;
	var $filter;

	function __construct($change_table_data, $filter, $conn) {
		$this->filter = $filter;
		$this->db_connection = $conn;
		$db_change_keys = array_keys($change_table_data);
		$language = new Multilanguage($filter->getUser());
		$this->old_id = $this->getValue($change_table_data, "old_id", $db_change_keys);
		$this->changelog_id = $this->getValue($change_table_data, "abs_change_id", $db_change_keys);
		$this->new_id = $this->getValue($change_table_data, "new_id", $db_change_keys);
		$this->abs_change_id = $this->getValue($change_table_data, "abs_change_id", $db_change_keys);
		$this->local_change_id = $this->getValue($change_table_data, "local_change_id", $db_change_keys);
		$this->base_table = $this->getValue($change_table_data, "change_element", $db_change_keys);
		$this->change_action = $this->getValue($change_table_data, "change_type", $db_change_keys);
		$this->doku_admin = $this->getValue($change_table_data, "doku_admin", $db_change_keys);;
		$this->change_admin = $this->getValue($change_table_data, "change_admin", $db_change_keys);;
		$this->change_time = $this->getValue($change_table_data, "change_time", $db_change_keys);
		$this->change_request = $this->getValue($change_table_data, "change_request_info", $db_change_keys);
		$this->dev_name = $this->getValue($change_table_data, "dev_name", $db_change_keys);
		$this->dev_id = $this->getValue($change_table_data, "dev_id", $db_change_keys);
		$this->mgm_name =  $this->getValue($change_table_data, "mgm_name", $db_change_keys);
		$this->mgm_id =  $this->getValue($change_table_data, "mgm_id", $db_change_keys);
		$this->change_header = $language->get_text_msg('report_change_time_of_change', 'html')  .": " . substr($this->change_time,0,19) . 
			", Managementsystem: " . $this->mgm_name;
		if (is_null($this->change_admin) or $this->change_admin == '')
			$this->change_admin_str = ", anonym";
		else
			$this->change_admin_str = ", Administrator: " . $this->change_admin;
		$this->unique_element_name = $this->getValue($change_table_data, "unique_name", $db_change_keys);
		$this->change_comment =	$this->getValue($change_table_data, "change_comment", $db_change_keys);
	}
	function select_mgm_name($mgmid) {
		if (is_null($mgmid))
			$this->error->raiseError("E-R3: Management Id is null.");
		$name = $this->db_connection->fworch_db_query("SELECT mgm_name FROM management WHERE mgm_id=$mgmid",$this->filter->getLogLevel());
		if ($this->error->isError($name)) {
			$this->error->raiseError($name->getMessage());
		}
		return $name->data[0]['mgm_name'];
	}
	function select_dev_name($devid) {
		if (is_null($devid))
			return NULL;
		$name = $this->db_connection->fworch_db_query("SELECT dev_name FROM device WHERE dev_id=$devid");
		if ($this->error->isError($name)) {
			$this->error->raiseError($name->getMessage());
		}
		return $name->data[0]['dev_name'];
	}
	
	function getChangeAction() {
		return $this->change_action;
	}
	function getChangeAdmin() {
		return $this->change_admin;
	}
	function getChangeTime() {
		return $this->change_time;
	}
	function getChangeComment() {
		return $this->change_comment;
	}
	function getAbsChangeId() {
		return $this->abs_change_id;
	}
	function getControlId() {
		return $this->control_id;
	}
	function getOldElement() {
		return $this->old_element;
	}
	function getNewRule() {
		return $this->new_rule;
	}
	function getDevId() {
		return $this->dev_id;
	}
	function getDevName() {
		return $this->dev_name;
	}
	function getMgmId() {
		return $this->mgm_id;
	}
	function getMgmName() {
		return $this->mgm_name;
	}
	function set_changelog_sql_values($table_name) {
		switch ($table_name) {
			case "rule":
				$log_id_name = "log_rule_id";
				$request_change_table = "request_rule_change";
				$changelog_table = "changelog_rule";
				$comment_field = $changelog_table . "_comment";
				break;
			case "object":
				$log_id_name = "log_obj_id";
				$request_change_table = "request_object_change";
				$changelog_table = "changelog_object";
				$comment_field = "changelog_obj_comment";
				break;
			case "service":
				$log_id_name = "log_svc_id";
				$request_change_table = "request_service_change";
				$changelog_table = "changelog_service";
				$comment_field = "changelog_svc_comment";
				break;
			case "usr":
				$log_id_name = "log_usr_id";
				$request_change_table = "request_user_change";
				$changelog_table = "changelog_user";
				$comment_field = "changelog_user_comment";
				break;
			default:
				$log_id_name = $table_name;
				$request_change_table = $table_name;
				$changelog_table = $table_name;
				$comment_field = $table_name;
				break;
		}
		return array($log_id_name, $request_change_table, $changelog_table, $comment_field);
	}
}
?>
