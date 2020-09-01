<?php
// $Id: config_single_mgm.php,v 1.1.2.8 2013-01-31 21:54:14 tim Exp $
// $Source: /home/cvs/iso/package/web/htdocs/config/Attic/config_single_mgm.php,v $
	$stamm="/";
	$page="config";
	require_once("check_privs.php");
	if (!$allowedToConfigureDevices) { header("Location: ".$stamm."config/configuration.php"); }	
	require_once ('multi-language.php');
	$language = new Multilanguage($_SESSION["dbuser"]);
	require_once ("db-input.php");
	$cleaner = new DbInput();  // for clean-function
	setlocale(LC_CTYPE, "de_DE.UTF-8");
	$request = $cleaner->clean_structure($_REQUEST);
	$session = $cleaner->clean_structure($_SESSION);
	
?>
<!DOCTYPE HTML PUBLIC "-//W3C//DTD HTML 4.01 Transitional//EN">
<html>
<head>
<meta http-equiv="Content-Type" content="text/html; charset=UTF-8">
<title>fworch Change Device Config</title>
<script type="text/javascript" src="<?php echo $stamm ?>js/browser.js"></script>
<script type="text/javascript" src="<?php echo $stamm ?>js/script.js"></script>
<link rel="stylesheet" type="text/css" href="<?php echo $stamm ?>css/firewall.css">
<script language="javascript" type="text/javascript">
	if(is_ie) document.write("<link rel='stylesheet' type='text/css' href='<?php echo $stamm ?>css/firewall_ie.css'>");

	function SubmitForm(Aktion,MgmId) {
		if (Aktion=='cancel') {
			document.forms.configuration.action="configuration.php";
			document.forms.configuration.target="_top";
		} else {
			document.forms.device_form.aktion.value	= Aktion;
			document.forms.device_form.target		= "_top";
			document.forms.device_form.mgm_id.value = MgmId;			
			document.forms.device_form.action		= "config_system.php";
		}
	}	
</script>
</head>

