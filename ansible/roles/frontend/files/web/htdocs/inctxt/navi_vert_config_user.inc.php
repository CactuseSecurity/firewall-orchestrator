<?php
// $Id: navi_vert_config_user.inc.php,v 1.1.2.2 2007-12-13 10:47:31 tim Exp $
// $Source: /home/cvs/iso/package/web/htdocs/inctxt/Attic/navi_vert_config_user.inc.php,v $
	require_once ("check_privs.php");
	require_once ("db-base.php");
	require_once ("db-div.php");
	require_once ("db-config.php");
	require_once ("display_menus.php");
	if (!$allowedToConfigureUsers) { header("Location: ".$stamm."index2.php"); }
	
	$isoadmin_list	= new IsoadminList();	
	$displ_users	= new DisplayIsoadmins($isoadmin_list->GetUsers());
	
	echo '<form name="navirep">';
	echo '<div id="menu">Administratoren';
	$displ_users->show_users();
	echo '</div>';
?>
<script type="text/javascript" src="<?php echo $stamm ?>js/tree.js"></script>
</form>