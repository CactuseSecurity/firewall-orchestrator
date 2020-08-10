<?php
/*
 * $Id: display-filter.php,v 1.1.2.9 2012-05-28 10:34:19 tim Exp $
 * $Source: /home/cvs/iso/package/web/include/Attic/display-filter.php,v $
 * Created on 29.10.2005
 *
 */
require_once ("operating-system.php");
require_once ("db-base.php");
require_once ("db-rule.php");
require_once ("db-gui-config.php");

class DisplayFilter {
	var $mgm_id;
	var $dev_id;
	var $client_id;
	var $client_filter_expr;
	var $foreign_usernames_pattern;
	var $src_name;
	var $src_ip;
	var $dst_name;
	var $dst_ip;
	var $svc_name;
	var $svc_proto;
	var $svc_dst_port;
	var $usr_id;
	var $usr_lastname;
	var $usr_firstname;
	var $rule_comment;
	var $show_disabled;
	var $show_rule_obj_only;
	var $show_any_rules;
	var $show_neg_rules;
	var $src_zone;
	var $dst_zone;
	var $filteredRowIds;
	var $dbuser;
	var $action;
	var $dbpw;
	var $db_connection;
	var $stamm;
	var $loglevel;
	var $mgm_filter;
	var $report_id;				// random number used for generating temporary data in temp_xxx tables
	var $error;
	var $client_net_ar;  // used only in display_rule_config::

	function __construct() {
		$this->error = new PEAR();
	}
	function getReportId () {
		return $this->report_id;
	}
	function getUser () {
		return $this->dbuser;
	}
	function TypeOfChangeTable() {
		// types: document | report | change_documentation
		if (!isset($this->type)) return 'report'; // no documentation just viewing changes 
		else return $this->type;
	}
	function addFilteredRow($rowId) {
		$this->filteredRowIds[] = $rowId;
	}
	function emptyFilteredRowIds() {
		$this->filteredRowIds = NULL;
		$this->filteredRowIds = array ();
	}
	function getFilteredRowIds() {
		return $this->filteredRowIds;
	}
	function getLogLevel() {
		return $this->loglevel;
	}	

// mgm_filter functions
	function getMgmFilterPlain() {
		return $this->mgm_filter;
	}	
	function getMgmFilter4View() {
		return $this->getMgmFilterPlain();
	}	
	function getMgmFilter() {
		$f1 = str_replace ('mgm_id', 'management.mgm_id', $this->getMgmFilterPlain());
		$f2 = str_replace ('dev_id', 'device.dev_id', $f1);
		return $f2;
	}	
	function getMgmFilter4ReportConfig() {
		return $this->getMgmFilter();
	}
	function getMgmFilter4ReportRulesearch() {
		$f1 = str_replace ('mgm_id', 'rule.mgm_id', $this->getMgmFilterPlain());
		return str_replace ('dev_id', 'rule_order.dev_id', $f1);
	}	
	function setMgmFilter($mgm_filter) {
		$this->mgm_filter = $mgm_filter;
	}	
	function isMgmFilterSet() {
		if ($this->getMgmFilterPlain() == ' (TRUE) ')
			return false;
		else
			return true;
	}	
	
