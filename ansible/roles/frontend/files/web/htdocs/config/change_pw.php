<?php
// $Id: change_pw.php,v 1.1.2.5 2012-04-30 17:21:10 tim Exp $
// $Source: /home/cvs/iso/package/web/htdocs/config/Attic/change_pw.php,v $
	$stamm="/";
	$page="config";
	require_once("check_privs.php");
	setlocale(LC_CTYPE, "de_DE.UTF-8");
?>
<!DOCTYPE HTML PUBLIC "-//W3C//DTD HTML 4.01 Transitional//EN">
<html>
<head>
<meta http-equiv="Content-Type" content="text/html; charset=UTF-8">
<title>ITSecOrg Password</title>
<script type="text/javascript" src="<?php echo $stamm ?>js/client.js"></script>
<script type="text/javascript" src="<?php echo $stamm ?>js/script.js"></script>
<link rel="stylesheet" type="text/css" href="<?php echo $stamm ?>css/firewall.css">
<script language="javascript" type="text/javascript">
  if(is_ie) document.write("<link rel='stylesheet' type='text/css' href='<?php echo $stamm ?>css/firewall_ie.css'>");
</script>
</head>
<body onload='changeColor1("n4");javascript:document.change_password.oldpassword.focus()'>
<?php
	include("header.inc.php");
	include("navi_head.inc.php");
	include("navi_hor.inc.php");
	include("navi_vert_config_main.inc.php");
?>
<div id="inhalt">&nbsp;
<b class="headlinemain"><?php echo $language->get_text_msg('change_password', 'html') ?><b>
<form name="change_password" action="change_pw2.php" method="post">
<table>
<tr><td>&nbsp;<td>&nbsp;
<tr><td align="left" colspan="2"><?php echo $language->get_text_msg('password_policy', 'html') ?>
<tr><td>&nbsp;<td>&nbsp;
<tr><td align="right"><?php echo $language->get_text_msg('old_password', 'html') ?> <td><input type="password" name="oldpassword" class="diff_nosize" size="16">
<tr><td align="right"><?php echo $language->get_text_msg('new_password', 'html') ?><td><input type="password" name="newpassword" class="diff_nosize" size="16">
<tr><td align="right"><?php echo $language->get_text_msg('new_password_repeat', 'html') ?><td><input type="password" name="newpassword2" class="diff_nosize" size="16">
<tr><td>&nbsp;<td>&nbsp;
<tr><td>&nbsp;<td><input type="submit" value="<?php echo $language->get_text_msg('set_password', 'html') ?>" class="button">
</form>
</div>
</body></html>
