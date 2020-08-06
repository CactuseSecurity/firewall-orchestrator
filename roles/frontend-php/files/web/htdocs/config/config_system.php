<?php
// $Id: config_system.php,v 1.1.2.12 2013-02-04 20:29:27 tim Exp $
// $Source: /home/cvs/iso/package/web/htdocs/config/Attic/config_system.php,v $
    header("Expires: Mon, 26 Jul 1997 05:00:00 GMT");                  // Date in the past   
    header('Last-Modified: '.gmdate('D, d M Y H:i:s') . ' GMT');
    header('Cache-Control: no-store, no-cache, must-revalidate');     // HTTP/1.1
    header('Cache-Control: pre-check=0, post-check=0, max-age=0');    // HTTP/1.1
    header ("Pragma: no-cache");
    header("Expires: 0");
    $stamm="/";	$page="config";
   	require_once ("db-input.php");
	setlocale(LC_CTYPE, "en_US.UTF-8");
	if (!isset($_SESSION)) session_start();
		
	$cleaner = new DbInput();  // for clean-function
	setlocale(LC_CTYPE, "de_DE.UTF-8");
	$request = $cleaner->clean_structure($_REQUEST);
	$session = $cleaner->clean_structure($_SESSION);
    
	require_once("check_privs.php");
	if (!$allowedToConfigureDevices) { header("Location: ".$stamm."config/configuration.php"); }
	require_once ('multi-language.php');
	$language = new Multilanguage($session["dbuser"]);
