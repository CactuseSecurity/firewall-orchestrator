<?php
// $Id: db-base.php,v 1.1.2.7 2011-05-20 12:43:15 tim Exp $
// $Source: /home/cvs/iso/package/web/include/Attic/db-base.php,v $
/*
 * Created on 01.11.2005
 *
 */
require_once ("PEAR.php");
require_once ("db-gui-config.php");
require_once ("operating-system.php");
class DbConnection {
	var $dbuser;
	var $dbpw;
	var $dbhost;
	var $dbname;
	var $dbport;
	var $dbtype;
	var $error;

	function __construct($DBConnection) {
		$this->dbuser = $DBConnection->dbuser;
		$this->dbname = $DBConnection->dbname;
		$this->dbhost = $DBConnection->dbhost;
		$this->dbpw   = $DBConnection->dbpw;
		$this->dbport = $DBConnection->dbport;
		$this->dbtype = $DBConnection->dbtype;
		$this->error = new PEAR();
	}
	function iso_db_query_with_flags($sql_code, $flags) {
		$log = new LogConnection();
		$start = explode(" ", microtime());
		$stime = $start[0] + $start[1];

		$db = pg_pconnect("host=" . $this->dbhost . " port=" . $this->dbport . " dbname=" . $this->dbname . " user=" . $this->dbuser . " password='" . $this->dbpw . "'");
		if (!$db) {
			$log->log_error("E-DB-SQL: error while connecting to database with user " . $this->dbuser . "; sql=" . $sql_code);
			return null;
		} else {
			$sqlerg = pg_query($db, $sql_code);
			if (!$sqlerg) {
				if ($db)	$log_txt = "E-DB-SQL: " . pg_last_error($db) . " while executing $sql_code";
				else		$log_txt = "E-DB-SQL: unknown error while executing $sql_code";
				$log->log_error($log_txt);
				pg_close($db);
				return $sqlerg;
			} else {
				pg_close($db);
				$endtime = explode(" ", microtime());
				$etime = $endtime[0] + $endtime[1];
				$log->log_debug("sql_query took " . sprintf('%.2f', $etime - $stime) . " seconds: $sql_code");
		
				// result in data[]-Form bringen:
				$result = new stdClass;
				$result->data = array ();
				$result->info['name'] = array ();
				$result->rows = 0;
				while ($row = pg_fetch_assoc($sqlerg)) {
					$result->rows++;
					$result->data[] = $row;
				}
				if (isset($result->data[0]) and $result->data[0]) {
					$result->cols = count($result->data[0]);
					$result->info['name'] = array_slice(array_keys($result->data[0]), 0, $result->cols);
				}
				return $result;
			}
		}
	}
	function iso_db_query_no_assoc($sql_code) {
		return $this->iso_db_query_with_flags($sql_code, 'DBX_RESULT_INDEX');
	}
	function iso_db_query($sql_code) {
		return $this->iso_db_query_with_flags($sql_code, 'DBX_RESULT_INDEX' | 'DBX_RESULT_INFO' | 'DBX_RESULT_ASSOC');
	}

