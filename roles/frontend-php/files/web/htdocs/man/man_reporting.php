<?php
// $Id: man_reporting.php,v 1.1.2.6 2011-05-24 15:01:37 tim Exp $
// $Source: /home/cvs/iso/package/web/htdocs/man/Attic/man_reporting.php,v $
	$stamm="/";
	$page="man";
	require_once("check_privs.php");
	if (!$allowedToViewReports) header("Location: ".$stamm."index2.php");
?>
<!DOCTYPE HTML PUBLIC "-//W3C//DTD HTML 4.01 Transitional//EN">
<html>
<head>
<meta http-equiv="Content-Type" content="text/html; charset=UTF-8">
<title>Manual - Architecture</title>
<meta name="robots" content="index,follow">
<meta http-equiv="cache-control" content="no-cache">
<meta name="revisit-after" content="2 days">
<meta http-equiv="content-language" content="de">

<script type="text/javascript" src="<?php echo $stamm ?>js/browser.js"></script>
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
<b class="headlinemain">fworch Reporting</b>
<br>&nbsp;<br>
Mit Hilfe des Men&uuml;punkts &quot;Reporting&quot; k&ouml;nnen verschiedene Reports aus der fworch 
Konfigurationsdatenbank generiert werden.<br>&nbsp;<br>
<br>
<b class="headline">Auswahl Report</b>
<br><br>In der linken Navigationsleiste ist zun&auml;chst mittels Drop-Down-Feld der gew&uuml;nschte Report-Typ auszuw&auml;hlen.
<br>
<br>
<b class="headline">Optionale Men&uuml;punkte</b>
<br><br>
<ul>
	<li>Sollten Sie den Report ausdrucken wollen, w&auml;hlen Sie bitte &quot;Druckansicht&quot;. 
</ul>
</div>
</body></html> 