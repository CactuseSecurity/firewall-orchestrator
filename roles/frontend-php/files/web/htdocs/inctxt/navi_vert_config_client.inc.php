<?php
// $Id: navi_vert_config_client.inc.php,v 1.1.2.4 2012-04-30 17:21:20 tim Exp $
// $Source: /home/cvs/iso/package/web/htdocs/inctxt/Attic/navi_vert_config_client.inc.php,v $
	require_once ("check_privs.php");
	require_once ("db-base.php");
	require_once ("db-div.php");
	require_once ("db-client.php");
	require_once ("display_menus.php");

	if (!$allowedToConfigureClients) { header("Location: ".$stamm."index2.php"); }
	$filter				= new RuleChangesFilter($request, $session, 'change_documentation');
	$db_connection		= new DbConnection(new DbConfig($session["dbuser"],$session["dbpw"]));
	$client_net_list	= new ClientNetList($filter,$db_connection,$management_filter);	
	$client_display		= new DisplayClientNet($client_net_list->GetClientNetworks());
	
	echo '<form name="navirep">';
	echo '<div id="menu">' . $language->get_text_msg('client_settings', 'html');
	$client_display->show_client_network_menue();
	echo '</div>';
?>
<script type="text/javascript" src="<?php echo $stamm ?>js/tree.js"></script>
</form>