<body class="iframe">
<div id="device_config">
<?php
//	$vars = $request; reset($vars); while (list($key, $val) = each($vars)) { echo "$key => $val<br>"; } reset ($vars);
	require_once ("db-base.php");
	require_once ("db-config.php");
	require_once ("db-div.php");	// ManagementList
	require_once ("display-table.php");
	require_once ("display_menus.php");
	require_once ("db-input.php");

	$format = 'html';
	$size = 70;		// size of text input field
	$db_connection = new DbConnection(new DbConfig($_SESSION["dbuser"],$_SESSION["dbpw"]));
	$devtyplist = new DevTypList($db_connection);
	$display_dev_types = new DisplayDevTypes($devtyplist->dev_typ_list);
	$dev_typ_selection_name = 'device_type_id'; 
	$device_type_id		= 1;
	$nl_sub = 'KerridschRitoern4711';
	$cleaner = new DbInput();  // for clean-function
	$aktion = $cleaner->clean($request['aktion'], 20);
	if (!isset($aktion)) $aktion = 'cancel';
	$ergebnis = '';
	$mgm_id = '';
	$mgm_name			= '';	$mgm_ssh_hostname	= '';
	$mgm_ssh_priv_key	= '';	$mgm_ssh_pub_key	= '';
	$mgm_ssh_port		= '';	$mgm_config_path	= '';
	$mgm_ssh_user		= '';	$mgm_do_import		= false;
	$mgm_created		= '';	$mgm_updated		= '';
	$mgm_hide_in_gui = false;	$mgm_importer_hostname	= '';
	if ($aktion != 'new_mgm') { // management wird nicht neu angelegt
		$input_fehler 		= $request['input_fehler'];
		$mgm_id				= $cleaner->clean($request['mgm_id'], 10);
		if (!isset($mgm_id) or $mgm_id=='') {
			$device_type_id		= $cleaner->clean($request['device_type_id'], 4);
			$dev_do_import		= $cleaner->clean($request['import_active'], 5);
			$mgm_name			= $cleaner->clean($request['mgm_name'], 100);
			$mgm_ssh_hostname	= $cleaner->clean($request['mgm_ssh_hostname'], 200);
			$mgm_importer_hostname	= $cleaner->clean($request['mgm_importer_hostname'], 200);
			$mgm_ssh_user		= $cleaner->clean($request['mgm_ssh_user'], 200);
			$mgm_ssh_pub_key	= $request['mgm_ssh_pub_key'];
			$mgm_ssh_pub_key 	= str_replace($nl_sub, "\n", $mgm_ssh_pub_key);
			$mgm_ssh_priv_key	= $request['mgm_ssh_priv_key'];
			$mgm_ssh_priv_key 	= str_replace($nl_sub, "\n", $mgm_ssh_priv_key);
			$mgm_ssh_port		= $request['mgm_ssh_port'];
			$mgm_config_path	= $request['mgm_config_path'];
			$mgm_hide_in_gui	= $cleaner->clean($request['mgm_hide_in_gui'],5);
		} else { 			
			$management_obj		= new Management($db_connection, $mgm_id);
			$mgm_name			= $management_obj->getMgmName();
			$device_type_id		= $management_obj->getDevTypId();
			$mgm_do_import		= $management_obj->getMgmDoImport();
			$mgm_created		= $management_obj->getMgmCreated();
			$mgm_updated		= $management_obj->getMgmLastUpdated();
			$mgm_ssh_user		= $management_obj->getMgmSshUser();
			$mgm_ssh_hostname	= $management_obj->getMgmSshHostname();
			$mgm_importer_hostname	= $management_obj->getMgmImporterHostname();
			$mgm_ssh_pub_key	= $management_obj->getMgmSshPubKey();
			$mgm_ssh_priv_key	= $management_obj->getMgmSshPrivKey();
			$mgm_ssh_port		= $management_obj->getMgmSshPort();
			$mgm_config_path	= $management_obj->getMgmConfigPath();
			$mgm_hide_in_gui	= $management_obj->getMgmHideInGui();
		}
	}

	if ($aktion == 'cancel' or $aktion=='display' or ($aktion=='save' and !$input_fehler))
		{ $nurlesen = 'readonly'; $select_disabled = 'disabled'; }
	else
		{ $nurlesen = ''; $select_disabled = ''; }
	if (isset($input_fehler) and $input_fehler!='') {
		$ergebnis = "<b>Fehler: $input_fehler</b><br>";
	}
	
	$dev_table = new DisplayTable('Management', $headers = array('Management', $mgm_id));
	$form = '<FORM id="device_form" name="device_form" method="POST" target="_self">';
	$form .= $dev_table->displayTableOpen($format) . $dev_table->displayTableHeaders($format);
	$form .= '<input type="hidden" name="aktion" value="">';
	$form .= '<input type="hidden" name="mgm_id" value="' . $mgm_id . '">';
	$form .= '<input type="hidden" name="mgm_or_dev" value="mgm">';
	$form .= $dev_table->displayRowSimple($format) . $dev_table->displayColumn('Management-Name',$format) . $dev_table->displayColumn('<input type="text" name="mgm_name" value="' .
			$mgm_name. '" ' . $nurlesen . ' size="'  . $size . '">',$format);
	$form .= $dev_table->displayRowSimple($format) . $dev_table->displayColumn('ssh hostname',$format) . $dev_table->displayColumn('<input type="text" name="mgm_ssh_hostname" value="' .
			$mgm_ssh_hostname . '" ' . $nurlesen .  ' size="'  . $size . '">',$format);
	$form .= $dev_table->displayRowSimple($format) . $dev_table->displayColumn('ssh port',$format) . $dev_table->displayColumn('<input type="text" name="mgm_ssh_port" value="' .
			$mgm_ssh_port . '" ' . $nurlesen .  ' size="'  . $size . '">',$format);
	$form .= $dev_table->displayRowSimple($format) . $dev_table->displayColumn('config path',$format) . $dev_table->displayColumn('<input type="text" name="mgm_config_path" value="' .
			$mgm_config_path . '" ' . $nurlesen .  ' size="'  . $size . '">',$format);
	$form .= $dev_table->displayRowSimple($format) . $dev_table->displayColumn('ssh user',$format) . $dev_table->displayColumn('<input type="text" name="mgm_ssh_user" value="' .
			$mgm_ssh_user . '" ' . $nurlesen .  ' size="'  . $size . '">',$format);
	$form .= $dev_table->displayRowSimple($format) . $dev_table->displayColumn('ssh private key',$format) .$dev_table->displayColumnNoHtmlTags('<textarea cols="' . $size . 
			'" rows="10" name="mgm_ssh_priv_key" ' . $nurlesen . '>' . $mgm_ssh_priv_key . '</textarea>');
	$form .= $dev_table->displayRowSimple($format) . $dev_table->displayColumn('ssh public key',$format) . $dev_table->displayColumnNoHtmlTags('<textarea cols="' . $size . 
			'" rows="3" name="mgm_ssh_pub_key" ' . $nurlesen . '>' . $mgm_ssh_pub_key . '</textarea>');
	$form .= $dev_table->displayRowSimple($format) . $dev_table->displayColumn($language->get_text_msg('device_type', 'html'),$format) . 
		$dev_table->displayColumn($display_dev_types->getDevTypSelection($dev_typ_selection_name, $selected_dev_typ_id = $device_type_id, $select_disabled),$format);
	$form .= $dev_table->displayRowSimple($format) . $dev_table->displayColumn('importer hostname',$format) . $dev_table->displayColumn('<input type="text" name="mgm_importer_hostname" value="' .
			$mgm_importer_hostname . '" ' . $nurlesen .  ' size="'  . $size . '">',$format);
	$form .= $dev_table->displayRowSimple($format) . $dev_table->displayColumn($language->get_text_msg('import_active', 'html'),$format) . $dev_table->displayColumn('<SELECT ' . $select_disabled  . ' name="import_active">' .
			'<OPTION ' . (($mgm_do_import)?'selected':'') . ' value="1">' . $language->get_text_msg('yes', 'html') . '</OPTION>' .
			'<OPTION ' . (($mgm_do_import)?'':'selected') . ' value="0">' . $language->get_text_msg('no', 'html') . '</OPTION>',$format);
	$form .= $dev_table->displayRowSimple($format) . $dev_table->displayColumn($language->get_text_msg('mgm_hide_in_gui', 'html'),$format) . $dev_table->displayColumn('<SELECT ' . $select_disabled  . ' name="mgm_hide_in_gui">' .
			'<OPTION ' . (($mgm_hide_in_gui)?'selected':'') . ' value="1">' . $language->get_text_msg('yes', 'html') . '</OPTION>' .
			'<OPTION ' . (($mgm_hide_in_gui)?'':'selected') . ' value="0">' . $language->get_text_msg('no', 'html') . '</OPTION>',$format);
	$form .= $dev_table->displayRowSimple($format) . $dev_table->displayColumn($language->get_text_msg('management_created', 'html'),$format) . $dev_table->displayColumn('<input type="text" value="' .
			$mgm_created . '" readonly size="'  . $size . '">', $format);
	$form .= $dev_table->displayRowSimple($format) . $dev_table->displayColumn($language->get_text_msg('management_last_changed', 'html'),$format) . $dev_table->displayColumn('<input type="text" value="' .
			$mgm_updated . '" readonly size="'  . $size . '">', $format);
	$form .= $dev_table->displayTableClose($format);
	$form .= '<table><tr><td>&nbsp;</td>';
	if ($aktion == 'cancel' or $aktion == '' or $aktion == 'display' or ($aktion == 'save' and !$input_fehler))
		$form .= '<td><input type="submit" value="' . $language->get_text_msg('change', 'html') . '" class="button" style="margin-right:15px;" ' .
			' onClick="javascript:SubmitForm(\'change\', \'' . $mgm_id . '\');"/></td>';
	if ($aktion == 'change' or $aktion == 'new_mgm' or ($aktion == 'save' and $input_fehler))
		$form .= '<td><input type="submit" value="' . $language->get_text_msg('save', 'html') . '" class="button" style="margin-right:15px;" ' .
					' onClick="javascript:SubmitForm(\'save\', \'' . $mgm_id . '\');"/></td>
				<td>&nbsp;</td>
				<td><input type="submit" value="' . $language->get_text_msg('cancel', 'html') . '" class="button" style="margin-right:15px;" ' .
					' onClick="javascript:SubmitForm(\'cancel\', \'' . $mgm_id . '\', \');"/></td>';
	$form .= '</tr></table></FORM>';
	echo "&nbsp;<br>$ergebnis<br><br>$form"; 
?>
</div>
</body></html>