	function getFilteredRowIdsArrayString() {
		$str = "'{";
		$first = 0;
		foreach ($this->filteredRowIds as $rowId) {
			if ($first > 0) {
				$str .= ",";
			}
			$str .= $rowId;
			$first ++;
		}
		$str .= "}'";
		return $str;
	}
	function get_foreign_username_pattern() {
		return $this->foreign_usernames_pattern;
	}
	function read_foreign_username_pattern() {
		$client_id = $this->getClientId();
		$client_id = trim($client_id);
		if ($client_id=='' or $client_id == 'NULL') return '';

		$sql_code = "SELECT client_username_pattern FROM client_username WHERE NOT client_id=$client_id";
		$foreign_user_pattern = $this->db_connection->fworch_db_query($sql_code);
		if ($this->error->isError($foreign_user_pattern)) $this->error->raiseError($foreign_user_pattern->getMessage());

		$foreign_user_pattern_array = array();
		$user_pattern_anz = $foreign_user_pattern->rows;
		for ($zi = 0; $zi < $user_pattern_anz; ++ $zi) {
			$foreign_user_pattern_array[] =   $foreign_user_pattern->data[$zi]["client_username_pattern"];
		}
		return implode('|', $foreign_user_pattern_array);
	}
	function setDisplayFilter($request, $requestKeys, $session, $sessionKeys) {
		// filters in session
		$this->dbuser = $this->getValue($session, "dbuser", $sessionKeys);
		$this->dbpw = $this->getValue($session, "dbpw", $sessionKeys);
		$this->client_filter_expr = $this->getValue($session, "ClientFilter", $sessionKeys);
		$this->loglevel = $this->getValue($session, "loglevel", $sessionKeys);
		$DbConnData = new DbConfig($this->dbuser, $this->dbpw);
		$this->db_connection = new DbConnection($DbConnData);

		// global filters
		$this->stamm = $this->getValue($request, "stamm", $requestKeys);
		$this->dev_id = $this->getValue($request, "devId", $requestKeys);
		$this->client_id = $this->getValue($request, "client_id", $requestKeys);
		$this->mgm_id = $this->get_mgm_id_of_dev_id($this->dev_id);
		$this->foreign_usernames_pattern = $this->read_foreign_username_pattern();

		// admin dependent display values (read from session - not request!)
		$this->mgm_filter = $this->getValue($session, "ManagementFilter", $sessionKeys);
//		$this->mgm_filter4view = $this->getValue($request, "ManagementFilter4View", $requestKeys);		

		// source filter
		$this->src_name = $this->getValue($request, "quellname", $requestKeys);
		$this->src_ip = $this->getValue($request, "quell_ip", $requestKeys);
		$this->src_zone = NULL;
		// destination filter
		$this->dst_name = $this->getValue($request, "zielname", $requestKeys);
		$this->dst_ip = $this->getValue($request, "ziel_ip", $requestKeys);
		$this->dst_zone = NULL;
		// service filter
		$this->svc_name = $this->getValue($request, "dienstname", $requestKeys);
		$this->svc_proto = $this->getValue($request, "dienst_ip", $requestKeys);
		$this->svc_dst_port = $this->getValue($request, "dienstport", $requestKeys);
		// user filter
		$this->usr_id = $this->getValue($request, "ben_id", $requestKeys);
		$this->usr_lastname = $this->getValue($request, "ben_name", $requestKeys);
		$this->usr_firstname = $this->getValue($request, "ben_vor", $requestKeys);
		// rule filter
		$this->rule_comment = $this->getValue($request, "regelkommentar", $requestKeys);
		// dependency filter
		$this->show_disabled = (NULL == $this->getRadioValue($request, "inactive", $requestKeys));
		$this->show_rule_obj_only = $this->getRadioValue($request, "notused", $requestKeys);
		$this->show_any_rules = $this->getRadioValue($request, "anyrules", $requestKeys);
		$this->show_neg_rules = $this->getRadioValue($request, "negrules", $requestKeys);
		$this->report_id = rand();

		// the following is for rulesearch reports:
		if ($this->filter_is_set())  // set action filter in case of filtering
			$this->action = "only_accepts";
		else
			$this->action = ''; // if no filter set - show rules of any action
	}
	function setClientNetArray($NetArray) {
		$this->client_net_ar = $NetArray;
	}
	function getValue($data, $key, $keys) {
		$value = NULL;
		if (in_array($key, $keys, true)) {
			if (strlen($data[$key]) > 0) {
				$value = $data[$key];
			}
		}
		return $value;
	}
	function getSessionUser() {
		return $this->dbuser;
	}
	function getSessionSecret() {
		return $this->dbpw;
	}
	function getRadioValue($data, $key, $keys) {
		$value = NULL;
		if (in_array($key, $keys, true)) {
			$value = $data[$key][0];
		}
		return $value;
	}
	function getFilter($value) {
		if (!is_null($value)) {
			$match = "/".$value."/i";
		} else {
			$match = NULL;
		}
		return $match;
	}
	function getIpFilter($value) {
		if (!is_null($value)) {
			$match = $value;
		} else {
			$match = NULL;
		}
		return $match;
	}
	function getProtoFilter($value) {
		if (!is_null($value) && $value != -1)
			$Match =  (int) $value;
		else 
			$Match = NULL;
		return $Match;
	}
	function getPortFilter($value) {
		if (!is_null($value))
			$Match =  (int) $value;
		else 
			$Match = NULL;
		return $Match;
	}
	function getStamm() {
		if (is_null($this->stamm)) {
			return "";
		} else {
			return $this->stamm;
		}
	}
	function getDeviceId() {
		return $this->dev_id;
	}
	function getClientId() {
		return $this->client_id;
	}
	function getManagementId() {
		return $this->mgm_id;
	}
	function getReportTime() {
		return $this->report_date1;
	}
	function showRuleObjectsOnly() {
		if (!is_null($this->show_rule_obj_only) && $this->show_rule_obj_only == "1") {
			return true;
		} else {
			return false;
		}
	}
	function showDisabled() {
		if (!is_null($this->show_disabled) && $this->show_disabled == "1") {
			return true;
		} else {
			return false;
		}
	}
	function showObjectsNotInRulebase() {
		if (!is_null($this->show_disabled) && $this->show_disabled == "1") {
			return true;
		} else {
			return false;
		}
	}
	function showAnyRules() {
		if (!is_null($this->show_any_rules) && $this->show_any_rules == "1") {
			return true;
		} else {
			return false;
		}
	}
	function showNegRules() {
		if (!is_null($this->show_neg_rules) && $this->show_neg_rules == "1") {
			return true;
		} else {
			return false;
		}
	}
	function get_mgm_id_of_dev_id($dev_id) {
		if (!is_null($dev_id) && !($dev_id == '') && !($dev_id == 'NULL')) {
			$mgm_table = $this->db_connection->fworch_db_query("SELECT mgm_id FROM device WHERE dev_id=$dev_id");
			$mgm_id = $mgm_table->data[0]['mgm_id'];
		} else {
			$mgm_id = NULL;
		}
		return $mgm_id;
	}
	function filter($filterString, $value) {
		$isFiltered = true;
		//echo "-->",$filterString,"::",$value,"<br>";
		if (!is_null($filterString) && !is_null($value)) {
			$isFiltered = preg_match($filterString, $value);
		} else
			if (!is_null($filterString) && is_null($value)) {
				$isFiltered = false;
			}
		//echo "Result is ",$isFiltered,"<br>";
		return $isFiltered;
	}
	function complete_cidr($cidr_in) {
		list ($ip, $mask) = explode('/', $cidr_in);
		if (!preg_match('/\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}/', $ip)) {
			if (preg_match('/^.*?\.$/', $ip)) // ip ends with dot
				$ip = substr($ip, 0, strlen($ip) - 1);
			$missing_bytes = 3 - substr_count($ip, '.');
			if ($missing_bytes) {
				$mask = 8 * (4 - $missing_bytes);
				for ($i = 0; $i < $missing_bytes; ++ $i) {
					$ip .= ".0";
				}
			} 
		}
		if (!isset($mask))	$mask = 32;
		return $ip.'/'.$mask;
	}
	function complete_cidr2($cidr_in) {
		if (!isset($cidr_in) or $cidr_in == '') return NULL;
		list ($ip, $mask) = explode('/', $cidr_in);
		if  (preg_match('/^.*?\.$/', $ip)) $ip = substr($ip, 0, strlen($ip) - 1); // ip ends with dot --> remove it
		if (!isset($mask)) { // setze Maske
			$dots = substr_count($ip, '.');
			if ($dots < 3) for ($i=0; $i<3-$dots; ++$i) $ip .= '.0';
			$mask = 32;
			$tmp_ip = $ip;
			while (preg_match('/^.*?\.0$/', $tmp_ip)) {
				$mask -= 8;
				$tmp_ip = substr($tmp_ip, 0, strlen($tmp_ip) - 2);
			}
		}
		return $ip.'/'.$mask;
	}
	function ips_overlap($cidr1, $cidr2) {
		list ($ip1, $mask1) = explode('/', $this->complete_cidr($cidr1));
		list ($ip2, $mask2) = explode('/', $this->complete_cidr($cidr2));
		$min_mask = (($mask1<$mask2)? $mask1 : $mask2);
		if ($min_mask == 0) $mask = 0x0;
		else $mask = 0xffffffff << (32 - $min_mask);
		$output = ((ip2long($ip2) & $mask) == (ip2long($ip1) & $mask));
		return $output;
	}
	function filterIp($filterString, $value) {
		$isFiltered = true;
		if (!is_null($filterString) && !is_null($value)) {
			$isFiltered = $this->ips_overlap($filterString, $value);
		} else
			if (!is_null($filterString) && is_null($value)) {
				$isFiltered = false;
			}
		return $isFiltered;
	}
	function filterEqualInt($filter, $value) {
		$isFiltered = true;
		if (!is_null($filter) && !is_null($value)) $isFiltered = ((int) $filter == (int) $value);
		else if (!is_null($filter) && is_null($value)) $isFiltered = false;
		return $isFiltered;
	}
	function filterIpProto($filterString, $ip_proto) { return $this->filterEqualInt($filterString, $ip_proto); }
	function filterPort($filterString, $port) { return $this->filterEqualInt($filterString, $port); }

