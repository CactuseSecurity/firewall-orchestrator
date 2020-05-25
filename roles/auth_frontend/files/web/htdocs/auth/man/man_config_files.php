<?php
// $Id: man_config_files.php,v 1.1.2.4 2011-05-23 13:28:35 tim Exp $
// $Source: /home/cvs/iso/package/web/htdocs/man/Attic/man_config_files.php,v $
	$stamm="/";
	$page="man";
	require_once("check_privs.php");
	if (!$allowedToDocumentChanges) header("Location: ".$stamm."index2.php");
?>
<!DOCTYPE HTML PUBLIC "-//W3C//DTD HTML 4.01 Transitional//EN">
<html>
<head>
<meta http-equiv="Content-Type" content="text/html; charset=UTF-8">
<title>Manual - Configuration</title>
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
// if ($allowedToDocumentChanges) {
?>

<form>
<div id="inhalt">
<b class="headlinemain">ITSecOrg Konfigurationsdateien</b>

Es gibt die folgenden Konfigurationsdateien in $ISOHOME/etc/:
<ul>
<li>iso.conf: globale Eintr&auml;ge (Datenbankverbindung, etc.)
<li>gui.conf: hier stehen die Konfigurationsdaten des Web-Frontends
<li>import.conf: Konfigurationsparameter, die spezifisch f&uuml;r das Import-Modul sind
</ul>
<?php if ($allowedToDocumentChanges) include ("man_config_files_examples.php"); ?>
</div>
</body></html> 