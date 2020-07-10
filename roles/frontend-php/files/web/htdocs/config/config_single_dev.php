<?php
// $Id: config_single_dev.php,v 1.1.2.6 2012-04-30 17:21:13 tim Exp $
// $Source: /home/cvs/iso/package/web/htdocs/config/Attic/config_single_dev.php,v $
	$stamm = "/";
	$page = "config";
	require_once ("check_privs.php");
	require_once ('multi-language.php');
	require_once ("db-input.php");
	$cleaner = new DbInput();  // for clean-function
	setlocale(LC_CTYPE, "de_DE.UTF-8");
	$request = $cleaner->clean_structure($_REQUEST);
	$session = $cleaner->clean_structure($_SESSION);
	
	$language = new Multilanguage($session["dbuser"]);
	if (!$allowedToConfigureDevices) header("Location: " . $stamm . "config/configuration.php");
?>
<!DOCTYPE HTML PUBLIC "-//W3C//DTD HTML 4.01 Transitional//EN">
<html>
<head>
<meta http-equiv="Content-Type" content="text/html; charset=UTF-8">
<title>ITSecOrg Change Device Config</title>
<script type="text/javascript" src="<?php echo $stamm ?>js/client.js"></script>
<script type="text/javascript" src="<?php echo $stamm ?>js/script.js"></script>
<link rel="stylesheet" type="text/css" href="<?php echo $stamm ?>css/firewall.css">
<script language="javascript" type="text/javascript">
	if(is_ie) document.write("<link rel='stylesheet' type='text/css' href='<?php echo $stamm ?>css/firewall_ie.css'>");

	function SubmitForm(Aktion,DevId) {
		document.forms.device_form.aktion.value		= Aktion;
		document.forms.device_form.target			= "_top";
		document.forms.device_form.dev_id.value		= DevId;			
		document.forms.device_form.action			= "config_system.php";
	}
</script>
</head>

