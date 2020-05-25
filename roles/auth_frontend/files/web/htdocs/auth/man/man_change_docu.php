<?php
// $Id: man_change_docu.php,v 1.1.2.3 2009-12-29 13:32:19 tim Exp $
// $Source: /home/cvs/iso/package/web/htdocs/man/Attic/man_change_docu.php,v $
	$stamm="/";
	$page="man";
	require_once("check_privs.php");
	if (!$allowedToDocumentChanges) header("Location: ".$stamm."index2.php");
?>
<!DOCTYPE HTML PUBLIC "-//W3C//DTD HTML 4.01 Transitional//EN">
<html>
<head>
<meta http-equiv="Content-Type" content="text/html; charset=UTF-8">
<title>Manual - Change Documentation</title>
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
 <b class="headlinemain">&Auml;nderung an der Dokumentation vornehmen</b>
<br><br>
Zur Oberfl&auml;che f&uuml;r die nachtr&auml;gliche Korrektur von bereits dokumentierten &Auml;nderungen gelangt
man &uuml;ber den Men&uuml;punkt &quot;Dokumentation&quot;, indem  dort in der linken Navigationsleiste der mit 
&quot;Korrigieren&quot; beschriftete Button angeklickt wird.<br>
Anschlie&szlig;end kann man zur Begrenzung der Suche nach dem zu korrigierenden Dokumentationseintrag diverse Filter eingeben:
<br>
<ul>
<li>Das Device, auf dem die &Auml;nderung stattgefunden hat
<li>Zeitraum, in dem die &Auml;nderung stattgefunden hat (nicht das Dokumentieren selbst)
<li>Sollen auch fremde (nicht vom angemeldeten Administrator stammende) &Auml;nderungen angezeigt werden?
</ul>
<br><br>
Die nach Auswahl von &quot;&Auml;nderungen anzeigen&quot; angezeigte Liste enth&auml;lt
nur bereits dokumentierte &Auml;nderungen. In dieser Liste kann man dann die zu korrigierende Dokumentationseingabe
ausw&auml;hlen und in der n&auml;chsten Eingabemaske ab&auml;ndern.   
<br><br>
Zur Kontrolle der vorgenommenen Korrektur kann man sich schlie&szlig;lich die Daten im &Auml;nderungsreporting anzeigen lassen.
</div>
</body></html>