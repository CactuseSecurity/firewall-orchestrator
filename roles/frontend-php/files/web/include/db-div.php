<?php
// $Id: db-div.php,v 1.1.2.10 2012-05-22 21:47:51 tim Exp $
// $Source: /home/cvs/iso/package/web/include/Attic/db-div.php,v $
require_once ("display-filter.php");
require_once ("db-base.php");
require_once ("PEAR.php");
require_once ('multi-language.php');

class ReportList {
	var $report_list;
	var $error;
	
	function __construct($filter, $db_connection) {
		$this->error = new PEAR();
		if ($this->error->isError($db_connection))
		$this->error->raiseError("F-RCF: Connection not initialized. " . $db_connection->getMessage());
		$db = new DbList();
		$db->initSessionConnection();
		$language = new Multilanguage($filter->getUser());
		$sql_code = "SELECT get_report_typ_list('report_typ'); FETCH ALL FROM report_typ";
		$this->report_list =$db->db_connection->fworch_db_query($sql_code);
		if ($this->error->isError($this->report_list)) $this->error->raiseError($this->system_list->getMessage());
	}
	function getReports() {
		return $this->report_list;
	}
}

class ReportFormatList {
	var $rf_list;
	var $rf_selected;
	var $error;
	
	function __construct() {
		$this->error = new PEAR();
		$this->rf_list = array(1 => "html", 2 => "simple.html", 3 => "junos", 4 => "csv", 5 => "ARS.csv",
				6 => "ARS.noname.csv", 7 => "json");
	}

	function GetReportFormatMenueBase($max_index) {
		$rf_menue = '<SELECT name="reportFormat_' . $max_index . '" class="filter filter210 dist_mit">';
		for ($reportformat_id = 1; $reportformat_id <= $max_index; ++ $reportformat_id) {
			$selected = '';
			$rf_name = $this->rf_list[$reportformat_id];
			if ($rf_name == $this->rf_selected) $selected = ' selected ';
			$rf_menue .= '<OPTION' . $selected .' value="'.$rf_name.'">' . $rf_name . '</OPTION>';
		}
		$rf_menue .= '</SELECT>';
		return $rf_menue;
	}
	function GetReportFormatMenue() { return $this->GetReportFormatMenueBase(7); }
	function GetReportFormatMenueHTMLOnly() { return $this->GetReportFormatMenueBase(2); }
}

class RequestTypeList {
	var $request_type_list;
	var $error;
	