	function filterService($name, $ipProtoId, $port) {
		$isName = ($this->filter($this->getFilter($this->svc_name), $name));
		$isIpProto = $this->filterIpProto($this->getProtoFilter($this->svc_proto), $ipProtoId);
		$isPort = $this->filterPort($this->getPortFilter($this->svc_dst_port), $port);
		return (strtolower($name)=='any') or ($isName && $isIpProto && $isPort);
	}
	function filterUser($id, $lastname, $firstname) {
		$isId = $this->filter($this->getFilter($this->usr_id), $id);
		$isLName = $this->filter($this->getFilter($this->usr_lastname), $lastname);
		$isFName = $this->filter($this->getFilter($this->usr_lastname), $firstname);
		return ($isId && $isLName && $isFName);
	}
	function filterSourceObject($name, $ip, $zone) {
		$isName = $this->filter($this->getFilter($this->src_name), $name);
		$isIp = $this->filterIp($this->getIpFilter($this->src_ip), $ip);
		$isZone = true;
		if (!is_null($this->src_zone)) $isZone = ($this->src_zone == $zone);
		return ($isName && $isIp && $isZone);
	}
	function filterDestinationObject($name, $ip, $zone) {
		$isName = $this->filter($this->getFilter($this->dst_name), $name);
		$isIp = $this->filterIp($this->getIpFilter($this->dst_ip), $ip);
		$isZone = true;
		if (!is_null($this->dst_zone)) $isZone = ($this->dst_zone == $zone);
		return ($isName && $isIp && $isZone);
	}
	function filterComment($comment) { return $this->filter($this->getFilter($this->rule_comment), $comment); }
	function filter_value_set($value) {
		if (!empty($value) and $value<>0)
			return true;
		else
			return false;
	}
	function filter_is_set() {
		return ($this->non_client_filter_is_set() or $this->client_filter_is_set()); 	
	}
	function non_client_filter_is_set() {
		return (
			$this->filter_value_set($this->src_name) or
			$this->filter_value_set($this->src_ip) or
			$this->filter_value_set($this->src_zone) or
			$this->filter_value_set($this->dst_name) or
			$this->filter_value_set($this->dst_ip) or
			$this->filter_value_set($this->dst_zone) or
			$this->filter_value_set($this->svc_name) or
			($this->filter_value_set($this->svc_proto) and $this->svc_proto<>-1) or
			$this->filter_value_set($this->svc_dst_port) or
			$this->filter_value_set($this->usr_id) or
			$this->filter_value_set($this->usr_lastname) or
			$this->filter_value_set($this->usr_firstname) or
			$this->filter_value_set($this->rule_comment)	
		);
	}
	function client_filter_is_set() {
		return ( $this->filter_value_set($this->client_id) and !(($this->getClientId())==0));
	}
	function is_valid_ip($cidr_in) {
		list ($ip, $mask) = explode('/', $cidr_in);
		list ($oct[3], $oct[2], $oct[1], $oct[0]) = explode('.', $ip);
		if (!isset($mask)) {
			for ($i=0; $i<=3; ++$i) if ($oct[$i]>256) return false; 
		} else {
			if ($mask == 32) return true;
			for ($i=3; $i>=0; --$i) {
				$binary_mask = 256 - pow(2, 8 - min($mask,8));
				if (($oct[$i] & $binary_mask) <> $oct[$i]) return false;
				$mask -= 8;
			} 
		}
		return true;
	}
	function check_filter_values() {
		$this->src_ip = $this->complete_cidr2($this->src_ip);
		$this->dst_ip = $this->complete_cidr2($this->dst_ip);
		$return_string = '';
		if ($this->filter_value_set($this->src_ip) and !$this->is_valid_ip($this->src_ip))
			$return_string .= "Ung&uuml;ltiger Quell-IP-Adressfilter: $this->src_ip. Bitte korrigieren.<br>";
		if ($this->filter_value_set($this->dst_ip) and !$this->is_valid_ip($this->dst_ip))
			$return_string .= "Ung&uuml;ltiger Ziel-IP-Adressfilter: $this->dst_ip. Bitte korrigieren.<br>";
		return $return_string;
	}

