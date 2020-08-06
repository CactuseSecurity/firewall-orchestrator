<?php
/*
 * $Id: db-user.php,v 1.1.2.2 2007-12-13 10:47:31 tim Exp $
 * $Source: /home/cvs/iso/package/web/include/Attic/db-user.php,v $
 * Created on 27.10.2005
 *
 */
require_once("db-base.php");
require_once("display-filter.php");
require_once("operating-system.php");

class UserList extends DbList {
	var $user_list;
	
	function __construct($filter,$order,$import_ids) {
		$this->error = new PEAR();
		$this->filter = $filter;
		$report_id = $this->filter->getReportId();	
		$order_str = "usr.user_name";
		if (!is_null($order)) {	$order_str = "$order"; }
		$rule_user_join = '';
		if ($filter->showRuleObjectsOnly()) {
			$usr_filter = " rule_id = ANY (" . $filter->getFilteredRuleIds() . ") ";
			$rule_user_join = ' INNER JOIN rule_from ON (usr.user_id=rule_from.user_id)';  // only join for rules that contain users in source
		} else {
			if (!$filter->showRuleObjectsOnly() && !is_null($filter->getManagementId()))
				$usr_filter = "usr.mgm_id=" . $filter->getManagementId();
			else  // show all users of all managements at a given time
				$usr_filter	= 'TRUE';
		}		
		$sql_code = "SELECT usr.*,stm_usr_typ.usr_typ_name" .
				" FROM usr LEFT JOIN stm_usr_typ USING (usr_typ_id) " . 
				" WHERE usr.user_id IN (SELECT usr.user_id AS id FROM usr $rule_user_join" .
					" INNER JOIN temp_mgmid_importid_at_report_time ON (temp_mgmid_importid_at_report_time.mgm_id=usr.mgm_id" .
					" AND temp_mgmid_importid_at_report_time.control_id>=usr.user_create" .
					" AND temp_mgmid_importid_at_report_time.control_id<=usr.user_last_seen)" .
					" WHERE temp_mgmid_importid_at_report_time.report_id=$report_id AND $usr_filter" .
				" UNION" .
					" (SELECT usergrp_flat_member_id as id FROM usergrp_flat" .
					" INNER JOIN usr ON (usergrp_flat.usergrp_flat_id=usr.user_id) $rule_user_join" .
					" INNER JOIN temp_mgmid_importid_at_report_time ON (temp_mgmid_importid_at_report_time.mgm_id=usr.mgm_id" .
					" AND temp_mgmid_importid_at_report_time.control_id>=usergrp_flat.import_created" .
					" AND temp_mgmid_importid_at_report_time.control_id<=usergrp_flat.import_last_seen)" .
					" WHERE temp_mgmid_importid_at_report_time.report_id=$report_id AND $usr_filter ))" .
				" ORDER BY $order_str";
//		$log = new LogConnection(); $log->log_debug("UserList: sql = $sql_code");
		$this->user_list = $this->select_users($sql_code, $import_ids);
}
	function select_users($sql_code, $import_ids) {
		$this->db_connection = $this->initConnection($this->filter->getSessionUser(), $this->filter->getSessionSecret());
		if (!$this->error->isError($this->db_connection)) {
			$usr_table = $this->db_connection->fworch_db_query($sql_code);
			if (!$this->error->isError($usr_table)) {
				$this->rows = $usr_table->rows;
				$usr_list = array();
				for ($zi=0; $zi<$this->rows; ++$zi) {
					$usr_list[] = new User($usr_table->data[$zi],$this->filter,$this->db_connection, $import_ids);	
				}
				return $usr_list;
			} else {
				$err = $usr_table;
				PEAR::raiseError($err->getMessage());
			}
		} else {
			$err = $this->db_connection;
			PEAR::raiseError("E-UL1: Could not connect to database." .$err->getMessage());
		}
	}
	
