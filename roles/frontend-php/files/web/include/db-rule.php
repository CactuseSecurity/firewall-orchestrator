<?php
// $Id: db-rule.php,v 1.1.2.7 2012-11-29 11:55:13 tim Exp $
// $Source: /home/cvs/iso/package/web/include/Attic/db-rule.php,v $

require_once ("db-base.php");
require_once ("db-import-ids.php");
require_once ("display-filter.php");


class Rule extends DbItem {
	// Variable declaration
	var $rule_id;
	var $rule_disabled;
	var $rule_src_neg;
	var $rule_src;
	var $rule_dst_neg;
	var $rule_dst;
	var $rule_svc;
	var $rule_svc_neg;
	var $rule_uid;
	var $rule_ruleid;
	var $rule_head_text;
	var $rule_num;
	var $rule_name;
	var $rule_dev_id;
	var $rule_action;
	var $rule_track;
	var $rule_installon;
	var $rule_comment;
	var $rule_from_zone;
	var $rule_to_zone;
	var $rule_from_zone_name;
	var $rule_to_zone_name;
//  LAST_CHANGE	var $import_ids;

	// Constructor
	function __construct($rule_table_data, $filter, $conn) {
		$this->error = new PEAR();
		$this->filter = $filter;
		$this->db_connection = $conn;
//		$this->import_ids = $import_ids;
		$db_rule_keys = array_keys($rule_table_data);
		$this->rule_id = $this->getValue($rule_table_data, "rule_id", $db_rule_keys);
		$this->rule_disabled = $this->getValue($rule_table_data, "rule_disabled", $db_rule_keys);
		$this->rule_src_neg = $this->getValue($rule_table_data, "rule_src_neg", $db_rule_keys);
		$this->rule_src = $this->get_rule_src($this->rule_id, $filter);
		$this->rule_dst_neg = $this->getValue($rule_table_data, "rule_dst_neg", $db_rule_keys);
		$this->rule_dst = $this->get_rule_dst($this->rule_id, $filter);
		$this->rule_svc_neg = $this->getValue($rule_table_data, "rule_svc_neg", $db_rule_keys);
		$this->rule_svc = $this->get_rule_svc($this->rule_id, $filter);
		$this->rule_uid = $this->getValue($rule_table_data, "rule_uid", $db_rule_keys);
		$this->rule_ruleid = $this->getValue($rule_table_data, "rule_ruleid", $db_rule_keys);
		$this->rule_head_text = $this->getValue($rule_table_data, "rule_head_text", $db_rule_keys);
		$this->rule_num = $this->getValue($rule_table_data, "rule_num", $db_rule_keys);
		$this->rule_name = $this->getValue($rule_table_data, "rule_name", $db_rule_keys);
		$this->rule_dev_id = $this->getValue($rule_table_data, "dev_id", $db_rule_keys);
		$this->rule_action = $this->getValue($rule_table_data, "rule_action", $db_rule_keys);
		$this->rule_track = $this->getValue($rule_table_data, "rule_track", $db_rule_keys);
		$this->rule_installon = $this->getValue($rule_table_data, "rule_installon", $db_rule_keys);
		$this->rule_comment = $this->getValue($rule_table_data, "rule_comment", $db_rule_keys);
		$this->rule_from_zone = $this->getValue($rule_table_data, 'rule_from_zone', $db_rule_keys);
		$this->rule_to_zone = $this->getValue($rule_table_data, 'rule_to_zone', $db_rule_keys);
		$this->rule_from_zone_name = $this->getValue($rule_table_data, 'rule_from_zone_name', $db_rule_keys);
		$this->rule_to_zone_name = $this->getValue($rule_table_data, 'rule_to_zone_name', $db_rule_keys);
		$this->display = true;

		//		$log = new LogConnection();
		//		$output = "reporting_tables_auditchanges->db-rule::rule_from_zone_name=" . $this->rule_from_zone_name;
		//		$log->log_error($output);
		//		echo "$output<br>";
	}
	// getters
	function getRuleId()				{ return $this->rule_id; }
	function isRuleHeader() 			{ return (isset($this->rule_head_text) and !($this->is_zone_header())); }
	function isRuleDisabled()			{ return (strcasecmp($this->rule_disabled, 'f') == 0); }
	function isRuleSourceNegated()		{ if ($this->rule_src_neg <> 't') return false; else return true; }
	function isRuleDestinationNegated()	{ if ($this->rule_dst_neg <> 't') return false; else return true; }
	function isRuleServiceNegated()		{ if ($this->rule_svc_neg <> 't') return false; else return true; }
	function is_zone_header() {
		$ruleheaderoffset = 99000; //unsauber - sollte aus config ausgelesen werden.
		if (is_numeric($this->rule_uid) and $this->rule_uid > $ruleheaderoffset and $this->rule_uid < $ruleheaderoffset+1000)
			return true;
			else
				return false;
	}
	function is_pass_rule() {
		$action_str = strtolower($this->getAction());
		if ($action_str == 'deny' or $action_str == 'drop' or $action_str == 'reject') return false;
		else return true;
	}
	function getRuleSource() {
		return $this->rule_src;
	}
	function getRuleDestination() {
		return $this->rule_dst;
	}
	function getRuleService() {
		return $this->rule_svc;
	}
	function getRuleUid() {
		return $this->rule_uid;
	}
	function getRuleRuleId() {
		return $this->rule_ruleid;
	}
	function getRuleHeadText() {
		return $this->rule_head_text;
	}
	function getRuleNum() {
		return $this->rule_num;
	}
	function getRuleName() {
		return $this->rule_name;
	}
	function getDevId() {
		return $this->rule_dev_id;
	}
	function getAction() {
		return $this->rule_action;
	}
	function getInstallOn() {
		return $this->rule_installon;
	}
	function getComment() {
		return $this->rule_comment;
	}
	function getFromZone() {
		return $this->rule_from_zone;
	}
	function getFromZoneName() {
		return $this->rule_from_zone_name;
	}
	function getToZone() {
		return $this->rule_to_zone;
	}
	function getToZoneName() {
		return $this->rule_to_zone_name;
	}
	function getTrack() {
		return $this->rule_track;
	}
	function getDevString() {
		$sql_code = "SELECT dev_name,mgm_name FROM device LEFT JOIN management USING (mgm_id) WHERE dev_id=" . $this->rule_dev_id;
		$dev_infos = $this->db_connection->fworch_db_query($sql_code);
		if ($this->error->isError($dev_infos)) {
			$this->error->raiseError($dev_infos->getMessage());
		}
		return "Management: " . $dev_infos->data[0]['mgm_name'] . ", Device: " .$dev_infos->data[0]['dev_name'];
	}
	function is_user_to_be_hidden($rule_username, $foreign_user_pattern) {
		if ($foreign_user_pattern =='')
			return false;
			else {
				return (ereg($foreign_user_pattern,$rule_username));
			}
	}
	function get_rule_src($rule_id, $filter) {
		$import_id = $filter->getRelevantImportId();
		if (is_null($filter->getManagementId())) $relevant_import_filter = '';
		else $relevant_import_filter = " (user_create IS NULL OR user_create<=$import_id AND user_last_seen>=$import_id) AND";

		$sql_code = "SELECT usr.user_name, usr.user_id, object.obj_name, object.obj_uid, object.obj_id, object.obj_ip, object.zone_id,  stm_obj_typ.obj_typ_name AS obj_type".
				" FROM object LEFT JOIN stm_obj_typ USING (obj_typ_id) " .
				" LEFT JOIN rule_from ON (object.obj_id=rule_from.obj_id) " .
				" LEFT JOIN usr ON (usr.user_id=rule_from.user_id) ".
				" WHERE rule_from.rule_id=$rule_id AND $relevant_import_filter object.obj_id IN (SELECT * FROM get_rule_src($rule_id, " .
				(is_null($filter->getClientId()) ? 'NULL' : $filter->getClientId()) .
				", '". $filter->getReportTime() . "'))  ORDER BY obj_name, user_name";
				$rule_src_table = $this->db_connection->fworch_db_query($sql_code);

				if ($this->error->isError($rule_src_table)) $this->error->raiseError($rule_src_table->getMessage());

				$src_array = array ();
				$obj_anz = $rule_src_table->rows;
				$foreign_user_pattern = $filter->get_foreign_username_pattern();
				for ($zi = 0; $zi < $obj_anz; ++ $zi) {
					$user_name = $rule_src_table->data[$zi]["user_name"];
					if ($this->is_user_to_be_hidden($rule_src_table->data[$zi]['user_name'], $foreign_user_pattern)) {  // hide user_name for client filtering
						$rule_src_table->data[$zi]["user_name"] = '[ANONYMISIERT]';
						$rule_src_table->data[$zi]["user_id"] = '[ANONYMISIERT]';
					}
					$src_item = new RuleNwObject($rule_src_table->data[$zi]);
					$src_array[] = $src_item;
				}
				return $src_array;
	}
	function get_rule_dst($rule_id, $filter) {
		$sql_code = "SELECT obj_id,obj_name,obj_uid,obj_ip,zone_id,stm_obj_typ.obj_typ_name AS obj_type ".
				"FROM object LEFT JOIN stm_obj_typ USING (obj_typ_id) ".
				"WHERE obj_id IN (SELECT * FROM get_rule_dst(".
				$rule_id.", " . (is_null($filter->getClientId()) ? 'NULL' : $filter->getClientId()).
				", '". $filter->getReportTime() . "'))  ORDER BY obj_name";
				$rule_dst_table = $rule_src_table = $this->db_connection->fworch_db_query($sql_code);
				if ($this->error->isError($rule_dst_table)) {
					$this->error->raiseError($rule_dst_table->getMessage());
				}

				$dst_array = array ();
				$obj_anz = $rule_dst_table->rows;
				for ($zi = 0; $zi < $obj_anz; ++ $zi) {
					$dst_item = new RuleNwObject($rule_dst_table->data[$zi]);
					$dst_array[] = $dst_item;
				}
				return $dst_array;
	}