	function filterSourceObjectGroup($names, $ips, $zone) { return $this->filterNwObjectGroup($names, $ips, $zone, 'src'); }

	function filterDestinationObjectGroup($names, $ips, $zone) { return $this->filterNwObjectGroup($names, $ips, $zone, 'dst'); }
	
	function filterNwObjectGroup($names, $ips, $zone, $src_or_dst) {
		$isName = false; $isIp = false;
		if ($src_or_dst == 'src') {
			$name_filter = $this->getFilter($this->src_name); $ip_filter = $this->getIpFilter($this->src_ip);
		} else {
			$name_filter = $this->getFilter($this->dst_name); $ip_filter = $this->getIpFilter($this->dst_ip);
		}
		if ($name_filter)
			foreach ($names as $nwobj_name) {
				$isMatch = $this->filter($name_filter, $nwobj_name);
				$isName = ($isName || $isMatch);
			}
		else $isName = true;
		if ($ip_filter)
			foreach ($ips as $nwobj_ip) {
				$isMatch = $this->filterIp($ip_filter, $nwobj_ip);
				$isIp = ($isIp || $isMatch);
			}
		else $isIp = true;
		$isZone = true; if (!is_null($this->dst_zone)) { $isZone = ($this->dst_zone == $zone); }
		return ($isName && $isIp && $isZone);
	}
	function filterServiceGroup ($names, $protos, $ports) {
		$isNames = false; $isProto = false; $isPort = false;
		$name_filter = $this->getFilter($this->svc_name);
		$proto_filter = $this->getProtoFilter($this->svc_proto);
		$port_filter = $this->getIpFilter($this->svc_dst_port);
		if ($name_filter)
			foreach ($names as $svc_name) {
				$isMatch = $this->filter($name_filter, $svc_name);
				$isName = ($isName || $isMatch);
			}
		else $isName = true;
		if ($proto_filter) {
			foreach ($protos as $ip_proto) {
				$isMatch = $this->filterIpProto($proto_filter, $ip_proto);
				$isProto = ($isProto || $isMatch);
			}
		} else $isProto = true;
		if ($port_filter)
			foreach ($ports as $port) {
				$isMatch = $this->filterPort($port_filter, $port);
				$isPort = ($isPort || $isMatch);
			}
		else $isPort = true;
		return ($isName && $isProto && $isPort);
	}
}

