<?php
// $Id: doc.php,v 1.1.2.4 2009-12-29 13:32:19 tim Exp $
// $Source: /home/cvs/iso/package/web/htdocs/hilfe/Attic/doc.php,v $
	require_once("check_privs.php")
?>
<!DOCTYPE HTML PUBLIC "-//W3C//DTD HTML 4.01 Transitional//EN">
<html>
<head>
<meta http-equiv="Content-Type" content="text/html; charset=UTF-8">
<title>ITSecOrg Hilfe zum Dokumentieren der offenen &Auml;nderungen</title>
<meta name="robots" content="index,follow">
<meta http-equiv="content-language" content="de">
<link rel="stylesheet" type="text/css" href="/css/firewall.css">
</head>

<body>
<br>&nbsp;<br>
<b class="headlinemain">ITSecOrg Dokumentation offener &Auml;nderungen</b>
<br>&nbsp;<br>
Diese Seite dient zur Dokumentation von an den Sicherheitssystemen vorgenommenen &Auml;nderungen.<br>
Das Import-Modul von ITSecOrg analysiert regelm&auml;&szlig;ig die Konfiguration der eingebundenen Sicherheitssysteme und
tr&auml;gt diese in die Konfigurationsdatenbank ein.<br>
Auf dieser Seite erh&auml;lt der Administrator die M&ouml;glichkeit, Informationen nachzutragen,
die nicht in der nativen Konfiguration des Sicherheitssystem vorhanden sind und somit nicht automatisiert dokumentiert
werden k&ouml;nnen.
  
<br>&nbsp;<br>
<table cellpadding="0" cellspacing="0" class="tab-border" style="margin:0px 10px;">
<tr><td class="celldev_wrap">&quot;Linke Spalte:<br>Anzahl offener &Auml;nderungen&quot;</td>
<td class="celldev_wrap">Hier wird angezeigt, ob und wenn ja, wie viele noch nicht dokumentierte &Auml;nderungen in 
der Konfigurationsdatenbank vorhanden sind.
</td>
</tr>
<tr><td class="celldev_wrap">Zeilen &quot;Mandant&quot; und &quot;Auftragsnummer&quot;</td><td class="celldev_wrap">Bis zu vier Auftr&auml;ge, die eine &Auml;nderung
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
</body></html>
