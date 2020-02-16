<?php
/*
 * $Id: db-accounting.php,v 1.1.2.2 2007-12-13 10:47:31 tim Exp $
 * $Source: /home/cvs/iso/package/web/include/Attic/db-accounting.php,v $
 * Created on 18.09.2006
 */
 
require_once ("db-base.php");
require_once ("db-rule.php");
require_once ("display-filter.php");

class AccRuleList extends RuleList {
	var $rule_list;
	var $rule_ids;
	var $AccRuleNumber;
	var $report_id;

	function __construct($filter) {
		$this->error = new PEAR();
		$this->filter = $filter;
		if(is_null($this->filter->getDeviceId()) ||	is_null($filter->getReportTime())) {
				$this->error->raiseError("E-RL2: Filter criteria is null.");
		}
		$report_id = $filter->getReportId();
 		$sqlcmd = "INSERT INTO temp_filtered_rule_ids SELECT $report_id AS report_id, get_rule_ids AS rule_id FROM get_rule_ids(".
			(is_null($this->filter->getDeviceId()) ? 'NULL' : $this->filter->getDeviceId()).
			",'".$this->filter->getReportTime()."', ".
			(is_null($this->filter->getClientId()) ? 'NULL' : $this->filter->getClientId()) . ", '" . $this->filter->getMgmFilter4ReportConfig() . "')";
		$this->db_connection = $this->initConnection($this->filter->getSessionUser(), $this->filter->getSessionSecret());
		if (!$this->error->isError($this->db_connection)) {
			$db_rule_ids = $this->db_connection->iso_db_query($sqlcmd);
			$this->rows = $db_rule_ids->rows;
			$rule_cnt = 0;
			$rule_id_list = '';
			for ($zi = 0; $zi < $this->rows; ++ $zi) {
				if ($rule_cnt > 0) $rule_id_list .= ",";
				$rule_id_list .= $db_rule_ids->data[$zi]['get_rule_ids'];
				$rule_cnt ++;
			} // rule_ids are stored now
	 		$sqlcmd = "SELECT rule.rule_id FROM rule " . 
	 				" JOIN temp_filtered_rule_ids ON (temp_filtered_rule_ids.rule_id=rule.rule_id) " .
	 				" WHERE  temp_filtered_rule_ids.report_id=$report_id" .
					" AND NOT rule.rule_disabled " .
					" GROUP BY rule.rule_id";
	//		echo "RuleList sql: $sqlcmd<br>";
			$this->db_connection = $this->initConnection($this->filter->getSessionUser(), $this->filter->getSessionSecret());
			if (!$this->error->isError($this->db_connection)) {
				$db_rule_ids = $this->db_connection->iso_db_query($sqlcmd);
				if (!$this->error->isError($db_rule_ids)) {
					$this->rows = $db_rule_ids->rows;
					$this->AccRuleNumber = $db_rule_ids->rows;
					$rule_cnt = 0;
					$rule_id_array = "'{";
					for ($zi = 0; $zi < $this->rows; ++ $zi) {
						if ($rule_cnt > 0) {
							$rule_id_array .= ",";
						}
						$rule_id_array .= $db_rule_ids->data[$zi]['rule_id'];
						$rule_cnt ++;
	//					echo "rule: " . $db_rule_ids->data[$zi]['rule_id'] . '<br>';
					}
					$rule_id_array .= "}'";
					$this->rule_ids = $rule_id_array;
				} else {
					$this->rule_ids = $db_rule_ids;
				}
	//			echo "rule_ids in RuleList: " . $this->rule_ids . "<br>";
			}
		}
	}
	function getAccRuleNumber() {
		return $this->AccRuleNumber;
	}
	function getFilteredRuleIds() {
		return $this->rule_ids;
	}	
}

class AccNetworkObjectList extends DbList {
	var $obj_number;
	
	function __construct($filter) {
		$this->error = new PEAR();
		$import_id = $filter->getRelevantImportId();		// TODO: unscharf bei Report �ber alle Systeme 
		$rule_filter = "SELECT * FROM get_obj_ids_of_filtered_ruleset(" .
				$filter->getFilteredRuleIds() . "," .
				(is_null($filter->getClientId()) ? 'NULL' : $filter->getClientId()) . ", '" .
				$filter->getReportTime() . "')";
		$sql_code = "SELECT obj_id AS id FROM object WHERE obj_id IN ($rule_filter) UNION " .
			"SELECT objgrp_flat_member_id as id FROM objgrp_flat WHERE objgrp_flat_id IN ($rule_filter) " .
//			" AND objgrp_flat.import_created<=$import_id AND objgrp_flat.import_last_seen>=$import_id " .
			" GROUP BY id";
		$db_connection = $this->initConnection($filter->getSessionUser(), $filter->getSessionSecret());
		if (!$this->error->isError($db_connection)) {
			$obj_list = $db_connection->iso_db_query($sql_code);
			$this->obj_number = $obj_list->rows; 
		}
	}
	function getRows() {
		return $this->obj_number;
	}
}

class AccServiceList extends DbList{
	var $svc_number;
	
	function __construct($filter) {
		$this->error = new PEAR();
		
		$import_id = $filter->getRelevantImportId();		// TODO: unscharf bei Report �ber alle Systeme 
		$sql_code = "SELECT svc_id AS id FROM rule_service WHERE rule_id = ANY (" . $filter->getFilteredRuleIds() . ") UNION " . 
					" SELECT svcgrp_flat_member_id AS id FROM svcgrp_flat WHERE svcgrp_flat_id IN " . 
					"( SELECT svc_id AS id FROM rule_service WHERE rule_id = ANY (" . $filter->getFilteredRuleIds() . ") ) " .
//					" AND svcgrp_flat.import_created<=$import_id AND svcgrp_flat.import_last_seen>=$import_id " .
					"GROUP BY id"; 
		$db_connection = $this->initConnection($filter->getSessionUser(), $filter->getSessionSecret());
		if (!$this->error->isError($db_connection)) {
			$svc_list = $db_connection->iso_db_query($sql_code);
			$this->svc_number = $svc_list->rows; 
		}
	}
	
	function getRows() {
		return $this->svc_number;
	}
}

class AccUserList extends DbList{
	var $user_number;
	
	function __construct($filter) {
		$this->error = new PEAR();
		$import_id = $filter->getRelevantImportId();		// TODO: unscharf bei Report �ber alle Systeme 
		$sql_code = "SELECT user_id AS id FROM rule_from WHERE rule_from.user_id IS NOT NULL AND rule_id = ANY (" . $filter->getFilteredRuleIds() . 
					") UNION SELECT usergrp_flat_member_id AS id FROM usergrp_flat WHERE usergrp_flat_id IN " . 
					"( SELECT user_id AS id FROM rule_from WHERE rule_from.user_id  IS NOT NULL AND rule_id = ANY (" . $filter->getFilteredRuleIds() . ") )" .
//					" AND usergrp_flat.import_created<=$import_id AND usergrp_flat.import_last_seen>=$import_id " .
					" GROUP BY id"; 
		$db_connection = $this->initConnection($filter->getSessionUser(), $filter->getSessionSecret());
		if (!$this->error->isError($db_connection)) {
			$user_list = $db_connection->iso_db_query($sql_code);
			$this->user_number = $user_list->rows; 
		}
	}
	
	function getRows() {
		return $this->user_number;
	}
}
?>