class RuleConfigurationFilter extends DisplayFilter {
	var $report_type;
	var $report_date1;
	var $filteredRuleIds;
	var $control_id;
	var $relevantImportId;

	function __construct($request, $session) {
		$this->error = new PEAR();
		$sessionKeys = array_keys($session);
		$requestKeys = array_keys($request);
		$this->setDisplayFilter($request, $requestKeys,$session,$sessionKeys);
		$this->report_type = $this->getValue($request, "repTyp", $requestKeys);
		$this->report_date1 = $this->getValue($request, "zeitpunkteins", $requestKeys);
		$this->report_date2 = NULL; // f�r Config Report nicht relevant
		$this->control_id = $this->getRelevantImportIdForThisDevice(); // TODO: allgemeingueltig machen
	}
	function getRelevantImportIdForThisDevice() {
		$report_timestamp = $this->getReportTime();
		$mgm_filter = $this->getMgmFilter();
		$dev_id = $this->getDeviceId();
		$sqlcmd = "SELECT max(control_id) AS import_id FROM import_control" .
				" INNER JOIN management USING (mgm_id)" .
				" INNER JOIN device ON (management.mgm_id=device.mgm_id)" .
				" WHERE start_time<='$report_timestamp' AND NOT stop_time IS NULL AND successful_import AND $mgm_filter" .
				" AND device.dev_id=$dev_id" .
				" GROUP BY device.dev_id";
				
		if (!is_null($dev_id) && !($dev_id == '') && !($dev_id == 'NULL')) {
			// TODO: auf db-import-ids.php umstellen:
			$import_id_dev_id_table = $this->db_connection->fworch_db_query($sqlcmd);
			if (!$this->error->isError($import_id_dev_id_table)) {
				$import_id = $import_id_dev_id_table->data[0]['import_id']; 
			} else {
				$err = $import_id_dev_id_table;
				$this->error->raiseError("E-NWL2: Could not query DB: $sqlcmd" . $err->getMessage());
				return $err;
			}
		}
		else {
//			echo "<br>ERROR empty dev_id in getRelevantImportIdForThisDevice::dev_id=" . $dev_id . "<br>";
			$import_id = 'undefined';
		}
		return $import_id;
	}
	function setFilteredRuleIds($ruleIds) {
		$this->filteredRuleIds = $ruleIds;
	}
	function getFilteredRuleIds() {
		return $this->filteredRuleIds;
	}
	function getImportId() {
		return $this->control_id;
	}
	function setRelevantImportId($id) {
		$this->relevantImportId = $id;
	}
	function getRelevantImportId() {
		$importId = "";
		if (is_null($this->relevantImportId)) {
			$importId = $this->control_id;
		} else {
			$importId = $this->relevantImportId;
		}
		return $importId;
	}
//	function get_date_config_report() {
//		return $this->report_date1;
//	}	
}

