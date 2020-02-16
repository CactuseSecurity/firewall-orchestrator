<?php
// $Id: config_client.php,v 1.1.2.8 2012-04-30 17:21:13 tim Exp $
// $Source: /home/cvs/iso/package/web/htdocs/config/Attic/config_client.php,v $
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
	if (!$allowedToConfigureClients) { header("Location: ".$stamm."config/configuration.php"); }
?>
<!DOCTYPE HTML PUBLIC "-//W3C//DTD HTML 4.01 Transitional//EN">
<html>
<head>
	<meta http-equiv="Content-Type" content="text/html; charset=UTF-8">
	<meta http-equiv="content-language" content="de">
	<title>ITSecOrg Configuration</title>
	<script type="text/javascript" src="<?php echo $stamm ?>js/client.js"></script>
	<script type="text/javascript" src="<?php echo $stamm ?>js/script.js"></script>
	<link rel="stylesheet" type="text/css" href="<?php echo $stamm ?>css/firewall.css">
	<script language="javascript" type="text/javascript">
		if(is_ie) document.write("<link rel='stylesheet' type='text/css' href='<?php echo $stamm ?>css/firewall_ie.css'>");
		
		function SubmitClientChangeInformation (Aktion, client_id, client_name, input_fehler) {
			document.forms.configuration.client_id.value=client_id;			
			document.forms.configuration.client_name.value=client_name;			
			document.forms.configuration.aktion.value = Aktion;
			if (Aktion=='change' || Aktion=='save') {
				document.forms.configuration.input_fehler.value = input_fehler;
				document.forms.configuration.target="Change_Config_Frame";
				document.forms.configuration.method="post";
				document.forms.configuration.action="config_single_client.php";
				document.forms.configuration.submit();
			}
		}
		function SubmitClientNetChangeInformation (Aktion, client_net_id, client_net_ip, net_client_id, input_fehler) {
			document.forms.configuration.net_client_id.value=net_client_id;
			document.forms.configuration.client_net_id.value=client_net_id;			
			document.forms.configuration.client_net_ip.value=client_net_ip;			
			document.forms.configuration.aktion.value = Aktion;
			if (Aktion=='change' || Aktion=='save') {
				document.forms.configuration.input_fehler.value = input_fehler;
				document.forms.configuration.target="Change_Config_Frame";
				document.forms.configuration.method="post";
				document.forms.configuration.action="config_single_client_net.php";
				document.forms.configuration.submit();
			}
		}
		function changeClient(ClientName, ClientId) {
			document.getElementById("headlineSys").innerHTML = ClientName;
			document.forms.configuration.client_id.value=ClientId;
			document.forms.configuration.client_name.value=ClientName;
			document.forms.configuration.client_net_id.value="";
			document.forms.configuration.client_or_network.value='client';
			SubmitForm('display');
			document.forms.configuration.submit();
		}
		function changeClientNet(ClientName, NetClientId, ClientNetIp, ClientNetId) {
			document.getElementById("headlineSys").innerHTML = ClientName + " / " + ClientNetIp;
			document.forms.configuration.client_net_id.value=ClientNetId;
			document.forms.configuration.net_client_id.value=NetClientId;
			document.forms.configuration.client_name.value=ClientName;
			document.forms.configuration.client_or_network.value='network';
			SubmitForm('display');
			document.forms.configuration.submit();
		}
		function SubmitForm(Aktion) {
			document.forms.configuration.aktion.value = Aktion;
			document.forms.configuration.method="post";
			document.forms.configuration.target="Change_Config_Frame";
			if (!document.forms.configuration.client_net_id.value && !document.forms.configuration.client_id.value &&  !(Aktion=='new_client_net' || Aktion=='new_client' || Aktion=='save')) {
				document.forms.configuration.action="no_client.php";
			} else {
				if (Aktion=='save') {
					document.forms.configuration.client_id.value		= top.Change_Config_Frame.document.forms.client_form.client_id.value;							
					document.forms.configuration.client_name.value		= top.Change_Config_Frame.document.forms.client_form.client_name.value;							
					document.forms.configuration.net_client_id.value	= top.Change_Config_Frame.document.forms.client_form.net_client_id.value;
					if (top.Change_Config_Frame.document.forms.client_form.client_id) { // saving client
						document.forms.configuration.client_name.value		= top.Change_Config_Frame.document.forms.client_form.client_name.value;							
 						document.forms.configuration.action="config_single_client.php";
					} else {  // saving network
						document.forms.configuration.client_net_ip.value	= top.Change_Config_Frame.document.forms.client_form.client_net_ip.value;
 						document.forms.configuration.action="config_single_client_net.php";
					}
				}
				if (Aktion=='new_client') {
					document.forms.configuration.client_id.value = '';
				}
				if (Aktion=='new_client' || Aktion=='new_client_net') {
					document.getElementById("headlineSys").innerHTML = '';
					document.forms.configuration.client_id='';
					document.forms.configuration.client_net_id='';
				}
				if (Aktion=='new_client' || (Aktion=='display' && document.forms.configuration.client_id.value)) {
					document.forms.configuration.action="config_single_client.php";
					document.forms.configuration.client_or_network.value='client';
				}
				if (Aktion=='new_client_net' || (Aktion=='display' && document.forms.configuration.client_net_id.value)) {
					document.forms.configuration.action="config_single_client_net.php";
					document.forms.configuration.client_or_network.value="network";
				}
				if (Aktion=='new_client_net') {
					document.forms.configuration.client_net_id.value = '';
					if (!document.forms.configuration.client_id.value) {
 						document.forms.configuration.action="no_client.php";
					} else {
 						document.forms.configuration.net_client_id.value = document.forms.configuration.client_id.value;
 						top.Change_Config_Frame.document.forms.client_form.net_client_id.value = document.forms.configuration.client_id.value;
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
		require_once ("db-client.php");
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
		if (isset($request['client_or_network'])) $client_or_network = $cleaner->clean($request['client_or_network'], 9);
		else $client_or_network = 'client';
		if (isset($request['client_id'])) $client_id = $cleaner->clean($request['client_id'], 10);
		if (isset($request['net_client_id'])) $net_client_id = $cleaner->clean($request['net_client_id'], 10);
		// client
		if (isset($request['client_name'])) $client_name = $cleaner->clean($request['client_name'], 200);
		// client_network
		if (isset($request['client_net_id'])) $client_net_id = $cleaner->clean($request['client_net_id'], 50);
		if (isset($request['client_net_ip'])) $client_net_ip = $cleaner->clean($request['client_net_ip'], 50);

		if ($aktion == 'delete' and $client_or_network == 'network' and isset($client_net_id) and $client_net_id <> '') { // Loeschen eines bestehenden client_net
			$sql_code = "DELETE FROM client_network WHERE client_net_id=$client_net_id";
			$result = $db_connection->iso_db_query($sql_code);
			if ($e->isError($result)) $input_fehler = "L&ouml;schen fehlgeschlagen.";
		}
		if ($aktion == 'save') {
			if ($client_or_network == 'client') {
				if (isset($client_id) and $client_id<>'') {
					$client_obj		= new Client($db_connection, $client_id);
					$client_created	= $client_obj->getClientCreated();
					$old_client_name	= $client_obj->getClientName();
				}
				$input_fehler = false;
				if (preg_match("/^$/", $client_name)) $input_fehler = 'Mandantenname darf nicht leer sein.'; 			
				
				// check for existing client with same name
				if (!$client_id or $client_id=='')	{ // Aenderung an bestehendem Mandant
					$client_double_sql_code = "SELECT client_name FROM client WHERE client_name='$client_name'";
					$client_double = $db_connection->iso_db_query($client_double_sql_code);
					if ($client_double->rows) $input_fehler = "Ein Mandant mit Namen $client_name existiert bereits.";
				}
				if (!$input_fehler) {
					if (isset($client_id) and $client_id<>'')	// Aenderung an bestehendem Mandant
						$sql_code = "UPDATE client SET client_name='$client_name' WHERE client_id=$client_id";
					else { // neuen Client anlegen
						$client_id_code = "SELECT MAX(client_id)+1 AS client_id FROM client";
						$next_free_client_id = $db_connection->iso_db_query($client_id_code); $next_free_client_id_no = $next_free_client_id->data[0]['client_id'];
						if (!isset($next_free_client_id_no)) $next_free_client_id_no = 1;
						$sql_code = "INSERT INTO client (client_id, client_name) VALUES ($next_free_client_id_no, '$client_name')";					
					}
//					echo "sql_code: $sql_code<br>";
					$result = $db_connection->iso_db_query($sql_code);
					if ($e->isError($result)) $input_fehler = "IP-Adresse &uuml;berpr&uuml;fen.";
				}
			}
			if ($client_or_network == 'network') {
				// check form input
				$input_fehler = false;
				if (preg_match("/^$/", $client_net_ip))				$input_fehler = 'Netzwerk darf nicht leer sein.';
				if (!preg_match("/^[\w\.\d\:\/]+$/", $client_net_ip))	$input_fehler = "Netzwerk-IP-Adresse ung&uuml;ltig.";
				// end check form input
				if (!$input_fehler) {
					if (isset ($client_net_id) and $client_net_id <> '') // Aenderung an bestehendem client_net
						$sql_code = "UPDATE client_network SET client_net_ip='$client_net_ip' WHERE client_net_id=$client_net_id";
					else { // neues Netz anlegen
						$client_net_id_code		 = "SELECT MAX(client_net_id)+1 AS client_net_id FROM client_network";
						$next_free_client_net_id = $db_connection->iso_db_query($client_net_id_code);
						$next_free_client_net_id_no	 = $next_free_client_net_id->data[0]['client_net_id'];
						if (!isset($next_free_client_net_id_no)) $next_free_client_net_id_no = 1;
						$sql_code	= "INSERT INTO client_network (client_net_id, client_net_ip, client_id) " .
							"VALUES ($next_free_client_net_id_no, '$client_net_ip', $net_client_id)";
						$client_net_id = $next_free_client_net_id_no;
					}
//							echo "sql_code: $sql_code<br>";
					$result = $db_connection->iso_db_query($sql_code);
					if ($e->isError($result) or !$result) $input_fehler = "IP-Adresse &uuml;berpr&uuml;fen.";
					else {
						$client_network_new = new ClientNetwork($db_connection, $client_net_id);
						$client_net_ip = $client_network_new->getClientNetIp();
					}
				}
			}
		}

		include("header.inc.php");
		include("navi_head.inc.php");
		include("navi_hor.inc.php");
		include("navi_vert_config_client.inc.php");
	?>
	<FORM id="configuration" name="configuration" action="" target="Change_Config_Frame" method="post">
		<input type="hidden" name="client_or_network" value=""/>
		<input type="hidden" name="input_fehler" value=""/>
		<input type="hidden" name="client_net_id" value=""/>
		<input type="hidden" name="Mandant" value=""/>
		<input type="hidden" name="IP Netz" value=""/>
		<input type="hidden" name="client_id" value=""/>
		<input type="hidden" name="net_client_id" value=""/>
		<input type="hidden" name="aktion" value=""/>
		<input type="hidden" name="client_net_ip" value=""/>
		<input type="hidden" name="client_name" value=""/>
		<div id="inhalt">&nbsp;
			<table>
				<tr><td>&nbsp;</td>
				<td><b class="headlinemain"><?php echo $language->get_text_msg('existing_client_information', 'html') ?>: <b><b id="headlineSys" class="headlinemain">
					&quot;<?php echo $language->get_text_msg('client', 'html') ?>&quot; / &quot;<?php echo $language->get_text_msg('client_network', 'html') ?>&quot; (<?php echo $language->get_text_msg('select_on_left', 'html')?>)</b>
				</tr>
				<tr><td>&nbsp;</td>
					<td><table>
						<tr><td><b class="headlinemain"><?php echo $language->get_text_msg('create_new_clients_or_networks', 'html') ?>:</b></td>
							<td>&nbsp;</td>
							<td><input type="submit" value="<?php echo $language->get_text_msg('new_client', 'html') ?>" class="button" style="margin-right:15px;" onClick="javascript:SubmitForm('new_client');"/></td>
							<td>&nbsp;</td>
							<td><input type="submit" value="<?php echo $language->get_text_msg('new_ip_network', 'html')?>" class="button" style="margin-right:15px;" onClick="javascript:SubmitForm('new_client_net');"/></td>
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
		if ($client_or_network == 'network')   
			echo "SubmitClientNetChangeInformation('$aktion', '$client_net_id', '$client_net_ip', '$net_client_id', '$input_fehler');";
		if ($client_or_network == 'client') {
			echo "SubmitClientChangeInformation('$aktion', '$client_id', '$client_name', '$input_fehler');";
		}
		echo "</script>";
	?>
	<script language="javascript\" type="text/javascript"> 
		Hoehe_Frame_setzen("Change_Config_Frame");
		position_iframe("Change_Config_Frame");
	</script>
</body>
</html>