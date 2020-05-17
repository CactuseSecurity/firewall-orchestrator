<?php
// $Id: man_report_rulesearch.php,v 1.1.2.3 2011-05-24 13:29:10 tim Exp $
// $Source: /home/cvs/iso/package/web/htdocs/man/Attic/man_report_rulesearch.php,v $
	$stamm="/";
	$page="man";
	require_once("check_privs.php");
	if (!$allowedToViewReports) header("Location: ".$stamm."index2.php");
?>
<!DOCTYPE HTML PUBLIC "-//W3C//DTD HTML 4.01 Transitional//EN">
<html>
<head>
<meta http-equiv="Content-Type" content="text/html; charset=UTF-8">
<title>Manual - Maintenance</title>
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
 <b class="headlinemain">Regelsuchereport</b>
<br><br>
<ol>
<li>In der linken Navigationsleiste w&auml;hlt man ein spezifisches Device oder "Alle Systeme", je nach dem, wo man eine Regel suchen m&ouml;chte.
<li>Nach dieser Auswahl erscheint der Name des gew&auml;hlten Managementsystems und des Devices rechts
        in der ersten &Uuml;berschrift.
<li>Der n&auml;chste Schritt ist die Auswahl des Report-Zeitpunktes: Zeitpunkt. Dieser Wert ist
        standardm&auml;&szlig;ig auf die aktuelle Zeit voreingestellt. Wichtig ist hierbei die Verwendung der korrekten Syntax:
        z. B. &quot;2006-01-01&quot; oder &quot;2006-01-01 11:37&quot; oder &quot;2006-01-01 11:37:12&quot;
<li>Bei Bedarf erfolgt eine Auswahl eines Mandanten und der damit verbundenen mandantenbezogenen Darstellung.
<li>Optional k&ouml;nnen Filter gesetzt werden. Namen werden als String oder Teilstring gefiltert (Beispiele: SAP-Server-010, SAP, Server oder 01). IP Adressen k&ouml;nnen als vollst&auml;ndige IP-Adresse (Bsp. 172.16.3.4) oder als IP-Netz in der Notation IP-Netzadresse/Maskenl&auml;nge in Bit (Bsp. 192.168.2.32/27) eingetragen werden.
<li>Nach Klicken auf &quot;Report erstellen&quot; wird der gew&uuml;nschte Report generiert und angezeigt. Das Generieren eines Reports kann, je nach Komplexit&auml;t der Umgebung, 30 Minuten und l&auml;nger dauern.
<li>Sollten Sie den Report ausdrucken wollen, w&auml;hlen Sie bitte &quot;Druckansicht&quot;. Da der Report hierf&uuml;r erneut generiert wird, f&auml;llt die Wartezeit nochmals an. Falls man nur die Druckversion ben&ouml;tigt, kann man direkt &quot;Druckansicht&quot; w&auml;hlen, ohne vorher auf &quot;Report erstellen&quot; zu klicken.
</ol>
<br>
<b class="headline">Ergebnis</b>
<ul>
<li>Als Ergebnis erh&auml;lt man vier Tabellen (Regeln, Netzwerkobjekte, Dienste, Benutzer)
      Die einzelnen Tabellen lassen sich &uuml;ber das +/- Symbol &uuml;ber der linken oberen Ecke der Tabelle aus- und einblenden.
      Weiterhin ist es m&ouml;glich pro Tabelle einzelne oder mehrere Spalten ein- und auszublenden. Dazu w&auml;hlt man im Auswahlfeld (links &uuml;ber der Tabelle)die gew&uuml;nschen Spalten aus und klickt auf  &quot;einblenden/ausblenden&quot;
</ul>
<br>
</div>
</body></html> 