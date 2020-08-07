<?php
// $Id: man_files.php,v 1.1.2.3 2009-12-29 13:32:19 tim Exp $
// $Source: /home/cvs/iso/package/web/htdocs/man/Attic/man_files.php,v $
	$stamm="/";
	$page="man";
	require_once("check_privs.php");
	if (!$allowedToDocumentChanges) header("Location: ".$stamm."index2.php");
?>
<!DOCTYPE HTML PUBLIC "-//W3C//DTD HTML 4.01 Transitional//EN">
<html>
<head>
<meta http-equiv="Content-Type" content="text/html; charset=UTF-8">
<title>Manual - Filesystem Structure</title>
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
 <b class="headlinemain">Filestruktur</b>
<br><br>
fworch Home ($FWORCHHOME): Hier liegen die fworch-Config-Dateien (conf),
 die Import-Skripte (importer) sowie die Web-GUI (web)
 
 <pre>
/usr/local/fworch
|-- etc
|   |-- gui.conf
|   |-- import.conf
|   `-- iso.conf
|-- importer
|   |-- CACTUS
|   |-- fworch-importer-loop.pl
|   |-- fworch-importer-main.pl
|   |-- fworch-importer-rollback.pl
|   |-- fworch-importer-single.pl
`-- web
    |-- htdocs
    `-- include
</pre>

<h3>Datenbank</h3>
Unter /var/iso liegen die Datenbank-Daten sowie die PosgreSQL-Konfigurationsdateien.

<pre>

/var/iso
|-- PG_VERSION
|-- base
|-- global
|-- pg_clog
|-- pg_hba.conf
|-- pg_ident.conf
|-- pg_subtrans
|-- pg_xlog
`-- postgresql.conf

</pre>
<h3>Logging</h3>
Unter /var/log/ liegen die Logdaten:

<pre>
/var/log/fworch.error   - Fehler und Warnungen
/var/log/fworch         - normales Logging
/var/log/fworch.debug   - Debug-Logging (Bei Bedarf)
</pre>
</div>
</body></html> 