<?php
// $Id: report_header.inc.php,v 1.1.2.4 2012-05-28 10:41:15 tim Exp $
// $Source: /home/cvs/iso/package/web/htdocs/inctxt/Attic/report_header.inc.php,v $
	require_once ('multi-language.php');
	if (!isset($language)) $language = new Multilanguage($session["dbuser"]);
	$reporting_times = '';
	if (isset($request['ManSystem']))	$man_sys		= $request['ManSystem'];
	else $man_sys = '';
	if (isset($request['Device'])) 	$device 		= $request['Device'];
	else $device = '';
	if (isset($request['client_id'])) $client_id		= $request['client_id']; else $client_id = '';
	if (isset($request['quellname'])) $src_name		= $request['quellname'] <> '' ? $request['quellname'] : "NULL";
	else $src_name = 'NULL';
	if (isset($request['quell_ip'])) $src_ip			= $request['quell_ip'] <> '' ? $request['quell_ip'] : "NULL";
	else $src_ip = 'NULL';
	if (isset($request['zielname'])) $dst_name		= $request['zielname'] <> '' ? $request['zielname'] : "NULL";
	else $dst_name = 'NULL';
	if (isset($request['ziel_ip'])) 	$dst_ip			= $request['ziel_ip'] <> '' ? $request['ziel_ip'] : "NULL";
	else $dst_ip = 'NULL';
	if (isset($request['dienstname'])) 	$svc_name		= $request['dienstname'] <> '' ? $request['dienstname'] : "NULL";
	else $svc_name = 'NULL';
	if (isset($request['dienst_ip'])) 	$svc_proto		= $request['dienst_ip'] <> '' ? $request['dienst_ip'] : "NULL";
	else $svc_proto = 'NULL';
	if (isset($request['dienstport'])) 	$svc_dst_port	= $request['dienstport'] <> '' ? $request['dienstport'] : "NULL";
	else $svc_dst_port = 'NULL';
	if (isset($request['ben_id'])) 	$usr_id			= $request['ben_id'] <> '' ? $request['ben_id'] : "NULL";
	else $usr_id = 'NULL';
	if (isset($request['ben_name'])) 	$usr_lastname	= $request['ben_name'] <> '' ? $request['ben_name'] : "NULL";
	else $usr_lastname = 'NULL';
	if (isset($request['ben_vor'])) 	$usr_firstname	= $request['ben_vor'] <> '' ? $request['ben_vor'] : "NULL";
	else $usr_firstname = 'NULL';
	if (isset($request['regelkommentar'])) 	$rule_comment	= $request['regelkommentar'] <> '' ? $request['regelkommentar'] : "NULL";
	else $rule_comment = 'NULL';
	if (isset($request['inactive'])) 	$show_disabled	= false;
	else $show_disabled	= true;
	if (isset($request['notused'])) 	$show_rule_obj_only=true;
	else $show_rule_obj_only	= false;
	$special_filter_text = "<tr><td>" . $language->get_text_msg('report_unused_objects', 'html') .": </td><td valign=\"bottom\">";
	$special_filter_text .=  ($show_rule_obj_only)?$language->get_text_msg('no', 'html'):$language->get_text_msg('yes', 'html');
	$special_filter_text .=  "</td></tr>\n";
	$special_filter_text .=  "<tr><td>" .  $language->get_text_msg('report_inactive_objects', 'html') . ": </td><td valign=\"bottom\">";
	$special_filter_text .= ($show_disabled==1)? $language->get_text_msg('yes', 'html') : $language->get_text_msg('no', 'html');
	$special_filter_text .= "</td></tr>\n";
	
	switch ($report_type) {
		case "changes":
		case "auditchanges":
		case "auditchangesdetails":
			$header_line = $language->get_text_msg('report_headline_changes', 'html');
			if (isset($request['zeitpunktalt'])) $report_date1	= $request['zeitpunktalt'];
			if (isset($request['zeitpunktneu'])) $report_date2	= $request['zeitpunktneu'];
			$reporting_times .= "<tr><td>" . $language->get_text_msg('report_changes_start_time', 'html') . ": </td><td>".$report_date1."</td></tr>\n";
			$reporting_times .= "<tr><td>" . $language->get_text_msg('report_changes_end_time', 'html') . ": </td><td>".$report_date2."</td></tr>\n";
			$special_filter_text = '';
			break;
		case "usage":
			$header_line = $language->get_text_msg('report_headline_usage', 'html');
			if (isset($request['zeitpunkteins'])) $report_date1   = $request['zeitpunkteins'];
			$report_date2	= NULL;
			$reporting_times .= "<tr><td>". $language->get_text_msg('report_time', 'html'). ": </td><td>".$report_date1."</td></tr>\n";
			$special_filter_text = '';
			break;
		case "duplicate":
			$header_line = $language->get_text_msg('report_headline_duplicate', 'html');
			$report_date1   = date('Y-m-d', time()); // now
			$report_date2	= NULL;
			$reporting_times .= "<tr><td>". $language->get_text_msg('report_time', 'html'). ": </td><td>".$report_date1."</td></tr>\n";
			$special_filter_text = '';
			break;
		case "rulesearch":	// Regelsuche
			$header_line = $language->get_text_msg('report_headline_rulesearch', 'html');
			if (isset($request['zeitpunkteins'])) $report_date1   = $request['zeitpunkteins'];
			$report_date2	= NULL;
			$reporting_times .= "<tr><td>". $language->get_text_msg('report_time', 'html'). ": </td><td>".$report_date1."</td></tr>\n";
			break;
		default:	// configuration or undefined
			$header_line = $language->get_text_msg('report_headline_configuration', 'html');
			if (isset($request['zeitpunkteins'])) $report_date1   = $request['zeitpunkteins'];
			$report_date2	= NULL;
			$reporting_times .= "<tr><td>". $language->get_text_msg('report_time', 'html'). ": </td><td>".$report_date1."</td></tr>\n";
	}

	echo "<b class=\"headlinemain\">$header_line</b><br>\n";
	echo '[' . $language->get_text_msg('report_generated_by', 'html') . 
		' IT Security Organizer, (c) Cactus eSecurity GmbH, <a href="http://www.cactus.de/">http://www.cactus.de</a>]<br><br>' . "\n";
	echo $linie;
	echo "<table width=\"730\" cellpadding=\"0\" cellspacing=\"0\"><tr>\n";
	echo "<td width=\"350\" valign=\"top\"\n";
	echo " <table width=\"350\" cellpadding=\"0\" cellspacing=\"0\">\n";
	echo "<tr><td><b>Management-System:&nbsp;</b></td><td><b>".$man_sys."</b></td></tr>\n";
	echo "<tr><td><b>Device: </b></td><td><b>".$device."</b></td></tr>\n";
	echo "<tr><td>Report Format: </td><td>".$report_format."</td></tr>\n";
	echo $reporting_times;
    if ($client_id<>"") {
		require_once("db-client.php");
		$client_conn = new DbConnection(new DbConfig($session["dbuser"],$session["dbpw"]));
		$cname_filter	= new RuleChangesFilter($request, $session, 'document');
		$client_list = new ClientList($cname_filter,$client_conn);
    	echo "<tr><td>" . $language->get_text_msg('client', 'html') . ": </td><td>" . $client_list->getClientName($client_id) . "</td></tr>\n";
    }
	if ($src_name!="NULL") 		echo "<tr><td>quellname: </td><td>".$src_name."</td></tr>\n";
	if ($src_ip!="NULL")		echo "<tr><td>quell_ip: </td><td>".$src_ip."</td></tr>\n";
	if ($dst_name!="NULL")		echo "<tr><td>zielname: </td><td>".$dst_name."</td></tr>\n";
	if ($dst_ip!="NULL")		echo "<tr><td>ziel_ip: </td><td>".$dst_ip."</td></tr>\n";
	if ($svc_name!="NULL")		echo "<tr><td>dienstname: </td><td>".$svc_name."</td></tr>\n";
	if ($svc_proto!="-1" and $svc_proto!="NULL")	echo "<tr><td>dienst IP: </td><td>".$svc_proto."</td></tr>\n";
	if ($svc_dst_port!="NULL")	echo "<tr><td>dienstport: </td><td>".$svc_dst_port."</td></tr>\n";
	if ($usr_id!="NULL")		echo "<tr><td>ben_id: </td><td>".$usr_id."</td></tr>\n";
	if ($usr_lastname!="NULL")	echo "<tr><td>ben_name: </td><td>".$usr_lastname."</td></tr>\n";
	if ($usr_firstname!="NULL")	echo "<tr><td>ben_vor: </td><td>".$usr_firstname."</td></tr>\n";
	if ($rule_comment!="NULL")	echo "<tr><td>regelkommentar: </td><td>".$rule_comment."</td></tr>\n";
	
	echo $special_filter_text;
	echo " </table>\n";
	echo "</td>\n";
	echo "</tr></table>\n";
	echo "$linie<br><br>";
?>