	function get_rule_svc($rule_id, $filter) {
		$report_id = $filter->getReportId();
		//		LAST_CHANGE $import_id_mgm_id_str = $this->import_ids->getImportIdMgmStringList();
		$sql_code =	"SELECT service.svc_id, service.svc_name, service.svc_uid, service.ip_proto_id, service.svc_prod_specific, " .
				"service.svc_port, stm_svc_typ.svc_typ_name as svc_type".
				" FROM rule_service" .
				" LEFT JOIN service USING (svc_id)" .
				" LEFT JOIN stm_svc_typ USING (svc_typ_id)".
				" INNER JOIN temp_mgmid_importid_at_report_time ON (service.mgm_id=temp_mgmid_importid_at_report_time.mgm_id)" .
				" WHERE rule_id=$rule_id AND temp_mgmid_importid_at_report_time.report_id=$report_id" .
				" AND temp_mgmid_importid_at_report_time.control_id<=service.svc_last_seen AND " .
				" temp_mgmid_importid_at_report_time.control_id>=service.svc_create ORDER BY service.svc_name";
		$rule_svc_table = $this->db_connection->fworch_db_query($sql_code);
		if ($this->error->isError($rule_svc_table)) {
			$this->error->raiseError($rule_svc_table->getMessage());
		}

		$svc_array = array ();
		$svc_anz = $rule_svc_table->rows;
		for ($zi = 0; $zi < $svc_anz; ++ $zi) {
			$svc_item = new RuleService($rule_svc_table->data[$zi]);
			$svc_array[] = $svc_item;
		}
		return $svc_array;
	}
}

