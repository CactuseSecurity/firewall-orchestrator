<?php
// $Id: navi_vert_config_dev.inc.php,v 1.1.2.4 2012-04-30 17:21:20 tim Exp $
// $Source: /home/cvs/iso/package/web/htdocs/inctxt/Attic/navi_vert_config_dev.inc.php,v $
	require_once ("check_privs.php");
	require_once ("db-base.php");
	require_once ("db-div.php");
	require_once ("display_menus.php");

	if (!$allowedToConfigureDevices) {
		header("Location: ".$stamm."index2.php");
	}
	$filter			= new RuleChangesFilter($request, $session, 'change_documentation');
	$db_connection	= new DbConnection(new DbConfig($session["dbuser"],$session["dbpw"]));
	$system_list	= new SystemList($filter,$db_connection,$management_filter, $leave_out_mgm_without_dev = false,
						$show_hidden_systems = true);	
	$ds				= new DisplaySystems($system_list->GetSystems());
	
	echo '<form name="navirep">';
	echo '<div id="menu">' . $language->get_text_msg('device_settings', 'html');
	$ds->show_all_systems("change", $mgm_is_linked = true, $select_all_systems = false, $show_only_managements = false,
			$force_dev_links = true, $simplify_combined_single_device = false);
	echo '</div>';
?>
<script type="text/javascript" src="<?php echo $stamm ?>js/tree.js"></script>
</form>