	function __construct($filter, $db_connection) {
		$this->error = new PEAR();
		if ($this->error->isError($db_connection))
			$this->error->raiseError("F-RCF: Connection not initialized. " . $db_connection->getMessage());
		$tenant_sql_code = "SELECT * FROM request_type ";
		$tenant_sql_code .= " ORDER BY request_type_name";
//		echo "tenant_sql_code: $tenant_sql_code<br>";
		$this->request_type_list = $db_connection->fworch_db_query ($tenant_sql_code);
		if ($this->error->isError($this->request_type_list)) $this->error->raiseError($this->request_type_list->getMessage());
	}
	function getRequestTypes() {
		return $this->request_type_list;
	}
	function getRequestTypeId($request_type_name) {
		$request_type_list = $this->getRequestTypes();
		for ($i=0; $i<$request_type_list->rows; ++$i) {
			if ($request_type_list->data[$i]['request_type_name'] == $request_type_name)
				return $request_type_list->data[$i]['request_type_id'];
		}
		return "ERROR: request_type_name $request_type_name not found";
	}
	function getRequestTypeName($request_type_id) {
		$request_type_list = $this->getRequestTypes();
		for ($i=0; $i<$request_type_list->rows; ++$i) {
			if ($request_type_list->data[$i]['request_type_id'] == $request_type_id)
				return $request_type_list->data[$i]['request_type_name'];
		}
		return "ERROR: request_type_id $request_type_id not found";
	}
	//   $tenant_nr : fortlaufende Nummer des Menues (nur f�r Post-�bergabe)
						         // $selected_tenant_name : Name des tenants der vorausgew�hlt ist (0 f�r keinen)
							                                 // menue_width: Breite
					                                     						//  spacing
	function get_request_type_menue_string_base
		($request_type_menue_nr, $selected_request_type_name, $unselect_not_possible, $menue_width, $spacing) {

		// setting default values
		$deselect_string = '[Bitte ausw&auml;hlen]';
		if ($spacing=='tight') $space_string = 'dist_mit';
		else $space_string = 'dist_single';
		if (!isset ($request_type_menue_nr)) $request_type_menue_nr = '';  // keine Menue-Nr --> kein Index f�r Form-Ausgabe
		if ($request_type_menue_nr === '') $separator='';
		else $separator = '$';
		if (!isset ($selected_request_type_name)) $selected_request_type_name = 0;  // nichts ausgew�hlt --> [keiner]
		if (!isset ($menue_width) or $menue_width=='') $menue_width = '';  // keine Breite ausgew�hlt: nix ausgeben
		else $menue_width = 'filter' . $menue_width;
		$request_type_list	= $this->getRequestTypes();
		$request_type_anzahl	= $request_type_list->rows;
		$request_type_menue	= '<SELECT size="1" name="request_type_id' . $separator . $request_type_menue_nr . '" class="filter ' . 
			$menue_width . ' ' . $space_string . '">';
		if (!$unselect_not_possible)
			$request_type_menue .= '<OPTION selected value="">' . $deselect_string . '</OPTION>'; // default: nix ausgew�hlt
		for ($i = 0; $i < $request_type_anzahl; ++ $i) {
			$request_type_id = $request_type_list->data[$i]['request_type_id'];
			$request_type_name = $request_type_list->data[$i]['request_type_name'];  
			if ($selected_request_type_name and $request_type_name == $selected_request_type_name) {
				$request_type_menue .= '<OPTION selected value="'.$request_type_id.'">'.$request_type_name.'</OPTION>';
			} else {
				$request_type_menue .= '<OPTION value="'.$request_type_id.'">'.$request_type_name.'</OPTION>';
			}
		}
		$request_type_menue .= '</SELECT>';
		return $request_type_menue;
	}
	function get_simple_request_type_menue_string($lock_request_type) {
		return $this->get_request_type_menue_string_base('', 0, $lock_request_type, 210, 'normal spacing');
	}	
	function get_request_type_menue_string_tight($request_type_menue_nr, $selected, $lock_request_type) {
		return $this->get_request_type_menue_string_base($request_type_menue_nr, $selected,$lock_request_type,'','tight');
	}
	function get_request_type_menue_string($request_type_menue_nr, $selected, $lock_request_type) {
		return $this->get_request_type_menue_string_base($request_type_menue_nr, $selected,$lock_request_type,'','normal_spacing');
	}
}

class uiuser {
	var $uiuser_username;
	var $uiuser_full_name;
	var $uiuser_id;
	var $error;
		
	function __construct($filter, $db_connection) {
		$this->error = new PEAR();
		$uiuser_name = $filter->getSessionUser();
		$this->uiuser_username = $uiuser_name;
		$sql_code = "SELECT uiuser_id, uiuser_first_name, uiuser_last_name FROM uiuser WHERE uiuser_username='" . $uiuser_name . "'";
		if ($this->error->isError($db_connection))
			$this->error->raiseError("F-RCF: Connection not initialized. " . $db_connection->getMessage());
		$uiuser_details = $db_connection->fworch_db_query ($sql_code,$filter->getLogLevel());
		if ($this->error->isError($uiuser_details)) $this->error->raiseError($uiuser_details->getMessage());
		$this->uiuser_full_name = $uiuser_details->data[0]['uiuser_first_name'] . 
			' ' . $uiuser_details->data[0]['uiuser_last_name'] ;
		$this->uiuser_id = $uiuser_details->data[0]['uiuser_id'];
//		echo "found uiuser_id: " . $this->uiuser_id . "<br>";
		if (!isset($this->uiuser_id)) $this->error->raiseError("ERROR: no matching uiuser_id found!");
	}
	function getFullName() {
		return $this->uiuser_full_name;
	}
	function getId() {
		return $this->uiuser_id;
	}
	function getUserName() {
		return $this->uiuser_username;
	}
}