class RuleList extends DbList {
	var $rule_list;
	var $rule_ids;
	var $db_connection;
	var $import_ids;

	function __construct($filter, $import_ids) {
		$this->error = new PEAR();
		$this->filter = $filter;
		$this->import_ids = $import_ids;
		if(is_null($this->filter->getDeviceId()) ||	is_null($this->filter->getReportTime())) {
				$this->error->raiseError("E-RL2: Filter criteria is null.");
		}
		$report_id = $this->filter->getReportId();
		$this->db_connection = $this->initConnection($this->filter->getSessionUser(), $this->filter->getSessionSecret());

 		$sqlcmd = "INSERT INTO temp_filtered_rule_ids SELECT $report_id AS report_id, " .
 		    "get_rule_ids_no_client_filter AS rule_id FROM get_rule_ids_no_client_filter(".
			(is_null($this->filter->getDeviceId()) ? 'NULL' : $this->filter->getDeviceId()). ",'".$this->filter->getReportTime()."', '" .
 			 $this->filter->getMgmFilter4ReportConfig() . "')";
		if (!$this->error->isError($this->db_connection)) {
			$this->db_connection->fworch_db_query($sqlcmd);
	 		$sqlcmd = "SELECT rule.rule_id FROM rule " . 
	 				" JOIN temp_filtered_rule_ids ON (temp_filtered_rule_ids.rule_id=rule.rule_id) " .
	 				" WHERE  temp_filtered_rule_ids.report_id=$report_id" .
	 				" GROUP BY rule.rule_id ";				
			$this->db_connection = $this->initConnection($this->filter->getSessionUser(), $this->filter->getSessionSecret());
			$db_rule_ids = $this->db_connection->fworch_db_query($sqlcmd);
			if (!$this->error->isError($db_rule_ids)) {
				$this->rows = $db_rule_ids->rows;
				$rule_cnt = 0;
				$rule_id_array = "'{";
				for ($zi = 0; $zi < $this->rows; ++ $zi) {
					if ($rule_cnt > 0) {
						$rule_id_array .= ",";
					}
					$rule_id_array .= $db_rule_ids->data[$zi]['rule_id'];
					$rule_cnt ++;
				}
				$rule_id_array .= "}'";
				$this->rule_ids = $rule_id_array;
			} else {
				$this->rule_ids = $db_rule_ids;
			}
		}
	}
	function deleteTempReport ($report_id) {
	 		$sqlcmd = "DELETE FROM temp_filtered_rule_ids WHERE report_id=$report_id";
			$this->db_connection->fworch_db_query($sqlcmd);		
	}
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
			$rule_table = $this->db_connection->fworch_db_query($sqlcmd);
			$rules = array ();
			$rule_anz = $rule_table->rows;
			$this->filter->setRelevantImportId($this->filter->getImportId());
			for ($zi = 0; $zi < $rule_anz; ++ $zi) {
				$rule = new Rule($rule_table->data[$zi], $this->filter, $this->db_connection, $this->import_ids);
				$rules[] = $rule;
			}
			$this->rule_list = $rules;
		}
		return $this->rule_list;
	}
}

