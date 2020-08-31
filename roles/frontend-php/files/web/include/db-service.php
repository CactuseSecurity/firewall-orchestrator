<?php
/*
 * $Id: db-service.php,v 1.1.2.6 2012-03-14 23:35:15 tim Exp $
 * $Source: /home/cvs/iso/package/web/include/Attic/db-service.php,v $
 * Created on 27.10.2005
 *
 */
require_once("db-base.php");
require_once("db-import-ids.php");
require_once ("display-filter.php");

class ServiceList extends DbList{
	var $service_list;
	
	function __construct($filter,$order,$import_ids) {
		$this->error = new PEAR();
		$this->filter = $filter;
		$report_id = $this->filter->getReportId();
		$order_str	 = "service.svc_name"; if (!is_null($order)) $order_str .= " $order";
		$rule_service_join = '';
		if ($filter->showRuleObjectsOnly()) {
			$svc_filter = " rule_id = ANY (" . $filter->getFilteredRuleIds() . ") ";
			$rule_service_join = ' INNER JOIN rule_service ON (service.svc_id=rule_service.svc_id)';
		} else {
			if (!$filter->showRuleObjectsOnly() && !is_null($filter->getManagementId()))
				$svc_filter = " service.mgm_id=" . $filter->getManagementId();
			else  // show all services of all managements at a given time
				$svc_filter	= 'TRUE';
		}		
		$sql_code = "SELECT service.*,stm_svc_typ.svc_typ_name,stm_ip_proto.ip_proto_name" .
				" FROM service LEFT JOIN stm_ip_proto USING (ip_proto_id) LEFT JOIN stm_svc_typ USING (svc_typ_id)" .
				" WHERE service.svc_id IN (SELECT service.svc_id AS id FROM service $rule_service_join" .
					" INNER JOIN temp_mgmid_importid_at_report_time ON (temp_mgmid_importid_at_report_time.mgm_id=service.mgm_id" .
					" AND temp_mgmid_importid_at_report_time.control_id>=service.svc_create" .
					" AND temp_mgmid_importid_at_report_time.control_id<=service.svc_last_seen)" .
					" WHERE temp_mgmid_importid_at_report_time.report_id=$report_id AND $svc_filter" .
				" UNION" .
					" (SELECT svcgrp_flat_member_id as id FROM svcgrp_flat" .
					" INNER JOIN service ON (svcgrp_flat.svcgrp_flat_id=service.svc_id) $rule_service_join" .
					" INNER JOIN temp_mgmid_importid_at_report_time ON (temp_mgmid_importid_at_report_time.mgm_id=service.mgm_id" .
					" AND temp_mgmid_importid_at_report_time.control_id>=svcgrp_flat.import_created" .
					" AND temp_mgmid_importid_at_report_time.control_id<=svcgrp_flat.import_last_seen)" .
					" WHERE temp_mgmid_importid_at_report_time.report_id=$report_id AND $svc_filter ))" .
				" ORDER BY $order_str";
//		$log = new LogConnection(); $log->log_debug("ServiceList: sql = $sql_code");
		$this->service_list = $this->select_services($sql_code, $import_ids);
	}
	function select_services($sql_code, $import_ids) {
		$this->db_connection = $this->initConnection($this->filter->getSessionUser(), $this->filter->getSessionSecret());
		if (!$this->error->isError($this->db_connection)) {
			$svc_table = $this->db_connection->fworch_db_query($sql_code);
			if (!$this->error->isError($svc_table)) {
				$this->rows = $svc_table->rows;
				$svc_list = array();
				for ($zi=0; $zi<$this->rows; ++$zi) {
					$svc_list[] = new Service($svc_table->data[$zi],$this->filter,$this->db_connection, $import_ids);	
				}
				return $svc_list;
			} else {
				$err = $svc_table;
				PEAR::raiseError($err->getMessage());
			}
		} else {
			$err = $this->db_connection;
			PEAR::raiseError("E-SL1: Could not connect to database." .$err->getMessage());
		}
	}
	
	function getServices() {
		return $this->service_list;
	}
}

class ServiceChangedList extends DbList {
	var $changes;

