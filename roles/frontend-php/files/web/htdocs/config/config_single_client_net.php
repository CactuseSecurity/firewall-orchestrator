<?php
// $Id: config_single_tenant_net.php,v 1.1.2.6 2012-04-30 17:21:11 tim Exp $
// $Source: /home/cvs/iso/package/web/htdocs/config/Attic/config_single_tenant_net.php,v $
	$stamm = "/";
	$page = "config";
	require_once ("db-input.php");
	$cleaner = new DbInput();  // for clean-function
	setlocale(LC_CTYPE, "de_DE.UTF-8");
	if (!isset($_SESSION)) session_start();
	$request = $cleaner->clean_structure($_REQUEST);
	$session = $cleaner->clean_structure($_SESSION);
	require_once ("check_privs.php");
	if (!$allowedToConfiguretenants) header("Location: " . $stamm . "config/configuration.php");
	require_once ('multi-language.php');
	$language = new Multilanguage($session["dbuser"]);
?>
<!DOCTYPE HTML PUBLIC "-//W3C//DTD HTML 4.01 Transitional//EN">
<html>
<head>
<meta http-equiv="Content-Type" content="text/html; charset=UTF-8">
<meta http-equiv="content-language" content="de">
<title>fworch Change tenant Config</title>
<script type="text/javascript" src="<?php echo $stamm ?>js/browser.js"></script>
<script type="text/javascript" src="<?php echo $stamm ?>js/script.js"></script>
<link rel="stylesheet" type="text/css" href="<?php echo $stamm ?>css/firewall.css">
<script language="javascript" type="text/javascript">
	if(is_ie) document.write("<link rel='stylesheet' type='text/css' href='<?php echo $stamm ?>css/firewall_ie.css'>");
		
	function SubmitForm(Aktion,tenantNetId) {
		if (Aktion=='cancel') {
			document.forms.configuration.action="configuration.php";
			document.forms.configuration.target="_top";
		} else {
			document.forms.tenant_form.aktion.value			= Aktion;
			document.forms.tenant_form.target				= "_top";
			document.forms.tenant_form.tenant_net_id.value	= tenantNetId;			
			document.forms.tenant_form.action				= "config_tenant.php"
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
		$ergebnis	= ''; $tenant_id = ''; $tenant_net_ip = ''; $net_tenant_id = ''; $tenant_net_created = ''; $tenant_name = '';
		$size = 70; // size of text input field
		$db_connection			= new DbConnection(new DbConfig($session["dbuser"], $session["dbpw"]));
		if (isset($_REQUEST['aktion'])) $aktion = $cleaner->clean($request['aktion'], 20);
		else $aktion = 'cancel';
		if (isset($request['net_tenant_id'])) $net_tenant_id = $cleaner->clean($request['net_tenant_id'], 20);
		if (isset($request['tenant_name'])) $tenant_name = $cleaner->clean($request['tenant_name'], 200);
		$tenant_net_id = $cleaner->clean($request['tenant_net_id'], 10);
		
		if ($aktion != 'new_tenant_net') { // tenant-net wird nicht neu angelegt
			$input_fehler = $cleaner->clean($request['input_fehler'],200);
			if (!isset($tenant_net_id) or $tenant_net_id=='') {
				$tenant_net_ip		= $cleaner->clean($request['tenant_net_ip'], 50);
			} else {				
				$tenant_net = new tenantNetwork($db_connection, $tenant_net_id);
				$tenant_net_ip = $tenant_net->gettenantNetIp();
				$tenant_name = $tenant_net->gettenantName();
				$tenant_net_created = $tenant_net->gettenantNetCreated();
			}
		}
		$tenant = new tenant($db_connection, $net_tenant_id);
		$tenant_name = $tenant->gettenantName();
		// now generate form for tenant net editing
		$tenant_net_table	= new DisplayTable($language->get_text_msg('tenant_network', 'html'), $headers = 
			array ($language->get_text_msg('tenant_network', 'html'), $tenant_net_id));
		if (isset($input_fehler) and $input_fehler!='') $ergebnis = "<b>Fehler, Speichern nicht erfolgreich: $input_fehler</b><br>";
		if ($aktion == 'display' or $aktion == 'cancel' or ($aktion == 'save' and !$input_fehler)) {
			$nurlesen = 'readonly';
			$select_disabled = 'disabled';
		} else {
			$nurlesen = '';
			$select_disabled = '';
		}
		$form = '<FORM id="tenant_form" name="tenant_form" method="POST" target="_top">';
		$form .= $tenant_net_table->displayTableOpen($format) . $tenant_net_table->displayTableHeaders($format);
		$form .= '<input type="hidden" name="tenant_net_id" value="' . $tenant_net_id . '">';
		$form .= '<input type="hidden" name="net_tenant_id" value="' . $net_tenant_id . '">';
		$form .= '<input type="hidden" name="aktion" value="">';
		$form .= '<input type="hidden" name="tenant_or_network" value="network">';
		$form .= $tenant_net_table->displayRowSimple($format) . $tenant_net_table->displayColumn($language->get_text_msg('tenant', 'html'),$format) .
			$tenant_net_table->displayColumn('<input type="text" name="tenant_net_ip" value="' .
			$tenant_name . '" readonly size="' . $size . '">',$format);
		$form .= $tenant_net_table->displayRowSimple($format) . $tenant_net_table->displayColumn($language->get_text_msg('tenant_network', 'html'),$format) .
			$tenant_net_table->displayColumn('<input type="text" name="tenant_net_ip" value="' .
			$tenant_net_ip . '" ' . $nurlesen . ' size="' . $size . '">',$format);
		$form .= $tenant_net_table->displayTableClose($format);
		$form .= '<table><tr><td>&nbsp;</td>';
		if ($aktion=='cancel' or $aktion == 'display' or ($aktion == 'save' and !$input_fehler))
			$form .= '<td><input type="submit" value="' . $language->get_text_msg('change', 'html') . '" class="button" style="margin-right:15px;" ' .
				' onClick="javascript:SubmitForm(\'change\', \'' . $tenant_net_id . '\');"/></td>';
		if ($aktion == 'change' or $aktion == 'new_tenant_net' or ($aktion == 'save' and $input_fehler))
			$form .= '<td><input type="submit" value="' . $language->get_text_msg('save', 'html') . '" class="button" style="margin-right:15px;" ' .
						' onClick="javascript:SubmitForm(\'save\',\'' . $tenant_net_id . '\');"/></td>' .
					'<td>&nbsp;</td><td><input type="submit" value="' . $language->get_text_msg('delete', 'html') . '" class="button" style="margin-right:15px;" ' .
						' onClick="javascript:SubmitForm(\'delete\', \'' . $tenant_net_id . '\');"/></td>' .
						'<td>&nbsp;</td><td><input type="submit" value="' . $language->get_text_msg('cancel', 'html') . '" class="button" style="margin-right:15px;" ' .
						' onClick="javascript:SubmitForm(\'display\', \'' . $tenant_net_id . '\');"/></td>';
		$form .= '</tr></table></FORM>';
		echo "<br>&nbsp;&nbsp;$ergebnis<br>$form";
	?>
</div>
</body>
</html>