?>
<!DOCTYPE HTML PUBLIC "-//W3C//DTD HTML 4.01 Transitional//EN">
<html>
<head>
	<meta http-equiv="Content-Type" content="text/html; charset=UTF-8">
	<title>ITSecOrg Configuration</title>
	<script type="text/javascript" src="<?php echo $stamm ?>js/client.js"></script>
	<script type="text/javascript" src="<?php echo $stamm ?>js/script.js"></script>
	<link rel="stylesheet" type="text/css" href="<?php echo $stamm ?>css/firewall.css">
	<script language="javascript" type="text/javascript">
		if(is_ie) document.write("<link rel='stylesheet' type='text/css' href='<?php echo $stamm ?>css/firewall_ie.css'>");
		
		function SubmitDeviceChangeInformation (Aktion, dev_id, dev_name, dev_mgm_id, dev_rulebase, dev_typ, dev_do_import, dev_hide_in_gui, input_fehler) {
			document.forms.configuration.dev_id.value=dev_id;			
			document.forms.configuration.dev_name.value=dev_name;			
			document.forms.configuration.dev_mgm_id.value=dev_mgm_id;			
			document.forms.configuration.device_type_id.value=dev_typ;			
			document.forms.configuration.dev_rulebase.value=dev_rulebase;
			document.forms.configuration.dev_hide_in_gui.value=dev_hide_in_gui;			
			document.forms.configuration.import_active.value=dev_do_import;			
			document.forms.configuration.aktion.value = Aktion;
			if (Aktion=='change' || Aktion=='save') {
				document.forms.configuration.mgm_id.value="";
				document.forms.configuration.input_fehler.value = input_fehler;
				document.forms.configuration.target="Change_Config_Frame";
				document.forms.configuration.method="post";
				document.forms.configuration.action="config_single_dev.php";
				document.forms.configuration.submit();
			}
		}
		function SubmitManagementChangeInformation (Aktion, mgm_id, mgm_name, ssh_username, ssh_hostname, importer_hostname, ssh_priv_key, ssh_pub_key, dev_typ, mgm_do_import, ssh_port, config_path, mgm_hide_in_gui, input_fehler) {
			document.forms.configuration.mgm_id.value=mgm_id;			
			document.forms.configuration.mgm_name.value=mgm_name;			
			document.forms.configuration.mgm_ssh_hostname.value=ssh_hostname;			
			document.forms.configuration.mgm_importer_hostname.value=importer_hostname;			
			document.forms.configuration.mgm_ssh_user.value=ssh_username;			
			document.forms.configuration.mgm_ssh_pub_key.value=ssh_pub_key;
			document.forms.configuration.mgm_ssh_priv_key.value=ssh_priv_key;
			document.forms.configuration.mgm_ssh_port.value=ssh_port;
			document.forms.configuration.mgm_config_path.value=config_path;
			document.forms.configuration.device_type_id.value=dev_typ;
			document.forms.configuration.import_active.value=mgm_do_import;			
			document.forms.configuration.mgm_hide_in_gui.value=mgm_hide_in_gui;			
			document.forms.configuration.aktion.value = Aktion;
			if (Aktion=='change' || Aktion=='save') {
				document.forms.configuration.dev_id.value="";
				document.forms.configuration.input_fehler.value = input_fehler;
				document.forms.configuration.target="Change_Config_Frame";
				document.forms.configuration.method="post";
				document.forms.configuration.action="config_single_mgm.php";
				document.forms.configuration.submit();
			}
		}		
		function changeDev(ManSys,Dev,DevId) {
			document.getElementById("headlineSys").innerHTML = ManSys + " / " + Dev;
			document.forms.configuration.dev_id.value=DevId;
			document.forms.configuration.mgm_id.value="";
			document.forms.configuration.mgm_or_dev.value='dev';
			SubmitForm('display');
			document.forms.configuration.submit();
		}
		function changeMgmt(ManSys,MgmId) {
			document.getElementById("headlineSys").innerHTML = "Management " + ManSys;
			document.forms.configuration.mgm_id.value=MgmId;
			document.forms.configuration.mgm_or_dev.value='mgm';
			SubmitForm('display');
			document.forms.configuration.submit();
		}
		
		function SubmitForm(Aktion) {
			document.forms.configuration.aktion.value = Aktion;
			document.forms.configuration.method="post";
			if (!document.forms.configuration.dev_id.value && !document.forms.configuration.mgm_id.value &&  !(Aktion=='new_dev' || Aktion=='new_mgm' || Aktion=='save')) {
				document.forms.configuration.action="/no_device.php";
				document.forms.configuration.target="Change_Config_Frame";
			} else {
				document.forms.configuration.aktion.value = Aktion;
				if (Aktion=='save') {
					document.forms.configuration.device_type_id.value	= top.Change_Config_Frame.document.forms.device_form.device_type_id.value;
					if (top.Change_Config_Frame.document.forms.device_form.mgm_id) {
						document.forms.configuration.mgm_id.value			= top.Change_Config_Frame.document.forms.device_form.mgm_id.value;							
						document.forms.configuration.mgm_name.value			= top.Change_Config_Frame.document.forms.device_form.mgm_name.value;							
						document.forms.configuration.mgm_importer_hostname.value	= top.Change_Config_Frame.document.forms.device_form.mgm_importer_hostname.value;							
						document.forms.configuration.mgm_ssh_port.value		= top.Change_Config_Frame.document.forms.device_form.mgm_ssh_port.value;							
						document.forms.configuration.mgm_ssh_user.value		= top.Change_Config_Frame.document.forms.device_form.mgm_ssh_user.value;							
						document.forms.configuration.mgm_ssh_pub_key.value	= top.Change_Config_Frame.document.forms.device_form.mgm_ssh_pub_key.value;							
						document.forms.configuration.mgm_ssh_priv_key.value	= top.Change_Config_Frame.document.forms.device_form.mgm_ssh_priv_key.value;							
						document.forms.configuration.mgm_config_path.value	= top.Change_Config_Frame.document.forms.device_form.mgm_config_path.value;							
						document.forms.configuration.import_active.value	= top.Change_Config_Frame.document.forms.device_form.import_active.value;
						document.forms.configuration.mgm_hide_in_gui.value	= top.Change_Config_Frame.document.forms.device_form.mgm_hide_in_gui.value;
 						document.forms.configuration.action="config_single_mgm.php";
					} else {
						document.forms.configuration.dev_id.value			= top.Change_Config_Frame.document.forms.device_form.dev_id.value;
						document.forms.configuration.dev_name.value			= top.Change_Config_Frame.document.forms.device_form.dev_name.value;
						document.forms.configuration.dev_mgm_id.value		= top.Change_Config_Frame.document.forms.device_form.dev_mgm_id.value;
						document.forms.configuration.dev_rulebase.value		= top.Change_Config_Frame.document.forms.device_form.dev_rulebase.value;
						document.forms.configuration.import_active.value	= top.Change_Config_Frame.document.forms.device_form.import_active.value;
						document.forms.configuration.dev_hide_in_gui.value	= top.Change_Config_Frame.document.forms.device_form.dev_hide_in_gui.value;
 						document.forms.configuration.action="config_single_dev.php";
					}
				}
				document.forms.configuration.target="Change_Config_Frame";
				if (Aktion=='new_dev' || Aktion=='new_mgm') {
					document.forms.configuration.dev_id.value = '';
					document.forms.configuration.mgm_id.value = '';
					document.getElementById("headlineSys").innerHTML = '';
				}
				if (Aktion=='new_dev' || (Aktion=='display' && document.forms.configuration.dev_id.value)) {
					document.forms.configuration.action="config_single_dev.php";
					document.forms.configuration.mgm_or_dev.value='dev';
				}
				if (Aktion=='new_mgm' || (Aktion=='display' && document.forms.configuration.mgm_id.value)) {
					document.forms.configuration.action="config_single_mgm.php";
					document.forms.configuration.mgm_or_dev.value='mgm';
				}
			}
		}
	</script>