	function __construct($filter) {
		$this->error = new PEAR();
		$this->filter = $filter;
		$first_import_id = $this->filter->getFirstImport();
		$last_import_id = $this->filter->getLastImport();
		if (!is_null($first_import_id) && !is_null($last_import_id)) {
			$sqlcmd = "SELECT changelog_service.*,import_control.start_time AS change_time,request.request_number,tenant.tenant_name " .
				" FROM changelog_service ".
				" LEFT JOIN import_control ON changelog_service.control_id=import_control.control_id ".
				" LEFT JOIN request_service_change ON changelog_service.log_svc_id=request_service_change.log_svc_id ".
				" LEFT JOIN request on request_service_change.request_id=request.request_id " .
				" LEFT JOIN tenant on tenant.tenant_id=request.tenant_id " .
				" WHERE changelog_service.mgm_id = ".$this->filter->getManagementId().
				" AND changelog_service.change_type_id = 3 " .   // Ausblenden von Initialen Aenderungen
				" AND changelog_service.control_id >= ".$this->filter->getFirstImport().
				" AND changelog_service.control_id <= ".$this->filter->getLastImport()." AND successful_import ".
				(!is_null($this->filter->gettenantId()) ? (" AND request.tenant_id = ".$this->filter->gettenantId()." ") : "").
				" ORDER BY changelog_service.log_svc_id";
			$this->db_connection = $this->initConnection($this->filter->getSessionUser(), $this->filter->getSessionSecret());
			if (!$this->error->isError($this->db_connection)) {
				$changeservice_table = $this->db_connection->fworch_db_query($sqlcmd);
				if (!$this->error->isError($changeservice_table)) {
					$this->rows = $changeservice_table->rows;
					$this->changes = array ();
					for ($zi = 0; $zi < $this->rows; ++ $zi) {
						$this->changes[] = new ChangedService($changeservice_table->data[$zi], $this->filter, $this->db_connection);
					}
				} else {
					$this->changes = $changeservice_table;
				}
			} else {
				$error = $this->db_connection;
				$this->error->raiseError($error->getMessage());
			}
		}
	}

	function getChanges() {
		if ($this->error->isError($this->changes)) {
			$this->error->raiseError("E-NWCL1: Changes not loaded properly. ".$this->changes->getMessage());
		}
		return $this->changes;
	}
}

class ChangedService extends DbItem {
	var $log_svc_id;
	var $abs_change_id;
	var $change_action;
	var $old_service;
	var $new_service;
	var $control_id;
	var $change_comment;
	var $change_time;
	var $tenant_request_str;
	var $filter;

	function __construct($changeservice_table_data, $filter, $conn) {
		$this->error = new PEAR();
		$this->filter = $filter;
		$this->db_connection = $conn;
		$db_change_keys = array_keys($changeservice_table_data);
		$this->log_svc_id = $this->getValue($changeservice_table_data, "log_svc_id", $db_change_keys);
		$this->change_action = $this->getValue($changeservice_table_data, "change_action", $db_change_keys);
		$this->abs_change_id = $this->getValue($changeservice_table_data, "abs_change_id", $db_change_keys);
		$this->change_comment = $this->getValue($changeservice_table_data, "changelog_svc_comment", $db_change_keys);
		$this->change_time = $this->getValue($changeservice_table_data, "change_time", $db_change_keys);
		if (!is_null($this->getValue($changeservice_table_data, "tenant_name", $db_change_keys))) {
			$this->tenant_request_str = $this->getValue($changeservice_table_data, "tenant_name", $db_change_keys) .
				": " . $this->getValue($changeservice_table_data, "request_number", $db_change_keys);
		} else {
			$this->tenant_request_str = "&nbsp;";
		}
		$this->control_id = $this->getValue($changeservice_table_data, "control_id", $db_change_keys);
		$this->old_service = $this->select_oldservice($this->log_svc_id, $filter->getFirstImport());
		$this->new_service = $this->select_newservice($this->log_svc_id, $filter->getLastImport());
		if (PEAR::isError($this->old_service)) {
			$err = $this->old_service;
			echo "FEHLER",$err->getMessage();
		}
		if (PEAR::isError($this->new_service)) {
			$err = $this->new_service;
			echo "FEHLER",$err->getMessage();
		}
	}
	function getSelectStatement($type,$id) {
		$sql_code_std = "SELECT service.*,stm_svc_typ.svc_typ_name,stm_ip_proto.ip_proto_name " .
				"FROM changelog_service,stm_ip_proto, service LEFT JOIN stm_svc_typ ON service.svc_typ_id=stm_svc_typ.svc_typ_id " .
				"WHERE stm_ip_proto.ip_proto_id = service.ip_proto_id ";
	
		$sql_code_old 		= "AND changelog_service.old_svc_id = service.svc_id ";
		$sql_code_new		= "AND changelog_service.new_svc_id = service.svc_id ";
		$sql_code_logsvc1 	= "AND changelog_service.log_svc_id = ";
		$sql_code_logsvc2 	= " ORDER BY changelog_service.log_svc_id";
		
		if($type == "old") {
			return  $sql_code_std.$sql_code_old.$sql_code_logsvc1.$id.$sql_code_logsvc2;
		} else {
			return $sql_code_std.$sql_code_new.$sql_code_logsvc1.$id.$sql_code_logsvc2;
		}
	}
	function select_oldservice($service_id, $import_id) {
		if (is_null($service_id))
			$this->error->raiseError("E-CHS1: service Id is null.");
		$change_table_old = $this->db_connection->fworch_db_query($this->getSelectStatement("old",$service_id));
		if ($this->error->isError($change_table_old)) {
			$this->error->raiseError($change_table_old->getMessage());
		}
		$this->filter->setRelevantImportId($import_id);
		if ($change_table_old->rows == 1) {
			return new Service($change_table_old->data[0], $this->filter, $this->db_connection);
		} else {
			PEAR::raiseError("E-CHS3: More than one result in old service.");
		}
	}
	function select_newservice($service_id, $import_id) {
		if (is_null($service_id))
			$this->error->raiseError("E-CHS2: Service Id is null");
		$change_table_new = $this->db_connection->fworch_db_query($this->getSelectStatement("new",$service_id));
		if ($this->error->isError($change_table_new)) {
			$this->error->raiseError($change_table_new->getMessage());
		}
		$this->filter->setRelevantImportId($import_id);
		if ($change_table_new->rows == 1) {
			return new Service($change_table_new->data[0], $this->filter, $this->db_connection);
		} else {
			PEAR::raiseError("E-CHS4: More than one result in new service.");
		}
	}

