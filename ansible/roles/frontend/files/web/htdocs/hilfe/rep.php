<?php
// $Id: rep.php,v 1.1.2.7 2012-03-07 11:44:42 tim Exp $
// $Source: /home/cvs/iso/package/web/htdocs/hilfe/Attic/rep.php,v $
	require_once("check_privs.php");
	if (!in_array('1', explode(',',$report_filter))) header("Location: ".$stamm."index2.php");
?>
<!DOCTYPE HTML PUBLIC "-//W3C//DTD HTML 4.01 Transitional//EN">
<html>
<head>
<meta http-equiv="Content-Type" content="text/html; charset=UTF-8">
<title>Hilf zu ITSecOrg Einstellungen</title>
<meta name="robots" content="index,follow">
<meta http-equiv="cache-control" content="no-cache">
<meta name="revisit-after" content="2 days">
<meta http-equiv="content-language" content="de">
<link rel="stylesheet" type="text/css" href="/css/firewall.css">
</head>

<body>
<br>&nbsp;<br>
<b class="headlinemain">ITSecOrg Reporting</b>
<br>&nbsp;<br>
Mit Hilfe des Men&uuml;punkts &quot;Reporting&quot; k&ouml;nnen verschiedene Reports aus der ITSecOrg 
Konfigurationsdatenbank generiert werden.<br>&nbsp;<br>
Die folgenden Reporttypen stehen zur Verf&uuml;gung:<br>&nbsp;<br>
<table cellpadding="0" cellspacing="0" class="tab-border" style="margin:0px 10px;">
<tr><td class="celldev_wrap">&quot;&Auml;nderungen&quot;</td>
<td class="celldev_wrap">Zeigt &Auml;nderungen an der Sicherheitskonfiguration in einem zu w&auml;hlenden Zeitraum.</td></tr>
<tr><td class="celldev_wrap">&quot;Konfiguration&quot;</td>
<td class="celldev_wrap">Zeigt die Sicherheitskonfiguration zu einem zu w&auml;hlenden Zeitpunkt.</td></tr>
</table>
<br>
<br>
<b class="headline">Auswahl Report</b>
<br><br>In der linken Navigationsleiste ist zun&auml;chst mittels Drop-Down-Feld der gew&uuml;nschte Report-Typ auszuw&auml;hlen.
<br>
<br>
<b class="headline">&Auml;nderungsreport</b>
<ol>
<li>In der linken Navigationsleiste w&auml;hlt man entweder &quot;Alle Systeme&quot; (ganz oben)
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
<li>W&auml;hlen Sie das gew&uuml;nschte Reportformat. 
</ol>
<b class="headline">Report-Formate</b>
<ul>
<li>html - Standard-Ausgabeformat
<li>simple.html - Ausgabe f&uuml;r Druckausgabe ohne interaktive Elemente 
<li>junos - erzeugt Konfigurations-Code für SRX-Firewall-Systeme 
<li>csv - Standardausgabe des Regelwerks im CSV-Format
<li>ARS.csv - Ausgabe des Regelwerks in speziellem CSV-Format für ARS-Import
<li>ARS.noname.csv - Ausgabe des Regelwerks in speziellem CSV-Format für ARS-Import ohne Objektnamen
</ul>
<br>
<b class="headline">Ergebnis</b>
<br>
Sie erhalten eine chronologisch sortierte Liste mit allen sicherheitsrelevanten &Auml;nderungen, die im ausgew&auml;hlten Zeitraum durchgef&uuml;hrt wurden.
Zu jeder &Auml;nderung werden die folgenden Details aufgelistet:
<br>
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
</body></html>