<?php
// $Id: import_status.php,v 1.1.2.3 2009-12-29 13:32:19 tim Exp $
// $Source: /home/cvs/iso/package/web/htdocs/config/Attic/import_status.php,v $
	$stamm = "/";
	$page = "config";
	require_once ("check_privs.php");
	setlocale(LC_CTYPE, "en_US.UTF-8");	
	if (!$allowedToViewImportStatus) { header("Location: /index2.php"); }
	require_once ("db-base.php");
	require_once ("display-filter.php");
	$db_connection = new DbConnection(new DbConfig($_SESSION["dbuser"],$_SESSION["dbpw"]));
?>
<!DOCTYPE HTML PUBLIC "-//W3C//DTD HTML 4.01 Transitional//EN">
<html>
<head>
	<meta http-equiv="Content-Type" content="text/html; charset=UTF-8">
	<title>Import-Status fworch</title>
	<meta http-equiv="content-language" content="de">
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

<iframe id="ChangeFrame" name="ChangeFrame" src="/config/import_status_iframe.php">
		[Your user agent does not support frames or is currently configured
	  	not to display frames.]
</iframe>

<?php include ("leer.inc.php"); ?>
<script language="javascript" type="text/javascript">
	Hoehe_Frame_setzen("ChangeFrame");
</script>	
</body>
</html>