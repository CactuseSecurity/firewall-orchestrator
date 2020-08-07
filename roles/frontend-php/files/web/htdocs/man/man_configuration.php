<?php
// $Id: man_configuration.php,v 1.1.2.5 2011-05-23 12:15:38 tim Exp $
// $Source: /home/cvs/iso/package/web/htdocs/man/Attic/man_configuration.php,v $
	$stamm="/";
	$page="man";
	require_once("check_privs.php");
	if (!$allowedToDocumentChanges) header("Location: ".$stamm."index2.php");
?>
<!DOCTYPE HTML PUBLIC "-//W3C//DTD HTML 4.01 Transitional//EN">
<html>
<head>
<meta http-equiv="Content-Type" content="text/html; charset=UTF-8">
<title>Manual - Configuration</title>
<script type="text/javascript" src="<?php echo $stamm ?>js/client.js"></script>
<script type="text/javascript" src="<?php echo $stamm ?>js/script.js"></script>
<link rel="stylesheet" type="text/css" href="<?php echo $stamm ?>css/firewall.css">
<script language="javascript" type="text/javascript">
  if(is_ie) document.write("<link rel='stylesheet' type='text/css' href='<?php echo $stamm ?>css/firewall_ie.css'>");
</script>
</head>

<body onLoad="changeColor1('n6');">
<?php
	include("header.inc.php");
	include("navi_head.inc.php");
	include("navi_hor.inc.php");
	include("navi_vert_manual.inc.php");
?>

<form>
<div id="inhalt">
<b class="headlinemain">fworch Konfiguration</b>
<big><big>fworch Konfiguration</big></big><br>
Folgende Konfigurationen k&ouml;nnen via Web-GUI modifiziert werden:
<br>
<ul>
  <li>&Auml;ndern des Benutzerpassworts
  <li>Einbinden eines neuen Devices</li>
  <li>Einbinden eines neuen Managements</li>
  <li>Entfernen eines nicht mehr vorhandenen Managements (Disable Import)</li>
</ul>
<br>

Diese Aktionen kann ein fworch Benutzer (sofern er die notwendigen Berechtigungen besitzt) &uuml;ber den Men&uuml;punkt Einstellungen durchf&uuml;hren.
<br>
Mit der Option &quot;Passwort &auml;ndern&quot; kann der Benutzer ein neues Passwort vergeben.
Dies sollte erstmalig direkt nach dem ersten Anmelden geschehen, um das Initial-Passwort abzu&auml;ndern,
das eventuell auch anderen Personen als dem Benutzer selbst bekannt sein kann.
<br>
Administratoren, die nach Inbetriebnahme des fworch-Systems das Team der Sicherheitsadministratoren verlassen,
d&uuml;rfen nicht gel&ouml;scht werden, da sonst das Reporting der von diesem Administrator durchgef&uml;hrten
&Auml;nderungen nicht mehr sauber dargestellt w&uuml;rde. Stattdessen ist Das Austrittsdatum (isoadmin_end_date) einzutragen, um
den Login zu unterbinden.
<br><br><br>
Folgende weitere Konfigurations&auml;nderungen werden derzeit noch per SQL-Kommando durchgef&uuml;hrt:<br>
<ul>
  <li>Anlegen eines neuen Administrators</li>
  <li>Login eines ausgeschiedenen Administrators disablen</li>
</ul>
<br>
</div>
</body></html> 