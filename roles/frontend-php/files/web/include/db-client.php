<?php
// $Id: db-client.php,v 1.1.2.3 2012-04-30 17:21:18 tim Exp $
// $Source: /home/cvs/iso/package/web/include/Attic/db-client.php,v $
require_once ("display-filter.php");
require_once ("db-base.php");
require_once ("multi-language.php");
require_once ("PEAR.php");


class Client {
	var $client_name;
	var $client_created;
	var $error;
	
	function __construct($db_connection, $client_id) {
		$this->error = new PEAR();
		$sql_code = "SELECT client_name, client_create FROM client WHERE client_id=$client_id";
		if ($this->error->isError($db_connection))
			$this->error->raiseError("F-RCF: Connection not initialized. " . $db_connection->getMessage());
		$client_details = $db_connection->fworch_db_query ($sql_code);
		if ($this->error->isError($client_details)) $this->error->raiseError($client_details->getMessage());
		$this->client_name = $client_details->data[0]['client_name'];
		$this->client_created = $client_details->data[0]['client_create'];
	}
	function getClientName() {
		return $this->client_name;
	}
	function getClientCreated() {
		return $this->client_created;
	}
}

class ClientNetwork {
	var $client_net_ip;
	var $client_net_created;
	var $client_id;
	var $client_name;
	var $error;
	
	function __construct($db_connection, $client_net_id) {
		$this->error = new PEAR();		
		$sql_code = "SELECT client_net_ip, client_name, client_id, client_net_create FROM client_network LEFT JOIN client USING (client_id) WHERE client_net_id=$client_net_id";
		if ($this->error->isError($db_connection)) $this->error->raiseError("F-RCF: Connection not initialized. " . $db_connection->getMessage());
		$client_net_details = $db_connection->fworch_db_query ($sql_code);
		if ($this->error->isError($client_net_details)) $this->error->raiseError($client_net_details->getMessage());
		$this->client_net_ip = $client_net_details->data[0]['client_net_ip'];
		$this->client_id = $client_net_details->data[0]['client_id'];
		$this->client_name = $client_net_details->data[0]['client_name'];
		$this->client_net_created = $client_net_details->data[0]['client_net_create'];
	}
	function getClientNetIp() {
		return $this->client_net_ip;
	}
	function getClientName() {
		return $this->client_name;
	}
	function getClientNetClientId() {
		return $this->client_id;
	}
	function getClientNetCreated() {
		return $this->client_net_created;
	}
}

class ClientList {
	var $client_list;
	var $error;
	