	function getUsers() {
		return $this->user_list;
	}
}
class UserChangedList extends DbList {
	var $changes;
	
	function __construct($filter) {
		$this->error = new PEAR();
		$this->filter = $filter;
		$first_import_id = $this->filter->getFirstImport();
		$last_import_id = $this->filter->getLastImport();
		if (!is_null($first_import_id) && !is_null($last_import_id)) {
			$sqlcmd = "SELECT changelog_user.*,import_control.start_time AS change_time,request.request_number,client.client_name " .
				" FROM changelog_user ".
				" LEFT JOIN import_control ON changelog_user.control_id=import_control.control_id ".
				" LEFT JOIN request_user_change ON changelog_user.log_usr_id=request_user_change.log_usr_id ".
				" LEFT JOIN request on request_user_change.request_id=request.request_id " .
				" LEFT JOIN client on client.client_id=request.client_id " .
				" WHERE changelog_user.mgm_id = ".$this->filter->getManagementId().
				" AND changelog_user.change_type_id = 3 " .   // Ausblenden von Initialen Aenderungen
				" AND changelog_user.control_id >= ".$this->filter->getFirstImport().
				" AND changelog_user.control_id <= ".$this->filter->getLastImport()." AND successful_import ".
				(!is_null($this->filter->getClientId()) ? (" AND request.client_id = ".$this->filter->getClientId()." ") : "").
				" ORDER BY changelog_user.log_usr_id";
			$this->db_connection = $this->initConnection($this->filter->getSessionUser(), $this->filter->getSessionSecret());
			if (!$this->error->isError($this->db_connection)) {
				$changeuser_table = $this->db_connection->fworch_db_query($sqlcmd);
				if (!$this->error->isError($changeuser_table)) {
					$this->rows = $changeuser_table->rows;
					$this->changes = array ();
					for ($zi = 0; $zi < $this->rows; ++ $zi) {
						$this->changes[] = new ChangedUser($changeuser_table->data[$zi], $this->filter, $this->db_connection);
					}
				} else {
					$this->changes = $changeuser_table;
				}
			} else {
				$error = $this->db_connection;
				$this->error->raiseError($error->getMessage());
			}
		}
	}

	function getChanges() {
		if ($this->error->isError($this->changes)) {
			$this->error->raiseError("E-UCL1: Changes not loaded properly. ".$this->changes->getMessage());
		}
		return $this->changes;
	}
}

class ChangedUser extends DbItem {
	var $log_usr_id;
	var $abs_change_id;
	var $change_action;
	var $old_user;
	var $new_user;
	var $control_id;
	var $change_comment;
	var $change_time;
	var $client_request_str;
	var $filter;
	