class RuleFindList extends RuleList {
	var $rule_list;
	var $rule_ids;
	var $report_id;
	var $import_ids;

	function __construct($filter,$import_ids) {
		$this->error = new PEAR();
		$this->filter = $filter;
		$this->import_ids = $import_ids;
		if(is_null($this->filter->getDeviceId()) ||	is_null($filter->getReportTime())) {
				$this->error->raiseError("E-RL2: Filter criteria is null.");
		}
		$report_id = $filter->getReportId();
 		$sqlcmd = "INSERT INTO temp_filtered_rule_ids SELECT $report_id AS report_id, get_rule_ids_no_client_filter AS rule_id FROM get_rule_ids_no_client_filter(".
			(is_null($filter->getDeviceId()) ? 'NULL' : $filter->getDeviceId()).
			",'".$filter->getReportTime()."', ".
//			(is_null($filter->getClientId()) ? 'NULL' : $filter->getClientId()). ", " .
			(is_null($filter->src_ip) ? 'NULL' : "'" . $filter->src_ip . "'") . ", " .
			(is_null($filter->dst_ip) ? 'NULL' : "'" . $filter->dst_ip . "'") . 
			", ". 'NULL' . "," . 'NULL' . "," . 'NULL' . ", '" . $this->filter->getMgmFilter4ReportRulesearch() . "')";
		$this->db_connection = $this->initConnection($this->filter->getSessionUser(), $this->filter->getSessionSecret());
		if (!$this->error->isError($this->db_connection)) {
			$this->db_connection->fworch_db_query($sqlcmd);
	 		$sqlcmd = "SELECT rule.rule_id FROM rule " . 
	 				" JOIN temp_filtered_rule_ids ON (temp_filtered_rule_ids.rule_id=rule.rule_id) " .
	 				" WHERE  temp_filtered_rule_ids.report_id=$report_id" .
	 				" GROUP BY rule.rule_id ";				
			$db_rule_ids = $this->db_connection->fworch_db_query($sqlcmd);
			if (!$this->error->isError($db_rule_ids)) {
				$this->rows = $db_rule_ids->rows;
				$rule_cnt = 0;
				$rule_id_array = "'{";
				for ($zi = 0; $zi < $this->rows; ++ $zi) {
					if ($rule_cnt > 0) {
						$rule_id_array .= ",";
					}
					$rule_id_array .= $db_rule_ids->data[$zi]['rule_id'];
					$rule_cnt ++;
				}
				$rule_id_array .= "}'";
				$this->rule_ids = $rule_id_array;
			} else {
				$this->rule_ids = $db_rule_ids;
			}
		}
	}
}