</head>

<body onLoad="changeColor1('n4');">
	<?php
		require_once ("db-base.php");
		require_once ("db-config.php");
		require_once ("display_menus.php");
		require_once ("db-input.php");		

		$e = new PEAR();
		$mgm_selection_name		= 'dev_mgm_id';
		$dev_typ_selection_name	= 'device_type_id';
		$nl_sub = 'KerridschRitoern4711';
		// both
		if (isset($request['aktion'])) $aktion					= $cleaner->clean($request['aktion'], 20);
		if (isset($request['mgm_or_dev'])) $mgm_or_dev			= $cleaner->clean($request['mgm_or_dev'], 5);
		if (isset($request['device_type_id'])) $device_type_id	= $cleaner->clean($request['device_type_id'], 4);
		if (isset($request['import_active'])) $dev_do_import	= $cleaner->clean($request['import_active'], 5);
		// device
		if (isset($request['dev_id'])) $dev_id					= $cleaner->clean($request['dev_id'], 10);
		if (isset($request['dev_name'])) $dev_name				= $cleaner->clean($request['dev_name'], 200);
		if (isset($request['dev_mgm_id'])) $dev_mgm_id			= $cleaner->clean($request['dev_mgm_id'], 10);
		if (isset($request['dev_rulebase'])) $dev_rulebase 		= $cleaner->clean($request['dev_rulebase'], 10000);
		if (isset($request['dev_hide_in_gui'])) $dev_hide_in_gui	= $cleaner->clean($request['dev_hide_in_gui'], 5);
		// management
		if (isset($request['mgm_id'])) $mgm_id					= $cleaner->clean($request['mgm_id'], 10);
		if (isset($request['mgm_name'])) $mgm_name				= $cleaner->clean($request['mgm_name'], 100);
		if (isset($request['mgm_ssh_hostname'])) $mgm_ssh_hostname	= $cleaner->clean($request['mgm_ssh_hostname'], 200);
		if (isset($request['mgm_importer_hostname'])) $mgm_importer_hostname	= $cleaner->clean($request['mgm_importer_hostname'], 200);
		if (isset($request['mgm_ssh_user'])) $mgm_ssh_user		= $cleaner->clean($request['mgm_ssh_user'], 200);
		if (isset($request['mgm_ssh_pub_key'])) $mgm_ssh_pub_key	= $cleaner->clean_allow_linebreak($request['mgm_ssh_pub_key'], 4200);
		if (isset($request['mgm_ssh_priv_key'])) $mgm_ssh_priv_key	= $cleaner->clean_allow_linebreak($request['mgm_ssh_priv_key'],4200);
		if (isset($request['mgm_ssh_port'])) $mgm_ssh_port		= $cleaner->clean($request['mgm_ssh_port'],5);
		if (isset($request['mgm_config_path'])) $mgm_config_path	= $cleaner->clean($request['mgm_config_path'],1000);
		if (isset($request['mgm_hide_in_gui'])) $mgm_hide_in_gui	= $cleaner->clean($request['mgm_hide_in_gui'], 5);
		
		$db_connection			= new DbConnection(new DbConfig($session["dbuser"], $session["dbpw"]));
		$ergebnis				= '';

		if ($aktion == 'save') {
			if ($mgm_or_dev == 'mgm') {
				$mgm_id	= $cleaner->clean($request['mgm_id'], 10);
				if (isset($mgm_id) and $mgm_id<>'') {
					$management_obj		= new Management($db_connection, $mgm_id);
					$mgm_created		= $management_obj->getMgmCreated();
				}
				$mgm_updated		= date('Y-m-d H:i');
				$input_fehler = false;
				if (preg_match("/\s/", $mgm_name)) $input_fehler = 'Management-Name darf keine Leerzeichen enthalten.'; 			
				if (preg_match("/^$/", $mgm_name)) $input_fehler = 'Management-Name darf nicht leer sein.'; 			
				if (preg_match("/^$/", $mgm_ssh_hostname)) $input_fehler = 'ssh Hostname darf nicht leer sein.'; 			
				if (preg_match("/^$/", $mgm_importer_hostname)) $input_fehler = 'Importer Hostname darf nicht leer sein.'; 			
				if (preg_match("/^$/", $mgm_ssh_priv_key)) $input_fehler = 'ssh private key darf nicht leer sein.'; 			
				if (preg_match("/^$/", $mgm_ssh_user)) $input_fehler = 'ssh user darf nicht leer sein.'; 		
				if (!preg_match("/^\d+$/", $mgm_ssh_port)) $input_fehler = 'ssh-Port ist keine Zahl.';
				if (!preg_match("/\/$/", $mgm_config_path) and !preg_match("/^$/", $mgm_config_path)) $input_fehler = 'Config-Pfad muss entweder leer sein oder mit "/" enden.';
				
				// check for existing mgm with same name and same type
				if (!$mgm_id or $mgm_id=='')	{// Aenderung an bestehendem Management
					$mgm_double_sql_code = "SELECT mgm_name, dev_typ_id FROM management WHERE mgm_name='$mgm_name' AND dev_typ_id=$device_type_id";
					$mgm_double = $db_connection->fworch_db_query($mgm_double_sql_code);
					if ($mgm_double->rows) $input_fehler = "Ein Management mit dem Namen $mgm_name vom selben Typ existiert bereits.";
				}				
				if (!$input_fehler) {
					if (isset($mgm_id) and $mgm_id<>'')	// Aenderung an bestehendem Management
						$sql_code = "UPDATE management SET mgm_name='$mgm_name', ssh_hostname='$mgm_ssh_hostname', importer_hostname='$mgm_importer_hostname', ssh_public_key='$mgm_ssh_pub_key', ssh_private_key='$mgm_ssh_priv_key'," .
								" ssh_user='$mgm_ssh_user', ssh_port=$mgm_ssh_port, config_path='$mgm_config_path', dev_typ_id=$device_type_id, " .
								"do_not_import=" . (($dev_do_import)?"FALSE":"TRUE") . ", hide_in_gui=" . (($mgm_hide_in_gui)?"TRUE":"FALSE") . ", mgm_update='$mgm_updated' WHERE mgm_id=$mgm_id";
					else { // neues Mgm anlegen
						$mgm_id_code = "SELECT MAX(mgm_id)+1 AS mgm_id FROM management";
						$next_free_mgm_id = $db_connection->fworch_db_query($mgm_id_code); $next_free_mgm_id_no = $next_free_mgm_id->data[0]['mgm_id'];
						if (!isset($next_free_mgm_id_no)) $next_free_mgm_id_no = 1;
						$sql_code = "INSERT INTO management (mgm_id, mgm_name, dev_typ_id, do_not_import, ssh_hostname, importer_hostname, ssh_user, ssh_public_key, ssh_private_key, ssh_port, config_path, hide_in_gui) " .
								"VALUES ($next_free_mgm_id_no, '$mgm_name', $device_type_id, " . (($dev_do_import)?"FALSE":"TRUE") .
									", '$mgm_ssh_hostname', '$mgm_importer_hostname', '$mgm_ssh_user', '$mgm_ssh_pub_key', '$mgm_ssh_priv_key', $mgm_ssh_port, '$mgm_config_path', " . (($mgm_hide_in_config)?"TRUE":"FALSE") . ") ";					
					}
//					echo "sql_code: $sql_code<br>"; // exit (1);
					$result = $db_connection->fworch_db_query($sql_code);
					if ($e->isError($result)) $input_fehler = 'db-sql-error';
				}
			}
			if ($mgm_or_dev == 'dev') {
				$dev_id = $cleaner->clean($request['dev_id'], 10);
				if (isset ($dev_id) and $dev_id <> '') { $device = new Device($db_connection, $dev_id); $dev_created = $device->getDevCreated(); }
				$dev_updated		= date('Y-m-d H:i');
				
				// check form input
					$input_fehler = false;
					// check for dev_typ == mgm_typ
					$mgm_sql_code = "SELECT dev_typ_manufacturer FROM management LEFT JOIN stm_dev_typ USING (dev_typ_id) WHERE mgm_id=$dev_mgm_id";
					$dev_sql_code = "SELECT dev_typ_manufacturer FROM stm_dev_typ WHERE dev_typ_id=$device_type_id";
					$mgm_manu = $db_connection->fworch_db_query($mgm_sql_code);
					$mgm_manu_name = $mgm_manu->data[0]['dev_typ_manufacturer'];
					$dev_manu = $db_connection->fworch_db_query($dev_sql_code);
					$dev_manu_name = $dev_manu->data[0]['dev_typ_manufacturer'];
					if ($mgm_manu_name != $dev_manu_name) {  // unless dev and mgm are from check point: do not accept different manu strings
						if (!(preg_match("/check/", $mgm_manu_name) and preg_match("/check/", $dev_manu_name)))
							$input_fehler = "Typ des Managements und des Devices passen nicht zusammen ($mgm_manu_name <> $dev_manu_name)!";
					}		
					if (!isset($dev_id) or $dev_id == '') { // Aenderung an bestehendem Management
						// check for existing dev with same name and same type
						$dev_double_sql_code = "SELECT dev_name, dev_typ_id FROM device WHERE dev_name='$dev_name' AND dev_typ_id=$device_type_id";
						$dev_double = $db_connection->fworch_db_query($dev_double_sql_code);
						if ($dev_double->rows) $input_fehler = "Ein Device mit dem Namen $dev_name vom selben Device-Typ existiert bereits.";
					}		
					if ($dev_manu_name == 'phion') {
						if (!preg_match("/^\d+[\_\/].*?active\.fwrule$/", $dev_rulebase))
							$input_fehler = $dev_rulebase . ' ist kein g&uuml;ltiger Policyname.<br>' .
							'Als Rulebase Name bei phion-Devices ist der Pfad zu der Policy auf phionMC relativ zu /opt/phion/rangetree/configroot/ anzugeben.<br>' .
							'Beispiel: 1/border/clusterservers/brd1/services/fw_active.fwrule';
						if (preg_match("/\//", $dev_rulebase))	$dev_rulebase = str_replace("/", "_", $dev_rulebase); // replace / with _ 			
					}
					if (preg_match("/^$/", $dev_name))					$input_fehler = 'Devicename darf nicht leer sein.';
					if (preg_match("/^$/", $dev_rulebase))				$input_fehler = 'Policyname darf nicht leer sein.';
					if (preg_match("/^$/", $dev_name))					$input_fehler = 'Devicename darf nicht leer sein.';
					if (preg_match("/\s/", $dev_name))					$input_fehler = 'Devicename darf keine Leerzeichen enthalten.';
					if ($mgm_manu_name != "Check Point R8x") {
						if (preg_match("/\s/", $dev_rulebase))				$input_fehler = 'Policyname darf keine Leerzeichen enthalten.';
						if (preg_match("/[\/\\\$\(\)\=]/", $dev_rulebase))	$input_fehler = 'Policyname darf keine Sonderzeichen (/,\,$,(,),=) enthalten.';
					}
				// end check form input
				if (!$input_fehler) {
					if (isset ($dev_id) and $dev_id <> '') // Aenderung an bestehendem Device
						$sql_code = "UPDATE device SET dev_name='$dev_name', mgm_id=$dev_mgm_id, dev_rulebase='$dev_rulebase', dev_typ_id=$device_type_id, " .
							"do_not_import=" . (($dev_do_import) ? "FALSE" : "TRUE") . ", hide_in_gui=" . (($dev_hide_in_gui) ? "TRUE" : "FALSE")
							. ", dev_update='$dev_updated' WHERE dev_id=$dev_id";
					else { // neues Device anlegen
						$dev_id_code			= "SELECT MAX(dev_id)+1 AS dev_id FROM device";
						$next_free_dev_id		= $db_connection->fworch_db_query($dev_id_code);
						$next_free_dev_id_no	= $next_free_dev_id->data[0]['dev_id'];
						if (!isset($next_free_dev_id_no)) $next_free_dev_id_no = 1;
						$sql_code				= "INSERT INTO device (dev_id, dev_name, mgm_id, dev_rulebase, dev_typ_id, do_not_import, hide_in_gui) " .
							"VALUES ($next_free_dev_id_no, '$dev_name', $dev_mgm_id, '$dev_rulebase', $device_type_id, " . (($dev_do_import) ? "FALSE" : "TRUE") .
								"," . (($dev_hide_in_gui) ? "TRUE" : "FALSE") . ")";
					}
		//					echo "sql_code: $sql_code<br>";
					$result = $db_connection->fworch_db_query($sql_code);
					if ($e->isError($result) or !$result) $input_fehler = 'db-sql-error';
				}
			}
//			$aktion = 'change'; // einfach nur anzeigen des devices/managements
		}

		include("header.inc.php");
		include("navi_head.inc.php");
		include("navi_hor.inc.php");
		include("navi_vert_config_dev.inc.php");
	?>
	<FORM id="configuration" name="configuration" action="" target="" method="post">
		<input type="hidden" name="mgm_or_dev" value=""/>
		<input type="hidden" name="input_fehler" value=""/>
		<input type="hidden" name="dev_id" value=""/>
		<input type="hidden" name="ManSystem" value=""/>
		<input type="hidden" name="device_type_id" value=""/>
		<input type="hidden" name="Device" value=""/>
		<input type="hidden" name="mgm_id" value=""/>
		<input type="hidden" name="aktion" value=""/>
		<input type="hidden" name="dev_name" value=""/>
		<input type="hidden" name="dev_rulebase" value=""/>
		<input type="hidden" name="import_active" value=""/>
		<input type="hidden" name="dev_mgm_id" value=""/>
		<input type="hidden" name="mgm_name" value=""/>
		<input type="hidden" name="mgm_ssh_hostname" value=""/>
		<input type="hidden" name="mgm_importer_hostname" value=""/>
		<input type="hidden" name="mgm_ssh_user" value=""/>
		<input type="hidden" name="mgm_ssh_pub_key" value=""/>
		<input type="hidden" name="mgm_ssh_priv_key" value=""/>
		<input type="hidden" name="mgm_ssh_port" value=""/>
		<input type="hidden" name="mgm_config_path" value=""/>
		<input type="hidden" name="mgm_hide_in_gui" value=""/>
		<input type="hidden" name="dev_hide_in_gui" value=""/>
		<div id="inhalt">&nbsp;
			<table>
				<tr><td>&nbsp;</td>
				<td><b class="headlinemain"><?php echo $language->get_text_msg('existing_device', 'html') ?>: <b><b id="headlineSys" class="headlinemain">
					&quot;Management System&quot; / &quot;Device&quot; (<?php echo $language->get_text_msg('select_on_left', 'html') ?>)</b>
				</tr>
				<tr><td>&nbsp;</td>
					<td><table>
						<tr><td><b class="headlinemain"><?php echo $language->get_text_msg('create_new_device', 'html') ?>:</b></td>
							<td>&nbsp;</td>
							<td><input type="submit" value="<?php echo $language->get_text_msg('new_device', 'html') ?>" class="button" style="margin-right:15px;" onClick="javascript:SubmitForm('new_dev');"/></td>
							<td>&nbsp;</td>
							<td><input type="submit" value="<?php echo $language->get_text_msg('new_management', 'html') ?>" class="button" style="margin-right:15px;" onClick="javascript:SubmitForm('new_mgm');"/></td>
							<td>&nbsp;</td>
						</tr>
					</table>
			</td></tr>
			<br>
			</td></tr>
			</table>
		</div>
	</FORM>
	
	<iframe id="Change_Config_Frame" name="Change_Config_Frame" src="/leer.php">
		[Your user agent does not support frames or is currently configured not to display frames.]
	</iframe>
	
	<?php include ("leer.inc.php"); ?>
	
	<?php
		echo "<script language=\"javascript\" type=\"text/javascript\">";
		if (!isset($device_type_id)) $device_type_id = '';
		if (!isset($dev_do_import)) $dev_do_import = '';
		if (!isset($mgm_ssh_port)) $mgm_ssh_port = '';
		if (!isset($mgm_config_path)) $mgm_config_path = '';
		if (!isset($input_fehler)) $input_fehler = '';
		if ($mgm_or_dev == 'dev')
			echo "SubmitDeviceChangeInformation('$aktion', '$dev_id', '$dev_name', '$dev_mgm_id', '$dev_rulebase', '$device_type_id', '$dev_do_import', '$dev_hide_in_gui', '$input_fehler');";
		if ($mgm_or_dev == 'mgm') {
			$mgm_ssh_pub_key 	= str_replace(array(chr(13).chr(10),chr(13),chr(10),"\n"), $nl_sub, $mgm_ssh_pub_key);
			$mgm_ssh_priv_key 	= str_replace(array(chr(13).chr(10),chr(13),chr(10),"\n"), $nl_sub, $mgm_ssh_priv_key);
			echo "SubmitManagementChangeInformation('$aktion', '$mgm_id', '$mgm_name', '$mgm_ssh_user', '$mgm_ssh_hostname', '$mgm_importer_hostname', " .
					"'$mgm_ssh_priv_key', '$mgm_ssh_pub_key', '$device_type_id', '$dev_do_import', '$mgm_ssh_port', '$mgm_config_path', '$mgm_hide_in_gui', '$input_fehler');";
		}
		echo "</script>";
	?>
	<script language="javascript\" type="text/javascript"> 
		Hoehe_Frame_setzen("Change_Config_Frame");
		position_iframe("Change_Config_Frame");
	</script>
</body>
</html>