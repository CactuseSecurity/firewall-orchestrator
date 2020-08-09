<?php

/*
 * $Id: db-nwobject.php,v 1.1.2.7 2012-11-29 10:48:00 tim Exp $
 * $Source: /home/cvs/iso/package/web/include/Attic/db-nwobject.php,v $
 * Created on 28.10.2005
 *
 */
require_once ("db-base.php");
require_once ("db-import-ids.php");
require_once ("display-filter.php");

class DuplicateNetworkObjectList extends NetworkObjectList {
	
	function __construct($filter, $order, $import_ids) {
		$this->error = new PEAR();
		$this->filter = $filter;
		$this->db_connection = $this->initConnection($this->filter->getSessionUser(), $this->filter->getSessionSecret());
		if (!$this->error->isError($this->db_connection)) {
			$report_id = $this->filter->getReportId();
			$order_str = " object.obj_name";
			$mgm_id_filter = $filter->getManagementId();
			$sql_code =
				"SELECT duplicate_network_objects.*,zone_name,stm_obj_typ.obj_typ_name " .
				"FROM duplicate_network_objects LEFT JOIN zone ON duplicate_network_objects.zone_id=zone.zone_id " .
				"WHERE mgm_id=$mgm_id_filter ORDER BY obj_ip,obj_name " .
				"ORDER BY $order_str";
//			$log = new LogConnection(); $log->log_debug("NetworkObjectList: sql = $sql_code");
//			echo ("DEBUG: NetworkObjectList: sql = $sql_code");
			$this->obj_list = $this->select_nwobjects($sql_code, $import_ids);
		} else {
			$err = $this->db_connection;
			$this->error->raiseError("E-NWL1: Could not connect to database." . $err->getMessage());
		}
	}
}

class NetworkObjectList extends DbList {
	var $obj_list;
	var $db_connection;

