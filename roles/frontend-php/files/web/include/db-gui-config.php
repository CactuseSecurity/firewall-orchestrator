<?php
/*
 * $Id: db-gui-config.php,v 1.1.2.11 2012-03-30 15:32:42 tim Exp $
 * $Source: /home/cvs/iso/package/web/include/Attic/db-gui-config.php,v $
 * Created on 11.01.2006
 *
 */
 
require_once("operating-system.php");

class Config {
	var $lines;
	var $loglevel;
	var $logtarget;
	var $log_facility;
	var $rule_header_offset;
	var $language;
	
	function __construct() {
		$lines	= file('iso.conf', $use_include_path = 1);
		foreach ($lines as $line) {
			$line_array = explode('#', $line); // remove comments
			$line = trim($line_array[0]);
			$line = $this->remove_superfluous_whitespaces($line);
			if ($line <> '\n' and $line <>'') {  // also removes empty lines
				if ($this->cfg_line_contains_str($line, 'language'))	$this->language  = $this->extract_value($line, 'language');
				if ($this->cfg_line_contains_str($line, 'loglevel'))	$this->loglevel  = $this->extract_value($line, 'loglevel');
				if ($this->cfg_line_contains_str($line, 'log_facility'))	$this->log_facility  = $this->extract_value($line, 'log_facility');
				if ($this->cfg_line_contains_str($line, 'logtarget'))	$this->logtarget = $this->extract_value($line, 'logtarget');
				if ($this->cfg_line_contains_str($line, 'rule_header_offset'))
					$this->rule_header_offset = $this->extract_value($line, 'rule_header_offset');
			} 
		}
	}
	
	function GetConfigLines() {		// now searches the include_path set in php.ini for config files
		$global_lines	= file('iso.conf', $use_include_path = 1);
		$gui_lines 	= file('gui.conf', $use_include_path = 1);
		$lines = array_merge($global_lines, $gui_lines);
		$new_lines = array();
		foreach ($lines as $line) {
			$line_array = explode('#', $line); // remove comments
			$line = trim($line_array[0]);
			$line = $this->remove_superfluous_whitespaces($line);
			if ($line <> '\n' and $line <>'')  $new_lines[] = $line; // also removes empty lines
		}
		return $new_lines;
	}
	function getRuleHeaderOffset () {  // netscreen only
		return $this->rule_header_offset;
	}
	function getLogLevel () {
		return $this->loglevel;
	}
	function getLogTarget () {
		return $this->logtarget;
	}
	function getLanguage () {
		return $this->language;
	}
	function getLogFacility () {
		return $this->log_facility;
	}
	function remove_superfluous_whitespaces($line) {
		$line = preg_replace('/\s+/', ' ', $line);
		return $line;
	}
	function cfg_line_contains_str($line, $key_string) {
		return (!strpos(strtolower($line),$key_string)===false);
	}
	function extract_value($line, $key_string) {
		$return_value = '';
		if (!strpos($line,$key_string)===false) {
			list(,$return_value) = explode("$key_string ", $line);
//			$log_e = new LogConnection(); $log_e->log_debug("found in config $key_string: .$return_value.");
			$return_value = trim($return_value);
		}
		return $return_value;
	}
}

/*
 * Konfigurationen, die vom gerade angemeldeten Benutzer unabh�ngig sind
 */
class DisplayConfig extends Config {  // setting default values here
	var $display_undoc_changes = 500;
	var $display_rulechanges_maxrows = 2;
	var $display_rulechanges_maxcols = 30;
	var $display_max_number_of_rules_in_ruleview = 400;
	var $display_number_of_requests = 4;
	var $display_approver = 1;
	var $display_request_type = 0;
	var $display_comment_is_mandatory = 1;

