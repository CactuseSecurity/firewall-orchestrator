<?php
// $Id: reporting.php,v 1.1.2.14 2012-05-28 10:38:49 tim Exp $
// $Source: /home/cvs/iso/package/web/htdocs/Attic/reporting.php,v $
	$stamm = "";
	$page = "rep";
	$menu_width = "730";	// :-( does not define width of vertical menu containing report and system lists
	require_once ("check_privs.php");
	if (!$allowedToViewReports) { header("Location: ".$stamm."index2.php"); }
	require_once ('multi-language.php');
	$language = new Multilanguage($_SESSION["dbuser"]);
	require_once ("db-base.php");
	require_once ("display-filter.php");
	require_once ("db-input.php");
	$cleaner = new DbInput();  // for clean-function
	setlocale(LC_CTYPE, "de_DE.UTF-8");
	$request = $cleaner->clean_structure($_REQUEST);
	$session = $cleaner->clean_structure($_SESSION);
	
	$db_connection = new DbConnection(new DbConfig($session["dbuser"],$session["dbpw"]));
	$filter	= new RuleChangesFilter($request, $session, 'document');
	$linie = "<table width='$menu_width' cellspacing='0' cellpadding='0' style='margin:6px 0px;'><tr>\n" .
	 	"<td style='background-color:#FFD;'><img src='".$stamm."img/1p_tr.gif' width='$menu_width' height='2' alt=''></td>\n</tr></table>";
