<?php
// $Id: manual.php,v 1.1.2.4 2011-04-11 20:27:18 tim Exp $
// $Source: /home/cvs/iso/package/web/htdocs/man/Attic/manual.php,v $
	$stamm="/";
	$man_path = $stamm . "man/";
	$page="man";
	require_once("check_privs.php");
?>
<!DOCTYPE HTML PUBLIC "-//W3C//DTD HTML 4.01 Transitional//EN">
<html>
<head>
<meta http-equiv="Content-Type" content="text/html; charset=UTF-8">
<title>ITSecOrg Manual</title>
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
<b class="headlinemain">ITSecOrg Handbuch</b>
<p>Das ITSecOrg-System (IT Security Organizer) stellt folgende Funktionalit&auml;ten zur Verf&uuml;gung:
<ul>
<?php
	if ($allowedToDocumentChanges)
		echo "<li>Teilautomatisierung der Dokumentation von &Auml;nderungen an den Firewall-Systemen";
?>
    <li>Generierung von Reports:
    	<ul>
          <li>Konfiguration eines Systems zu einem beliebigen Zeitpunkt
          <li>&Auml;nderungen an einem System in einem Zeitintervall
          <li>Wege-Report: Welche Regeln erlauben den Zugriff zwischen zwei Netzwerkbereichen
        </ul>
	<li>Revisionssicherheit
<?php
	if ($allowedToDocumentChanges)	{
		// nicht ganz sauber: Wenn Aenderung dokumentiert werden duerfen, sind auch diese Punkte sichtbar 
		echo "<li>Mandantenf&auml;hig: Erstellung von mandantenbezogenen Reports bei f&uuml;r " .
			"mehrere Mandanten genutzten Sicherheitssystemen";
		echo "<li>Rollenkonzept (z.B. Zugriff der Mandanten nur auf sie betreffende Daten)";
	}
?>
</ul>
<br>
Die Komponenten (Datenbank, Import-Modul und Webserver) k&ouml;nnen sowohl 
auf einem System als auch auf separaten Servern laufen (siehe auch Abschnitt "<a href="<?php echo $man_path ?>man_architecture.php">Architektur</a>").
<p>
F&uuml;r die folgenden Produkte sind Import-Module vorhanden:
<ul>
    <li>Check Point NG, NGX, R65, R70, R71, R75, R77
    <li>Fortinet FortiGate 5.x
    <li>Juniper ScreenOS 5.x, 6.x 
    <li>Juniper JUNOS 10.x
    <li>phion MC 3.2
</ul>
<br>
<h4>Terminologie<h4>
<ul>
	<li>Managementsystem: System, das die Sicherheitskonfiguration enth&auml;lt (Config-Holder); bei Netscreen das Firewall-System selbst, nicht das NSM
	<li>Device: Filtersysteme, das die Sicherheitskonfiguration umsetzt (Enforcement Point, z.B. Firewallmodul)
	<li>Mandant: Firma oder Abteilung, deren IT abgesichert wird (Client / Kunde)
</ul>
</p>
</div>
</body></html> 