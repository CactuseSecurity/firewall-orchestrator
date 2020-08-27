<?php
// $Id: reporting_tables.php,v 1.1.2.21 2012-05-28 10:38:49 tim Exp $
// $Source: /home/cvs/iso/package/web/htdocs/Attic/reporting_tables.php,v $
	require_once ("db-input.php");
	require_once ("db-input.php");
	$cleaner = new DbInput();  // for clean-function
	setlocale(LC_CTYPE, "de_DE.UTF-8");
	if (!isset($_SESSION)) session_start();
	$request = $cleaner->clean_structure($_REQUEST);
	$session = $cleaner->clean_structure($_SESSION);
	$stamm="";
//	$session['tenantFilter'] = ' (TRUE) ';
//	$_SESSION['tenantFilter'] = ' (TRUE) ';
//	if (isset($request['tenant_id']) and !($request['tenant_id'] == '')) {
//		$session['tenantFilter'] = " (tenant_id=" . $request['tenant_id'] . ") ";
//		$_SESSION['tenantFilter'] = $session['tenantFilter'];
//	} 
//	echo "request:<br>\n"; print_r($request);
//	echo "session:<br>\n"; print_r($session);
//	$felder = $args;  ksort($felder);  reset($felder);
//	while (list($feldname, $val) = each($felder)) { echo ("found $feldname: $val"); } reset($felder);
	
	require_once("check_privs.php");
	require_once ("cli_functions.php");	
	require_once("display-filter.php");
	require_once ('multi-language.php');
	$e = new PEAR();
	$language = new Multilanguage($session["dbuser"]);
	if (isset($request['repTyp'])) 	$report_type 	= $request['repTyp'];
	else $report_type = 'configuration';
	if ($report_type === 'configuration' || $report_type === 'rulesearch')
		$report_format = $request['reportFormat_7'];
	else
		$report_format = $request['reportFormat_2'];
	$dev_id			= $request['devId'];
	$report_type 	= $request['repTyp'];
	if ($report_format == 'html' or $report_format == 'simple.html') {
		$linie="<table width='730' cellspacing='0' cellpadding='0' style='margin:6px 0px;'><tr>\n" .
	 			"<td style='background-color:#FFD;'><img src='".$stamm."img/1p_tr.gif' width='730' height='2' alt=''></td>\n" .
	 			"</tr></table>\n";
		output_html_header_config_report($stamm);
		echo '<body class="iframe" onLoad="javascript:parent.document.getElementById' . "('leer').style.visibility='hidden';\">";
		echo '<div id="inhalt1"><form id="reporting_result" name="reporting_result" action="" method="post">';
		include ($stamm . "inctxt/report_header.inc.php");
	} else {
		$linie = '';
		header('Content-Type: text/plain');
	}
	
	
