<?php
// $Id: man_report_change.php,v 1.1.2.3 2011-05-24 13:29:10 tim Exp $
// $Source: /home/cvs/iso/package/web/htdocs/man/Attic/man_report_change.php,v $
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
<b class="headline">&Auml;nderungsreport</b>
<br>
<ol>
<li>In der linken Navigationsleiste ist zun&auml;chst mittels Drop-Down-Feld den Report-Typ &quot;&Auml;nderungsreport&quot; auszuw&auml;hlen.
<li>Darauf hin w&auml;hlt man in der linken Navigationsleiste entweder &quot;Alle Systeme&quot; (ganz oben)
oder aber ein spezifisches Device, f&uuml;r das man die vorgenommenen &Auml;nderungen anzeigen lassen m&ouml;chte.
<li>Nach dieser Auswahl erscheint der Name des gew&auml;hlten Managementsystems und des Devices rechts
	in der ersten &Uuml;berschrift.
<li>Der n&auml;chste Schritt ist die Auswahl des Report-Zeitraums: Startzeitpunkt und Endezeitpunkt. Diese Werte sind
	standardm&auml;&szlig;ig auf den gesamten letzten Monat voreingestellt. Wichtig ist hierbei die Verwendung der korrekten Syntax:
	z. B. &quot;2006-01-01&quot; oder &quot;2006-01-01 11:37&quot; oder &quot;2006-01-01 11:37:12&quot; 
<li>Die Auswahl eines Mandanten und eine damit verbundene mandantenbezogene Filterung der &Auml;nderungen ist derzeit
	noch nicht m&ouml;glich. Eine Auswahl an dieser Stelle wird derzeit ignoriert und hat keinerlei Auswirkung auf
	das Reportergebnis. 
<li>Das Setzen &quot;Weiterer Filteroptionen&quot; ist derzeit noch nicht m&ouml;glich.
	Eine Auswahl an dieser Stelle wird derzeit ignoriert und hat keinerlei Auswirkung auf
	das Reportergebnis.
<li>Nach Klicken auf &quot;Report erstellen&quot; wird der gew&uuml;nschte Report generiert und angezeigt.
<li>Sollten Sie den Report ausdrucken wollen, w&auml;hlen Sie bitte &quot;Druckansicht&quot;. Da der Report hierf&uuml;r erneut generiert wird
, f&auml;llt die Wartezeit nochmals an. Falls man nur die Druckversion ben&ouml;tigt, kann man direkt &quot;Druckansicht&quot; w&auml;hlen, ohne vorher auf &quot;Report erstellen&quot; zu klicken.
</ol>
<br>
<b class="headline">Ergebnis</b>
<br><br>
Sie erhalten eine chronologisch sortierte Liste mit allen sicherheitsrelevanten &Auml;nderungen, die im ausgew&auml;hlten Zeitraum durchgef&uuml;hrt wurden.
Zu jeder &Auml;nderung werden die folgenden Details aufgelistet:
<br><br>
<table cellpadding="0" cellspacing="0" class="tab-border" style="margin:0px 10px;">
<tr><td class="celldev_wrap">&quot;Auftrag&quot;</td>
<td class="celldev_wrap">Enth&auml;lt alle Auftraggeber und Auftragsnummern</td></tr>
<tr><td class="celldev_wrap">&quot;Kommentar&quot;</td>
<td class="celldev_wrap">Enth&auml;lt eine kurze Beschreibung der &Auml;nderung, die der Administrator bei der Dokumenation der &Auml;nderung eingetragen hat.</td></tr>
<?php
	if ($allowedToViewAdminNames) {
		echo '<tr><td class="celldev_wrap">&quot;Change Admin&quot;</td>';
		echo '<td class="celldev_wrap">Enth&auml;lt den Namen des Administrators des Sicherheitssystems,' .
			 ' der die &Auml;nderung vorgenommen hat, sofern dieser bekannt ist. Ob hier ein Name erscheint, '.
		 	 ' h&auml;ngt davon ab, ob das ITSecOrg-Import-Modul den Namen in der Konfiguration des' .
		 	 ' Sicherheitssystems finden konnte. Zus&auml;tzlich muss der Username zuvor in der ITSecOrg-Datenbank angelegt' .
		 	 ' worden sein.</td></tr>';
		echo '<tr><td class="celldev_wrap">&quot;Doku Admin&quot;</td>';
		echo '<td class="celldev_wrap">Enth&auml;lt den Namen des ITSecOrg-Administrators,' .
			 ' der die &Auml;nderung dokumentiert hat, sofern dieser bekannt ist. Der Name ist hier zu sehen, wenn '.
		 	 ' die &Auml;nderung dokumentiert wurde.</td></tr>';
	}
?>
<tr><td class="celldev_wrap">&quot;Typ&quot;</td>
<td class="celldev_wrap">Enth&auml;lt die Art der &Auml;nderung:<br>
+ Element neu eingef&uuml;gt<br>
- Element gel&ouml;scht<br>
&Delta; Element ge&auml;ndert
</td></tr>
<tr><td class="celldev_wrap">&quot;Betroffenes Element&quot;</td>
<td class="celldev_wrap">Name des ge&auml;nderten Elements
</td></tr>
<tr><td class="celldev_wrap">&quot;Details&quot;</td>
<td class="celldev_wrap">Auflistung aller Einzel&auml;nderungen im Detail
<tr><td class="celldev_wrap">&quot;Quelle/Ziel/Dienst/AKtion&quot;</td>
<td class="celldev_wrap">Bei Regel&auml;nderungen die zentralen Informationen &uuml;ber die Regel
</td></tr></table>

</div>
</body></html> 