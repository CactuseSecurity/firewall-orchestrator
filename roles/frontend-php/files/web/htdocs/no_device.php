<?php
// $Id: no_device.php,v 1.1.2.6 2012-04-30 17:21:27 tim Exp $
// $Source: /home/cvs/iso/package/web/htdocs/Attic/no_device.php,v $
?>
<!DOCTYPE HTML PUBLIC "-//W3C//DTD HTML 4.01 Transitional//EN">
<html>
<head>
<?php
 $stamm="";
 if (!isset($_SESSION)) session_start();
?>
<link rel="stylesheet" type="text/css" href="<?php echo $stamm ?>css/firewall.css">
</head>
<body class="iframe">
<br>
&nbsp;
<?php 
	require_once ('multi-language.php');
	$language = new Multilanguage($_SESSION["dbuser"]);
	echo $language->get_text_msg('no_device_selected', 'html');
?>
</body></html>