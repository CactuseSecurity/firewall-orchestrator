<?php
// $Id: man_concepts.php,v 1.1.2.4 2009-12-29 13:45:04 tim Exp $
// $Source: /home/cvs/iso/package/web/htdocs/man/Attic/man_concepts.php,v $
	$stamm="/";
	$page="man";
	require_once("check_privs.php");
	if (!$allowedToDocumentChanges) header("Location: ".$stamm."index2.php");
?>
<!DOCTYPE HTML PUBLIC "-//W3C//DTD HTML 4.01 Transitional//EN">
<html>
<head>
<meta http-equiv="Content-Type" content="text/html; charset=UTF-8">
<title>Manual - Concepts</title>
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
<br>
<b class="headlinemain">fworch Konzepte</b>
<br>&nbsp;<br>
<A name="clients"></A>
<h3>Mandantentrennung</h3>
In fworch werden die folgenden Kriterien f&uuml;r die Trennung der Sicherheitskonfigurationen nach Mandanten verwendet:
<br>
<ul>
<li>IP-Adressen: Die fworch-Datenbank enth&auml;lt eine Tabelle mit IP-Adressbereichen und eine Zuordnung
zu den einzelnen Mandanten.
<li>Directory-Abfragen f&uuml;r Benutzer: Sofern ein LDAP-Verzeichnis vorhanden ist, kann die Mandantenzugeh&ouml;rigkeit
	eines Benutzers dort abgefragt werden.
</ul>
<br>
Als Maxime f&uuml;r die Mandatenf&auml;higkeit gilt:<br>
<b>
Zeige alle Zugriffsregeln, die Zugriffe aus oder in das Netzwerk des Mandanten erlauben oder verbieten.
</b>
<br><br>
Realisiert wird diese Maxime durch folgende Definitionen:
<ul>
<li>Regeln, die das Netzwerk des ausgew&auml;hlten Mandanten nicht betreffen (keine &Uuml;berlappung der IP-Adressbereiche),
	werden ausgeblendet.
<li>Teile von Regeln (Quell- oder Ziel-IP-Adressbereiche sowie Benutzer) werden ausgeblendet,
	wenn hier&uuml;ber keine Freischaltungen von oder auf das Netz des augew&auml;hlten Mandanten realisiert wird.
</ul>
<br>
Beispiel der Anwendung eines Filters f&uuml;r Mandant 1 auf ein Regelwerk:
<br>
<br>
<img src="<?php echo $stamm ?>img/client_filter.jpg">
<br>
<br>
Es sei herausgestellt, dass in den letzten beiden Regeln ein spezielles &quot;Any&quot;-Objekt vorkommt.
Dieses Objekt, das alle IP-Adressen umfasst, beinhaltet in jedem Fall das Netzwerk eines beliebigen
Mandanten und somit werden solche Regeln immer angezeigt.  
<A name="roles"></A>
<h3>Rollen</h3>
F&uuml;r die Verwendung von fworch k&ouml;nnen verschiedene Benutzergruppen definiert werden.
Abh&auml;ngig von der Gruppenzugeh&ouml;rigkeit kann ein Benutzer verschiedene Rollen und damit auch 
verschiedene Privilegien erhalten.
<br>
So ist es zum Beispiel m&ouml;glich, Benutzer zu definieren, die nur Reports f&uuml;r einen
bestimmten Mandanten generieren, nicht aber &Auml;nderungen dokumentieren k&ouml;nnen.
<br>
Die folgende Liste zeigt einen Auszug der zu vergebenden Privilegien:
<br><br>
<ul>
<li>Sicht auf Managementsysteme
<li>Sicht auf Devices
<li>Sicht auf Mandanten bzw. Einschr&auml;nkung auf einen Mandanten
<li>Dokumentieren m&ouml;glich
<li> &Auml;nderung der Dokumentation m&ouml;glich
</ul>
<br>
Ein Beispiel f&uuml;r aus verschiedenen Rollen resultierende Sichten auf die fworch-GUI zeigt die folgende Abbildung:
<br><br>
<img src="<?php echo $stamm ?>img/roles.jpg">
<br><br>
Der Benutzer im unteren Browserfenster hat keine Berechtigung zum Dokumentieren von &Auml;nderungen und 
daher ist der Men&uuml;punkt ausgeblendet. Auch durch manuelles Aufrufen der URL mittels Eingabe in die Adresszeile
kann der Benutzer nicht auf die Dokumentationsseite gelangen.

<A name="revision"></A>
<h3>Revisionssicherheit</h3>

fworch gew&auml;hrleistet eine l&uuml;ckenlose Dokumentation der &Auml;nderungen
an allen unterst&uuml;tzten Sicherheitssystemen:<br>
<ul>
<li>Alle &Auml;nderungen werden automatisch eingelesen
<li>Alle Eingaben der Administratoren zu Grund und Beauftragung der &Auml;dnerungen werden mitprotokolliert.
<li>Die zur Korrektur von Fehleingaben m&ouml;glichen &Auml;nderungen an den Eingaben der Administratoren
	werden ebenfalls in einer Historie protokolliert. 
</ul>
<br><br>
Voraussetzung f&uuml;r die Revisisonssicherheit des fworch-Systems ist allerdings, dass sichergestellt ist, 
dass niemand die fworch-Datenbank direkt (ohne Verwendung der fworch-GUI) ver&auml;ndern kann.
<br><br>
Dies kann erreicht werden, wenn sowohl der Unix-root-Account sowie alle Datenbanknutzer mit administrativen Rechten
des fworch-Datenbanksystems kontrolliert werden und z.B. nur unter Einhaltung des 4-Augen-Prinzips verwendet werden.
</div>
</body></html> 