class RuleNwObject {
	var $userId;
	var $userName;
	var $objectIp;
	var $objectId;
	var $objectName;
	var $zoneId;
//	var $zoneName;
	var $objectType;
	var $error;

	function __construct($rule_obj_data) {
		$this->error = new PEAR();
		$db_obj_keys = array_keys($rule_obj_data);
		$this->userId = $this->getValue($rule_obj_data, "user_id", $db_obj_keys);
		$this->userName = $this->getValue($rule_obj_data, "user_name", $db_obj_keys);
		$this->objectIp = $this->getValue($rule_obj_data, "obj_ip", $db_obj_keys);
		$this->objectId = $this->getValue($rule_obj_data, "obj_id", $db_obj_keys);
		$this->objectName = $this->getValue($rule_obj_data, "obj_name", $db_obj_keys);
		$this->zoneId = $this->getValue($rule_obj_data, "zone_id", $db_obj_keys);
//		$this->zoneName = $this->getValue($rule_obj_data, "zone_name", $db_obj_keys);
		$this->objectType = $this->getValue($rule_obj_data, "obj_type", $db_obj_keys);
	}
	function getValue($data, $key, $keys) {
		if (in_array($key, $keys, true)) {
			return $data[$key];
		} else {
			return NULL;
		}
	}
	function getUserId() {
		return $this->userId;
	}
	function getUserName() {
		return $this->userName;
	}
	function getObjectIp() {
		return $this->objectIp;
	}
	function getObjectId() {
		return $this->objectId;
	}
	function getObjectName() {
		return $this->objectName;
	}
	function getZoneId() {
		return $this->zoneId;
	}
	function getObjectType() {
		return $this->objectType;
	}
}

class RuleService {
	var $name;
	var $id;
	var $ip_proto_id;
	var $svc_prod_specific;
	var $svc_port;
	var $svc_type;
	var $error;