?>
<!DOCTYPE HTML PUBLIC "-//W3C//DTD HTML 4.01 Transitional//EN">
<html>
<head>
<!--			  document.reporting.repFormat.value='<?php echo $report_format ?>'; -->
	<meta http-equiv="Content-Type" content="text/html; charset=UTF-8">
	<title>ITSecOrg Reporting</title>
	<meta http-equiv="content-language" content="de">
	<script type="text/javascript" src="<?php echo $stamm ?>js/client.js"></script>
	<script type="text/javascript" src="<?php echo $stamm ?>js/script.js"></script>
	<link rel="stylesheet" type="text/css" href="<?php echo $stamm ?>css/firewall.css">
	<script language="javascript" type="text/javascript">
		if(is_ie) document.write("<link rel='stylesheet' type='text/css' href='<?php echo $stamm ?>css/firewall_ie.css'>");

		function SubMitForm(wohin) {
			if (document.reporting.ManSystem.value!="") {
				if (document.navirep && !document.navirep.reporttyp.selectedIndex) {
					document.navirep.reporttyp.selectedIndex = 0;
				}
				if (document.getElementById("headlineRep")) {
					document.getElementById("headlineRep").innerHTML = 
						document.navirep.reporttyp.options[document.navirep.reporttyp.selectedIndex].text;
					if (Number(document.navirep.reporttyp.value) == '5' || Number(document.navirep.reporttyp.value) == '6') {
						document.reporting.action="reporting_tables_audit_changes.php";
					} else {
						document.reporting.action="reporting_tables.php";
					}
					document.reporting.target="_blank";
					document.reporting.method="post";
				}
			} else {
				document.reporting.action="no_device.php";
				document.reporting.target="Report_Frame";
				document.reporting.method="post";
			}
		}
		function doDev(ManSys,Dev,DevId) {
			document.getElementById("headlineRep").innerHTML = document.navirep.reporttyp.options[document.navirep.reporttyp.selectedIndex].text;
			document.getElementById("headlineSys").innerHTML = "<?php echo $language->get_text_msg('from', 'html') ?> "+ ManSys +" / "+ Dev;
			document.reporting.devId.value=DevId;
			document.reporting.ManSystem.value=ManSys;
			document.reporting.Device.value=Dev;
			document.getElementById("Report_Frame").src="leer.php";
			position_iframe("Report_Frame");
		}
	
		function CheckIfAllSystemsSelected() {
			document.getElementById("select_all_devices").style.display="none";
			if (document.reporting.ManSystem.value == "Alle") {
				document.reporting.ManSystem.value = "&quot;Management System&quot;";
				document.reporting.Device.value = "&quot;Device&quot;";
				document.reporting.devId.value='NULL';
				document.getElementById("headlineSys").innerHTML = ManSys;
				document.getElementById("headlineSys").value = "<?php echo $language->get_text_msg('from', 'html') ?> &quot;Management System&quot; / &quot;Device&quot; (<?php echo $language->get_text_msg('select_on_left', 'html') ?>)";
				doDev(document.reporting.ManSystem.value, document.reporting.Device.value, document.reporting.devId.value);
			}
		}
		
		function SwitchReportType(){
			if (document.navirep) {
				if (!document.navirep.reporttyp.selectedIndex) document.navirep.reporttyp.selectedIndex = 0;
				if (document.getElementById("headlineRep")) {
					console.log("report_typ: " + document.navirep.reporttyp.value);
//					alert("report_typ: " + document.navirep.reporttyp.value);				
					document.getElementById("headlineRep").innerHTML = document.navirep.reporttyp.options[document.navirep.reporttyp.selectedIndex].text;
					switch (Number(document.navirep.reporttyp.value)) {
						case 1:  // Konfiguration
							document.reporting.repTyp.value="configuration";
							document.getElementById("zeitpunkt").style.display="inline";
							document.getElementById("zweizeitpunkt").style.display="none";
							document.getElementById("extra_filter_button").style.display="inline";
							document.getElementById("filter_id").style.display="inline";
							document.getElementById("filter_id_min").style.display="none";
							CheckIfAllSystemsSelected(); 
							break;
						case 2:  // Aenderungen
							document.reporting.repTyp.value="changes";
							document.getElementById("zeitpunkt").style.display="none";
							document.getElementById("zweizeitpunkt").style.display="inline";
							document.getElementById("extra_filter_button").style.display="none";
							document.getElementById("filter_id").style.display="none";
							document.getElementById("filter_id_min").style.display="inline";
							document.getElementById("select_all_devices").style.display="inline";
							break;
						case 3:  // Verwendung	
							document.reporting.repTyp.value="usage";
							document.getElementById("zeitpunkt").style.display="inline";
							document.getElementById("zweizeitpunkt").style.display="none";
							document.getElementById("extra_filter_button").style.display="none";
							document.getElementById("filter_id").style.display="none";
							document.getElementById("filter_id_min").style.display="inline";
							document.getElementById("select_all_devices").style.display="inline";
							break;
						case 4:  // Regelsuche	
							document.reporting.repTyp.value="rulesearch";
							document.getElementById("zeitpunkt").style.display="inline";
							document.getElementById("zweizeitpunkt").style.display="none";
							document.getElementById("extra_filter_button").style.display="inline";
							document.getElementById("filter_id").style.display="inline";
							document.getElementById("filter_id_min").style.display="none";
							document.getElementById("select_all_devices").style.display="inline";				
							break;
						case 5:  // Audit-Aenderungen
							document.reporting.repTyp.value="auditchanges";
							document.getElementById("zeitpunkt").style.display="none";
							document.getElementById("zweizeitpunkt").style.display="inline";
							document.getElementById("extra_filter_button").style.display="none";
							document.getElementById("filter_id").style.display="none";
							document.getElementById("filter_id_min").style.display="inline";
							document.getElementById("select_all_devices").style.display="none";
							break;
						case 6:  // Audit-Aenderungen Details
							document.reporting.repTyp.value="auditchangesdetails";
							document.getElementById("zeitpunkt").style.display="none";
							document.getElementById("zweizeitpunkt").style.display="inline";
							document.getElementById("extra_filter_button").style.display="none";
							document.getElementById("filter_id").style.display="none";
							document.getElementById("filter_id_min").style.display="inline";
							document.getElementById("select_all_devices").style.display="none";
							break;
/*
						case 7:  // Duplikate	
							document.reporting.repTyp.value="duplicates";
							document.getElementById("zeitpunkt").style.display="none";
							document.getElementById("zweizeitpunkt").style.display="none";
							document.getElementById("extra_filter_button").style.display="none";
							document.getElementById("filter_id").style.display="none";
							document.getElementById("filter_id_min").style.display="inline";
							document.getElementById("select_all_devices").style.display="none";				
							break;
*/
					}
		// Report Frame existiert nicht mehr:
		//			document.getElementById("Report_Frame").src="leer.php";
		//			position_iframe("Report_Frame");
				}
			}
		}
		window.onload=function(){
 			changeColor1('n3');
			SwitchReportType();
		}

		//-->
	</script>
</head>

<body>
	<?php
		include ($stamm."inctxt/header.inc.php");
		include ($stamm."inctxt/navi_head.inc.php");
		include ($stamm."inctxt/navi_hor.inc.php");
		include ($stamm."inctxt/navi_vert_reporting.inc.php");
	?>
	<form id="reporting" name="reporting" action="" target="" method="post">
		<input type="hidden" name="devId" value=""/>
		<input type="hidden" name="repTyp" value=""/>
		<input type="hidden" name="ManSystem" value=""/>
		<input type="hidden" name="Device" value=""/>
		<div id="inhalt">
			<table cellpadding="0" cellspacing="0" width="700">
				<tr>
					<td class="headlinemain"><b id="headlineRep" class="headlinemain">&quot;Report-Typ&quot;</b>&nbsp;<b id="headlineSys" class="headlinemain">
					<?php echo $language->get_text_msg('of', 'html') ?> &quot;Management System&quot; / &quot;Device&quot; (<?php echo $language->get_text_msg('select_on_left', 'html') ?>)</b></td>
				</tr>
			</table><br>
			<?php   
				if (isset($request['repTyp'])) $report_type = $request['repTyp'];
				include ($stamm."inctxt/reporting_filter.inc.php"); // basic filtering
				echo $linie;
			?>
		</div>
	</form>
</body>
</html>
