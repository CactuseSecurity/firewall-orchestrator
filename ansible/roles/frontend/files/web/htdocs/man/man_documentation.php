<?php
// $Id: man_documentation.php,v 1.1.2.4 2009-12-29 13:45:04 tim Exp $
// $Source: /home/cvs/iso/package/web/htdocs/man/Attic/man_documentation.php,v $
	$stamm="/";
	$page="man";
	require_once("check_privs.php");
	if (!$allowedToDocumentChanges) header("Location: ".$stamm."index2.php");
?>
<!DOCTYPE HTML PUBLIC "-//W3C//DTD HTML 4.01 Transitional//EN">
<html>
<head>
<meta http-equiv="Content-Type" content="text/html; charset=UTF-8">
<title>Manual - Documentation</title>
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
<b class="headlinemain">ITSecOrg Dokumentation offener &Auml;nderungen</b>
<br>&nbsp;<br>
Eine der Kernfunktionalit&auml;ten von ITSecOrg ist die Dokumentation von an den Sicherheitssystemen vorgenommenen 
&Auml;nderungen.<br>
Das Import-Modul von ITSecOrg analysiert regelm&auml;&szlig;ig die Konfiguration der eingebundenen Sicherheitssysteme und
tr&auml;gt diese in die Konfigurationsdatenbank ein.<br>
Der Administrator erh&auml;lt in der Weboberfl&auml;che die M&ouml;glichkeit, Informationen nachzutragen,
die nicht in der nativen Konfiguration des Sicherheitssystem vorhanden sind und somit nicht automatisiert dokumentiert
werden k&ouml;nnen.
<ol>
	<li> Der Sicherheitsadministrator f&uuml;hrt eine &Auml;nderung auf einem Firewall-System durch. 
	<li>Diese &Auml;nderung wird in der Konfiguration des Sicherheitssystems abgespeichert.
	<li>Erkennt das ITSecOrg-Import-Modul eine &Auml;nderung, wird die &Auml;nderung analysiert und in die 
   ITSecOrg Datenbank geschrieben.
	<li>Der Administrator, der eine &Auml;nderung vorgenommen hat, sollte sich also ca. 10 Minuten nach dem Abspeichern
	 der &Auml;nderung via Browser auf das Webfrontend des ITSecOrg-System verbinden.
	<li>Dort werden die gerade durchgef&uuml;hrten &Auml;nderungen im Men&uuml;punkt "Dokumentation" dokumentiert :
</ol>
<br>
Im Men&uuml; "Dokumentation" werden alle &Auml;nderungen angezeigt, die noch nicht dokumentiert wurden.
<br>
Der Administrator tr&auml;gt nun f&uuml;r alle &Auml;nderungen, die er gemacht hat,
<ul>
<li> eine Beschreibung (Freitext) im Feld Kommentar
<li>den Mandanten aus der Auswahlliste, der die &Auml;nderung genehmigt hat, sowie
<li>die zugeh&ouml;rige Auftragsnummer (des Mandanten)
</ul>
ein.

Sind alle &Auml;nderungen kommentiert, kann der "Abschicken"-Button gedr&uuml;ckt werden.
Alle &Auml;nderungen, f&uuml;r die keine Dokumentation eingegeben wurde, bleiben in dieser Ansicht erhalten
 und k&ouml;nnen von einem anderen Security-Administrator, der diese &Auml;nderungen vorgenommen hat,
 in einem sp&auml;teren Arbeitsschritt dokumentiert werden.
 
 <br><br>
 Die folgende Tabelle beschreibt die Felder der Eingabemaske:<br>

