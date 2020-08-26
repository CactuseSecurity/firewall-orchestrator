<?php

/*
 * $Id: display_menus.php,v 1.1.2.9 2012-04-30 17:21:16 tim Exp $
 * $Source: /home/cvs/iso/package/web/include/Attic/display_menus.php,v $
 * Created on 03.09.2006
 *
 */
 
require_once ("db-div.php");
require_once ("multi-language.php");

//require_once ("operating-system.php"); // only for log debugging


class DisplaytenantNet {

	var $tenant_net_list;

	function __construct($tenant_net_list) {
		$this->tenant_net_list = $tenant_net_list;
	}

	function show_tenant_network_menue() {
		$tenant_net_list = $this->tenant_net_list;
		$anz = $tenant_net_list->rows;
		$old_tenant = NULL;
		$old_net = NULL;
		$menu = '<ul>';

		for ($zi=0; $zi<$anz; $zi++) {
			$tenant_name = $tenant_net_list->data[$zi]['tenant_name'];
			$tenant_id = $tenant_net_list->data[$zi]['tenant_id'];
			$tenant_net_ip = $tenant_net_list->data[$zi]['tenant_net_ip'];
			$tenant_net_id = $tenant_net_list->data[$zi]['tenant_net_id'];
			$tenant_id = $tenant_net_list->data[$zi]['tenant_id'];
			if ($old_tenant == NULL or $old_tenant != $tenant_id) {
				if (!($old_tenant == NULL)) {  // nicht erstes Element
					if (!($old_net == NULL)) $menu .= "</ul>";	//vorheriges netz war gesetzt
					$menu .= "</li>";
				}
				$menu .= "<li><a href=\"javascript:changetenant('$tenant_name', $tenant_id);\" class=\"baum\">$tenant_name</a>"; 
				if (isset($tenant_net_id) and !($tenant_net_id == '')) $menu .= '<ul>';
			}
			if (isset($tenant_net_id) and !($tenant_net_id == ''))   // Netze zu tenant ausgeben
				$menu .= "<li><a href=\"javascript:changetenantNet('$tenant_name', '$tenant_id', '$tenant_net_ip', $tenant_net_id);\" class=\"baum\">$tenant_net_ip</a></li>"; 
			$old_tenant = $tenant_id;
			if (isset($tenant_net_id)) $old_net = $tenant_net_id;
			else $old_net = NULL;
		} 
		if (isset($tenant_net_id) and !($tenant_net_id == '')) 
			$menu .= '</ul>';
		$menu .= '</li></ul>';
		echo $menu;
	}
}

function getManagementSelection($name, $selected_mgm_id, $disable_string) {
	$syslist = $this->syslist;
	$anz = $syslist->rows;
	$form = "<SELECT name=\"$name\" $disable_string>";
	$old_mgm = '';
	for ($zi=0; $zi<$anz; $zi++) {
		$mgm_id = $syslist->data[$zi]['mgm_id'];
		$management = $syslist->data[$zi]['mgm_name'] . " (id: $mgm_id)";
			
		if ($management != $old_mgm) {
			if ($mgm_id == $selected_mgm_id) $selected = 'selected'; else $selected = '';
			$form .= "<OPTION $selected value=\"$mgm_id\">$management</OPTION>";
		}
		$old_mgm = $management;
	} // for
	$form .= '</SELECT>';
	return $form;
}

class DisplaySystems {

	var $syslist;

	function DisplaySystems($syslist) {
		$this->syslist = $syslist;
	}