	function __construct() {
		$this->ReadDisplayConfig();
	}
	function ReadDisplayConfig() {
		$lines = $this->GetConfigLines();
		foreach ($lines as $line) {
			if ($this->cfg_line_contains_str($line, 'max_number_of_rules_in_ruleview'))
				$this->display_max_number_of_rules_in_ruleview = $this->extract_value($line, 'max_number_of_rules_in_ruleview');
			if ($this->cfg_line_contains_str($line, 'number_of_undocumented_changes'))
				$this->display_undoc_changes = $this->extract_value($line, 'number_of_undocumented_changes');
			if ($this->cfg_line_contains_str($line, 'number-of-requests'))
				$this->display_number_of_requests = $this->extract_value($line, 'number-of-requests');				
			if ($this->cfg_line_contains_str($line, 'display-approver'))
				$this->display_approver = $this->extract_value($line, 'display-approver');				
			if ($this->cfg_line_contains_str($line, 'display-request-type'))
				$this->display_request_type = $this->extract_value($line, 'display-request-type');				
			if ($this->cfg_line_contains_str($line, 'comment-is-mandatory'))
				$this->display_comment_is_mandatory = $this->extract_value($line, 'comment-is-mandatory');
			if (!strpos($line,'undocumented_rule_changes')===false) {
				if ($this->cfg_line_contains_str($line, 'maxcols'))
					$this->display_rulechanges_maxcols = $this->extract_value($line, 'maxcols');
				if ($this->cfg_line_contains_str($line, 'maxrows'))
					$this->display_rulechanges_maxrows = $this->extract_value($line, 'maxrows');
			}
		}		
	}
	function getMaxUndocChangesNumber ()	{ return $this->display_undoc_changes; }
	function getMaxRowsRuleChanges ()		{ return $this->display_rulechanges_maxrows; }
	function getMaxColsRuleChanges ()		{ return $this->display_rulechanges_maxcols; }
	function getMaxRuleDisplayNumber ()		{ return $this->display_max_number_of_rules_in_ruleview; }
	function getNumberOfRequests ()			{ return $this->display_number_of_requests; }
	function getDisplayApprover ()			{ return $this->display_approver; }
	function getDisplayRequestType ()		{ return $this->display_request_type; }
	function getIsCommentMandatory ()		{ return $this->display_comment_is_mandatory; }
}

/*
 * Konfiguration der Datenbankanbindung aus Configfile auslesen und zur Verf�gung stellen
 */
class DbConfig extends Config {
	var $dbhost = 'localhost'; // setting default values
	var $dbname = 'isodb';
	var $dbport = '5432';
	var $dbtype = 'DBX_PGSQL';
	var $dbuser;
	var $dbpw;

	function __construct($user,$password) {
		$this->dbuser = $user;
		$this->dbpw = $password;
		if (!$this->ReadDbConfigFromSession()) { $this->InitDbConfig(); }
	}
	function InitDbConfig() {
		$this->ParseAndInitDbConfig();
		$this->WriteDbConfigToSession();
	}
	function ParseAndInitDbConfig() {
		$lines = $this->GetConfigLines();
		foreach ($lines as $line) {
			if ($this->cfg_line_contains_str($line, 'database hostname'))
				$this->dbhost = $this->extract_value($line, 'database hostname');
//			if ($this->cfg_line_contains_str($line, 'database type'))
//				$this->dbtype = 0 + $this->extract_value($line, 'database type');
			if ($this->cfg_line_contains_str($line, 'database name'))
				$this->dbname = $this->extract_value($line, 'database name');
			if ($this->cfg_line_contains_str($line, 'database port'))
				$this->dbport = $this->extract_value($line, 'database port');
		}		
	}
	function WriteDbConfigToSession() {
		if (!isset($_SESSION)) session_start();
		$_SESSION['dbhost'] = $this->dbhost;
		$_SESSION['dbtype'] = $this->dbtype;
		$_SESSION['dbname'] = $this->dbname;
		$_SESSION['dbport'] = $this->dbport;
		$_SESSION['dbuser'] = $this->dbuser;
		$_SESSION['dbpw']   = $this->dbpw;
/*
		setcookie('dbhost', $this->dbhost, $secure=true);
		setcookie('dbtype', $this->dbtype, $secure=true);
		setcookie('dbname', $this->dbname, $secure=true);
		setcookie('dbport', $this->dbport, $secure=true);
		setcookie('dbuser', $this->dbuser, $secure=true);
		setcookie('dbpw', $this->dbpw, $secure=true);
*/
 	}
	function ReadDbConfigFromSession() {
		if (!isset($_SESSION)) session_start();
		if (isset($_SESSION['dbhost'])) {
			$this->dbhost = $_SESSION['dbhost']; 
			$this->dbtype = $_SESSION['dbtype'];
			$this->dbname = $_SESSION['dbname'];
			$this->dbport = $_SESSION['dbport'];
			$this->dbuser = $_SESSION['dbuser'];
			$this->dbpw   = $_SESSION['dbpw'];
			return true;
		} else 
			return false;
	}
}
	
