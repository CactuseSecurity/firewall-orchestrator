<?php
// $Id: navi_vert_documentation.inc.php,v 1.1.2.4 2012-04-30 17:21:22 tim Exp $
// $Source: /home/cvs/iso/package/web/htdocs/inctxt/Attic/navi_vert_documentation.inc.php,v $
	require_once("db-change.php");
	require_once("display_changes.php");
	if (!isset($_SESSION)) session_start();
	require_once ("db-input.php");
	$cleaner = new DbInput();  // for clean-function
	setlocale(LC_CTYPE, "de_DE.UTF-8");
	$request = $cleaner->clean_structure($_REQUEST);
	$session = $cleaner->clean_structure($_SESSION);

	$additional_filter = "FALSE"; // $changes wird leer sein --> nur zum Z�hlen der Changes via views verwendet
	$ruleFilter = new RuleChangesFilter($request,$session,'document');
	$changes = new ChangeList($ruleFilter,$additional_filter,'view_undocumented_changes');
	$management_filter_4_view = $ruleFilter->getMgmFilter4View();
	if ($page=='doc') {
		$total_change_number = $changes->total_undocumented_change_number($management_filter_4_view);
		$anonymous_change_number = $changes->undocumented_change_number_anonymous($management_filter_4_view);
		$own_change_number = $changes->undocumented_change_number_per_user($user_id,$management_filter_4_view);
	} else  {
		$total_change_number = $changes->total_documented_change_number($management_filter_4_view);
		$anonymous_change_number = $changes->documented_change_number_anonymous($management_filter_4_view);
		$own_change_number = $changes->documented_change_number_per_user($user_id,$management_filter_4_view);
	}

	$foreign_change_number = $total_change_number - $anonymous_change_number - $own_change_number;
	$check_own_changes = true;
	$check_anon_changes = true;
	$check_foreign_changes = true;
	$check_only_selfdocumented = false;
	$default_start_date = date('Y-m-d', time() - (7 * 24 * 60 * 60)); // 7 days; 24 hours; 60 mins; 60secs
	$default_end_date = date('Y-m-d', time() + (24 * 60 * 60)); // (tomorrow)
	$start_date = $default_start_date;
	$end_date = $default_end_date;

	echo '<div id="menu">';
		echo '<FORM id="docu_filter" name="docu_filter" action="documentation_iframe.php" target="ChangeFrame" method="post" OnSubmit="' . 
			'javascript:ShowHideLeer(' . "'hide'" . ')">';
			if ($page == 'changedoc') echo '<input type="hidden" name="change_docu" value="1">';
			echo '<input type="hidden" name="mgm_id" value=""/>';
			echo '<table><tr><td>&nbsp;<td>';
				echo '<b class="headlinemain">' . $language->get_text_msg('display_filter', 'html') . ' </b></td></tr>';
				echo '<tr><td>&nbsp;</td><td><input type="submit" value="' . $language->get_text_msg('refresh', 'html') .
					'" class="button">&nbsp;</td></tr>';
				echo '<table>' . 
					'<tr><td>' . $language->get_text_msg('from', 'html') . 
					'</td><td><input size="14" maxlength="16" class="filter filter100" type="text" value="' . $start_date  .'" name="zeitpunktalt"></td>' .
					'<tr><td>' . $language->get_text_msg('to', 'html') . 
					'</td><td><input size="14" maxlength="16" class="filter filter100" type="text" value="' . $end_date  .'" name="zeitpunktneu"></td>' .
					'</td></tr></table>';
				echo '<br><table>' . 
						'<tr><td>&nbsp;</td><td>' .
						$language->get_text_msg('my_own', 'html') . '</td><td align="right"><b>' . $own_change_number . '</b></td></tr>';
				echo '<tr><td>&nbsp;</td><td>' . $language->get_text_msg('anonymous', 'html') .
					'</td><td align="right"><b>' . $anonymous_change_number . '</b></td></tr>';
				echo '<tr><td><input type="checkbox" name="show_other_admins_changes" '	. $check_foreign_changes .
					'></td><td>' . $language->get_text_msg('foreign', 'html') . '</td><td align="right"><b>' . $foreign_change_number . '</b></td></tr>';
				if ($page<>'doc') {  // nur im "Korrigieren"-Fall
					echo '<tr><td><input type="checkbox" name="show_only_selfdoc" ' . $check_only_selfdocumented .
						'></td><td colspan="2">' . $language->get_text_msg('only_self_documented', 'html') . '</td></tr>';
				}
				echo '<tr><td colspan="3"><hr>' .
					'<tr><td>&nbsp;<td>' . $language->get_text_msg('total', 'html') .  '<td align="right"><b>' . $total_change_number . '</b></td></tr>' .
					'</table>';
				if ($page<>'doc') {  // nur im "Korrigieren"-Fall
					echo '<br><table><tr><td><FIELDSET><LEGEND><b class="headmenu">' . $language->get_text_msg('document_filter', 'html') . '</b></LEGEND>' . 
						'<table><tr><td>' . $language->get_text_msg('request', 'html') . 
						'</td><td><input class="filter filter45" type="text" value="" name="request_nr_filter"></td>' .
						'<tr><td>' . $language->get_text_msg('document_comment', 'html') .
						'</td><td><input class="filter filter45" type="text" value="" name="comment_filter"></td>' .
						'<tr><td>' . $language->get_text_msg('object_comment', 'html') .
						'</td><td><input class="filter filter45" type="text" value="" name="obj_comment_filter"></td>' .
						'</td></tr></table></FIELDSET></td></tr></table>';
				}
			echo "</table>";
		echo '</FORM>';
	
		require_once ("db-base.php");
		require_once ("db-div.php");
		require_once ("display_menus.php");
		$db = new DbList();
		$db->initSessionConnection();
		$filter			= new RuleChangesFilter($request, $session, 'report');
		$system_list	= new SystemList($filter, $db->db_connection, $management_filter, $leave_out_mgm_without_dev = true, 
							$show_hidden_systems = false);	
			
		echo "<b class=\"headmenu\">" . $language->get_text_msg('management_filter', 'html') . "</b>";
		$ds = new DisplaySystems($system_list->GetSystems());
		$ds->show_all_systems("do", $mgm_is_linked = true, $select_all_systems = true, $show_only_managements = true,
			$force_dev_links = false, $simplify_combined_single_device = true);
	echo '</div>';
?>
<script type="text/javascript" src="<?php echo $stamm ?>js/tree.js"></script>
</form>
<script language="javascript" type="text/javascript">
	document.getElementById("select_all_devices").style.display="inline"; 
	document.getElementById("menu").style.height = getInnerHeight() - 87;
</script>
