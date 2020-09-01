<?php
// $Id: navi_vert_reporting.inc.php,v 1.1.2.6 2012-05-28 10:41:14 tim Exp $
// $Source: /home/cvs/iso/package/web/htdocs/inctxt/Attic/navi_vert_reporting.inc.php,v $
	require_once ("check_privs.php");
	require_once ("db-base.php");
	require_once ("db-div.php");
	require_once ("display_menus.php");
	
	if (!$allowedToViewReports) {
		header("Location: ".$stamm."index2.php");
	}
	$db = new DbList();
	$db->initSessionConnection();
	$filter			= new RuleChangesFilter($request, $session, 'report');
	$system_list	= new SystemList($filter, $db->db_connection, $management_filter, $leave_out_mgm_without_dev = true,
						$show_hidden_systems = false);	
//	echo "<p>DEBUG: system_list_anzahl = " . $system_list->rows . "<p>";
//	echo "<p>DEBUG: tenant_filter_expr = " . $filter->tenant_filter_expr . "<p>";
	$reportlist		= new ReportList($filter, $db->db_connection);
	
	echo '<form name="navirep">';
	echo "<div id=\"menu\">";
		$disp_reports = new DisplayReports($reportlist->GetReports());
		$disp_reports->show_report_menu($report_filter);
	
		echo "<b class=\"headmenu\">" . $language->get_text_msg('systems_capital', 'html') . "</b>";
		$ds = new DisplaySystems($system_list->GetSystems());
		$ds->show_all_systems("do", $mgm_is_linked = false, $select_all_systems = true, $show_only_managements = false,
			 $force_dev_links = true, $simplify_combined_single_device = true);
	echo '</li></ul></li></div>';
?>
<script type="text/javascript" src="<?php echo $stamm ?>js/tree.js"></script>
<script language="javascript" type="text/javascript">
	SwitchReportType();
	position_iframe("Report_Frame");
	document.getElementById("menu").style.height = top.innerHeight - 90;	
</script>
</form>