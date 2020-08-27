<?php
// $Id: man_architecture.php,v 1.1.2.3 2009-12-29 13:32:19 tim Exp $
// $Source: /home/cvs/iso/package/web/htdocs/man/Attic/man_architecture.php,v $
	$stamm="/";
	$page="man";
	require_once("check_privs.php");
?>
<!DOCTYPE HTML PUBLIC "-//W3C//DTD HTML 4.01 Transitional//EN">
<html>
<head>
<meta http-equiv="Content-Type" content="text/html; charset=UTF-8">
<title>Manual - Architecture</title>
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
 <!--Tabelle Inhalt START-->
<form>
<div id="inhalt">
 <b class="headlinemain">Architektur</b>
 <p></p>
<br>
fworch besteht aus drei Komponenten:
<ul>
	<li>Datenbank-Server
	<li>Import-Modul(e)
	<li>Webserver-Frontend
</ul>
<br>
<img src="<?php echo $stamm ?>img/architecture.jpg">
<br><br>
Diese Komponenten k&ouml;nnen sowohl auf separaten Systemen als auch gemeinsam auf einem Server laufen.
Es k&ouml;nnen auch mehrere Webserver-Frontends und Import-Module aufgesetzt werden.
So kann z.B. der Einsatz mehrerer Import-Module auf unterschiedlichen Servern sinnvoll sein, wenn z.B.
der scp-Zugriff auf verschiedene Systeme nicht von einer zentralen Stelle m&ouml;glich oder
sicherheitstechnisch nicht erlaubt ist.
<br>
Die Verwendung von mehereren Frontend-Webservern ist dann sinnvoll, wenn unterschiedliche Gruppen
(z.B. interne Revision, Sicherheitsadministration, Sicherheitsmanagement und Mandanten) auf fworch zugreifen.
<br><br>
<img src="<?php echo $stamm ?>img/components.jpg">
</div>
</body></html> 