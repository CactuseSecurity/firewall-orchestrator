<?php
// $Id: config_tenant.php,v 1.1.2.8 2012-04-30 17:21:13 tim Exp $
// $Source: /home/cvs/iso/package/web/htdocs/config/Attic/config_tenant.php,v $
    header("Expires: Mon, 26 Jul 1997 05:00:00 GMT");                  // Date in the past   
    header('Last-Modified: '.gmdate('D, d M Y H:i:s') . ' GMT');
    header('Cache-Control: no-store, no-cache, must-revalidate');     // HTTP/1.1
    header('Cache-Control: pre-check=0, post-check=0, max-age=0');    // HTTP/1.1
    header ("Pragma: no-cache");
    header("Expires: 0");
    $stamm="/";	$page="config";
	require_once("check_privs.php");
	require_once ("db-input.php");
	$cleaner = new DbInput();  // for clean-function
	setlocale(LC_CTYPE, "de_DE.UTF-8");
	$request = $cleaner->clean_structure($_REQUEST);
	$session = $cleaner->clean_structure($_SESSION);
	if (!$allowedToConfiguretenants) { header("Location: ".$stamm."config/configuration.php"); }
?>
<!DOCTYPE HTML PUBLIC "-//W3C//DTD HTML 4.01 Transitional//EN">
<html>
<head>
	<meta http-equiv="Content-Type" content="text/html; charset=UTF-8">
	<meta http-equiv="content-language" content="de">
	<title>fworch Configuration</title>
	<script type="text/javascript" src="<?php echo $stamm ?>js/browser.js"></script>
	<script type="text/javascript" src="<?php echo $stamm ?>js/script.js"></script>
	<link rel="stylesheet" type="text/css" href="<?php echo $stamm ?>css/firewall.css">
	<script language="javascript" type="text/javascript">
		if(is_ie) document.write("<link rel='stylesheet' type='text/css' href='<?php echo $stamm ?>css/firewall_ie.css'>");
		
		function SubmittenantChangeInformation (Aktion, tenant_id, tenant_name, input_fehler) {
			document.forms.configuration.tenant_id.value=tenant_id;			
			document.forms.configuration.tenant_name.value=tenant_name;			
			document.forms.configuration.aktion.value = Aktion;
			if (Aktion=='change' || Aktion=='save') {
				document.forms.configuration.input_fehler.value = input_fehler;
				document.forms.configuration.target="Change_Config_Frame";
				document.forms.configuration.method="post";
				document.forms.configuration.action="config_single_tenant.php";
				document.forms.configuration.submit();
			}
		}
		function SubmittenantNetChangeInformation (Aktion, tenant_net_id, tenant_net_ip, net_tenant_id, input_fehler) {
			document.forms.configuration.net_tenant_id.value=net_tenant_id;
			document.forms.configuration.tenant_net_id.value=tenant_net_id;			
			document.forms.configuration.tenant_net_ip.value=tenant_net_ip;			
			document.forms.configuration.aktion.value = Aktion;
			if (Aktion=='change' || Aktion=='save') {
				document.forms.configuration.input_fehler.value = input_fehler;
				document.forms.configuration.target="Change_Config_Frame";
				document.forms.configuration.method="post";
				document.forms.configuration.action="config_single_tenant_net.php";
				document.forms.configuration.submit();
			}
		}
		function changetenant(tenantName, tenantId) {
			document.getElementById("headlineSys").innerHTML = tenantName;
			document.forms.configuration.tenant_id.value=tenantId;
			document.forms.configuration.tenant_name.value=tenantName;
			document.forms.configuration.tenant_net_id.value="";
			document.forms.configuration.tenant_or_network.value='tenant';
			SubmitForm('display');
			document.forms.configuration.submit();
		}
		function changetenantNet(tenantName, NettenantId, tenantNetIp, tenantNetId) {
			document.getElementById("headlineSys").innerHTML = tenantName + " / " + tenantNetIp;
			document.forms.configuration.tenant_net_id.value=tenantNetId;
			document.forms.configuration.net_tenant_id.value=NettenantId;
			document.forms.configuration.tenant_name.value=tenantName;
			document.forms.configuration.tenant_or_network.value='network';
			SubmitForm('display');
			document.forms.configuration.submit();
		}
		function SubmitForm(Aktion) {
			document.forms.configuration.aktion.value = Aktion;
			document.forms.configuration.method="post";
			document.forms.configuration.target="Change_Config_Frame";
			if (!document.forms.configuration.tenant_net_id.value && !document.forms.configuration.tenant_id.value &&  !(Aktion=='new_tenant_net' || Aktion=='new_tenant' || Aktion=='save')) {
				document.forms.configuration.action="no_tenant.php";
			} else {
				if (Aktion=='save') {
					document.forms.configuration.tenant_id.value		= top.Change_Config_Frame.document.forms.tenant_form.tenant_id.value;							
					document.forms.configuration.tenant_name.value		= top.Change_Config_Frame.document.forms.tenant_form.tenant_name.value;							
					document.forms.configuration.net_tenant_id.value	= top.Change_Config_Frame.document.forms.tenant_form.net_tenant_id.value;
					if (top.Change_Config_Frame.document.forms.tenant_form.tenant_id) { // saving tenant
						document.forms.configuration.tenant_name.value		= top.Change_Config_Frame.document.forms.tenant_form.tenant_name.value;							
 						document.forms.configuration.action="config_single_tenant.php";
					} else {  // saving network
						document.forms.configuration.tenant_net_ip.value	= top.Change_Config_Frame.document.forms.tenant_form.tenant_net_ip.value;
 						document.forms.configuration.action="config_single_tenant_net.php";
					}
				}
				if (Aktion=='new_tenant') {
					document.forms.configuration.tenant_id.value = '';
				}
				if (Aktion=='new_tenant' || Aktion=='new_tenant_net') {
					document.getElementById("headlineSys").innerHTML = '';
					document.forms.configuration.tenant_id='';
					document.forms.configuration.tenant_net_id='';
				}
				if (Aktion=='new_tenant' || (Aktion=='display' && document.forms.configuration.tenant_id.value)) {
					document.forms.configuration.action="config_single_tenant.php";
					document.forms.configuration.tenant_or_network.value='tenant';
				}
				if (Aktion=='new_tenant_net' || (Aktion=='display' && document.forms.configuration.tenant_net_id.value)) {
					document.forms.configuration.action="config_single_tenant_net.php";
					document.forms.configuration.tenant_or_network.value="network";
				}
				if (Aktion=='new_tenant_net') {
					document.forms.configuration.tenant_net_id.value = '';
					if (!document.forms.configuration.tenant_id.value) {
 						document.forms.configuration.action="no_tenant.php";
					} else {
 						document.forms.configuration.net_tenant_id.value = document.forms.configuration.tenant_id.value;
 						top.Change_Config_Frame.document.forms.tenant_form.net_tenant_id.value = document.forms.configuration.tenant_id.value;
					}
				}
			}
		}
	</script>