	function getLogServiceId() {
		return $this->log_svc_id;
	}
	function getChangeAction() {
		return $this->change_action;
	}
	function getChangeComment() {
		return $this->change_comment;
	}
	function getChangeTime() {
		return $this->change_time;
	}
	function gettenantRequestString() {
		return $this->tenant_request_str;
	}
	function getAbsChangeId() {
		return $this->abs_change_id;
	}
	function getControlId() {
		return $this->control_id;
	}
	function getOldService() {
		return $this->old_service;
	}
	function getNewService() {
		return $this->new_service;
	}
}

class Service extends DbItem {
	// Variable declaration
	var $svc_id;
	var $svc_name;
	var $svc_typ;
	var $svc_ip;
	var $svc_ip_id;
	var $svc_comment;
	var $svc_dstport;
	var $svc_dstport_end;
	var $svc_srcport;
	var $svc_srcport_end;
	var $svc_timeout;
	var $svc_uid;
	var $members;
	var $svc_rpc;
	
	var $display;
	// Constructor
	function __construct($service_table_data,$filter,$conn,$import_ids) {
		$this->error = new PEAR();
		$this->filter = $filter;
		$this->db_connection = $conn;
		$db_service_keys = array_keys($service_table_data);
		$this->svc_id = $this->getValue($service_table_data,"svc_id",$db_service_keys);
		$this->svc_name = $this->getValue($service_table_data,"svc_name",$db_service_keys);
		$this->svc_typ = $this->getValue($service_table_data,"svc_typ_name",$db_service_keys);
		$this->svc_ip = $this->getValue($service_table_data,"ip_proto_name",$db_service_keys);
		$this->svc_ip_id = $this->getValue($service_table_data,"ip_proto_id",$db_service_keys);
		$this->svc_comment = $this->getValue($service_table_data,"svc_comment",$db_service_keys);
		$this->svc_dstport = $this->getValue($service_table_data,"svc_port",$db_service_keys);
		$this->svc_dstport_end = $this->getValue($service_table_data,"svc_port_end",$db_service_keys);
		$this->svc_srcport = $this->getValue($service_table_data,"svc_source_port",$db_service_keys);
		$this->svc_srcport_end = $this->getValue($service_table_data,"svc_source_port_end",$db_service_keys);
		$this->svc_timeout = $this->getValue($service_table_data,"svc_timeout",$db_service_keys);
		$this->svc_uid = $this->getValue($service_table_data,"svc_uid",$db_service_keys);
		$this->svc_rpc = $this->getValue($service_table_data,"svc_rpcnr",$db_service_keys);
		$this->members = $this->select_group_members($this->svc_id, $import_ids);
	
		$this->display = true;		
	}
	function select_group_members($svc_id, $import_ids) {
		if (is_null($svc_id))
			$this->error->raiseError("E-S1: Service Id is null");
		$import_id = $import_ids->getRelevantImportIdForService($svc_id);
		$sqlcmd = "SELECT svcgrp_member_id,service.svc_id,service.svc_name " .
				"FROM svcgrp LEFT JOIN service ON svcgrp_member_id=service.svc_id " .
				"WHERE svcgrp_id=$svc_id AND service.svc_create<=$import_id AND service.svc_last_seen>=$import_id";
		$group_members = $this->db_connection->fworch_db_query($sqlcmd);
		if ($this->error->isError($group_members)) {
			$this->error->raiseError($group_members->getMessage());
		}
		$members = array();
		$group_anz = $group_members->rows;
		for($zi=0; $zi<$group_anz; ++$zi) {
			$members[] = new MemberService($group_members->data[$zi]);
		}
		return $members;
	}

