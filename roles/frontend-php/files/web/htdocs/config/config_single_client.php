<?php
// $Id: config_single_tenant.php,v 1.1.2.4 2012-04-30 17:21:14 tim Exp $
// $Source: /home/cvs/iso/package/web/htdocs/config/Attic/config_single_tenant.php,v $
	$stamm = "/";
	$page = "config";
	require_once ("check_privs.php");
	require_once ("db-input.php");
	$cleaner = new DbInput();  // for clean-function
	setlocale(LC_CTYPE, "de_DE.UTF-8");
	$request = $cleaner->clean_structure($_REQUEST);
	$session = $cleaner->clean_structure($_SESSION);
	if (!$allowedToConfiguretenants) header("Location: " . $stamm . "config/configuration.php");
	require_once ('multi-language.php');
	$language = new Multilanguage($session["dbuser"]);
?>
<!DOCTYPE HTML PUBLIC "-//W3C//DTD HTML 4.01 Transitional//EN">
<html>
<head>
<meta http-equiv="Content-Type" content="text/html; charset=UTF-8">
<title>fworch Change tenant Config</title>
<script type="text/javascript" src="<?php echo $stamm ?>js/browser.js"></script>
<script type="text/javascript" src="<?php echo $stamm ?>js/script.js"></script>
<link rel="stylesheet" type="text/css" href="<?php echo $stamm ?>css/firewall.css">
<script language="javascript" type="text/javascript">
	if(is_ie) document.write("<link rel='stylesheet' type='text/css' href='<?php echo $stamm ?>css/firewall_ie.css'>");

	function SubmitForm(Aktion,tenantId) {
		if (Aktion=='cancel') {
			document.forms.configuration.action="configuration.php";
			document.forms.configuration.target="_top";
		} else {
			document.forms.tenant_form.aktion.value			= Aktion;
			document.forms.tenant_form.target				= "_top";
			document.forms.tenant_form.tenant_id.value		= tenantId;			
			document.forms.tenant_form.action				= "config_tenant.php";
		}
	}
</script>
</head>

<body class="iframe">
<div id="tenant_config">
	<?php
//		$vars = $_REQUEST; reset($vars); while (list($key, $val) = each($vars)) { echo "$key => $val<br>"; } reset ($vars);
		require_once ("db-base.php");
		require_once ("db-config.php");
		require_once ("db-tenant.php");
		require_once ("display-table.php");
		require_once ("display_menus.php");
		
		$format = 'html';
		$size = 70; // size of text input field
		$db_connection = new DbConnection(new DbConfig($_SESSION["dbuser"], $_SESSION["dbpw"]));
		$nl_sub = 'KerridschRitoern4711';
		$aktion = $cleaner->clean($request['aktion'], 20);
		if (!isset($aktion)) $aktion = 'cancel';
		$ergebnis = ''; $tenant_net_id = ''; $tenant_net_ip = ''; $net_tenant_id = ''; $tenant_net_created = '';
					
		if ($aktion != 'new_tenant') { // tenant wird nicht neu angelegt
			$input_fehler 		= $cleaner->clean($request['input_fehler'],500);
			$tenant_id = $cleaner->clean($request['tenant_id'], 10);
			if (!isset($tenant_id) or $tenant_id=='') {
				$tenant_name		= $cleaner->clean($request['tenant_name'],200);
				$input_fehler = $cleaner->clean($request['input_fehler'], 500);
			} else {				
				$tenant = new tenant($db_connection, $tenant_id);
				$tenant_name = $tenant->gettenantName();
				$tenant_created = $tenant->gettenantCreated();
			}
		}
//		if (!isset($tenant_id)) { $tenant_id = -1; } // creating new tenant	
		// now generate form for device editing
		$tenant_table	= new DisplayTable('Mandant', $headers = array ($language->get_text_msg('tenant_id', 'html'), $tenant_id));
		if (isset($input_fehler) and $input_fehler!='') $ergebnis = "<b>Fehler, Speichern nicht erfolgreich: $input_fehler</b><br>";
		if ($aktion == 'display' or $aktion == 'cancel' or ($aktion == 'save' and !$input_fehler)) {
			$nurlesen = 'readonly';
			$select_disabled = 'disabled';
		} else {
			$nurlesen = '';
			$select_disabled = '';
		}
		$form = '<FORM id="tenant_form" name="tenant_form" method="POST" target="_top">';
		$form .= $tenant_table->displayTableOpen($format) . $tenant_table->displayTableHeaders($format);
		$form .= '<input type="hidden" name="tenant_id" value="' . $tenant_id . '">';
		$form .= '<input type="hidden" name="aktion" value="">';
		$form .= '<input type="hidden" name="tenant_or_network" value="tenant">';
		$form .= $tenant_table->displayRowSimple($format) . $tenant_table->displayColumn($language->get_text_msg('tenant_name', 'html'),$format) . 
			$tenant_table->displayColumn('<input type="text" name="tenant_name" value="' .
			$tenant_name . '" ' . $nurlesen . ' size="' . $size . '">',$format);
		$form .= $tenant_table->displayTableClose($format);
		$form .= '<table><tr><td>&nbsp;</td>';
		if (isset($tenant_id) and !($tenant_id==0) and ($aktion=='cancel' or $aktion == 'display' or ($aktion == 'save' and !$input_fehler)))
			$form .= '<td><input type="submit" value="' . $language->get_text_msg('change', 'html') . '" class="button" style="margin-right:15px;" ' .
				' onClick="javascript:SubmitForm(\'change\', \'' . $tenant_id . '\');"/></td>';
		if ($aktion == 'change' or $aktion == 'new_tenant' or ($aktion == 'save' and $input_fehler))
			$form .= '<td><input type="submit" value="' . $language->get_text_msg('save', 'html') . '" class="button" style="margin-right:15px;" ' .
						' onClick="javascript:SubmitForm(\'save\',\'' . $tenant_id . '\');"/></td>
					<td>&nbsp;</td>
					<td><input type="submit" value="' . $language->get_text_msg('cancel', 'html') . '" class="button" style="margin-right:15px;" ' .
						' onClick="javascript:SubmitForm(\'display\', \'' . $tenant_id . '\');"/></td>';
		$form .= '</tr></table></FORM>';
		// end of form generation, output form:
		echo "&nbsp;<br>$ergebnis<br><br>$form";
	?>
</div>
</body></html>