	function getUser() {
		return $this->dbuser;
	}
	function getPw() {
		return $this->dbpw;
	}
	function isoadmin_check_pwd_history($user, $password) {
		$log = new LogConnection();
		$delimiter = '%';
		$sql_request = "SELECT isoadmin_pwd_history FROM isoadmin WHERE isoadmin_username = '$user'; ";
		$result = $this->iso_db_query($sql_request);
		if (!isset($result) or $result == '' or $result->data[0]['isoadmin_pwd_history']=='') {
//			$log->log_debug("isoadmin_check_kennwort_history:: no history found");
			return true;
		} else {
			$old_hash_string = $result->data[0]['isoadmin_pwd_history'];
//			$log->log_debug("isoadmin_check_kennwort_history:: old_hash_string=$old_hash_string");
			$old_hashes = preg_split("[\%]", $old_hash_string);
			foreach ($old_hashes as $hash) {
//				$log->log_debug("isoadmin_check_kennwort_history:: hash=$hash");
				if (password_verify($password, $hash)) {
//					$log->log_debug("isoadmin_check_kennwort_history:: kennwort_verify=YES, found kennwort reuse");
					return false;					
				} else {
//					$log->log_debug("isoadmin_check_kennwort_history:: kennwort_verify=NO, kennwort not reused");
				}
			}
			return true;
		}
	}
	function isoadmin_append_pwd_hash($user, $password) {
		$delimiter = '%';
		$sql_request = "SELECT isoadmin_pwd_history FROM isoadmin WHERE isoadmin_username = '$user'; ";
		$result = $this->iso_db_query($sql_request);
		if (!isset($result) or $result == '' or $result->data[0]['isoadmin_pwd_history']=='') {
			$old_hash_string = '';
		} else {
			$old_hash_string = $result->data[0]['isoadmin_pwd_history'] . $delimiter;
		}
		$pwd_hash = password_hash($password, PASSWORD_DEFAULT);
		$sql_request = "UPDATE isoadmin SET isoadmin_pwd_history='$old_hash_string$pwd_hash' WHERE isoadmin_username = '$user'; ";
		$result = $this->iso_db_query($sql_request);

		// TODO: only keep last x passwords - using $old_hashes = preg_split("\%", $old_hash_string);
	}
	function check_login($user_in, $password_in) {
		$log = new LogConnection();
		$log->log_debug("start of check_login for user $user_in");
		$return_value = '';
//		$return_value = false;
		if ($user_in == 'dbadmin' or $user_in == 'itsecorg' or $user_in == 'postgres') {
			$log->log(LOG_ERR, "E-DB4: Login mit Admin $user_in nicht erlaubt.");
			$return_value = 'superuser_login';
		} else {
			$link_str = "host=" . $this->dbhost . " port=" . $this->dbport . " dbname=" . $this->dbname . " user=" . $user_in . " password='" . $password_in . "'";
//			$log->log(LOG_ERR, "INFO: link_str = $link_str");
			$link = pg_connect($link_str);
			if (!is_object($link) && !$link) {
				$log->log(LOG_ERR, 'E-DB2: Could not connect to database');
				$return_value = 'wrong_creds1';
			} else {
				$testrequest = "SELECT * FROM object LIMIT 1";
				$result = pg_query($link, $testrequest);
				if (!is_object($result) && !$result) {
					$log->log(LOG_ERR, "E-DB1: $testrequest");
					$return_value = 'database-connect-error';
				} else {
					if ($this->error->isError($result)) {
						$log->log(LOG_ERR, "E-DB3: no permissions for $testrequest");
						$return_value = 'wrong_creds2';
					} else {
						$log->log_debug("checking whether Kennwort for user $user_in must be changed");
						$testrequest = "SELECT isoadmin_username, isoadmin_last_name, isoadmin_password_must_be_changed " . 
							"FROM isoadmin WHERE isoadmin_username='" . $user_in . "';";
						$result = $this->iso_db_query($testrequest);
//						if (is_null($isoadmin_username) or $isoadmin_username=='') {
//						if ($this->error->isError($result)) {
						if (!$result) {
							$log->log_debug("User $user_in does not exist in database table isoadmin");
							$return_value = 'wrong_creds3';
						} else {
							$isoadmin_username = $result->data[0]["isoadmin_username"];
							$isoadmin_change_kennwort = $result->data[0]["isoadmin_password_must_be_changed"];
							$log->log_debug("Found user $isoadmin_username with kennwort_change: " . $isoadmin_change_kennwort);
							if (!is_null($isoadmin_change_kennwort) and $isoadmin_change_kennwort=='f') {
								$log->log_debug("Kennwort for user $user_in must not be changed");
							} else {
								$log->log_debug("Kennwort for user $user_in must be changed");
								$return_value = 'password_must_be_changed';
							}
						}
						$testrequest = "SELECT isoadmin_end_date FROM isoadmin WHERE isoadmin_username='" . $user_in . "'";
						$result = pg_query($link, $testrequest);
						if (!($this->error->isError($result))) {
							$end_date = $result->data[0]['isoadmin_end_date'];
							if (!is_null($end_date) and $end_date < date('Y-m-d H:i')) {
								$log->log_debug("Account for user $user_in expired");
								$return_value = 'expired';
							}
						}
					}
				}
			}
			if ($link) pg_close($link);
		}
		return $return_value;
	}
	function is_session_started() {
		return (isset($_SESSION));
	}
}

class DbList {
	var $rows;
	var $filter;
	var $db_connection;
	var $error;

	function __construct() {
		$this->error = new PEAR();
	}
	function getRows() {
		return $this->rows;
	}
	function initSessionConnection() {
		if (isset($_SESSION["auth"]) && isset($_SESSION["dbuser"]) && isset($_SESSION["dbpw"]) && $_SESSION["auth"] == "true") {
			$this->db_connection = new DbConnection(new DbConfig($_SESSION["dbuser"], $_SESSION["dbpw"]));
		} else {
			$this->db_connection = NULL;
		}
	}
	function initConnection($user, $pw) {
		$conn = NULL;
		if (is_null($this->db_connection)) {
			$conn = new DbConnection(new DbConfig($user, $pw));
		} else {
			if ($user <> $this->db_connection->getUser() || $pw <> $this->db_connection->getPw()) {
				$this->db_connection = NULL;
				$this->error->raiseError("E-DB3: Already created connection should be reused with different user.");
			} else {
				$conn = $this->db_connection;
			}
		}
		return $conn;
	}
}

class DbItem {
	var $db_connection;
	var $display;
	var $error;

	function getValue($data, $key, $keys) {
		if (in_array($key, $keys, true)) {
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
}
?>