	function __construct($filter, $db_connection) {
		$this->error = new PEAR();
		if ($this->error->isError($db_connection))
			$this->error->raiseError("F-RCF: Connection not initialized. " . $db_connection->getMessage());
		$client_sql_code = "SELECT * FROM client ";
		if (isset($filter->client_filter_expr)) $client_sql_code .= " WHERE " . $filter->client_filter_expr; 
		$client_sql_code .= " ORDER BY client_name";
//		echo "client_sql_code: $client_sql_code<br>";
		$this->client_list = $db_connection->fworch_db_query ($client_sql_code);
		if ($this->error->isError($this->client_list)) $this->error->raiseError($this->client_list->getMessage());
	}
	function getClients() {
		return $this->client_list;
	}
	function getClientId($client_name) {
		$client_list = $this->getClients();
		for ($i=0; $i<$client_list->rows; ++$i) {
			if ($client_list->data[$i]['client_name'] == $client_name)
				return $client_list->data[$i]['client_id'];
		}
		return "ERROR: client name .$client_name. not found";
	}
	function filter_is_mandatory($client_filter) {
//		echo "client_filter: .$client_filter.<br>";
		return ($client_filter != ' (TRUE) ');
	}
	function getClientName($client_id) {
		$client_list = $this->getClients();
		for ($i=0; $i<$client_list->rows; ++$i) {
			if ($client_list->data[$i]['client_id'] == $client_id)
				return $client_list->data[$i]['client_name'];
		}
		return "ERROR: client id .$client_id. not found";
	}
	//   $client_nr : fortlaufende Nummer des Menues (nur f�r Post-�bergabe)
						// $selected_client_name : Name des Clients der vorausgew�hlt ist (0 f�r keinen)
											   // $unselect_possible: show [keiner] 
											                      // menue_width: Breite
					                                     						//  spacing
	function get_client_menue_string_base
		($client_menue_nr, $selected_client_name, $unselect_not_possible, $menue_width, $spacing, $gui_user) {
		$language = new Multilanguage($gui_user);
		// setting default values
		$deselect_string = $language->get_text_msg('please_select', 'html'); // '[Bitte ausw&auml;hlen]';
		if ($spacing=='tight') $space_string = 'dist_mit';
		else $space_string = 'dist_single';
		if (!isset ($client_menue_nr)) $client_menue_nr = '';  // keine Menue-Nr --> kein Index f�r Form-Ausgabe
		if ($client_menue_nr === '') $separator='';
		else $separator = '$';
		if (!isset ($selected_client_name)) $selected_client_name = 0;  // nichts ausgew�hlt --> [keiner]
		if (!isset ($menue_width) or $menue_width=='') $menue_width = '';  // keine Breite ausgew�hlt: nix ausgeben
		else $menue_width = 'filter' . $menue_width;
		$client_list	= $this->getClients();
		$client_anzahl	= $client_list->rows;
		$client_menue	= '<SELECT size="1" name="client_id' . $separator . $client_menue_nr . '" class="filter ' . 
			$menue_width . ' ' . $space_string . '">';
		if (!$unselect_not_possible)
			$client_menue .= '<OPTION selected value="">' . $deselect_string . '</OPTION>'; // default: nix ausgew�hlt
		for ($i = 0; $i < $client_anzahl; ++ $i) {
			$client_id = $client_list->data[$i]['client_id'];
			$client_name = $client_list->data[$i]['client_name'];  
			if ($selected_client_name and $client_name == $selected_client_name) {
				$client_menue .= '<OPTION selected value="'.$client_id.'">'.$client_name.'</OPTION>';
			} else {
				$client_menue .= '<OPTION value="'.$client_id.'">'.$client_name.'</OPTION>';
			}
		}
		$client_menue .= '</SELECT>';
		return $client_menue;
	}
	function get_simple_client_menue_string($lock_client, $gui_user) {
		return $this->get_client_menue_string_base('', 0, $lock_client, 210, 'normal spacing', $gui_user);
	}	
	function get_client_menue_string_tight($client_menue_nr, $selected, $lock_client, $gui_user) {
		return $this->get_client_menue_string_base($client_menue_nr, $selected,$lock_client,'','tight', $gui_user);
	}
	function get_client_menue_string($client_menue_nr, $selected, $lock_client, $gui_user) {
		return $this->get_client_menue_string_base($client_menue_nr, $selected,$lock_client,'','normal_spacing', $gui_user);
	}
}

class ClientNetList extends ClientList {
	var $client_net_list;
	var $client_net_ar;
	var $error;
	
	function __construct($filter, $db_connection, $management_filter) {
		$this->error = new PEAR();
		if ($this->error->isError($db_connection))
		$this->error->raiseError("F-RCF: Connection not initialized. " . $db_connection->getMessage());
		$client_sql_code = "SELECT * FROM client LEFT JOIN client_network USING (client_id)";
		if (isset($filter->client_filter_expr)) $client_sql_code .= " WHERE " . $filter->client_filter_expr;
		$client_sql_code .= " ORDER BY client_name, client_net_ip";
		//		echo "client_sql_code: $client_sql_code<br>";
		$this->client_net_list = $db_connection->fworch_db_query ($client_sql_code);
		$this->client_net_ar = array();
		for ($zi = 0; $zi < $this->client_net_list->rows; ++ $zi) $this->client_net_ar[] = $this->client_net_list->data[$zi]['client_net_ip'];
		if ($this->error->isError($this->client_net_list)) $this->error->raiseError($this->client_net_list->getMessage());
	}
	function getClientNetworks() {
		return $this->client_net_list;
	}
	function filter_is_mandatory($client_filter) {
		//		echo "client_filter: .$client_filter.<br>";
		return ($client_filter != ' (TRUE) ');
	}
}

?>