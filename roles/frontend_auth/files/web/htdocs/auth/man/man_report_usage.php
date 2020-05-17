<?php
// $Id: man_report_usage.php,v 1.1.2.3 2011-05-24 13:29:10 tim Exp $
// $Source: /home/cvs/iso/package/web/htdocs/man/Attic/man_report_usage.php,v $
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
<b class="headlinemain">Verwendungsreport</b>
<br><br>
<ol>
<li>In der linken Navigationsleiste w&auml;hlt man entweder &quot;Alle Systeme&quot; oder ein spezifisches Device, f&uuml;r das man die Anzahl der verwendeten Objekte anzeigen lassen m&ouml;chte.
<li>Nach dieser Auswahl erscheint der Name des gew&auml;hlten Managementsystems und des Devices rechts
        in der ersten &Uuml;berschrift.
<li>Der n&auml;chste Schritt ist die Auswahl des Report-Zeitpunktes: Zeitpunkt. Dieser Wert ist
        standardm&auml;&szlig;ig auf die aktuelle Zeit voreingestellt. Wichtig ist hierbei die Verwendung der korrekten Syntax:
        z. B. &quot;2006-01-01&quot; oder &quot;2006-01-01 11:37&quot; oder &quot;2006-01-01 11:37:12&quot;
<li>Bei Bedarf erfolgt eine Auswahl eines Mandanten und der damit verbundenen mandantenbezogenen Darstellung.
<li>Nach Klicken auf &quot;Report erstellen&quot; wird der gew&uuml;nschte Report generiert und angezeigt. Das Generieren eines Reports kann, je nach Komplexit&auml;t der Umgebung, 30 Minuten und l&auml;nger dauern.
<li>Sollten Sie den Report ausdrucken wollen, w&auml;hlen Sie bitte &quot;Druckansicht&quot;. Da der Report hierf&uuml;r erneut generiert wird, f&auml;llt die Wartezeit nochmals an. Falls man nur die Druckversion ben&ouml;tigt, kann man direkt &quot;Druckansicht&quot; w&auml;hlen, ohne vorher auf &quot;Report erstellen&quot; zu klicken.
</ol>
<br>
<b class="headline">Ergebnis</b>
<ul>
<li>Als Ergebnis erh&auml;lt man eine Tabelle in der die Anzahl der jeweiligen Objekte (Regeln, Netzwerkobjekte, Dienste, Benutzer) aufgelistet wird.
</ul>
<br>
</div>
</body></html> 