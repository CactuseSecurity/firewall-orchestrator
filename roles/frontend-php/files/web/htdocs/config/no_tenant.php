<!DOCTYPE HTML PUBLIC "-//W3C//DTD HTML 4.01 Transitional//EN">
<html>
<head>
<?php
 $stamm="";
?>
<link rel="stylesheet" type="text/css" href="<?php echo $stamm ?>css/firewall.css">
</head>
<body class="iframe">
<br>
&nbsp;
<?php 
	require_once ('multi-language.php');
	require_once ("db-input.php");
	$cleaner = new DbInput();  // for clean-function
	setlocale(LC_CTYPE, "de_DE.UTF-8");
	$request = $cleaner->clean_structure($_REQUEST);
	$session = $cleaner->clean_structure($_SESSION);
	$language = new Multilanguage($session["dbuser"]);
	echo $language->get_text_msg('no_tenant_selected', 'html');
?>
</body></html>