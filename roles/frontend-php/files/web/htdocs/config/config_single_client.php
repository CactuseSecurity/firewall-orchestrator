<?php
// $Id: config_single_client.php,v 1.1.2.4 2012-04-30 17:21:14 tim Exp $
// $Source: /home/cvs/iso/package/web/htdocs/config/Attic/config_single_client.php,v $
	$stamm = "/";
	$page = "config";
	require_once ("check_privs.php");
	require_once ("db-input.php");
	$cleaner = new DbInput();  // for clean-function
	setlocale(LC_CTYPE, "de_DE.UTF-8");
	$request = $cleaner->clean_structure($_REQUEST);
	$session = $cleaner->clean_structure($_SESSION);
	if (!$allowedToConfigureClients) header("Location: " . $stamm . "config/configuration.php");
	require_once ('multi-language.php');
	$language = new Multilanguage($session["dbuser"]);
?>
<!DOCTYPE HTML PUBLIC "-//W3C//DTD HTML 4.01 Transitional//EN">
<html>
<head>
<meta http-equiv="Content-Type" content="text/html; charset=UTF-8">
<title>fworch Change Client Config</title>
<script type="text/javascript" src="<?php echo $stamm ?>js/client.js"></script>
<script type="text/javascript" src="<?php echo $stamm ?>js/script.js"></script>
<link rel="stylesheet" type="text/css" href="<?php echo $stamm ?>css/firewall.css">
<script language="javascript" type="text/javascript">
	if(is_ie) document.write("<link rel='stylesheet' type='text/css' href='<?php echo $stamm ?>css/firewall_ie.css'>");

	function SubmitForm(Aktion,ClientId) {
		if (Aktion=='cancel') {
			document.forms.configuration.action="configuration.php";
			document.forms.configuration.target="_top";
		} else {
			document.forms.client_form.aktion.value			= Aktion;
			document.forms.client_form.target				= "_top";
			document.forms.client_form.client_id.value		= ClientId;			
			document.forms.client_form.action				= "config_client.php";
		}
	}
</script>
</head>

<body class="iframe">
<div id="client_config">
	<?php
//		$vars = $_REQUEST; reset($vars); while (list($key, $val) = each($vars)) { echo "$key => $val<br>"; } reset ($vars);
		require_once ("db-base.php");
		require_once ("db-config.php");
		require_once ("db-client.php");
		require_once ("display-table.php");
		require_once ("display_menus.php");
		
		$format = 'html';
		$size = 70; // size of text input field
		$db_connection = new DbConnection(new DbConfig($_SESSION["dbuser"], $_SESSION["dbpw"]));
		$nl_sub = 'KerridschRitoern4711';
		$aktion = $cleaner->clean($request['aktion'], 20);
		if (!isset($aktion)) $aktion = 'cancel';
		$ergebnis = ''; $client_net_id = ''; $client_net_ip = ''; $net_client_id = ''; $client_net_created = '';
					
		if ($aktion != 'new_client') { // client wird nicht neu angelegt
			$input_fehler 		= $cleaner->clean($request['input_fehler'],500);
			$client_id = $cleaner->clean($request['client_id'], 10);
			if (!isset($client_id) or $client_id=='') {
				$client_name		= $cleaner->clean($request['client_name'],200);
				$input_fehler = $cleaner->clean($request['input_fehler'], 500);
			} else {				
				$client = new Client($db_connection, $client_id);
				$client_name = $client->getClientName();
				$client_created = $client->getClientCreated();
			}
		}
//		if (!isset($client_id)) { $client_id = -1; } // creating new client	
		// now generate form for device editing
		$client_table	= new DisplayTable('Mandant', $headers = array ($language->get_text_msg('client_id', 'html'), $client_id));
		if (isset($input_fehler) and $input_fehler!='') $ergebnis = "<b>Fehler, Speichern nicht erfolgreich: $input_fehler</b><br>";
		if ($aktion == 'display' or $aktion == 'cancel' or ($aktion == 'save' and !$input_fehler)) {
			$nurlesen = 'readonly';
			$select_disabled = 'disabled';
		} else {
			$nurlesen = '';
			$select_disabled = '';
		}
		$form = '<FORM id="client_form" name="client_form" method="POST" target="_top">';
		$form .= $client_table->displayTableOpen($format) . $client_table->displayTableHeaders($format);
		$form .= '<input type="hidden" name="client_id" value="' . $client_id . '">';
		$form .= '<input type="hidden" name="aktion" value="">';
		$form .= '<input type="hidden" name="client_or_network" value="client">';
		$form .= $client_table->displayRowSimple($format) . $client_table->displayColumn($language->get_text_msg('client_name', 'html'),$format) . 
			$client_table->displayColumn('<input type="text" name="client_name" value="' .
			$client_name . '" ' . $nurlesen . ' size="' . $size . '">',$format);
		$form .= $client_table->displayTableClose($format);
		$form .= '<table><tr><td>&nbsp;</td>';
		if (isset($client_id) and !($client_id==0) and ($aktion=='cancel' or $aktion == 'display' or ($aktion == 'save' and !$input_fehler)))
			$form .= '<td><input type="submit" value="' . $language->get_text_msg('change', 'html') . '" class="button" style="margin-right:15px;" ' .
				' onClick="javascript:SubmitForm(\'change\', \'' . $client_id . '\');"/></td>';
		if ($aktion == 'change' or $aktion == 'new_client' or ($aktion == 'save' and $input_fehler))
			$form .= '<td><input type="submit" value="' . $language->get_text_msg('save', 'html') . '" class="button" style="margin-right:15px;" ' .
						' onClick="javascript:SubmitForm(\'save\',\'' . $client_id . '\');"/></td>
					<td>&nbsp;</td>
					<td><input type="submit" value="' . $language->get_text_msg('cancel', 'html') . '" class="button" style="margin-right:15px;" ' .
						' onClick="javascript:SubmitForm(\'display\', \'' . $client_id . '\');"/></td>';
		$form .= '</tr></table></FORM>';
		// end of form generation, output form:
		echo "&nbsp;<br>$ergebnis<br><br>$form";
	?>
</div>
</body></html>