class RequestList {
	var $requests;
	var $error;
			
	function __construct ($request_change_table, $log_id_name, $local_change_id,
			$changelog_table, $comment_field, $db_connection) { 
		$this->error = new PEAR();
		$sql_code = "SELECT request_id FROM $request_change_table WHERE $log_id_name=$local_change_id";
		if ($this->error->isError($db_connection))
			$this->error->raiseError("F-RCF: Connection not initialized. " . $db_connection->getMessage());
		$request_id_list = $db_connection->fworch_db_query ($sql_code);
		if ($this->error->isError($request_id_list)) $this->error->raiseError($request_id_list->getMessage());
		$sql_code = "SELECT request_id,request_number,tenant_name,tenant_id,$comment_field AS comment FROM request " . 
					"LEFT JOIN tenant USING(tenant_id) " . 
					"LEFT JOIN $request_change_table USING (request_id) " .
					"LEFT JOIN $changelog_table USING ($log_id_name) WHERE $log_id_name=$local_change_id AND request_id IN (" ;
		for ($i=0; $i<$request_id_list->rows; $i++) $sql_code .= $request_id_list->data[$i]['request_id'] . ',';
		$sql_code = substr($sql_code,0,strlen($sql_code)-1) . ')';
//		echo "sql: $sql_code<br>";
		$this->requests = $db_connection->fworch_db_query ($sql_code,$loglevel);
		if ($this->error->isError($this->requests)) $this->error->raiseError($this->requests->getMessage());
	}
	function getRequesttenantName($number) {
		return $this->requests->data[$number]['tenant_name'];
	}
	function getRequesttenantId($number) {
		return $this->requests->data[$number]['tenant_id'];
	}
	function getRequestNumber($number) {
		return $this->requests->data[$number]['request_number'];
	}
	function getRequestId($number) {
		return $this->requests->data[$number]['request_id'];
	}
	function getRequestComment() {
		return $this->requests->data[0]['comment']; // der Kommentar ist f�r alle Auftr�ge identisch
	}
}

class IpProtoList {
	var $ip_proto_list;
	var $error;
		
	function __construct($filter, $db_connection) {
		$this->error = new PEAR();
		if ($this->error->isError($db_connection))
			$this->error->raiseError("F-RCF: Connection not initialized. ".$this->db_connection->getMessage());
		$sql_code = "SELECT * FROM stm_ip_proto WHERE ip_proto_id>0 ORDER BY ip_proto_id";
		$ip_protos = $db_connection->fworch_db_query($sql_code);
		if ($this->error->isError($ip_protos)) $this->error->raiseError($ip_protos->getMessage());
		$this->ip_proto_list = $ip_protos;
	}
	function getIpProtoList() {
		return $this->ip_proto_list;
	}
	function getIpProtoForm() {
		$ip_proto_list = $this->getIpProtoList();
		$proto_anz = $ip_proto_list->rows;
		$proto_list_html = "";
		for($zi=0;$zi < $proto_anz; ++$zi) {
			$proto_list_html .= "<option value='".$ip_proto_list->data[$zi]['ip_proto_id']."'>".
					$ip_proto_list->data[$zi]['ip_proto_name']." (" . $ip_proto_list->data[$zi]['ip_proto_id'] . ")</option>";
		}
		return $proto_list_html;
	}
}

class SystemList {
	var $system_list;
	var $error;
		
