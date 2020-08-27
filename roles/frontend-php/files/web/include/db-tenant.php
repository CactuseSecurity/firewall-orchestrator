<?php
// $Id: db-tenant.php,v 1.1.2.3 2012-04-30 17:21:18 tim Exp $
// $Source: /home/cvs/iso/package/web/include/Attic/db-tenant.php,v $
require_once ("display-filter.php");
require_once ("db-base.php");
require_once ("multi-language.php");
require_once ("PEAR.php");


class tenant {
	var $tenant_name;
	var $tenant_created;
	var $error;
	
	function __construct($db_connection, $tenant_id) {
		$this->error = new PEAR();
		$sql_code = "SELECT tenant_name, tenant_create FROM tenant WHERE tenant_id=$tenant_id";
		if ($this->error->isError($db_connection))
			$this->error->raiseError("F-RCF: Connection not initialized. " . $db_connection->getMessage());
		$tenant_details = $db_connection->fworch_db_query ($sql_code);
		if ($this->error->isError($tenant_details)) $this->error->raiseError($tenant_details->getMessage());
		$this->tenant_name = $tenant_details->data[0]['tenant_name'];
		$this->tenant_created = $tenant_details->data[0]['tenant_create'];
	}
	function gettenantName() {
		return $this->tenant_name;
	}
	function gettenantCreated() {
		return $this->tenant_created;
	}
}

class tenantNetwork {
	var $tenant_net_ip;
	var $tenant_net_created;
	var $tenant_id;
	var $tenant_name;
	var $error;
	
	function __construct($db_connection, $tenant_net_id) {
		$this->error = new PEAR();		
		$sql_code = "SELECT tenant_net_ip, tenant_name, tenant_id, tenant_net_create FROM tenant_network LEFT JOIN tenant USING (tenant_id) WHERE tenant_net_id=$tenant_net_id";
		if ($this->error->isError($db_connection)) $this->error->raiseError("F-RCF: Connection not initialized. " . $db_connection->getMessage());
		$tenant_net_details = $db_connection->fworch_db_query ($sql_code);
		if ($this->error->isError($tenant_net_details)) $this->error->raiseError($tenant_net_details->getMessage());
		$this->tenant_net_ip = $tenant_net_details->data[0]['tenant_net_ip'];
		$this->tenant_id = $tenant_net_details->data[0]['tenant_id'];
		$this->tenant_name = $tenant_net_details->data[0]['tenant_name'];
		$this->tenant_net_created = $tenant_net_details->data[0]['tenant_net_create'];
	}
	function gettenantNetIp() {
		return $this->tenant_net_ip;
	}
	function gettenantName() {
		return $this->tenant_name;
	}
	function gettenantNettenantId() {
		return $this->tenant_id;
	}
	function gettenantNetCreated() {
		return $this->tenant_net_created;
	}
}

class tenantList {
	var $tenant_list;
	var $error;
	
