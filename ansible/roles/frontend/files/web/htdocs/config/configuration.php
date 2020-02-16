<?php
// $Id: configuration.php,v 1.1.2.5 2012-04-30 17:21:12 tim Exp $
// $Source: /home/cvs/iso/package/web/htdocs/config/Attic/configuration.php,v $
	$stamm="/";	$page="config";
	require_once("check_privs.php");
	setlocale(LC_CTYPE, "en_US.UTF-8");	
	require_once ('multi-language.php');
	$language = new Multilanguage($_SESSION["dbuser"]);
?>
<!DOCTYPE HTML PUBLIC "-//W3C//DTD HTML 4.01 Transitional//EN">
<html><head>
<meta http-equiv="Content-Type" content="text/html; charset=UTF-8">
<title>ITSecOrg Configuration</title>
<script type="text/javascript" src="<?php echo $stamm ?>js/client.js"></script>
<script type="text/javascript" src="<?php echo $stamm ?>js/script.js"></script>
<link rel="stylesheet" type="text/css" href="<?php echo $stamm ?>css/firewall.css">
<script language="javascript" type="text/javascript">
  if(is_ie) document.write("<link rel='stylesheet' type='text/css' href='<?php echo $stamm ?>css/firewall_ie.css'>");
</script>
</head>
<body onLoad="changeColor1('n4')">
<?php
	include("header.inc.php");
	include("navi_head.inc.php");
	include("navi_hor.inc.php");
	include("navi_vert_config_main.inc.php");
?>
<div id="inhalt">&nbsp;
</div>
</body></html>