	function show_all_systems($function, $mgm_is_linked, $select_all_systems, $show_only_managements, $force_dev_links,
			$simplify_combined_single_device) {
		$language = new Multilanguage($_SESSION["dbuser"]);
		$syslist = $this->syslist;
		$anz = $syslist->rows;
	 	$old_management = NULL;
		$is_combined_mgmt_and_dev = false;
		$was_combined_mgmt_and_dev = false;
		if (!isset($simplify_combined_single_device)) $simplify_combined_single_device = true;
		if (!isset($force_dev_links)) $force_dev_links = false;
		$menu = '<ul>'; 
		if ($select_all_systems) {
			$menu .= '<div class="nomargin" id="select_all_devices" style="display:none;">';
			if ($show_only_managements and !$force_dev_links)
				$menu .= "<li><a href=\"javascript:${function}Mgmt('" . $language->get_text_msg('all_systems', 'html') .
					"','NULL');\" class=\"baum\">" . $language->get_text_msg('all_systems', 'html') . "</a></li>";
			else
				$menu .= "<li><a href=\"javascript:${function}Dev('" . $language->get_text_msg('all', 'html') . 
					"','" . $language->get_text_msg('systems', 'html') . "','NULL');\" class=\"baum\">" . 
					$language->get_text_msg('all_systems', 'html') . "</a></li>";
			$menu .= '</div>';
		}
		for ($zi=0; $zi<$anz; $zi++) {
			$dname = $syslist->data[$zi]['dev_name'];
			$device = $syslist->data[$zi]['dev_id'];
			$management = $syslist->data[$zi]['mgm_name'];
			$mgm_id = $syslist->data[$zi]['mgm_id'];
			if ($simplify_combined_single_device) {
				$was_combined_mgmt_and_dev = $is_combined_mgmt_and_dev;
				$is_combined_mgmt_and_dev = false;
				if ($dname == $management and $old_management != $management) { 
					if ($zi+1 == $anz) $is_combined_mgmt_and_dev = true;
					else {
						$next_mgm = $syslist->data[$zi+1]['mgm_name'];
						if ($next_mgm != $management) $is_combined_mgmt_and_dev = true;
					}	
				}					
			}
			if ($old_management != $management and $old_management != NULL and !$was_combined_mgmt_and_dev) {
				if (!$show_only_managements) $menu .= "</ul></li>";  //Wenn neues Management, erst altes abschliessen
			}
			if ($is_combined_mgmt_and_dev) {
				if ($force_dev_links)
					$menu .= "<li><a href=\"javascript:${function}Dev('$management','$dname','$device');\" class=\"baum\">$dname</a></li>";   //Dev=Management ausgeben
				else
					$menu .= "<li><a href=\"javascript:${function}Mgmt('$management',$mgm_id);\" class=\"baum\">$management</a></li>";  //Dev=Management ausgeben
			} else {
				if ($old_management == NULL or $old_management != $management) { 
					if ($mgm_is_linked)	$menu .= "<li><a href=\"javascript:${function}Mgmt('$management',$mgm_id);\" class=\"baum\">$management</a>";  //Neues Management anfangen
					else $menu .= "<li>$management";  //Neues Management anfangen
					if (!$show_only_managements) $menu .= '<ul>';
				}
				if (!$show_only_managements)
					$menu .= "<li><a href=\"javascript:${function}Dev('$management','$dname','$device');\" class=\"baum\">$dname</a></li>";  //Devices zu Management ausgeben
			}
		 	$old_management = $management;
		} // for 		
		$menu .= '</ul></li></ul>';
		echo $menu;
	}
	function getManagementSelection($name, $selected_mgm_id, $disable_string) {
		$syslist = $this->syslist;
		$anz = $syslist->rows;
		$form = "<SELECT name=\"$name\" $disable_string>";
		$old_mgm = '';
		for ($zi=0; $zi<$anz; $zi++) {
			$mgm_id = $syslist->data[$zi]['mgm_id'];
			$management = $syslist->data[$zi]['mgm_name'] . " (id: $mgm_id)";
			
			if ($management != $old_mgm) {
				if ($mgm_id == $selected_mgm_id) $selected = 'selected'; else $selected = '';
				$form .= "<OPTION $selected value=\"$mgm_id\">$management</OPTION>";
			}
			$old_mgm = $management;
		} // for 		
		$form .= '</SELECT>';
		return $form;
	}
}

class DisplayReports {

	var $reportlist;

	function DisplayReports($complete_replist) {
		$this->reportlist = $complete_replist;
	}

	function show_report_menu($filtered_replist) {
		$reportlist = $this->reportlist;
		$anz = $reportlist->rows;		
//		$log = new LogConnection();
		$filtered_report_array = explode(',', $filtered_replist);
//		for ($i=0; $i<count($filtered_report_array); ++$i) $log->log_debug("show_report_menu: i=0, filtered_report_array[$i]=". $filtered_report_array[$i]);
		if ($anz>0) {
//			$log->log_debug("anz=$anz, filtered_replist=$filtered_replist.");
			echo "<b class=\"headmenu\">Report</b><br>" .
				 "<SELECT name=\"reporttyp\" style=\"color:#333;font-size:10px;margin-top:4px;\" onChange=\"SwitchReportType();\">\n";
			for ($i=0; $i<$anz; $i++) {
				$id = $reportlist->data[$i]['report_typ_id'];
//				$log->log_debug("show_report_menu: i=$i, id=$id");
				if (array_search($id, $filtered_report_array)===false) {
				} else {
					$rt_name = $reportlist->data[$i]['report_typ_name'];
					$rt_id = $reportlist->data[$i]['report_typ_id'];
					echo "<OPTION value=\"$rt_id\" >$rt_name</OPTION>\n";
				}
			}
			echo "</SELECT><br><br>";
		}
	}
}

class DisplayManagements {

	var $management_list;

	function DisplayManagements($mgmlist) {
		$this->management_list = $mgmlist;
	}

	function show_management_menu($selected_id, $disabled) {
		$mgmlist = $this->management_list;
		$anz = $mgmlist->rows;		
		if ($disabled) $disabled = $disabled; else $disabled = '';
		if ($anz>0) {
			echo '<SELECT name="mgmlist" $disabled style="color:#333;font-size:10px;margin-top:4px;">\n';
			for ($i=0; $i<$anz; ++$i) {
				$mgm_name = $mgmlist->data[$i][mgm_name];
				$mgm_id = $mgmlist->data[$i][mgm_id];
				if ($slected_id = $mgm_id) $selected = 'selected'; else $selected = '';
				echo "<OPTION value=\"$mgm_id\" $selected>$mgm_name</OPTION>\n";
			}
			echo "</SELECT>";
		}
	}
}

class DisplayIsoadmins {
	var $isoadmin_list;

	function DisplayIsoadmins($userlist) {
		$this->isoadmin_list = $userlist;
	}

	function show_users() {
		$admlist = $this->isoadmin_list;
		$anz = $admlist->rows;
		$menu = '<ul>'; 
		for ($zi=0; $zi<$anz; $zi++) {
			$adm_name	= $admlist->data[$zi]['isoadmin_username'];
			$adm_id		= $admlist->data[$zi]['isoadmin_id'];
			$menu		.= "<li><a href=\"javascript:changeIsoadmin('$adm_name',$adm_id);\" class=\"baum\">$adm_name</a></li>"; 
		} // for 		
		$menu .= '</ul></li></ul>';
		echo $menu;
	}
}
?>