	function __construct($rule_svc_data) {
		$this->error = new PEAR();
		$db_svc_keys = array_keys($rule_svc_data);
		$this->id = $this->getValue($rule_svc_data, "svc_id", $db_svc_keys);
		$this->ip_proto_id = $this->getValue($rule_svc_data, "ip_proto_id", $db_svc_keys);
		$this->name = $this->getValue($rule_svc_data, "svc_name", $db_svc_keys);
		$this->svc_port = $this->getValue($rule_svc_data, "svc_port", $db_svc_keys);
		$this->svc_prod_specific = $this->getValue($rule_svc_data, "svc_prod_specific", $db_svc_keys);
		$this->svc_type = $this->getValue($rule_svc_data, "svc_type", $db_svc_keys);		
	}

	function getValue($data, $key, $keys) {
		if (in_array($key, $keys, true)) {
			return $data[$key];
		} else {
			return NULL;
		}
	}
	function getName() {
		return $this->name;
	}
	function getId() {
		return $this->id;
	}
	function getIpProtoId() {
		return $this->ip_proto_id;
	}
	function getProdSpecificName() {
		return $this->svc_prod_specific;
	}
	function getServiceId() {
		return $this->getId();
	}
	function getPort() {
		return $this->svc_port;
	}
	function getServiceType() {
		return $this->svc_type;
	}
}

//////////////////////////////////////////////////////////////////////////////////////
// from here on used for changes

class RuleSingle extends DbList {
	var $rule_id;
	var $rule_uid;
	var $rule_ruleid;
	var $rule_name;
	var $rule_typ;
	var $rule_action;
	var $rule_track;
	var $rule_comment;
	var $rule_src;
	var $rule_src_neg;
	var $rule_dst;
	var $rule_dst_neg;
	var $rule_svc;
	var $rule_svc_neg;
	var $rule_disabled;
	var $rule_from_zone_name;
	var $rule_to_zone_name;
	
	function __construct($rule_id,$filter) {
		$this->error = new PEAR();
		$sqlcmd =	"SELECT rule_head_text,rule_name,rule_uid,rule_ruleid,rule_src,rule_dst,rule_svc,"
					. "from_zone.zone_name AS rule_from_zone_name,to_zone.zone_name AS rule_to_zone_name,rule_comment,rule_action," .
					"rule_track,rule_disabled,rule_src_neg,rule_dst_neg,rule_svc_neg FROM rule " .
					" LEFT JOIN zone as from_zone ON rule.rule_from_zone=from_zone.zone_id" .
					" LEFT JOIN zone as to_zone ON rule.rule_to_zone=to_zone.zone_id" .
					" WHERE rule_id=$rule_id";
		$this->db_connection = $this->initConnection($filter->getSessionUser(), $filter->getSessionSecret());
		if (!$this->error->isError($this->db_connection)) {
			$data = $this->db_connection->fworch_db_query($sqlcmd);
			if (!$this->error->isError($data)) {
				$data = $data->data[0];
			} else {
				echo "error_case?!";
				$this->changeobject = $data;
			}
		} else {
			$error = $this->db_connection;
			$this->error->raiseError($error->getMessage());
		}	
		$this->filter = $filter;
		$objectKeys 	= array_keys($data);
		$this->rule_id = $this->getValue($data,"rule_id",$objectKeys);
		$this->rule_uid = $this->getValue($data,"rule_uid",$objectKeys);
		$this->rule_header = $this->getValue($data,"rule_head_text",$objectKeys);
		$this->rule_name = $this->getValue($data,"rule_name",$objectKeys);
		$this->rule_ruleid = $this->getValue($data,"rule_ruleid",$objectKeys);
		$this->rule_src = $this->getValue($data,"rule_src",$objectKeys);
		$this->rule_from_zone_name = $this->getValue($data,"rule_from_zone_name",$objectKeys);
		$this->rule_dst = $this->getValue($data,"rule_dst",$objectKeys);
		$this->rule_to_zone_name = $this->getValue($data,"rule_to_zone_name",$objectKeys);
		$this->rule_svc = $this->getValue($data,"rule_svc",$objectKeys);
		$this->rule_action = $this->getValue($data,"rule_action",$objectKeys);
		$this->rule_track = $this->getValue($data,"rule_track",$objectKeys);
		$this->rule_comment = $this->getValue($data,"rule_comment",$objectKeys);
		$this->rule_src_neg = !($this->getValue($data,"rule_src_neg",$objectKeys)==='f');
		$this->rule_dst_neg = !($this->getValue($data,"rule_dst_neg",$objectKeys)==='f');
		$this->rule_svc_neg = !($this->getValue($data,"rule_svc_neg",$objectKeys)==='f');
		$this->rule_disabled = !($this->getValue($data,"rule_disabled",$objectKeys)==='f');
		$this->display = true;
	}
	function isRuleDisabled()		{ if ($this->rule_disabled <> 't') return false; else return true; }
	function isRuleSourceNegated()		{ if ($this->rule_src_neg <> 't') return false; else return true; }
	function isRuleDestinationNegated()	{ if ($this->rule_dst_neg <> 't') return false; else return true; }
	function isRuleServiceNegated()		{ if ($this->rule_svc_neg <> 't') return false; else return true; }
	function getValue($data, $key, $keys) {
		if (in_array($key, $keys, true)) {
			return $data[$key];
		} else {
			return NULL;
		}
	}
}