	function __construct($filter, $order, $import_ids) {
		$this->error = new PEAR();
		$this->filter = $filter;
		$this->db_connection = $this->initConnection($this->filter->getSessionUser(), $this->filter->getSessionSecret());
		if (!$this->error->isError($this->db_connection)) {
			$report_id = $this->filter->getReportId();
			$import_id = $filter->getRelevantImportId();
//			echo "relevant import id: $import_id<br>";
			$order_str = " object.obj_name";
			if (!is_null($order)) $order_str = $order;
			if ($filter->showRuleObjectsOnly()) {
				$rule_filter = "SELECT * FROM get_obj_ids_of_filtered_ruleset_flat(" .
				$filter->getFilteredRuleIds() . "," .
				 (is_null($filter->getClientId()) ? 'NULL' : $filter->getClientId()) . ", '" .
				$filter->getReportTime() . "') ";
			} else {
				if (!$filter->showRuleObjectsOnly() && !is_null($filter->getManagementId())) {
					$rule_filter = "SELECT * FROM get_obj_ids_of_filtered_management(" .
					$filter->getManagementId() . "," .
					$filter->getRelevantImportId() . "," .
					 (is_null($filter->getClientId()) ? 'NULL' : $filter->getClientId()) . ")";
				} else { // filter over all management systems displaying all nwobjects (not only those in rulebase)
					$grp_flat_rule_filter	= "";
					$object_rule_filter		= "INNER JOIN temp_mgmid_importid_at_report_time ON (temp_mgmid_importid_at_report_time.mgm_id=object.mgm_id " .
							" AND temp_mgmid_importid_at_report_time.control_id>=object.obj_create AND temp_mgmid_importid_at_report_time.control_id<=obj_last_seen) " .
							" WHERE temp_mgmid_importid_at_report_time.report_id=$report_id";
				}
			}
			if (isset($rule_filter)) { // for all other cases 
				$grp_flat_rule_filter	= "objgrp_flat_id IN ($rule_filter) AND";
				$object_rule_filter		= "WHERE object.obj_id IN ($rule_filter)";
			}
			$mgm_id_filter = $filter->getManagementId();
			
			// problem: search across more than one management: filter to only objects valid at report time
			if (is_null($filter->getManagementId())) $relevant_import_filter = '';   
			else $relevant_import_filter = " object.obj_create<=$import_id AND object.obj_last_seen>=$import_id AND";

			$sql_code =
				"SELECT object.*,zone_name,stm_obj_typ.obj_typ_name" .
				" FROM object LEFT JOIN zone ON object.zone_id=zone.zone_id" .
				" LEFT JOIN stm_obj_typ ON object.obj_typ_id=stm_obj_typ.obj_typ_id" .
				" WHERE $relevant_import_filter obj_id IN" .
					" (" .
//							"SELECT obj_id AS id FROM object $object_rule_filter" .
//						" UNION" .
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
			$this->obj_list = $this->select_nwobjects($sql_code, $import_ids);
		} else {
			$err = $this->db_connection;
			$this->error->raiseError("E-NWL1: Could not connect to database." . $err->getMessage());
		}
	}
	function select_nwobjects($sql_code, $import_ids) {
		$obj_table = $this->db_connection->fworch_db_query($sql_code);
		if (!$this->error->isError($obj_table)) {
			$this->rows = $obj_table->rows;
			$obj_list = array ();
			for ($zi = 0; $zi < $this->rows; ++ $zi) {
				$obj_list[] = new NetworkObject($obj_table->data[$zi], $this->filter, $this->db_connection, $import_ids);
			}
			$obj_list_query = " AND obj_id IN (";
			for ($zi = 0; $zi < $this->rows; ++ $zi) {
				$obj_list_query .= $obj_list[$zi]->obj_id;
				if ($zi<$this->rows-1) $obj_list_query .= ",";
			}
			$obj_list_query .= ") ";
//			$log = new LogConnection(); $log->log_debug("NetworkObjectList::select_nwobjects: obj_list_query = $obj_list_query");
			for ($zi = 0; $zi < $this->rows; ++ $zi) {
//				if ($obj_list[$zi]->getName() == 'lan_grp_clients_ber_fs83') {
//					echo "DEBUG: members of lan_grp_clients_ber_fs83: " . implode($obj_list[$zi]->getMembers()) . '<br>';
//					echo "DEBUG: uid of lan_grp_clients_ber_fs83: " . $obj_list[$zi]->getUid() . '<br>';
//					echo "DEBUG: obj_id of lan_grp_clients_ber_fs83: " . $obj_list[$zi]->getId() . '<br>';
//				}
				if ($obj_list[$zi]->getType() == 'group')
					$obj_list[$zi]->members = $obj_list[$zi]->resolve_group($this->filter, $import_ids, $obj_list_query);
			}
			return $obj_list;
		}
	}
	function getRows() {
		return $this->rows;
	}
	function getNetworkObjects() {
		return $this->obj_list;
	}
}
class NetworkObjectChangedList extends DbList {
	var $changes;