<body class="iframe">
<div id="device_config">
	<?php
		require_once ("db-base.php");
		require_once ("db-config.php");
		require_once ("db-div.php"); // ManagementList
		require_once ("display-table.php");
		require_once ("display_menus.php");
		$format = 'html';
		$size = 70; // size of text input field
		$db_connection			= new DbConnection(new DbConfig($session["dbuser"], $session["dbpw"]));
		$devtyplist				= new DevTypList($db_connection);
		$display_dev_types		= new DisplayDevTypes($devtyplist->dev_typ_list);
		$mgm_selection_name		= 'dev_mgm_id';
		$dev_typ_selection_name	= 'device_type_id';
		$aktion = $request['aktion'];
		if (!isset($aktion)) $aktion = 'cancel';
		$ergebnis	= '';
		$dev_id = ''; $dev_name = ''; $dev_mgm_name = ''; $dev_mgm_id = ''; $dev_rulebase = '';
		$device_type_id = ''; $dev_do_import = 0; $dev_created = ''; $dev_updated = ''; $dev_hide_in_gui = '';
					
		if ($aktion != 'new_dev') { // device wird nicht neu angelegt
			$input_fehler = $request['input_fehler'];
			$dev_id = $request['dev_id'];
			if (!isset($dev_id) or $dev_id=='') {
				$device_type_id		= $request['device_type_id'];
				$dev_name			= $request['dev_name'];
				$dev_mgm_id			= $request["$mgm_selection_name"];
				$dev_rulebase		= $request['dev_rulebase'];
				$dev_do_import		= $request['import_active'];
				$dev_hide_in_gui	= $request['dev_hide_in_gui'];
			} else {				
				$device = new Device($db_connection, $dev_id);
				$dev_name = $device->getDevName();
				$dev_mgm_name = $device->getDevMgmName();
				$dev_mgm_id = $device->getDevMgmId();
				$dev_rulebase = $device->getDevRulebase();
				$device_type_id = $device->getDevTypId();
				$dev_do_import = $device->getDevDoImport();
				$dev_hide_in_gui = $device->getDevHideInGui();
				$dev_created = $device->getDevCreated();
				$dev_updated = $device->getDevLastUpdated();
			}
		}
		
		// now generate form for device editing
			$dev_table			= new DisplayTable('Device', $headers = array ('Device', $dev_id));
			$mgm_system_list	= new ManagementList(new RuleChangesFilter($request, $session, 'report'), $db_connection, $management_filter = 'TRUE');
			$display_systems	= new DisplaySystems($mgm_system_list->GetSystems());
			
			if (isset($input_fehler) and $input_fehler!='') {
				$ergebnis = "<b>Fehler, Speichern nicht erfolgreich: $input_fehler</b><br>";
			}
			if ($aktion == 'display' or $aktion == 'cancel' or ($aktion == 'save' and !$input_fehler)) {
				$nurlesen = 'readonly';
				$select_disabled = 'disabled';
			} else {
				$nurlesen = '';
				$select_disabled = '';
			}
			$form = '<FORM id="device_form" name="device_form" method="POST" target="_top">';
			$form .= $dev_table->displayTableOpen($format) . $dev_table->displayTableHeaders($format);
			$form .= '<input type="hidden" name="dev_id" value="' . $dev_id . '">';
			$form .= '<input type="hidden" name="aktion" value="">';
			$form .= '<input type="hidden" name="mgm_or_dev" value="dev">';
			$form .= $dev_table->displayRowSimple($format) . $dev_table->displayColumn('Device',$format) . $dev_table->displayColumn('<input type="text" name="dev_name" value="' .
				$dev_name . '" ' . $nurlesen . ' size="' . $size . '">',$format);
			$form .= $dev_table->displayRowSimple($format) . $dev_table->displayColumn('Management',$format) .
				$dev_table->displayColumn($display_systems->getManagementSelection($mgm_selection_name, $dev_mgm_id, $select_disabled),$format);
			$form .= $dev_table->displayRowSimple($format) . $dev_table->displayColumn($language->get_text_msg('rulebase_name', 'html'), $format) . $dev_table->displayColumn('<input type="text" name="dev_rulebase" ' .
				'alt="Name des Regelwerks (bei Netscreen immer ns_sys_config, bei phion der Pfad der fwrule.active-Datei relativ zu /opt/phion/rangetree/configroot/)" value="' .
				$dev_rulebase . '" ' . $nurlesen . ' size="' . $size . '">',$format);
			$form .= $dev_table->displayRowSimple($format) . $dev_table->displayColumn($language->get_text_msg('device_type', 'html'), $format) .
				$dev_table->displayColumn($display_dev_types->getDevTypSelection($dev_typ_selection_name, $selected_dev_typ_id = $device_type_id, $select_disabled),$format);
			$form .= $dev_table->displayRowSimple($format) . $dev_table->displayColumn($language->get_text_msg('import_active', 'html'), $format) . $dev_table->displayColumn('<SELECT ' . $select_disabled . ' name="import_active">' .
				'<OPTION ' . (($dev_do_import) ? 'selected' : '') . ' value="1">' . $language->get_text_msg('yes', 'html'). '</OPTION>' .
				'<OPTION ' . (($dev_do_import) ? '' : 'selected') . ' value="0">' . $language->get_text_msg('no', 'html'). '</OPTION>',$format);
			$form .= $dev_table->displayRowSimple($format) . $dev_table->displayColumn($language->get_text_msg('dev_hide_in_gui', 'html'), $format) . $dev_table->displayColumn('<SELECT ' . $select_disabled . ' name="dev_hide_in_gui">' .
				'<OPTION ' . (($dev_hide_in_gui) ? 'selected' : '') . ' value="1">' . $language->get_text_msg('yes', 'html'). '</OPTION>' .
				'<OPTION ' . (($dev_hide_in_gui) ? '' : 'selected') . ' value="0">' . $language->get_text_msg('no', 'html'). '</OPTION>',$format);
			$form .= $dev_table->displayRowSimple($format) . $dev_table->displayColumn($language->get_text_msg('device_created', 'html'), $format) . $dev_table->displayColumn('<input type="text" value="' .
				$dev_created . '" readonly size="' . $size . '">',$format);
			$form .= $dev_table->displayRowSimple($format) . $dev_table->displayColumn($language->get_text_msg('device_last_changed', 'html'), $format) . $dev_table->displayColumn('<input type="text" value="' .
				$dev_updated . '" readonly size="' . $size . '">',$format);
			$form .= $dev_table->displayTableClose($format);
			$form .= '</tr>' . $dev_table->displayTableClose($format);
			$form .= '<table><tr><td>&nbsp;</td>';
			if ($aktion=='cancel' or $aktion == 'display' or ($aktion == 'save' and !$input_fehler))
				$form .= '<td><input type="submit" value="' . $language->get_text_msg('change', 'html') . '" class="button" style="margin-right:15px;" ' .
					' onClick="javascript:SubmitForm(\'change\', \'' . $dev_id . '\');"/></td>';
			if ($aktion == 'change' or $aktion == 'new_dev' or ($aktion == 'save' and $input_fehler))
				$form .= '<td><input type="submit" value="' . $language->get_text_msg('save', 'html') .'" class="button" style="margin-right:15px;" ' .
							' onClick="javascript:SubmitForm(\'save\',\'' . $dev_id . '\');"/></td>' .
						'<td>&nbsp;</td>' .
						'<td><input type="submit" value="' . $language->get_text_msg('cancel', 'html') . '" class="button" style="margin-right:15px;" ' .
							' onClick="javascript:SubmitForm(\'cancel\', \'' . $dev_id . '\');"/></td>';
			$form .= '</tr></table></FORM>';
		// end of form generation, output form:
		echo "&nbsp;<br>$ergebnis<br><br>$form";
	?>
</div>
</body></html>