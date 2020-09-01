<?php
// $Id: navi_vert_config_tenant.inc.php,v 1.1.2.4 2012-04-30 17:21:20 tim Exp $
// $Source: /home/cvs/iso/package/web/htdocs/inctxt/Attic/navi_vert_config_tenant.inc.php,v $
	require_once ("check_privs.php");
	require_once ("db-base.php");
	require_once ("db-div.php");
	require_once ("db-tenant.php");
	require_once ("display_menus.php");

	if (!$allowedToConfiguretenants) { header("Location: ".$stamm."index2.php"); }
	$filter				= new RuleChangesFilter($request, $session, 'change_documentation');
	$db_connection		= new DbConnection(new DbConfig($session["dbuser"],$session["dbpw"]));
	$tenant_net_list	= new tenantNetList($filter,$db_connection,$management_filter);	
	$tenant_display		= new DisplaytenantNet($tenant_net_list->GettenantNetworks());
	
	echo '<form name="navirep">';
	echo '<div id="menu">' . $language->get_text_msg('tenant_settings', 'html');
	$tenant_display->show_tenant_network_menue();
	echo '</div>';
?>
<script type="text/javascript" src="<?php echo $stamm ?>js/tree.js"></script>
</form>