	function __construct($filter) {
		$this->error = new PEAR();
		$this->filter = $filter;
		$first_import_id = $this->filter->getFirstImport();
		$last_import_id = $this->filter->getLastImport();
		if (!is_null($first_import_id) && !is_null($last_import_id)) {
			$sqlcmd = "SELECT changelog_object.*,import_control.start_time AS change_time,request.request_number,client.client_name " .
			" FROM changelog_object " .
			" LEFT JOIN import_control ON changelog_object.control_id=import_control.control_id " .
			" LEFT JOIN request_object_change ON changelog_object.log_obj_id=request_object_change.log_obj_id " .
			" LEFT JOIN request on request_object_change.request_id=request.request_id " .
			" LEFT JOIN client on client.client_id=request.client_id " .
			" WHERE changelog_object.mgm_id = " . $this->filter->getManagementId() .
			" AND changelog_object.change_type_id = 3 " . // Ausblenden von Initialen Aenderungen
			" AND changelog_object.control_id >= " . $this->filter->getFirstImport() .
			" AND changelog_object.control_id <= " . $this->filter->getLastImport() . "  AND successful_import " .
			 (!is_null($this->filter->getClientId()) ? (" AND request.client_id = " . $this->filter->getClientId() . " ") : "") .
			" ORDER BY changelog_object.log_obj_id";
			$this->db_connection = $this->initConnection($this->filter->getSessionUser(), $this->filter->getSessionSecret());
			if (!$this->error->isError($this->db_connection)) {
				$changeobject_table = $this->db_connection->fworch_db_query($sqlcmd);
				if (!$this->error->isError($changeobject_table)) {
					$this->rows = $changeobject_table->rows;
					$this->changes = array ();
					for ($zi = 0; $zi < $this->rows; ++ $zi) {
						$this->changes[] = new ChangedNetworkObject($changeobject_table->data[$zi], $this->filter, $this->db_connection);
					}
				} else {
					$this->changes = $changeobject_table;
				}
			} else {
				$error = $this->db_connection;
				$this->error->raiseError($error->getMessage());
			}
		}
	}
	function getChanges() {
		if ($this->error->isError($this->changes)) {
			$this->error->raiseError("E-RCL1: Changes not loaded properly. " . $this->changes->getMessage());
		}
		return $this->changes;
	}
}

class ChangedNetworkObject extends DbItem {
	var $log_obj_id;
	var $abs_change_id;
	var $change_action;
	var $old_object;
	var $new_object;
	var $control_id;
	var $change_comment;
	var $change_time;
	var $client_request_str;
	var $filter;

	function __construct($changeobject_table_data, $filter, $conn) {
		$this->error = new PEAR();
		$this->filter = $filter;
		$this->db_connection = $conn;
		$db_change_keys = array_keys($changeobject_table_data);
		$this->log_obj_id = $this->getValue($changeobject_table_data, "log_obj_id", $db_change_keys);
		$this->change_action = $this->getValue($changeobject_table_data, "change_action", $db_change_keys);
		$this->abs_change_id = $this->getValue($changeobject_table_data, "abs_change_id", $db_change_keys);
		$this->change_comment = $this->getValue($changeobject_table_data, "changelog_obj_comment", $db_change_keys);
		$this->change_time = $this->getValue($changeobject_table_data, "change_time", $db_change_keys);
		if (!is_null($this->getValue($changeobject_table_data, "client_name", $db_change_keys))) {
			$this->client_request_str = $this->getValue($changeobject_table_data, "client_name", $db_change_keys) .
			": " . $this->getValue($changeobject_table_data, "request_number", $db_change_keys);
		} else {
			$this->client_request_str = "&nbsp;";
		}
		$this->control_id = $this->getValue($changeobject_table_data, "control_id", $db_change_keys);
		$this->old_object = $this->select_oldobject($this->log_obj_id, $filter->getFirstImport());
		$this->new_object = $this->select_newobject($this->log_obj_id, $filter->getLastImport());
	}