	function __construct($changeuser_table_data, $filter, $conn) {
		$this->error = new PEAR();
		$this->filter = $filter;
		$this->db_connection = $conn;
		$db_change_keys = array_keys($changeuser_table_data);
		$this->log_usr_id = $this->getValue($changeuser_table_data, "log_usr_id", $db_change_keys);
		$this->change_action = $this->getValue($changeuser_table_data, "change_action", $db_change_keys);
		$this->abs_change_id = $this->getValue($changeuser_table_data, "abs_change_id", $db_change_keys);
		$this->change_comment = $this->getValue($changeuser_table_data, "changelog_user_comment", $db_change_keys);
		$this->change_time = $this->getValue($changeuser_table_data, "change_time", $db_change_keys);
        if (!is_null($this->getValue($changeuser_table_data, "client_name", $db_change_keys))) {
            $this->client_request_str = $this->getValue($changeuser_table_data, "client_name", $db_change_keys) .
                ": " . $this->getValue($changeuser_table_data, "request_number", $db_change_keys);
        } else {
            $this->client_request_str = "&nbsp;";
        }
		$this->control_id = $this->getValue($changeuser_table_data, "control_id", $db_change_keys);
		$this->old_user = $this->select_olduser($this->log_usr_id, $filter->getFirstImport());
		$this->new_user = $this->select_newuser($this->log_usr_id, $filter->getLastImport());
	}
	function getSelectStatement($type,$id) {
		$sql_code_std = "SELECT changelog_user,*,usr.*,stm_usr_typ.usr_typ_name " .
					"FROM changelog_user,usr LEFT JOIN stm_usr_typ ON usr.usr_typ_id=stm_usr_typ.usr_typ_id " .
					"WHERE ";
		$sql_code_old 		= "changelog_user.old_user_id = usr.user_id ";
		$sql_code_new		= "changelog_user.new_user_id = usr.user_id ";
		$sql_code_logusr1 	= "AND changelog_user.log_usr_id = ";
		$sql_code_logusr2 	= " ORDER BY changelog_user.log_usr_id";
		if($type == "old") {
			return  $sql_code_std.$sql_code_old.$sql_code_logusr1.$id.$sql_code_logusr2;
		} else {
			return $sql_code_std.$sql_code_new.$sql_code_logusr1.$id.$sql_code_logusr2;
		}
	}
	function select_olduser($log_user_id, $import_id) {
		if (is_null($log_user_id))
			$this->error->raiseError("E-CHU1: User Id is null.");
		$change_table_old = $this->db_connection->fworch_db_query($this->getSelectStatement("old",$log_user_id));
		if ($this->error->isError($change_table_old)) {
			$this->error->raiseError($change_table_old->getMessage());
		}
		$this->filter->setRelevantImportId($import_id);
		if ($change_table_old->rows == 1) {
			return new User($change_table_old->data[0], $this->filter, $this->db_connection);
		} else {
			PEAR::raiseError("E-CHU3: More than one result in old user.");
		}
	}
	function select_newuser($log_user_id, $import_id) {
		if (is_null($log_user_id))
			$this->error->raiseError("E-CHU2: User Id is null");
		$change_table_new = $this->db_connection->fworch_db_query($this->getSelectStatement("new",$log_user_id));
		if ($this->error->isError($change_table_new)) {
			$this->error->raiseError($change_table_new->getMessage());
		}
		$this->filter->setRelevantImportId($import_id);
		if ($change_table_new->rows == 1) {
			return new User($change_table_new->data[0], $this->filter, $this->db_connection);
		} else {
			PEAR::raiseError("E-CHU4: More than one result in new user.");
		}
	}
	function getLogUserId() { return $this->log_usr_id; }
	function getChangeAction() { return $this->change_action; }
	function getChangeComment() { return $this->change_comment; }
	function getChangeTime() { return $this->change_time; }
	function getClientRequestString() { return $this->client_request_str; }
	function getAbsChangeId() { return $this->abs_change_id; }
	function getControlId() { return $this->control_id; }
	function getOldUser() { return $this->old_user; }
	function getNewUser() { return $this->new_user; }
}

class User extends DbItem {
	// Variable declaration
	var $usr_id;
	var $usr_name;
	var $usr_typ;
	var $usr_typ_id;
	var $usr_comment;
	var $usr_uid;
	var $members;