<br>&nbsp;<br>
<table cellpadding="0" cellspacing="0" class="tab-border" style="margin:0px 10px;">
<tr><td class="celldev_wrap">&quot;Linke Spalte:<br>Anzahl offener &Auml;nderungen&quot;</td>
<td class="celldev_wrap">Hier wird angezeigt, ob und wenn ja, wie viele noch nicht dokumentierte &Auml;nderungen in 
der Konfigurationsdatenbank vorhanden sind.
</td>
</tr>
<tr><td class="celldev_wrap">Zeilen &quot;Genehmigung durch&quot; und &quot;Auftragsnummer&quot;</td><td class="celldev_wrap">
Mehrere Auftr&auml;ge, die eine &Auml;nderung
verursacht haben, k&ouml;nnen hier mit den Teilinformationen Auftraggeber (Mandant) und Auftragsnummer eingetragen werden.</td></tr>
<tr><td class="celldev_wrap">&quot;Kommentar&quot;</td><td class="celldev_wrap">In dieses Feld kann ein beliebiger Text (auch mehrzeilig) als Beschreibung eines Auftrags (z.B. die Begr&uuml;ndung aus dem Antragsformular) eingetragen werden.</td></tr>
<tr><td class="celldev_wrap">Schaltfl&auml;chen<br>&quot;Abschicken&quot;<br>und &quot;Zur&uuml;cksetzen&quot;</td>
<td class="celldev_wrap">Wenn alle Felder ausgef&uuml;llt sind (das Markieren der zu dokumentierenden &Auml;nderungszeilen in den folgenden Tabellen nicht vergessen)
k&ouml;nnen die Daten durch Dr&uuml;cken der Schaltfl&auml;che &quot;Abschicken&quot; in die Datenbank geschrieben werden.<br>
Die somit dokumentierten &Auml;nderungen verschwinden anschlie&szlig;end aus der Ansicht der offenen &Auml;nderungen.</td></tr>
<tr><td class="celldev_wrap">&Auml;nderungstabellen</td><td class="celldev_wrap">Die Tabellen enthalten alle noch nicht dokumentierten &Auml;nderungen in chronologischer Reihenfolge beginnend mit den
&auml;ltesten &Auml;nderungen.<br>
Eine Tabelle enth&auml;lt jeweils eine Gruppierung von &Auml;nderungen, die alle
<ul>
<li>zum selben Zeitpunkt,
<li>auf dem selben Management System und
<li>vom selben Administrator
</ul>
vorgenommen wurden.
<br>&nbsp;<br>
<table cellpadding="0" cellspacing="0" class="tab-border" style="margin:0px 10px;">
<tr><td class="celldev_wrap">Erste Spalte: &quot;Auswahl&quot;
<td class="celldev_wrap">Eine oder mehrere Einzel&auml;nderungen k&ouml;nnen ausgew&auml;hlt werden, die alle mit den
eingegebenen Daten dokumentiert werden.</td></tr>
<tr><td class="celldev_wrap">Zweite Spalte: &quot;Typ&quot;
<td class="celldev_wrap">
+ Element neu eingef&uuml;gt<br>
- Element gel&ouml;scht<br>
&Delta; Element ge&auml;ndert<br>
</td></tr>
<tr><td class="celldev_wrap">Dritte Spalte: &quot;Betroffenes Element&quot;
</td><td class="celldev_wrap">Typ des Elements (Netzwerkobjekt, Benutzer, Netwerkdienst, Regel und Name)</td></tr>
<tr><td class="celldev_wrap">Vierte Spalte: &quot;Details&quot;
</td><td class="celldev_wrap">Enth&auml;lt die Basiskenndaten des Elements bzw. bei Elementver&auml;nderungen die &Auml;nderungsdetails</td></tr>
<tr><td class="celldev_wrap">F&uuml;nfte bis achte Spalte: &quot;Quelle/Ziel/Dienst/Aktion&quot;
</td><td class="celldev_wrap">Enth&auml;lt nur bei Regeln zur Identifikation einen Auszug aus den Firewall-Regeldetails. Dargestellt wird bei l&auml;ngeren Regeln nur der Anfang der Regel, angedeutet durch drei Punkte (...)</td></tr>
</table>
</td></tr>
</table>
</div>
</body></html> 