	function __construct($filter, $db_connection) {
		$this->error = new PEAR();
		if ($this->error->isError($db_connection))
			$this->error->raiseError("F-RCF: Connection not initialized. " . $db_connection->getMessage());
		$tenant_sql_code = "SELECT * FROM tenant ";
		if (isset($filter->tenant_filter_expr)) $tenant_sql_code .= " WHERE " . $filter->tenant_filter_expr; 
		$tenant_sql_code .= " ORDER BY tenant_name";
//		echo "tenant_sql_code: $tenant_sql_code<br>";
		$this->tenant_list = $db_connection->fworch_db_query ($tenant_sql_code);
		if ($this->error->isError($this->tenant_list)) $this->error->raiseError($this->tenant_list->getMessage());
	}
	function gettenants() {
		return $this->tenant_list;
	}
	function gettenantId($tenant_name) {
		$tenant_list = $this->gettenants();
		for ($i=0; $i<$tenant_list->rows; ++$i) {
			if ($tenant_list->data[$i]['tenant_name'] == $tenant_name)
				return $tenant_list->data[$i]['tenant_id'];
		}
		return "ERROR: tenant name .$tenant_name. not found";
	}
	function filter_is_mandatory($tenant_filter) {
//		echo "tenant_filter: .$tenant_filter.<br>";
		return ($tenant_filter != ' (TRUE) ');
	}
	function gettenantName($tenant_id) {
		$tenant_list = $this->gettenants();
		for ($i=0; $i<$tenant_list->rows; ++$i) {
			if ($tenant_list->data[$i]['tenant_id'] == $tenant_id)
				return $tenant_list->data[$i]['tenant_name'];
		}
		return "ERROR: tenant id .$tenant_id. not found";
	}
	//   $tenant_nr : fortlaufende Nummer des Menues (nur f�r Post-�bergabe)
						// $selected_tenant_name : Name des tenants der vorausgew�hlt ist (0 f�r keinen)
											   // $unselect_possible: show [keiner] 
											                      // menue_width: Breite
					                                     						//  spacing
	function get_tenant_menue_string_base
		($tenant_menue_nr, $selected_tenant_name, $unselect_not_possible, $menue_width, $spacing, $gui_user) {
		$language = new Multilanguage($gui_user);
		// setting default values
		$deselect_string = $language->get_text_msg('please_select', 'html'); // '[Bitte ausw&auml;hlen]';
		if ($spacing=='tight') $space_string = 'dist_mit';
		else $space_string = 'dist_single';
		if (!isset ($tenant_menue_nr)) $tenant_menue_nr = '';  // keine Menue-Nr --> kein Index f�r Form-Ausgabe
		if ($tenant_menue_nr === '') $separator='';
		else $separator = '$';
		if (!isset ($selected_tenant_name)) $selected_tenant_name = 0;  // nichts ausgew�hlt --> [keiner]
		if (!isset ($menue_width) or $menue_width=='') $menue_width = '';  // keine Breite ausgew�hlt: nix ausgeben
		else $menue_width = 'filter' . $menue_width;
		$tenant_list	= $this->gettenants();
		$tenant_anzahl	= $tenant_list->rows;
		$tenant_menue	= '<SELECT size="1" name="tenant_id' . $separator . $tenant_menue_nr . '" class="filter ' . 
			$menue_width . ' ' . $space_string . '">';
		if (!$unselect_not_possible)
			$tenant_menue .= '<OPTION selected value="">' . $deselect_string . '</OPTION>'; // default: nix ausgew�hlt
		for ($i = 0; $i < $tenant_anzahl; ++ $i) {
			$tenant_id = $tenant_list->data[$i]['tenant_id'];
			$tenant_name = $tenant_list->data[$i]['tenant_name'];  
			if ($selected_tenant_name and $tenant_name == $selected_tenant_name) {
				$tenant_menue .= '<OPTION selected value="'.$tenant_id.'">'.$tenant_name.'</OPTION>';
			} else {
				$tenant_menue .= '<OPTION value="'.$tenant_id.'">'.$tenant_name.'</OPTION>';
			}
		}
		$tenant_menue .= '</SELECT>';
		return $tenant_menue;
	}
	function get_simple_tenant_menue_string($lock_tenant, $gui_user) {
		return $this->get_tenant_menue_string_base('', 0, $lock_tenant, 210, 'normal spacing', $gui_user);
	}	
	function get_tenant_menue_string_tight($tenant_menue_nr, $selected, $lock_tenant, $gui_user) {
		return $this->get_tenant_menue_string_base($tenant_menue_nr, $selected,$lock_tenant,'','tight', $gui_user);
	}
	function get_tenant_menue_string($tenant_menue_nr, $selected, $lock_tenant, $gui_user) {
		return $this->get_tenant_menue_string_base($tenant_menue_nr, $selected,$lock_tenant,'','normal_spacing', $gui_user);
	}
}

class tenantNetList extends tenantList {
	var $tenant_net_list;
	var $tenant_net_ar;
	var $error;
	
	function __construct($filter, $db_connection, $management_filter) {
		$this->error = new PEAR();
		if ($this->error->isError($db_connection))
		$this->error->raiseError("F-RCF: Connection not initialized. " . $db_connection->getMessage());
		$tenant_sql_code = "SELECT * FROM tenant LEFT JOIN tenant_network USING (tenant_id)";
		if (isset($filter->tenant_filter_expr)) $tenant_sql_code .= " WHERE " . $filter->tenant_filter_expr;
		$tenant_sql_code .= " ORDER BY tenant_name, tenant_net_ip";
		//		echo "tenant_sql_code: $tenant_sql_code<br>";
		$this->tenant_net_list = $db_connection->fworch_db_query ($tenant_sql_code);
		$this->tenant_net_ar = array();
		for ($zi = 0; $zi < $this->tenant_net_list->rows; ++ $zi) $this->tenant_net_ar[] = $this->tenant_net_list->data[$zi]['tenant_net_ip'];
		if ($this->error->isError($this->tenant_net_list)) $this->error->raiseError($this->tenant_net_list->getMessage());
	}
	function gettenantNetworks() {
		return $this->tenant_net_list;
	}
	function filter_is_mandatory($tenant_filter) {
		//		echo "tenant_filter: .$tenant_filter.<br>";
		return ($tenant_filter != ' (TRUE) ');
	}
}

?>