	function getValue($data,$key,$keys) {
		if(in_array($key,$keys,true)) {
			return $data[$key];
		} else {
			return NULL;
		}
	}
	function isDisplay() {
		return $this->display;
	}
	
	function setDisplay($display) {
		$this->display = $display;
	}
	// getters
	function getId() {
		return $this->svc_id;
	}
	function getName() {
		return $this->svc_name;
	}
	function getType() {
		return $this->svc_typ;
	}
	function getComment() {
		return $this->svc_comment;
	}
	function getIp() {
		return $this->svc_ip;
	}
	function getUid() {
		return $this->svc_uid;
	}
	function getIpId() {
		return $this->svc_ip_id;
	}
	function getDestinationPort() {
		return $this->svc_dstport;
	}
	function getDestinationPortEnd() {
		return $this->svc_dstport_end;
	}
	function getSourcePort() {
		return $this->svc_srcport;
	}
	function getSourcePortEnd() {
		return $this->svc_srcport_end;
	}
	function getTimeout() {
		return $this->svc_timeout;
	}
	function getMembers() {
		return $this->members;
	}
	function getRpc() {
		return $this->svc_rpc;
	}
}
 
class MemberService extends Service {
	var $svcgrp_member_id;
	
	function MemberService($data) {
		$serviceKeys 	= array_keys($data);
		$this->svcgrp_member_id = $this->getValue($data,"svcgrp_member_id",$serviceKeys);
		$this->svc_id 		= $this->getValue($data,"svc_id",$serviceKeys);
		$this->svc_name 	= $this->getValue($data,"svc_name",$serviceKeys);
	}	
}


class ServiceSingle extends DbList {
	var $svc_id;
	var $svc_uid;
	var $svc_name;
	var $svc_typ;
	var $svc_dport;
	var $svc_proto;
	var $svc_comment;
	var $members;
	
	function __construct($svc_id,$filter) {
		$this->error = new PEAR();
		$sqlcmd =	"SELECT svc_name,svc_typ_name,svc_port,svc_member_names,ip_proto_name FROM service " .
					"LEFT JOIN stm_svc_typ USING (svc_typ_id) LEFT JOIN stm_ip_proto USING (ip_proto_id) " .
					"WHERE svc_id=$svc_id";
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
		$this->svc_id = $this->getValue($data,"svc_id",$objectKeys);
		$this->svc_uid = $this->getValue($data,"svc_uid",$objectKeys);
		$this->svc_name = $this->getValue($data,"svc_name",$objectKeys);
		$this->svc_typ = $this->getValue($data,"svc_typ_name",$objectKeys);
		$this->svc_dport = $this->getValue($data,"svc_port",$objectKeys);
		$this->svc_proto = $this->getValue($data,"ip_proto_name",$objectKeys);
		$this->svc_comment = $this->getValue($data,"svc_comment",$objectKeys);
		$this->members = $this->getValue($data,"svc_member_names",$objectKeys);
		$this->display = true;
	}
	function getValue($data, $key, $keys) {
		if (in_array($key, $keys, true)) {
			return $data[$key];
		} else {
			return NULL;
		}
	}
}

class ServiceCompare extends DbList {
	
