<?php
// $Id: hilfe.php,v 1.1.2.5 2011-04-29 16:03:58 tim Exp $
// $Source: /home/cvs/iso/package/web/htdocs/Attic/hilfe.php,v $
	$page="hilfe";
	require_once("check_privs.php");
?>
<!DOCTYPE HTML PUBLIC "-//W3C//DTD HTML 4.01 Transitional//EN">
<html>
<head>
<meta http-equiv="Content-Type" content="text/html; charset=UTF-8">
<title>fworch Hilfe</title>
<meta name="robots" content="index,follow">
<meta http-equiv="content-language" content="de">

<?php
 $stamm="../";
 $daten=preg_split("[\?&]", $_SERVER['QUERY_STRING']);
 $datennul=preg_split("[\=]", $daten[0]);
?>

<script type="text/javascript" src="<?php echo $stamm ?>js/client.js"></script>
<script type="text/javascript" src="<?php echo $stamm ?>js/script.js"></script>
<link rel="stylesheet" type="text/css" href="<?php echo $stamm ?>css/firewall.css">
<script language="javascript" type="text/javascript">
  if(is_ie) document.write("<link rel='stylesheet' type='text/css' href='<?php echo $stamm ?>css/firewall_ie.css'>");
</script>

</head>
<body class="iframe" onLoad="focus();">
<?php
	$incl_url = $datennul[1] . '.php';
	include($incl_url);
?>
</body></html> 