	function __construct($filter, $db_connection, $management_filter, $leave_out_mgm_without_dev, $show_hidden_systems) {
		$this->error = new PEAR();
		if ($this->error->isError($db_connection))
			$this->error->raiseError("F-RCF: Connection not initialized. " . $db_connection->getMessage());
		$db = new DbList();
		$db->initSessionConnection();
		if ($leave_out_mgm_without_dev) $join_type = 'INNER'; else $join_type = 'LEFT';
		$sql_code = 
			"SELECT management.mgm_id,management.mgm_name,device.dev_id,device.dev_name,stm_dev_typ.dev_typ_manufacturer " . 
			"FROM management $join_type JOIN device USING (mgm_id) LEFT JOIN stm_dev_typ ON (device.dev_typ_id=stm_dev_typ.dev_typ_id) " .
			"WHERE ";
		if ($show_hidden_systems)
			$sql_code .= "TRUE ";
		else 
			$sql_code .= "NOT device.hide_in_gui AND NOT management.hide_in_gui "; 
		if ($filter->isMgmFilterSet()) $sql_code .= " AND " . $filter->getMgmFilter();
//		if (isset($filter->tenant_filter_expr)) $sql_code .=  " AND " . $filter->tenant_filter_expr; 
		$sql_code .= " ORDER BY dev_typ_manufacturer,mgm_name,dev_name"; 
		$this->system_list =$db->db_connection->fworch_db_query($sql_code);
//		echo "found " . $this->system_list->rows . " systems with sql code:  $sql_code<br"; 
		if ($this->error->isError($this->system_list)) $this->error->raiseError($this->system_list->getMessage());
	}
	function getSystems() {
		return $this->system_list;
	}
}

/*
class DeviceList {
	var $device_list;
	var $dev_list;
	var $error;
		
	function __construct($filter, $db_connection, $management_filter) {
		$this->error = new PEAR();
		if ($this->error->isError($db_connection))
			$this->error->raiseError("F-RCF: Connection not initialized. " . $db_connection->getMessage());
		$db = new DbList();
		$db->initSessionConnection();
		$sql_code = 
			"SELECT device.*,stm_dev_typ.dev_typ_manufacturer,stm_dev_typ.dev_typ_version,stm_dev_typ.dev_typ_name " . 
			"FROM device LEFT JOIN management using (mgm_id) LEFT JOIN stm_dev_typ using (dev_typ_id)"; 
		if (isset($management_filter)) $sql_code .= " AND $management_filter ";
//		if (isset($filter->tenant_filter_expr)) $tenant_sql_code .=  " AND " . $filter->tenant_filter_expr; 
		$sql_code .= " ORDER BY dev_typ_manufacturer,mgm_name"; 
		$this->device_list =$db->db_connection->fworch_db_query($sql_code);
		
		if ($this->error->isError($this->device_list)) $this->error->raiseError($this->device_list->getMessage());

		$anz = $this->device_list->rows;
		for ($i=0; $i<$anz; ++$i) {
			$dev_list[$this->device_list->data[$i]['dev_id']] = $this->device_list->data[$i];
		}
		$this->$dev_list = $dev_list; 
	}
	
	function getDevices() {
		return $this->device_list;
	}
	function getMgmId ($dev_id) {
		return $this->dev_list[$dev_id];
	}
}
*/
		
class ManagementList {
	var $management_list;
	var $error;
	 	
	function __construct($filter, $db_connection, $management_filter) {
		$this->error = new PEAR();
		if ($this->error->isError($db_connection))
			$this->error->raiseError("F-RCF: Connection not initialized. " . $db_connection->getMessage());
		$db = new DbList();
		$db->initSessionConnection();
		$sql_code = 
			"SELECT management.*,stm_dev_typ.dev_typ_manufacturer,stm_dev_typ.dev_typ_version,stm_dev_typ.dev_typ_name " . 
			"FROM management LEFT JOIN stm_dev_typ using (dev_typ_id) WHERE NOT management.hide_in_gui "; 
		if (isset($management_filter)) $sql_code .= " AND $management_filter ";
//		if (isset($filter->tenant_filter_expr)) $sql_code .=  " AND " . $filter->tenant_filter_expr; 
		$sql_code .= " ORDER BY dev_typ_manufacturer,mgm_name"; 
		$this->management_list =$db->db_connection->fworch_db_query($sql_code);
		if ($this->error->isError($this->management_list)) $this->error->raiseError($this->management_list->getMessage());
	}
	function GetSystems() {
		return $this->management_list;
	}
}

?>