	function __construct($svc_id1, $svc_id2,$filter) {
		$this->error = new PEAR();
		$sqlcmd =	"SELECT * FROM service WHERE svc_id=$svc_id1 OR svc_id=$svc_id2 order by svc_id";
		$this->db_connection = $this->initConnection($filter->getSessionUser(), $filter->getSessionSecret());
		if (!$this->error->isError($this->db_connection)) {
			$changes = $this->db_connection->fworch_db_query($sqlcmd);
			if ($this->error->isError($changes)) {
				echo "error_case?!";
				$this->changeobject = $data;
			}
		} else {
			$error = $this->db_connection;
			$this->error->raiseError($error->getMessage());
		}	
		$this->filter = $filter;
		$diffs = array();
		$feldanz = $changes->cols;
		for ($fi=0; $fi<$feldanz; $fi++) {
			$fieldname = $changes->info['name'][$fi];
			$val1 = $changes->data[0]["$fieldname"];
			$val2 = $changes->data[1]["$fieldname"];
			if ($fieldname <> "svc_id" and $fieldname<>"active" and  $fieldname<>"last_change_admin" and
				$fieldname <> "svc_member_refs" and
				strpos($fieldname,"last_seen")===false and strpos($fieldname,"create")===false) {
				if ($val1 <> $val2) {
					if ($fieldname=='svc_member_names') {
						$members_old = explode ('|', $val1);
						$members_new = explode ('|', $val2);
						$new_members 		= array_merge(array_diff($members_new, $members_old));
						$deleted_members 	= array_merge(array_diff($members_old, $members_new));
						foreach ($new_members as $newmember) {
							$diffs[] = "Neues Gruppenmitglied: $newmember";
						}
						foreach ($deleted_members as $delmember) {
							$diffs[] = "Gruppenmitglied $delmember gel&ouml;scht";
						}
					} else $diffs[] = "Neu $fieldname $val2 (alt: $val1)";
				}
			}
		}
		$this->diffs = $diffs;
	}
	function getValue($data, $key, $keys) {
		if (in_array($key, $keys, true)) {
			return $data[$key];
		} else {
			return NULL;
		}
	}
}


class ServiceGroupFlat extends DbList {  // used for filtering into groups (config report)
	var $svc_names;
	var $svc_protos;
	var $svc_ports;

	function __construct($svc_id_of_group, $filter, $import_ids) {			// $filter is just used as db connection ?!
		$this->error = new PEAR();
		$this->db_connection = $this->initConnection($filter->getSessionUser(), $filter->getSessionSecret());
		if (is_null($svc_id_of_group))
			$this->error->raiseError("E-O1: Service Id is null");
		$import_id = $filter->getRelevantImportId();
		if ($import_id=='undefined') {  // case of multiple managments (rulesearch)
//			$import_ids = new ImportIds($filter);
			$import_id = $import_ids->getRelevantImportIdForService($svc_id_of_group);
//			echo "debug20120314     fixed error       2 - ServiceGroupFlat - obj_id=$svc_id_of_group, import_id=$import_id<br>\n";
		} else {  
//			echo "debug20120314 single device rep ok  1 - ServiceGroupFlat - obj_id=$svc_id_of_group, import_id=$import_id<br>\n";
		}
		if (!isset($import_id) or $import_id=='') {
//			echo "debug20120314 error empty imp_id    3 - ServiceGroupFlat - obj_id=$svc_id_of_group, import_id=$import_id<br>\n";
		}
		if ($import_id=='undefined') {  // case of multiple managments (rulesearch)
			$import_ids = new ImportIds($filter);
			$import_id = $import_ids->getRelevantImportIdForService($svc_id_of_group);
		}
		$sqlcmd = "SELECT svc_name, ip_proto_id, svc_port, svc_port_end " .
				"FROM svcgrp_flat LEFT JOIN service ON svcgrp_flat_member_id=service.svc_id LEFT JOIN stm_svc_typ USING (svc_typ_id)" .
				"WHERE svcgrp_flat_id=$svc_id_of_group AND NOT svc_typ_name='group'" .
				" AND svcgrp_flat.import_created<=$import_id AND svcgrp_flat.import_last_seen>=$import_id";
				// beware of services with proto or port NULL values
		$group_members = $this->db_connection->fworch_db_query($sqlcmd);
		if ($this->error->isError($group_members)) $this->error->raiseError($group_members->getMessage());
		$this->svc_names = array();
		$this->svc_protos = array();
		$this->svc_ports = array();
		for ($zi=0; $zi<$group_members->rows; ++$zi) {
			$this->svc_names[] = $group_members->data[$zi]['svc_name'];
			$this->svc_protos[] = $group_members->data[$zi]['ip_proto_id'];
			$port_range = $group_members->data[$zi]['svc_port'];
			if (isset($group_members->data[$zi]['svc_port_end']) and !($group_members->data[$zi]['svc_port_end']=='') and
			 !($group_members->data[$zi]['svc_port']==$group_members->data[$zi]['svc_port_end'])) {
				$port_range .= "-" . $group_members->data[$zi]['svc_port_end'];
			} 
			$this->svc_ports[] = $port_range;
		}
	}
 
 	function getServiceNames() {
		return $this->svc_names;
	}
 	function getServiceProtoIds() {
		return $this->svc_protos;
	}
	function getPorts() {
		return $this->svc_ports;
	}
}
 
?>