if ($dev_id <> "") {
	$start = explode(" ", microtime());
	$stime = $start[0] + $start[1];
	switch ($report_type) {
		case "changes":
			$ruleFilter = new RuleChangesFilter($request,$session,'report');
			require_once ("db-change.php");
			require_once ("display_changes.php");
			require_once ("db-div.php");
			$columns = 'Auftrag,DokuKommentar,&Auml;nderungstyp,';
			if ($allowedToViewAdminNames) $columns .= 'Change Admin,Doku Admin,';
//			$columns .= 'Typ,Betroffenes Element,Details,Quelle,Ziel,Dienst,Aktion,Kommentar';
			$columns .= 'Betroffenes Element,Details,Quelle,Ziel,Dienst,Aktion,Kommentar';
			$columns = explode(',',$columns);
			$ruleFilter->set_show_other_admins_changes(true); // show all changes not just from the currently logged on user
			echo (" "); flush(); // reducing span before http output to avoid browser timeout 
			$management_filter_4_view = $ruleFilter->getMgmFilter4View();
			$changes = new ChangeList($ruleFilter,$management_filter_4_view,'view_reportable_changes');
			$changeTable = new DisplayChangeTable($columns, $changes);
			echo $changeTable->displayChanges($ruleFilter,$management_filter_4_view, $change_docu_allowed = false);
			break;

/*		case "auditchanges":
			// write config at start time in JSON format
			// write config at end time in JSON format
			// generate change config rules by comparing two configs 
			require_once ("db-rule.php");
	 		require_once ("display_rule_config.php");
			require_once ("db-import-ids.php");
				
			$ruleFilter = new RuleConfigurationFilter($request,$session);
			$import_ids = new ImportIds($ruleFilter); // generating relevant import ids per mgmt in temp table
			$import_ids->set_all_tables();
			if (isset($dev_id) and !($dev_id==''))	$dev_name = getDevName ($dev_id, 'confexporter', '');
			if (!isset($dev_name) or $dev_name == 'NULL' or $dev_name == '') { 
				echo "ERROR: device with ID $dev_id not found - aborting"; exit (1);
			}
			$rule_list = new RuleList($ruleFilter, $import_ids);
			$ruleTable = new RuleConfigTable($headers = array("Nr","ID","Quelle","Ziel","Dienst","Aktion","Tracking","Install on","Kommentar","Name"), $rule_list);
			$rule_output = $ruleTable->display($ruleFilter, 'JSON', $import_ids);
			echo $rule_output;
			$import_ids->delete_relevant_import_times_from_temp_table();
			$rule_list->deleteTempReport($ruleFilter->getReportId());
			break;
*/			
		case "usage":
			require_once ("db-accounting.php");
			require_once ("display-table.php");
			
			$acc_ruleFilter		= new RuleConfigurationFilter($request,$session);
			$acc_rule_list		= new AccRuleList($acc_ruleFilter);

			// setting filtered rule_ids
			$filtered_rule_ids	= $acc_rule_list->getFilteredRuleIds();	$acc_ruleFilter->setFilteredRuleIds($filtered_rule_ids);
			
			$acc_nwobj_list		= new AccNetworkObjectList($acc_ruleFilter);
			$acc_svc_list		= new AccServiceList($acc_ruleFilter);
			$acc_user_list		= new AccUserList($acc_ruleFilter);

			$headers = array("In der Konfiguration verwendete ...","Anzahl");
			echo (" "); flush(); // reducing span before http output to avoid browser timeout 
			$usage_table = new DisplayTable('usage', $headers, $report_format);
			$table_str = $usage_table->displayTableOpen($report_format);
			$table_str .= $usage_table->displayTableHeaders($report_format);
			$table_str .= $usage_table->displayRow(0,$report_format) . $usage_table->displayColumn('Regeln',$report_format) . $usage_table->displayColumnNum($acc_rule_list->getAccRuleNumber(),$report_format);
			$table_str .= $usage_table->displayRow(1,$report_format) . $usage_table->displayColumn('Netzwerkobjekte',$report_format) . $usage_table->displayColumnNum($acc_nwobj_list->getRows(),$report_format);
			$table_str .= $usage_table->displayRow(2,$report_format) . $usage_table->displayColumn('Netzwerkdienste',$report_format) . $usage_table->displayColumnNum($acc_svc_list->getRows(),$report_format);
			$table_str .= $usage_table->displayRow(3,$report_format) . $usage_table->displayColumn('Benutzer',$report_format) . $usage_table->displayColumnNum($acc_user_list->getRows(),$report_format);
			$table_str .= $usage_table->displayTableClose($report_format);
			$acc_rule_list->deleteTempReport($acc_ruleFilter->getReportId());
			echo $table_str;
			break;
		case "duplicates":
			require_once ("db-nwobject.php");
			require_once ("display-table.php");
			
			$dup_ruleFilter		= new RuleConfigurationFilter($request,$session);  // only used for filtering device / tenant
			$dup_nwobj_list		= new DupNetworkObjectList($dup_ruleFilter);
//			$filtered_rule_ids	= $acc_rule_list->getFilteredRuleIds();	$acc_ruleFilter->setFilteredRuleIds($filtered_rule_ids);
			$dup_objectTable = new NwObjectConfigTable($headers = array("Name","Zone","Typ","IP","Member","UID","Kommentar"), $dup_nwobj_list);
			echo $dup_objectTable->display($ruleFilter, $report_format);
//			$acc_rule_list->deleteTempReport($acc_ruleFilter->getReportId());
			break;
		default:   // "configuration", "rulesearch" or undefined		
			require_once ("db-rule.php");
	 		require_once ("display_rule_config.php");
			require_once ("db-nwobject.php");
			require_once ("display_nwobject_config.php");
			require_once ("db-service.php");
			require_once ("display_service_config.php");
			require_once ("db-user.php");
			require_once ("db-import-ids.php");
			require_once ("display_user_config.php");

			$ruleFilter = new RuleConfigurationFilter($request,$session);
			$import_ids = new ImportIds($ruleFilter); // generating relevant import ids per mgmt in temp table
			$import_ids->set_all_tables();
			if (isset($dev_id) and !($dev_id==''))	$dev_name = getDevName ($dev_id, 'confexporter', '');
			if ($report_type == 'rulesearch') {
				$rule_list = new RuleFindList($ruleFilter, $import_ids);
			} else {	// configuration
				if (!isset($dev_name) or $dev_name == 'NULL' or $dev_name == '') { 
					echo "ERROR: device with ID $dev_id not found - aborting"; exit (1);
				}
				$rule_list = new RuleList($ruleFilter, $import_ids);
			}
			$ruleTable = new RuleConfigTable($headers = array("Nr","ID","Quelle","Ziel","Dienst","Aktion","Tracking","Install on","Kommentar","Name"), $rule_list);
			if ($e->isError($ruleTable)) { $err = $ruleTable; echo "An error occured." . $err->getMessage(); }
			else {
				$rule_output = $ruleTable->display($ruleFilter, $report_format, $import_ids);
				if ($report_format <> 'html' and $report_format <> 'simple.html') {
					$header_output = $ruleTable->displayCommentLineSeparator ($report_format);
					$header_output .= $ruleTable->displayCommentLine("fworch config export for $report_format format", $report_format);
					$header_output .= $ruleTable->displayCommentLine("[generated by IT Security Organizer, (c) Cactus eSecurity GmbH, http://www.cactus.de]", $report_format);
					$header_output .= $ruleTable->displayCommentLine("report time: " . $ruleFilter->getReportTime(), $report_format);
					$header_output .= $ruleTable->displayCommentLine("device id = $dev_id, device name = $dev_name, management name = " . $request['ManSystem'], $report_format);
					$header_output .= $ruleTable->displayCommentLineSeparator($report_format);
				}
				if ($report_format<>'csv' and $report_format<>'ARS.csv' and $report_format<>'ARS.noname.csv') {
					$filtered_rule_id = $ruleTable->getFilteredRuleIds();
					$ruleFilter->setFilteredRuleIds($filtered_rule_id);
					$objectTable = new NwObjectConfigTable($headers = array("Name","Zone","Typ","IP","Member","UID","Kommentar"), new NetworkObjectList($ruleFilter, $order=NULL, $import_ids));
					$nwobject_output = $objectTable->display($ruleFilter, $report_format);
					$serviceTable = new ServiceConfigTable($headers = array("Name","Typ","Member","IP-Proto.","Zielport","Quellport","Timeout<br>(sec)","UID","Kommentar"),new ServiceList($ruleFilter, $order=NULL, $import_ids));
					$service_output = $serviceTable->display($ruleFilter, $report_format);
					if ($report_format<>'junos') {
						$userTable = new UserConfigTable($headers = array("Name","Typ","Uid","Kommentar","Member"), new UserList($ruleFilter, $order=NULL, $import_ids));
						$user_output = $userTable->display($ruleFilter, $report_format);
					}
				}
				switch ($report_format) {	// dealing with different orders of config output
					case 'junos':
						echo $header_output;
						echo "security {\n";
						echo "\tzones {\n$nwobject_output\t}\n";
						echo "\tpolicies {\n$rule_output\t}\n}\n";
						echo "applications {\n$service_output}\n"; // user fuer junos report format noch nicht implementiert
						break;
					case 'csv': case 'ARS.csv': case 'ARS.noname.csv': // Nur Regeln ausgeben
						echo $rule_output; break;
					default: 
						echo $rule_output . $nwobject_output . $service_output . $user_output; break;
				} 
			}
			$import_ids->delete_relevant_import_times_from_temp_table();
			$rule_list->deleteTempReport($ruleFilter->getReportId());
			break;
	}
	$endtime = explode(" ", microtime());
	$etime = $endtime[0] + $endtime[1];
	$log = new LogConnection(); $log->log_debug("report generation (type: $report_type, device id: $dev_id) took " . sprintf('%.2f', $etime - $stime) . " seconds.");
	if ($report_format=='html' or $report_format=='simple.html') {
		echo "\n<br><br>\n" . $language->get_text_msg ('report_generation', 'html') . ' ' .
			$language->get_text_msg ('report_generation_took', 'html') . " " .  sprintf('%.2f', $etime - $stime) . " " .
			$language->get_text_msg ('seconds', 'html') . ".<br>\n";
	} else {
		echo $ruleTable->displayCommentLineSeparator($report_format) . $ruleTable->displayCommentLine("end of configuration", $report_format) . $ruleTable->displayCommentLineSeparator ($report_format);
	}
} else {
	echo "Bitte Device ausw&auml;hlen.";
}
if ($report_format == 'html' or $report_format == 'simple.html') echo "</form></div></body></html>";
?>