	function getSelectStatement($type, $id) {
		$sql_code_std = "SELECT object.*,zone_name,stm_obj_typ.obj_typ_name " .
		"FROM changelog_object, object LEFT JOIN zone ON object.zone_id=zone.zone_id, stm_obj_typ " .
		"WHERE object.obj_typ_id=stm_obj_typ.obj_typ_id ";
		$sql_code_old = "AND changelog_object.old_obj_id = object.obj_id ";
		$sql_code_new = "AND changelog_object.new_obj_id = object.obj_id ";
		$sql_code_logobj1 = "AND changelog_object.log_obj_id = ";
		$sql_code_logobj2 = " ORDER BY changelog_object.log_obj_id";

		if ($type == "old") {
			return $sql_code_std . $sql_code_old . $sql_code_logobj1 . $id . $sql_code_logobj2;
		} else {
			return $sql_code_std . $sql_code_new . $sql_code_logobj1 . $id . $sql_code_logobj2;
		}
	}
	function select_oldobject($object_id, $import_id) {
		if (is_null($object_id))
			$this->error->raiseError("E-CHNW1: Object Id is null.");
		$change_table_old = $this->db_connection->fworch_db_query($this->getSelectStatement("old", $object_id));
		if ($this->error->isError($change_table_old)) {
			$this->error->raiseError($change_table_old->getMessage());
		}
		$this->filter->setRelevantImportId($import_id);
		if ($change_table_old->rows == 1) {
			return new NetworkObject($change_table_old->data[0], $this->filter, $this->db_connection);
		} else {
			$this->error->raiseError("E-CHNW3: More than one result in old object.");
		}
	}
	function select_newobject($object_id, $import_id) {
		if (is_null($object_id))
			$this->error->raiseError("E-CHNW2: Object Id is null");
		$change_table_new = $this->db_connection->fworch_db_query($this->getSelectStatement("new", $object_id));
		if ($this->error->isError($change_table_new)) {
			$this->error->raiseError($change_table_new->getMessage());
		}
		$this->filter->setRelevantImportId($import_id);
		if ($change_table_new->rows == 1) {
			return new NetworkObject($change_table_new->data[0], $this->filter, $this->db_connection);
		} else {
			$this->error->raiseError("E-CHNW4: More than one result in new object.");
		}
	}

	function getLogObjectId() {
		return $this->log_obj_id;
	}
	function getChangeAction() {
		return $this->change_action;
	}
	function getChangeTime() {
		return $this->change_time;
	}
	function getChangeComment() {
		return $this->change_comment;
	}
	function getClientRequestString() {
		return $this->client_request_str;
	}
	function getAbsChangeId() {
		return $this->abs_change_id;
	}
	function getControlId() {
		return $this->control_id;
	}
	function getOldObject() {
		return $this->old_object;
	}
	function getNewObject() {
		return $this->new_object;
	}
}

class NetworkObjectSingle extends DbList {
	var $obj_id;
	var $obj_uid;
	var $obj_name;
	var $obj_typ;
	var $obj_ip;
	var $obj_ip_end;
	var $obj_zone;
	var $obj_comment;
	var $members;