class RuleChangesFilter extends DisplayFilter {
	var $report_type;
	var $report_date1;
	var $report_date2;
	var $filteredRuleIds;
	var $first_control_id;
	var $last_control_id;
	var $relevantImportId;
	var $client_list;
	var $ip_proto_list;
	var $show_other_admins_changes;
	var $show_only_selfdoc;
	var $db_user_id;
	var $type;

	function __construct($request, $session, $type) {
		$this->error = new PEAR();
		$sessionKeys = array_keys($session);
		$requestKeys = array_keys($request);
		$this->setDisplayFilter($request, $requestKeys,$session,$sessionKeys);
		$this->report_type = $this->getValue($request, "repTyp", $requestKeys);
		$this->report_date1 = $this->getValue($request, "zeitpunktalt", $requestKeys);
		$this->report_date2 = $this->getValue($request, "zeitpunktneu", $requestKeys);
		
		// erstmal default-Werte setzen
		if ($type == 'report' or $type == 'change_documentation') {
			$this->set_show_other_admins_changes(true);
		} else {
			$this->set_show_other_admins_changes(false);
		}					
		// wenn post-variable gesetzt --> �berschreiben
		if (isset($request['show_other_admins_changes']))
			$this->set_show_other_admins_changes(true);
		else 
			$this->set_show_other_admins_changes(false);
		if (isset($request['show_only_selfdoc']))
			$this->set_only_selfdoc(true);
		else 
			$this->set_only_selfdoc(false);
		$this->db_user_id = $this->getValue($session, "dbuserid", $sessionKeys);
		$this->type = $type;
		if (isset($this->dev_id))
			list ($this->first_control_id, $this->last_control_id) = 
				$this->get_changed_control_ids($this->dev_id, $this->report_date1, $this->report_date2);
		$this->client_list = $this->get_client_list($session["ClientFilter"]);
	}
	function get_start_date() {
		return ($this->report_date1);
	}
	function get_end_date() {
		return ($this->report_date2);
	}
	function show_other_admins_changes() {
		return ($this->show_other_admins_changes == true);
	}
	function set_show_other_admins_changes($bool) {
		$this->show_other_admins_changes = $bool;
	}
	function set_only_selfdoc($bool) {
		$this->show_only_selfdoc = $bool;
	}
	function show_only_selfdoc() {
		return ($this->show_only_selfdoc == true);
	}
	function setFilteredRuleIds($ruleIds) {
		$this->filteredRuleIds = $ruleIds;
	}
	function getFilteredRuleIds() {
		return $this->filteredRuleIds;
	}
	function getFirstImport() {
		return $this->first_control_id;
	}
	function getLastImport() {
		return $this->last_control_id;
	}
	function setRelevantImportId($id) {
		$this->relevantImportId = $id;
	}
	function getRelevantImportId() {
		return $this->relevantImportId;
	}
	function getClients() {
		return $this->client_list;
	}
	function get_changed_control_ids($devid, $time1, $time2) {
		$sqlcmd1 = "select get_next_import_id as import_id from get_next_import_id($devid,'".$time1."')";
		$sqlcmd2 = "select get_previous_import_id as import_id from get_previous_import_id($devid,'".$time2."')";
		if (!is_null($time1)) {
			$import_id1 = $this->db_connection->fworch_db_query($sqlcmd1);
			$id1 = $import_id1->data[0]['import_id'];
		} else {
			$id1 = NULL;
		}
		if (!is_null($time2)) {
			$import_id2 = $this->db_connection->fworch_db_query($sqlcmd2);
			$id2 = $import_id2->data[0]['import_id'];
		} else {
			$id2 = NULL;
		}
		return array ($id1, $id2);
	}
	function get_client_list($client_filter) {
		if ($this->error->isError($this->db_connection)) {
			$this->error->raiseError("F-RCF: Connection not initialized. ".$this->db_connection->getMessage());
		}
		if (isset ($client_filter))
			$sql_code = "SELECT * FROM client WHERE $client_filter ORDER BY client_name";
		else
			$sql_code = "SELECT * FROM client ORDER BY client_name";
		$clients = $this->db_connection->fworch_db_query($sql_code);

		if ($this->error->isError($clients)) {
			$this->error->raiseError($clients->getMessage());
		}
		return $clients;
	}
}
?>