/*
 * Konfigurationen, die vom gerade angemeldeten Benutzer abhaengig sind
 */

class UserConfig extends Config {
	var $groups;
	var $privileges;
	var $visible_reports;
	var $visible_clients;
	var $fixed_client_filter;
	var $fixed_request_type_filter;
	var $visible_managements;
	var $visible_devices;
	var $default_client;
	var $default_request_type;
	
	function __construct($user) {
		// find all groups the user belongs to
		$lines = $this->GetConfigLines();
		$log = new LogConnection();
		$this->groups = array();
		$this->default_client = 'NULL';
		$this->default_request_type = 'NULL';
		$this->fixed_client_filter = 'NULL';
		$this->fixed_request_type_filter = 'NULL';
		foreach ($lines as $line) {
			if (!strpos($line,$user)===false and !strpos($line, 'members:')===false) {  // found a line containing group definition for current user
				list(,$group) = explode(' ', $line);
				$members = explode(' ', $line);
				if (array_search($group,$this->groups)===false and !array_search($user,$members)===false) $this->groups[] = $group;
			}
		}			
		reset($lines);
		reset($this->groups);
		$this->privileges = array();
		$this->visible_clients = array();
		$this->visible_managements = array();
		$this->visible_devices = array();
		$this->visible_reports = array();
		foreach ($lines as $line) {
			foreach ($this->groups as $group) {
				if (!strpos($line,$group)===false) {
					if (!strpos($line, 'privileges:')===false) {
						list(,$privileges) = explode('privileges:', $line);
						$priv_ar = explode(' ', trim($privileges));
						foreach ($priv_ar as $priv) {
							if (array_search($priv,$this->privileges)===false) $this->privileges[] = $priv;
						}
					} else if (!strpos($line, 'visible-clients:')===false) {
						list(,$visible_clients) = explode('visible-clients:', $line);
						$client_ar = explode(',', trim($visible_clients));
						foreach ($client_ar as $client) {
							$client2 = $this->remove_quotes(trim($client));
							if (array_search($client2,$this->visible_clients)===false)
								$this->visible_clients[] = $client2;
						}
					} else if (!strpos($line, 'visible-managements:')===false) {
						list(,$visible_mgms) = explode('visible-managements:', $line);
						$mgm_ar = explode(' ', trim($visible_mgms));
						foreach ($mgm_ar as $mgm) {
							if (array_search($mgm,$this->visible_managements)===false)
								$this->visible_managements[] = $mgm;
						}
					} else if (!strpos($line, 'visible-devices:')===false) {
						list(,$visible_devs) = explode('visible-devices:', $line);
						$dev_ar = explode(' ', trim($visible_devs));
						foreach ($dev_ar as $dev) {
							if (array_search($dev,$this->visible_devices)===false)
								$this->visible_devices[] = $dev;
						}
					} else if (!strpos($line, 'visible-reports:')===false) {
						list(,$visible_reports) = explode('visible-reports:', $line);
						$report_ar = explode(' ', trim($visible_reports));
						
						foreach ($report_ar as $report) {
							$log->log_debug("found vis. report in config line: $report");
							if (array_search($report,$this->visible_reports)===false)
								$this->visible_reports[] = $report;
						}
					} else if (!strpos($line, 'fixed-client-filter:')===false) {
						list(,$fixed_client_filter) = explode('fixed-client-filter:', $line);
						$this->fixed_client_filter = trim($fixed_client_filter);
					} else if (!strpos($line, 'fixed-request-type-filter:')===false) {
						list(,$fixed_request_type_filter) = explode('fixed-request-type-filter:', $line);
						$this->fixed_request_type_filter = trim($fixed_request_type_filter);
//						$log->log_debug("found fixed request type for group $group: " . $this->fixed_request_type_filter);
					}
				}
			}
		}	
		// set $default_client & $default_request_type		
		reset($lines);
		$group = $this->groups[0];  // choosing first group found in the gui.conf file as primary group of this user
//		$log->log_debug("first group for user found: $group");
		if (isset($group)) {
			foreach ($lines as $line) {
				if (!strpos($line, 'default-client')===false and !strpos($line, $group)===false) { 
					list(,$default_client) = explode(':', $line);
					$default_client = trim($default_client);
					$default_client = $this->remove_quotes($default_client);
					$this->default_client = $default_client;
//					$log->log_debug("found default client for group $group: $default_client");
				}
				if (!strpos($line, 'default-request-type')===false and !strpos($line, $group)===false) { 
					list(,$default_request_type) = explode(':', $line);
					$default_request_type = trim($default_request_type);
					$default_request_type = $this->remove_quotes($default_request_type);
					$this->default_request_type = $default_request_type;
//					$log->log_debug("found default request_type for group $group: $default_request_type");
				}
			}
		}
		
		foreach ($this->visible_managements as $mgm1) 
			$log->log_debug("found visible management for current user: $mgm1");
		foreach ($this->visible_devices as $dev) 
			$log->log_debug("found visible device for current user: $dev");
		foreach ($this->visible_reports as $rep1) 
			$log->log_debug("found visible report for current user: $rep1");
 	}
	function remove_quotes($str) {
		$result = trim($str, "'");
		$result = trim($result, '"');
		return $result;
	}
	function getUserId($username,$password) {
		$db_connection = new DbConnection(new DbConfig($username,$password));
		$isoadmin_id = $db_connection->iso_db_query ("SELECT isoadmin_id FROM isoadmin WHERE isoadmin_username='" . $username . "'");
		$isoadmin_id = $isoadmin_id->data[0]['isoadmin_id'];
		return $isoadmin_id;
	}
	function allowedToDocumentChanges () {
		if (array_search('document-changes',$this->privileges)===false)	return false;
		else return true;
	}
	function allowedToViewImportStatus () {
		if (array_search('view-import-status',$this->privileges)===false)	return false;
		else return true;
	}
	function allowedToViewAllObjectsOfMgm () {
		if (array_search('view-all-objects-filter',$this->privileges)===false)	return false;
		else return true;
	}
	function allowedToChangeDocumentation () {
		if (array_search('change-documentation',$this->privileges)===false)	return false;
		else return true;
	}
	function allowedToConfigureUsers() {
		if (array_search('admin-users',$this->privileges)===false)
			return false;
		else return true;
	}
	function allowedToConfigureDevices() {
		if (array_search('admin-devices',$this->privileges)===false)
			return false;
		else return true;
	}
	function allowedToConfigureClients() {
		if (array_search('admin-clients',$this->privileges)===false)
			return false;
		else return true;
	}
	function allowedToViewReports() {
		if (array_search('view-reports',$this->privileges)===false)
			return false;
		else return true;
	}
	function allowedToViewAdminNames() {
		if (array_search('view-change-admin-names',$this->privileges)===false)
			return false;
		else return true;
	}
	function getDefaultClient() {
		return $this->default_client;
	}
	function getDefaultRequestType() {
		return $this->default_request_type;
	}
	function getReportFilter() {
		$log = new LogConnection();	
		$rep_filter = array();
		$db_connection = new DbConnection(new DbConfig($_SESSION["dbuser"],$_SESSION["dbpw"]));
		if (array_search('ALL', $this->visible_reports)===false) {
			foreach ($this->visible_reports as $report) {
				$log->log_debug("getReportFilter() processing report $report");
				$sql_statement = "SELECT report_typ_id FROM stm_report_typ WHERE lower(report_typ_name_english) like lower('%$report%')";
				$log->log_debug("sql=$sql_statement");
				$report_id = $db_connection->iso_db_query ($sql_statement);
				if (isset($report_id->data[0]['report_typ_id']) && !$report_id->data[0]['report_typ_id']=='') {
					$rep_filter[] = $report_id->data[0]['report_typ_id'];
					$log->log_debug("report_filter_array=" . implode(',',$rep_filter));
				}
			}
		} else {
			$sql_statement = "SELECT report_typ_id FROM stm_report_typ";
			$report_id = $db_connection->iso_db_query ($sql_statement);
			$anzahl_reports = $report_id->rows;
			for ($zi = 0; $zi < $anzahl_reports; ++ $zi) { $rep_filter[] = $report_id->data[$zi]['report_typ_id']; }
		}
		$log->log_debug("getReportFilter()=" . implode(',', $rep_filter));
		return implode(',', $rep_filter);
	}
	function getVisibleClientFilter() {
		$client_filter = ' ( FALSE ';
		if (array_search('ALL', $this->visible_clients)===false) { 
			foreach ($this->visible_clients as $client) { $client_filter .= " OR client_name='$client' "; }
		} else $client_filter = ' (TRUE';
		$client_filter .= ') ';
		return $client_filter;
	}
	function getManagementFilter() {		
		return $this->getManagementFilter_base('');
	}
	function getManagementFilter_base($mgm_table_name) {
		$db_connection = new DbConnection(new DbConfig($_SESSION["dbuser"],$_SESSION["dbpw"]));
		if (!isset($mgm_table_name) or $mgm_table_name=='') {
			$mgm_table_name = '';
			$dev_table_name = '';
		} else {
			$dev_table_name = 'device.';
			$mgm_table_name .= '.';
		}
		$filter = ' ( FALSE ';
		if (array_search('ALL', $this->visible_managements)===false) { 
			foreach ($this->visible_managements as $mgm) {
				// get mgm_id for name:
				$mgm_id = $db_connection->iso_db_query ("SELECT mgm_id FROM management WHERE mgm_name='$mgm'");
				if (isset($mgm_id) and isset($mgm_id->data[0])) {
					$mgm_id = $mgm_id->data[0]['mgm_id'];
					if (isset($mgm_id) and $mgm_id<>"NULL" and !($mgm_id===''))
						$filter .= (" OR " . $mgm_table_name . "mgm_id=$mgm_id ");
				}
			}
		} else $filter = ' ( TRUE ';  // user is allowed to view all managements (ALL)
		$dev_filter = " ( ${dev_table_name}dev_id IS NULL ";
		if (array_search('ALL', $this->visible_devices)===false) { 
			foreach ($this->visible_devices as $dev) {
				if (strpos($dev,'all-of-mgm(')===false) { // normaler device-Eintrag 
					$dev_id = $db_connection->iso_db_query ("SELECT dev_id FROM device WHERE dev_name='$dev'");
					if (isset($dev_id) and isset($dev_id->data[0])) {
						$dev_id = $dev_id->data[0]['dev_id'];
						if (isset($dev_id)) { if ($dev_id==='') {} else { $dev_filter .= (" OR ${dev_table_name}dev_id=$dev_id "); } } 
					}
				} else  // all of management Eintrag
					list(,$mgm) = explode('(', $dev);
					$mgm = substr($mgm,0, strlen($mgm)-1); // Klammer-zu abschneiden
					$mgm_id = $db_connection->iso_db_query ("SELECT mgm_id FROM management WHERE mgm_name='$mgm'");
					if (isset($mgm_id) and isset($mgm_id->data[0])) {
						$mgm_id = $mgm_id->data[0]['mgm_id'];
						if (isset($mgm_id)) { if ($mgm_id==='') {} else { $dev_filter .= " OR ${mgm_table_name}mgm_id=$mgm_id "; } } 
					}
			}
		} else $dev_filter = ' ( TRUE '; // user is allowed to view all devices (ALL)
		$filter .= ' ) AND ' . $dev_filter . ' ) ';
		$log = new LogConnection(); $log->log_debug("getManagementFilter_base($mgm_table_name)=$filter");
		return $filter;
	}
	function getVisibleManagements() {
		return $this->visible_managements;
	}
}
?>