class RuleCompare extends DbList {
	var $diffs;
	var $filter;
	
	function __construct($oldid, $newid, $filter) {
		$this->error = new PEAR();
		$log = new LogConnection();
		$this->filter = $filter;
		$sqlcmd = "SELECT rule.* FROM rule WHERE rule_id=$oldid OR rule_id=$newid order by rule_id";
		$this->db_connection = $this->initConnection($this->filter->getSessionUser(), $this->filter->getSessionSecret());
		if (!$this->error->isError($this->db_connection)) {
			$changes = $this->db_connection->fworch_db_query($sqlcmd);
			if ($this->error->isError($changes)) $log->log_error("ERROR: RuleCompare cannot exec $sqlcmd");
		} else {
			$error = $this->db_connection;
			$log->log_error("ERROR: RuleCompare cannot connect to database.");
			$this->error->raiseError($error->getMessage());
		}
		$diffs = array();
		$rowcount = $changes->rows;
		$feldanz = $changes->cols;
		for ($fi=0; $fi<$feldanz; $fi++) {
			$fieldname = $changes->info['name'][$fi];
			$val1 = $changes->data[0]["$fieldname"];
			$val2 = $changes->data[1]["$fieldname"];
			if ($fieldname <> "rule_id" and $fieldname <> "rule_num" and $fieldname<>"active" and 
				$fieldname<>"last_change_admin" and $fieldname<>'track_id' and $fieldname<>'action_id' and
				$fieldname<>'rule_src_refs' and $fieldname<>'rule_dst_refs' and $fieldname<>'rule_svc_refs' and 
				strpos($fieldname,"last_seen")===false and strpos($fieldname,"create")===false) {
				if ($val1 <> $val2) {
					if ($fieldname=='rule_src' or $fieldname=='rule_dst' or $fieldname=='rule_svc') {
						if ($fieldname=='rule_src') $feld = 'Quelle';
						if ($fieldname=='rule_dst') $feld = 'Ziel';
						if ($fieldname=='rule_svc') $feld = 'Dienst';
						$members_old = explode ('|', $val1);
						$members_new = explode ('|', $val2);
						$new_members 		= array_merge(array_diff($members_new, $members_old));
						$deleted_members 	= array_merge(array_diff($members_old, $members_new));
						foreach ($new_members as $newmember) {
							$diffs[] = "$feld neu: $newmember";
						}
						foreach ($deleted_members as $delmember) {
							$diffs[] = "$feld gel&ouml;scht: $delmember ";
						}
					} else $diffs[] = "Neu $fieldname $val2 (alt: $val1)";
				}
			}
			$this->diffs = $diffs;
		}
	}
}

?>