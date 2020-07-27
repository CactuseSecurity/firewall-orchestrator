<?php
// $Id: import_status_iframe.php,v 1.1.2.4 2011-05-20 12:47:30 tim Exp $
// $Source: /home/cvs/iso/package/web/htdocs/config/Attic/import_status_iframe.php,v $
	require_once("cli_functions.php");
	require_once("check_privs.php");
	require_once("display-filter.php");
	setlocale(LC_CTYPE, "en_US.UTF-8");	
	if (!isset($_SESSION)) session_start();
	if (!$allowedToViewImportStatus) { header("Location: /leer.php"); }
	require_once ("db-import-status.php");
	require_once ("display_import_status.php");
	
	$db_connection = new DbConnection(new DbConfig('itsecorg',''));
	$import_status_table = new DisplayImportStatusTable(new ImportStatusList());
	if ($opt = getopt("o:", array("outputmode:"))) {
		$opt = $cleaner->clean_structure($opt);
	}
	if (!isset($opt['outputmode']))
		$opt = getArgs($_SERVER['argv']);
	echo $import_status_table->displayImportStatus($opt['outputmode']);
?>