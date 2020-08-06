<?php
// $Id: man_maintenance.php,v 1.1.2.3 2009-12-29 13:32:19 tim Exp $
// $Source: /home/cvs/iso/package/web/htdocs/man/Attic/man_maintenance.php,v $
	$stamm="/";
	$page="man";
	require_once("check_privs.php");
	if (!$allowedToDocumentChanges) header("Location: ".$stamm."index2.php");
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
 <b class="headlinemain">Systempflege</b>
<br><br>
<h3>Betriebssystem</h3> 
Als Betriebssystem f&uuml;r fworch wird standardm&auml;&szlig;ig Debian Sarge eingesetzt.
Das Produkt l&auml;uft aber auch auf anderen Unix-Derivaten.
<br>Im Folgenden wird von der Verwendung des Standard-Betriebssystems ausgegangen.
<br>
<h3>Update</h3> 
Es wird empfohlen regelm&auml;&szlig;ige (sicherheitsrelevante) Updates des Betriebssystems via apt-get durchzuf&uuml;hren.
<br>
Da alle Komponenten bis auf die Postgres-Datenbank als Pakete (.deb) installiert wurden, sollten die PostgreSQL-Updates verfolgt
werden, um bei sicherheitskritischen Updates die Datenbank ggf. neu zu kompilieren.
<br> 
<h3>Backup</h3>
Ein Backup der Datenbank l�uft 1 x pro Nacht via cronjob. Das Datenbank-Backup liegt unter /var/iso/backup und 
sollte auf einen Backup-Rechner &uuml;bertragen werden.

<h3>Datenbank-Pflege</h3>
Der Prozess pg_autovacuum wird (ab Version 8.1) automatisch von postgres mitgestartet.
L&auml;uft dieser Prozess nicht, kann z.B. der Import eines Managements unverh&auml;ltnism&auml;&szlig;ig lange dauern (>5 Minuten).

<h3>Prozesse</h3>
Die folgenden Skripte dienen zum Starten und Stoppen der einzelnen Komponenten.
<br>
Die Datenbank darf erst gestoppt werden, wenn der Import-Prozess vorher gestoppt wurde.
<br>
Das Stoppen des Import-Prozesses verl&auml;uft "sanft", d.h. ein eventuell laufender Import-Prozess eines einzelnen Managements wird nicht abgebrochen.
<br> 
<ul>
	<li>Webserver: /etc/init.d/apache2 start | stop
	<li>Datenbank: /etc/init.d/postgres start | stop
	<li>fworch Import-Prozess: /etc/init.d/fworch.import start | stop | status
</ul>
<br><br>
Beispiel f�r fworch-import Aufrufe:

<pre>
fworch-Dev:/etc/init.d # ./fworch-import start

Starting fworch Import process ...

the following import processes are running:
fworch  9461  0.0  0.2  2840 1172 pts/2    S+   11:21   0:00 bash -c (cd /usr/local/fworch/importer; /usr/local/fworch/importer/iso-importer-main.pl) >/dev/null 2>&1 &
fworch  9462  0.0  0.4  4452 2452 pts/2    R+   11:21   0:00 /usr/bin/perl -w /usr/local/fworch/importer/iso-importer-main.pl

fworch-Dev:/etc/init.d # ./fworch-import status

the following import processes are running:
fworch  9461  0.0  0.2  2840 1172 pts/2    S    11:21   0:00 bash -c (cd /usr/local/fworch/importer; /usr/local/fworch/importer/iso-importer-main.pl) >/dev/null 2>&1 &
fworch  9462  0.0  1.1  8152 5920 pts/2    S    11:21   0:00 /usr/bin/perl -w /usr/local/fworch/importer/iso-importer-main.pl
fworch  9472  0.0  1.2  8516 6544 pts/2    S    11:21   0:00 /usr/bin/perl -w /usr/local/fworch/importer/iso-importer-loop.pl
fworch  9502  0.0  2.6 16184 13788 pts/2   S    11:21   0:03 /usr/bin/perl -w /usr/local/fworch/importer/iso-importer-single.pl mgm_id=540

fworch-Dev:/etc/init.d # ./fworch-import stop

Shutting down ITSecorg Import process ... waiting for running imports to finish

the following import processes are running:
fworch  9502  0.0  2.6 16184 13788 pts/2   S    11:21   0:03 /usr/bin/perl -w /usr/local/fworch/importer/iso-importer-single.pl mgm_id=540

no import processes are running
</pre>
</div>
</body></html> 