</head>

<body onLoad="changeColor1('n4');">
	<?php
		require_once ("db-base.php");
		require_once ("db-config.php");
		require_once ("db-tenant.php");
		require_once ("display_menus.php");
		require_once ("db-input.php");		

		$e = new PEAR();
		$nl_sub = 'KerridschRitoern4711';
		$db_connection = new DbConnection(new DbConfig($_SESSION["dbuser"], $_SESSION["dbpw"]));
		$cleaner = new DbInput();
//		$vars = $request; reset($vars); while (list($key, $val) = each($vars)) { echo "$key => $val<br>"; } reset ($vars);
		// both
		if (isset($request['aktion'])) $aktion = $cleaner->clean($request['aktion'], 20);
		else $aktion = '';
		if (isset($request['tenant_or_network'])) $tenant_or_network = $cleaner->clean($request['tenant_or_network'], 9);
		else $tenant_or_network = 'tenant';
		if (isset($request['tenant_id'])) $tenant_id = $cleaner->clean($request['tenant_id'], 10);
		if (isset($request['net_tenant_id'])) $net_tenant_id = $cleaner->clean($request['net_tenant_id'], 10);
		// tenant
		if (isset($request['tenant_name'])) $tenant_name = $cleaner->clean($request['tenant_name'], 200);
		// tenant_network
		if (isset($request['tenant_net_id'])) $tenant_net_id = $cleaner->clean($request['tenant_net_id'], 50);
		if (isset($request['tenant_net_ip'])) $tenant_net_ip = $cleaner->clean($request['tenant_net_ip'], 50);

		if ($aktion == 'delete' and $tenant_or_network == 'network' and isset($tenant_net_id) and $tenant_net_id <> '') { // Loeschen eines bestehenden tenant_net
			$sql_code = "DELETE FROM tenant_network WHERE tenant_net_id=$tenant_net_id";
			$result = $db_connection->fworch_db_query($sql_code);
			if ($e->isError($result)) $input_fehler = "L&ouml;schen fehlgeschlagen.";
		}
		if ($aktion == 'save') {
			if ($tenant_or_network == 'tenant') {
				if (isset($tenant_id) and $tenant_id<>'') {
					$tenant_obj		= new tenant($db_connection, $tenant_id);
					$tenant_created	= $tenant_obj->gettenantCreated();
					$old_tenant_name	= $tenant_obj->gettenantName();
				}
				$input_fehler = false;
				if (preg_match("/^$/", $tenant_name)) $input_fehler = 'Mandantenname darf nicht leer sein.'; 			
				
				// check for existing tenant with same name
				if (!$tenant_id or $tenant_id=='')	{ // Aenderung an bestehendem Mandant
					$tenant_double_sql_code = "SELECT tenant_name FROM tenant WHERE tenant_name='$tenant_name'";
					$tenant_double = $db_connection->fworch_db_query($tenant_double_sql_code);
					if ($tenant_double->rows) $input_fehler = "Ein Mandant mit Namen $tenant_name existiert bereits.";
				}
				if (!$input_fehler) {
					if (isset($tenant_id) and $tenant_id<>'')	// Aenderung an bestehendem Mandant
						$sql_code = "UPDATE tenant SET tenant_name='$tenant_name' WHERE tenant_id=$tenant_id";
					else { // neuen tenant anlegen
						$tenant_id_code = "SELECT MAX(tenant_id)+1 AS tenant_id FROM tenant";
						$next_free_tenant_id = $db_connection->fworch_db_query($tenant_id_code); $next_free_tenant_id_no = $next_free_tenant_id->data[0]['tenant_id'];
						if (!isset($next_free_tenant_id_no)) $next_free_tenant_id_no = 1;
						$sql_code = "INSERT INTO tenant (tenant_id, tenant_name) VALUES ($next_free_tenant_id_no, '$tenant_name')";					
					}
//					echo "sql_code: $sql_code<br>";
					$result = $db_connection->fworch_db_query($sql_code);
					if ($e->isError($result)) $input_fehler = "IP-Adresse &uuml;berpr&uuml;fen.";
				}
			}
			if ($tenant_or_network == 'network') {
				// check form input
				$input_fehler = false;
				if (preg_match("/^$/", $tenant_net_ip))				$input_fehler = 'Netzwerk darf nicht leer sein.';
				if (!preg_match("/^[\w\.\d\:\/]+$/", $tenant_net_ip))	$input_fehler = "Netzwerk-IP-Adresse ung&uuml;ltig.";
				// end check form input
				if (!$input_fehler) {
					if (isset ($tenant_net_id) and $tenant_net_id <> '') // Aenderung an bestehendem tenant_net
						$sql_code = "UPDATE tenant_network SET tenant_net_ip='$tenant_net_ip' WHERE tenant_net_id=$tenant_net_id";
					else { // neues Netz anlegen
						$tenant_net_id_code		 = "SELECT MAX(tenant_net_id)+1 AS tenant_net_id FROM tenant_network";
						$next_free_tenant_net_id = $db_connection->fworch_db_query($tenant_net_id_code);
						$next_free_tenant_net_id_no	 = $next_free_tenant_net_id->data[0]['tenant_net_id'];
						if (!isset($next_free_tenant_net_id_no)) $next_free_tenant_net_id_no = 1;
						$sql_code	= "INSERT INTO tenant_network (tenant_net_id, tenant_net_ip, tenant_id) " .
							"VALUES ($next_free_tenant_net_id_no, '$tenant_net_ip', $net_tenant_id)";
						$tenant_net_id = $next_free_tenant_net_id_no;
					}
//							echo "sql_code: $sql_code<br>";
					$result = $db_connection->fworch_db_query($sql_code);
					if ($e->isError($result) or !$result) $input_fehler = "IP-Adresse &uuml;berpr&uuml;fen.";
					else {
						$tenant_network_new = new tenantNetwork($db_connection, $tenant_net_id);
						$tenant_net_ip = $tenant_network_new->gettenantNetIp();
					}
				}
			}
		}

		include("header.inc.php");
		include("navi_head.inc.php");
		include("navi_hor.inc.php");
		include("navi_vert_config_tenant.inc.php");
	?>
	<FORM id="configuration" name="configuration" action="" target="Change_Config_Frame" method="post">
		<input type="hidden" name="tenant_or_network" value=""/>
		<input type="hidden" name="input_fehler" value=""/>
		<input type="hidden" name="tenant_net_id" value=""/>
		<input type="hidden" name="Mandant" value=""/>
		<input type="hidden" name="IP Netz" value=""/>
		<input type="hidden" name="tenant_id" value=""/>
		<input type="hidden" name="net_tenant_id" value=""/>
		<input type="hidden" name="aktion" value=""/>
		<input type="hidden" name="tenant_net_ip" value=""/>
		<input type="hidden" name="tenant_name" value=""/>
		<div id="inhalt">&nbsp;
			<table>
				<tr><td>&nbsp;</td>
				<td><b class="headlinemain"><?php echo $language->get_text_msg('existing_tenant_information', 'html') ?>: <b><b id="headlineSys" class="headlinemain">
					&quot;<?php echo $language->get_text_msg('tenant', 'html') ?>&quot; / &quot;<?php echo $language->get_text_msg('tenant_network', 'html') ?>&quot; (<?php echo $language->get_text_msg('select_on_left', 'html')?>)</b>
				</tr>
				<tr><td>&nbsp;</td>
					<td><table>
						<tr><td><b class="headlinemain"><?php echo $language->get_text_msg('create_new_tenants_or_networks', 'html') ?>:</b></td>
							<td>&nbsp;</td>
							<td><input type="submit" value="<?php echo $language->get_text_msg('new_tenant', 'html') ?>" class="button" style="margin-right:15px;" onClick="javascript:SubmitForm('new_tenant');"/></td>
							<td>&nbsp;</td>
							<td><input type="submit" value="<?php echo $language->get_text_msg('new_ip_network', 'html')?>" class="button" style="margin-right:15px;" onClick="javascript:SubmitForm('new_tenant_net');"/></td>
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
		if ($tenant_or_network == 'network')   
			echo "SubmittenantNetChangeInformation('$aktion', '$tenant_net_id', '$tenant_net_ip', '$net_tenant_id', '$input_fehler');";
		if ($tenant_or_network == 'tenant') {
			echo "SubmittenantChangeInformation('$aktion', '$tenant_id', '$tenant_name', '$input_fehler');";
		}
		echo "</script>";
	?>
	<script language="javascript\" type="text/javascript"> 
		Hoehe_Frame_setzen("Change_Config_Frame");
		position_iframe("Change_Config_Frame");
	</script>
</body>
</html>