	function __construct($user_table_data,$filter,$conn,$import_ids) {
		$this->error = new PEAR();
		$this->filter = $filter;
		$this->db_connection = $conn;
		$db_user_keys = array_keys($user_table_data);
		$this->usr_id = $this->getValue($user_table_data,"user_id",$db_user_keys);
		$this->usr_name = $this->getValue($user_table_data,"user_name",$db_user_keys);
		$this->usr_typ = $this->getValue($user_table_data,"usr_typ_name",$db_user_keys);
		$this->usr_typ_id = $this->getValue($user_table_data,"usr_typ_id",$db_user_keys);
		$this->usr_comment = $this->getValue($user_table_data,"user_comment",$db_user_keys);
		$this->usr_uid = $this->getValue($user_table_data,"user_uid",$db_user_keys);
		$this->members = $this->select_group_members($this->usr_id,$import_ids);
		$this->display = true;
	}
	function select_group_members($usr_id,$import_ids) {
		if (is_null($usr_id)) PEAR::raiseError("E-U1: User Id is null");
		$import_id = $import_ids->getRelevantImportIdForUser($usr_id);
		$sqlcmd = "SELECT usergrp_member_id,usr.user_name " .
				"FROM usergrp LEFT JOIN usr ON usergrp_member_id=usr.user_id " .
				"WHERE usergrp_id=$usr_id AND usr.user_create<=$import_id AND usr.user_last_seen>=$import_id";
		$group_members = $this->db_connection->fworch_db_query($sqlcmd);
		if ($this->error->isError($group_members)) PEAR::raiseError($group_members->getMessage());
		$members = array();
		for($zi=0; $zi<$group_members->rows; ++$zi) $members[] = new MemberUser($group_members->data[$zi]);
		return $members;
	}
	// getters
	function getId()		{ return $this->usr_id; }
	function getName()		{ return $this->usr_name; }
	function getType()		{ return $this->usr_typ; }
	function getTypeId()	{ return $this->usr_typ_id; }
	function getUid()		{ return $this->usr_uid; }
	function getComment()	{ return $this->usr_comment; }
	function getMembers()	{ return $this->members; }
}
 
class MemberUser extends User {
	var $usr_id;
	var $usr_name;
	
	function __construct($data) {
		$userKeys = array_keys($data);
		$this->usr_id = $this->getValue($data, "usergrp_member_id", $userKeys);
		$this->usr_name = $this->getValue($data, "user_name", $userKeys);
//		$log = new LogConnection(); $log->log_debug("MemberUser for user " . $this->usr_name . ", id: " . $this->usr_id);
	}	
}

class UserSingle extends DbList {
	var $user_id;
	var $user_uid;
	var $user_name;
	var $user_typ;
	var $user_firstname;
	var $user_lastname;
	var $user_comment;
	var $members;
	
	function __construct($usr_id,$filter) {
		$this->error = new PEAR();
		$sqlcmd =	"SELECT user_name,usr_typ_name,user_firstname,user_lastname,user_member_names,user_member_refs FROM usr " .
					"LEFT JOIN stm_usr_typ USING (usr_typ_id) " .
					"WHERE user_id=$usr_id";
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
		$this->user_id = $this->getValue($data,"user_id",$objectKeys);
		$this->user_uid = $this->getValue($data,"user_uid",$objectKeys);
		$this->user_name = $this->getValue($data,"user_name",$objectKeys);
		$this->user_typ = $this->getValue($data,"usr_typ_name",$objectKeys);
		$this->user_firstname = $this->getValue($data,"user_firstname",$objectKeys);
		$this->user_lastname = $this->getValue($data,"user_lastname",$objectKeys);
		$this->user_comment = $this->getValue($data,"user_comment",$objectKeys);
		$this->members = $this->getValue($data,"user_member_names",$objectKeys);
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

class UserCompare extends DbList {
	
	function __construct($oldid, $newid,$filter) {
		$this->error = new PEAR();
		$sqlcmd =	"SELECT * FROM usr WHERE user_id=$oldid OR user_id=$newid order by user_id";
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
		$diffs = array();
		$feldanz = $changes->cols;
		for ($fi=0; $fi<$feldanz; $fi++) {
			$fieldname = $changes->info['name'][$fi];
			$val1 = $changes->data[0]["$fieldname"];
			$val2 = $changes->data[1]["$fieldname"];
			if ($fieldname <> "user_id" and $fieldname<>"active" and  $fieldname<>"last_change_admin" and
				$fieldname <> "user_member_refs" and 
				strpos($fieldname,"last_seen")===false and strpos($fieldname,"create")===false) {
				if ($val1 <> $val2) {
//					echo "$fieldname: $val1 <> $val2<br>";
					if ($fieldname=='user_member_names') {
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
?>