	function __construct($obj_id, $filter) {
		$this->error = new PEAR();
		$sqlcmd = "SELECT obj_name,obj_typ_name,obj_ip,obj_member_names,zone_name FROM object " .
		"LEFT JOIN stm_obj_typ USING (obj_typ_id) LEFT JOIN zone USING (zone_id)" .
		"WHERE obj_id=$obj_id";
		$this->db_connection = $this->initConnection($filter->getSessionUser(), $filter->getSessionSecret());
		//		$this->db_connection = $this->initSessionConnection();
		if (!$this->error->isError($this->db_connection)) {
			$data = $this->db_connection->fworch_db_query($sqlcmd);
			//			echo "data: $data<br>";
			if (!$this->error->isError($data)) {
				//				echo "rows: $data->rows<br>";
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
		//		$this->db_connection = $conn;
		$objectKeys = array_keys($data);
		$this->obj_id = $this->getValue($data, "obj_id", $objectKeys);
		$this->obj_uid = $this->getValue($data, "obj_uid", $objectKeys);
		$this->obj_name = $this->getValue($data, "obj_name", $objectKeys);
		$this->obj_typ = $this->getValue($data, "obj_typ_name", $objectKeys);
		$this->obj_ip = $this->getValue($data, "obj_ip", $objectKeys);
		$this->obj_ip_end = $this->getValue($data, "obj_ip_end", $objectKeys);
		$this->obj_zone_id = $this->getValue($data, "zone_id", $objectKeys);
		$this->obj_zone = $this->getValue($data, "zone_name", $objectKeys);
		$this->obj_comment = $this->getValue($data, "obj_comment", $objectKeys);
		$this->members = $this->getValue($data, "obj_member_names", $objectKeys);
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

class NetworkObjectCompare extends DbList {
	
	function __construct($obj_id1, $obj_id2, $filter) {
		$this->error = new PEAR();
		$sqlcmd = "SELECT * FROM object WHERE obj_id=$obj_id1 OR obj_id=$obj_id2 order by obj_id";
		$this->db_connection = $this->initConnection($filter->getSessionUser(), $filter->getSessionSecret());
		if (!$this->error->isError($this->db_connection)) {
			$changes = $this->db_connection->fworch_db_query($sqlcmd);
			if ($this->error->isError($changes)) {
				echo "error_case?!";
				$this->changeobject = $changes;
			}
		} else {
			$error = $this->db_connection;
			$this->error->raiseError($error->getMessage());
		}
		$this->filter = $filter;
		$diffs = array ();
		$feldanz = $changes->cols;
		for ($fi = 0; $fi < $feldanz; $fi++) {
			$fieldname = $changes->info['name'][$fi];
			$val1 = $changes->data[0]["$fieldname"];
			$val2 = $changes->data[1]["$fieldname"];
			if ($fieldname <> "obj_id" and $fieldname <> "active" and $fieldname <> "last_change_admin" and $fieldname <> "obj_member_refs" and strpos($fieldname, "last_seen") === false and strpos($fieldname, "create") === false) {
				if ($val1 <> $val2) {
					//					echo "$fieldname: $val1 <> $val2<br>";
					if ($fieldname == 'obj_member_names') {
						$members_old = explode('|', $val1);
						$members_new = explode('|', $val2);
						$new_members = array_merge(array_diff($members_new, $members_old));
						$deleted_members = array_merge(array_diff($members_old, $members_new));
						foreach ($new_members as $newmember) {
							$diffs[] = "Neues Gruppenmitglied: $newmember";
						}
						foreach ($deleted_members as $delmember) {
							$diffs[] = "Gruppenmitglied $delmember gel&ouml;scht";
						}
					} else
						$diffs[] = "Neu $fieldname $val2 (alt: $val1)";
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

class NetworkObject extends DbItem {
	var $obj_id;
	var $obj_uid;
	var $obj_name;
	var $obj_typ;
	var $obj_ip;
	var $obj_ip_end;
	var $obj_zone;
	var $obj_comment;
	var $members;

	function __construct($data, $filter, $conn, $import_ids) {
		$this->error = new PEAR();
		$this->filter = $filter;
		$this->db_connection = $conn;
		$objectKeys = array_keys($data);
		$this->obj_id = $this->getValue($data, "obj_id", $objectKeys);
		$this->obj_uid = $this->getValue($data, "obj_uid", $objectKeys);
		$this->obj_name = $this->getValue($data, "obj_name", $objectKeys);
		$this->obj_typ = $this->getValue($data, "obj_typ_name", $objectKeys);
		$this->obj_ip = $this->getValue($data, "obj_ip", $objectKeys);
		$this->obj_ip_end = $this->getValue($data, "obj_ip_end", $objectKeys);
		$this->obj_zone_id = $this->getValue($data, "zone_id", $objectKeys);
		$this->obj_zone = $this->getValue($data, "zone_name", $objectKeys);
		$this->obj_comment = $this->getValue($data, "obj_comment", $objectKeys);
//		$this->members = $this->select_group_members($this->obj_id, $this->filter, $import_ids);

		$this->display = true;
	}
	function resolve_group($filter, $import_ids, $obj_list_query) {
		$obj_id = $this->obj_id;
		if (is_null($obj_id)) $this->error->raiseError("E-O1: Object Id is null");
		$import_id = $import_ids->getRelevantImportIdForObject($obj_id);
		$sqlcmd = "SELECT objgrp_member_id AS obj_id,object.obj_name " .
		"FROM objgrp LEFT JOIN object ON objgrp_member_id=object.obj_id " .
		"WHERE objgrp_id=$obj_id AND object.obj_create<=$import_id AND object.obj_last_seen>=$import_id " .
		"$obj_list_query ORDER BY obj_name";
		$log = new LogConnection(); $log->log_debug("NetworkObject::resolve_group: sql = $sqlcmd");	
		$group_members = $this->db_connection->fworch_db_query($sqlcmd);
		if ($this->error->isError($group_members)) {
			$this->error->raiseError($group_members->getMessage());
		}
		$members = array ();
		$group_anz = $group_members->rows;
		for ($zi = 0; $zi < $group_anz; ++ $zi) {
			$members[] = new MemberNetworkObject($group_members->data[$zi]);
		}
		return $members;
	}
	function select_group_members($obj_id, $filter, $import_ids) {
		if (is_null($obj_id))
			$this->error->raiseError("E-O1: Object Id is null");
		//		$import_id = $this->filter->getRelevantImportId();
		$import_id = $import_ids->getRelevantImportIdForObject($obj_id);
		$sqlcmd = "SELECT objgrp_member_id AS obj_id,object.obj_name " .
		"FROM objgrp LEFT JOIN object ON objgrp_member_id=object.obj_id " .
		"WHERE objgrp_id=$obj_id AND object.obj_create<=$import_id AND object.obj_last_seen>=$import_id " .
		" ORDER BY obj_name";
		$group_members = $this->db_connection->fworch_db_query($sqlcmd);
		if ($this->error->isError($group_members)) {
			$this->error->raiseError($group_members->getMessage());
		}
		$members = array ();
		$group_anz = $group_members->rows;
		for ($zi = 0; $zi < $group_anz; ++ $zi) {
			$members[] = new MemberNetworkObject($group_members->data[$zi]);
		}
		return $members;
	}
	// getters	
	function getId() {
		return $this->obj_id;
	}
	function getName() {
		return $this->obj_name;
	}
	function getUid() {
		return $this->obj_uid;
	}
	function getType() {
		return $this->obj_typ;
	}
	function getIp() {
		return $this->obj_ip;
	}
	function getIpEnd() {
		return $this->obj_ip_end;
	}
	function getZone() {
		return $this->obj_zone;
	}
	function getZoneId() {
		return $this->obj_zone_id;
	}
	function getComment() {
		return $this->obj_comment;
	}
	function getMembers() {
		return $this->members;
	}
}
class MemberNetworkObject extends NetworkObject {
	var $obj_id;
	var $obj_name;

	function __construct($data) {
		$objectKeys = array_keys($data);
		$this->obj_id = $this->getValue($data, "obj_id", $objectKeys);
		$this->obj_name = $this->getValue($data, "obj_name", $objectKeys);
	}
}

class NWObjectGroupFlat extends DbList { // used for filtering into groups (config report)
	var $obj_names;
	var $obj_ips;
	var $rows;

	function __construct($obj_id_of_group, $filter) { // $filter is just used as db connection ?!
		$this->error = new PEAR();
		$this->db_connection = $this->initConnection($filter->getSessionUser(), $filter->getSessionSecret());
		if (is_null($obj_id_of_group))
			$this->error->raiseError("E-O1: Object Id is null");
		$import_id = $filter->getRelevantImportId();
		if ($import_id=='undefined') {  // case of multiple managments (rulesearch)
			$import_ids = new ImportIds($filter);
			$import_id = $import_ids->getRelevantImportIdForObject($obj_id_of_group);
//			echo "debug20120314     fixed error       2 - NWObjectGroupFlat - obj_id=$obj_id_of_group, import_id=$import_id<br>\n";
//			echo "debug20120314 show import_ids    4 - NWObjectGroupFlat"; print_r($import_ids); reset($import_ids); echo "<br>\n";
		} else {
//			echo "debug20120314 single device rep ok  1 - NWObjectGroupFlat - obj_id=$obj_id_of_group, import_id=$import_id<br>\n";
		}
		if (!isset($import_id) or $import_id=='') {
//			echo "debug20120314 error empty imp_id    3 - NWObjectGroupFlat - obj_id=$obj_id_of_group, import_id=$import_id<br>\n";
		}
		$sqlcmd = "SELECT object.obj_name,object.obj_ip " .
		"FROM objgrp_flat LEFT JOIN object ON objgrp_flat_member_id=object.obj_id " .
		"WHERE objgrp_flat_id=$obj_id_of_group" .
		" AND objgrp_flat.import_created<=$import_id AND objgrp_flat.import_last_seen>=$import_id";
		$group_members = $this->db_connection->fworch_db_query($sqlcmd);
		if ($this->error->isError($group_members))
			$this->error->raiseError($group_members->getMessage());
		$this->obj_ips = array ();
		$this->obj_names = array ();
		for ($zi = 0; $zi < $group_members->rows; ++ $zi) {
			$this->obj_ips[] = $group_members->data[$zi]['obj_ip'];
			$this->obj_names[] = $group_members->data[$zi]['obj_name'];
		}
	}
	function getObjectIps() {
		return $this->obj_ips;
	}
	function getObjectNames() {
		return $this->obj_names;
	}
}
class NWObjectGroupFlatNoGroups extends DbList { // used for filtering into groups (config report junos)
	var $obj_names;
	var $obj_ips;
	var $obj_ids;
	var $rows;

	function __construct($obj_id_of_group, $filter, $import_ids) { // $filter is just used as db connection ?!
		$this->error = new PEAR();
		$this->db_connection = $this->initConnection($filter->getSessionUser(), $filter->getSessionSecret());
		if (is_null($obj_id_of_group))
			$this->error->raiseError("E-O1: Object Id is null");
		$import_id = $filter->getRelevantImportId();
		if ($import_id=='undefined') {  // case of multiple managments (rulesearch)
			if (isset($import_ids)) $import_id = $import_ids->getRelevantImportIdForObject($obj_id_of_group);
//			echo "debug20120314     fixed error       2 - NWObjectGroupFlatNoGroups - obj_id=$obj_id_of_group, import_id=$import_id<br>\n";
//			echo "debug20120314 show import_ids    4 - NWObjectGroupFlatNoGroups"; print_r($import_ids); reset($import_ids); echo "<br>\n";
		} else {
//			echo "debug20120314 single device rep ok  1 - NWObjectGroupFlatNoGroups - obj_id=$obj_id_of_group, import_id=$import_id<br>\n";
		}
		if (!isset($import_id) or $import_id=='') {
//			echo "debug20120314 error empty imp_id    3 - NWObjectGroupFlatNoGroups - obj_id=$obj_id_of_group, import_id=$import_id<br>\n";
		}
		$sqlcmd = "SELECT object.obj_name,object.obj_ip, object.obj_id " .
		"FROM objgrp_flat LEFT JOIN object ON objgrp_flat_member_id=object.obj_id LEFT JOIN stm_obj_typ USING (obj_typ_id) " .
		"WHERE objgrp_flat_id=$obj_id_of_group AND NOT obj_typ_name='group' AND " .
		"objgrp_flat.import_created<=$import_id AND objgrp_flat.import_last_seen>=$import_id";
		$group_members = $this->db_connection->fworch_db_query($sqlcmd);
		if ($this->error->isError($group_members))
			$this->error->raiseError($group_members->getMessage());
		$this->obj_ips = array ();
		$this->obj_ids = array ();
		$this->obj_names = array ();
		for ($zi = 0; $zi < $group_members->rows; ++ $zi) {
			$this->obj_ips[] = $group_members->data[$zi]['obj_ip'];
			$this->obj_ids[] = $group_members->data[$zi]['obj_id'];
			$this->obj_names[] = $group_members->data[$zi]['obj_name'];
		}
	}
	function getObjectIps() {
		return $this->obj_ips;
	}
	function getObjectIds() {
		return $this->obj_ids;
	}
	function getObjectNames() {
		return $this->obj_names;
	}
}
?>
