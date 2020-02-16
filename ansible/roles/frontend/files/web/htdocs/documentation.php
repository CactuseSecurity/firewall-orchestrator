<?php
// $Id: documentation.php,v 1.1.2.8 2012-06-06 09:35:29 tim Exp $
// $Source: /home/cvs/iso/package/web/htdocs/Attic/documentation.php,v $
	if (!isset($_SESSION)) session_start();
	require_once ("db-input.php");
	$cleaner = new DbInput();  // for clean-function
	setlocale(LC_CTYPE, "de_DE.UTF-8");
	$request = $cleaner->clean_structure($_REQUEST);
	$session = $cleaner->clean_structure($_SESSION);

	require_once ("check_privs.php");
	require_once ('multi-language.php');
	$language = new Multilanguage($session["dbuser"]);
	$stamm = "";
	if (isset($request['change_docu'])) $page = "changedoc"; else $page = "doc";
	if (($page=="changedoc" and !$allowedToChangeDocumentation) or ($page=="doc" and !$allowedToDocumentChanges)) {
		header("Location: ".$stamm."index2.php");
	}
	require_once ("display-filter.php");
	require_once ("db-div.php");
	require_once ("operating-system.php");
	$linie = "<table width='730' cellspacing='0' cellpadding='0' style='margin:6px 0px;'><tr>\n".
	 	"<td style='background-color:#FFD;'><img src='".$stamm."img/1p_tr.gif' width='730' height='2' alt=''></td>\n</tr></table>";
	$filter	= new RuleChangesFilter($request, $session, 'document');
	$db_connection = new DbConnection(new DbConfig($session["dbuser"],$session["dbpw"])); // needed for auftragsfilter.inc.php		
	$display_config = new DisplayConfig();
	$no_of_requests_to_display = $display_config->getNumberOfRequests();
	$display_approver = $display_config->getDisplayApprover();
	$is_comment_mandatory = $display_config->getIsCommentMandatory();
	$display_request_type = $display_config->getDisplayRequestType();
?>
<!DOCTYPE HTML PUBLIC "-//W3C//DTD HTML 4.01 Transitional//EN">
<html>
<head>
<meta http-equiv="Content-Type" content="text/html; charset=UTF-8">
<title>ITSecOrg Documentation</title>
<meta name="robots" content="index,follow">
<meta http-equiv="cache-control" content="no-cache">
<meta http-equiv="content-language" content="de">
<script type="text/javascript" src="<?php echo $stamm ?>js/client.js"></script>
<script type="text/javascript" src="<?php echo $stamm ?>js/script.js"></script>
<link rel="stylesheet" type="text/css" href="<?php echo $stamm ?>css/firewall.css">
<script language="javascript" type="text/javascript">
	if(is_ie) document.write("<link rel='stylesheet' type='text/css' href='<?php echo $stamm ?>css/firewall_ie.css'>");

	function doMgmt(ManSys,ManId) {
		document.getElementById("headlineSys").innerHTML = ManSys;
		document.forms.docu_filter.mgm_id.value=ManId;
		document.getElementById("ChangeFrame").src="leer.php";
	}
	function checkform(max_requests, display_approver, display_request_type, comment_field_is_mandatory) {
		if (max_requests > 9) {
			alert ("<?php echo $language->get_text_msg ('missing_comment', 'plain_text'); ?>");
			return false;
		}
		if (comment_field_is_mandatory && (!document.forms.docu_values.comment || !document.forms.docu_values.comment.value || document.forms.docu_values.comment=='')) {
			alert ("<?php echo $language->get_text_msg ('missing_comment', 'plain_text'); ?>");
			document.forms.docu_values.comment.focus();
			return false;
		}

		var non_empty_request_ids = '';
		var non_empty_client_ids = '';
		var non_empty_req_type_ids = '';
		
		for (var idx = 0; idx < max_requests; idx++) {
			var req_id_field_name = 'request' + '$' + idx;
			var client_id_field_name = 'client_id' + '$' + idx;
			var req_type_id_field_name = 'request_type_id' + '$' + idx;

			if (document.forms.docu_values[req_id_field_name].value && document.forms.docu_values[req_id_field_name].value != '')
				non_empty_request_ids += idx;				
			if (document.forms.docu_values[client_id_field_name].value && document.forms.docu_values[client_id_field_name].value != '')
				non_empty_client_ids += idx;
			if (document.forms.docu_values[req_type_id_field_name].value && document.forms.docu_values[req_type_id_field_name].value != '')
				non_empty_req_type_ids += idx;
		}
		// alert ("req_ids: " + non_empty_request_ids); alert ("req_type_ids: " + non_empty_req_type_ids);	alert ("cust_ids: " + non_empty_client_ids);
		if (non_empty_request_ids == '') {
			alert ("<?php echo $language->get_text_msg ('missing_request_number', 'plain_text'); ?>");
			return false;
		}
		if (display_approver && non_empty_request_ids!=non_empty_client_ids) {
			alert ("<?php echo $language->get_text_msg ('missing_client_for_request', 'plain_text'); ?>");
			return false;
		}
		if (display_request_type && non_empty_request_ids!=non_empty_req_type_ids) {
			alert ("<?php echo $language->get_text_msg ('missing_request_type', 'plain_text'); ?>");
			return false;
		}
		/* die ausgewaehlten Aenderungen aus dem iframe in den top frame uebertragen */
		for (var i = 0; i < parent.ChangeFrame.document.forms.selectchange.elements.length; i++) {
			var VarName = parent.ChangeFrame.document.forms.selectchange.elements[i].name;
			if (VarName.search(/^alle_auswaehlen_/) == -1) {  // nicht select_table Flag, sondern einzelner Change
				if (parent.ChangeFrame.document.forms.selectchange.elements[i].checked) {
					document.forms.docu_values.change_selection.value += VarName;
				}
			}
		}
		if (!document.forms.docu_values.change_selection.value || document.forms.docu_values.change_selection.value=='') {
			alert ("<?php echo $language->get_text_msg ('no_change_selected', 'plain_text'); ?>");
			parent.ChangeFrame.focus();
			return false;
		}
		document.forms.docu_values.action="submit_documentation_data.php";
		document.forms.docu_values.target="_self";
		document.forms.docu_values.method="post";
		return true;
	}
</script>
</head>

<?php
	if ($page=='doc') $color_page = 'n1'; else $color_page = 'n2';
	echo "<body onLoad=\"changeColor1('" . $color_page . "'); doMgmt('" . $language->get_text_msg('all_systems', 'html') . "','NULL')\">";

	require_once ("db-div.php");
	require_once ("db-client.php");
	require_once ("db-gui-config.php");
	require_once ("display_changes.php");

	include ($stamm."inctxt/header.inc.php");
	include ($stamm."inctxt/navi_head.inc.php");
	include ($stamm."inctxt/navi_hor.inc.php");
	include ($stamm."inctxt/navi_vert_documentation.inc.php");
	
	$format = 'html';
	echo '<FORM id="docu_values" name="submit_docs" action="" target="_self" method="post" onSubmit="return checkform(' .
			$no_of_requests_to_display . ',' . $display_approver . ',' . $display_request_type . ',' . $is_comment_mandatory . ');">';
		echo '<div id="inhalt">';
			echo '<input type="hidden" name="doc_type" value="' . $page . '"/>';
			echo '<input type="hidden" name="change_selection" value=""/>';
			$db_connection	= new DbConnection(new DbConfig($_SESSION["dbuser"],$_SESSION["dbpw"]));
			$clist			= new ClientList($filter,$db_connection);  // filter schon in navi_vert_document.inc.php gesetzt
			$rtlist			= new RequestTypeList($filter,$db_connection);  
			$isoadmin		= new IsoAdmin($filter,$db_connection);
			$isoadmin_id	= $isoadmin->getId();
			$isoadmin_fullname = $isoadmin->getFullName();
			$headers = '';
			$changeTable = new DisplayChangeTable($headers, $changes);
				echo "<INPUT type=\"hidden\" value=\"" . $isoadmin_id . "\" name=\"doku_admin_id\">";
				echo '<INPUT type="hidden" value="' . $isoadmin_fullname . '" name="doku_admin">';	
				if ($page=='doc') $page_title = $language->get_text_msg ('documentation_title', 'html');
				else $page_title = $language->get_text_msg ('change_documentation_title', 'html');
				echo '<b class="headlinemain">' . $page_title . '</b>' .
					'&nbsp;<b class="headlinemain">(' . $language->get_text_msg ('management_filter', 'html') .
					': <b><b id="headlineSys" class="headlinemain">' .
					$language->get_text_msg ('all', 'html') . '</b><b class="headlinemain">)</b>';
				echo '<br><table>' . $changeTable->displayRowSimple($format);
					$auftragsdaten = '<FIELDSET><LEGEND>' . $language->get_text_msg ('request_data', 'html') . '</LEGEND><table>';
// Genehmigung
					if ($display_approver) {
						$auftragsdaten .=  $changeTable->displayRowSimple($format) . 
							$changeTable->displayColumnNoBorder($language->get_text_msg ('client', 'html'), $format);	
						for ($i = 0; $i < $no_of_requests_to_display; ++ $i)
							$auftragsdaten .= $changeTable->displayColumnNoBorder
								($clist->get_client_menue_string($i, (($i==0)?$default_client:0),$filter_is_mandatory=false,$session["dbuser"]),$format);
					} else { // same in hidden notation
						for ($i = 0; $i < $no_of_requests_to_display; ++ $i)
							$auftragsdaten .= '<input type="hidden" name="client_id$' . $i . '" value="NULL"/>';
					}
// Auftragstyp
					if ($display_request_type) {
						$auftragsdaten .= $changeTable->displayRowSimple($format) .
							$changeTable->displayColumnNoBorder($language->get_text_msg ('request_type', 'html'), $format);
						for ($i = 0; $i < $no_of_requests_to_display; ++ $i)
							$auftragsdaten .= $changeTable->displayColumnNoBorder
								($rtlist->get_request_type_menue_string($i, (($i==0)?$default_request_type:0),$filter_is_mandatory=false),$format);
									//setting filter_is_mandatory to false results in no default_request_type to be chosen
					} else {	// same in hidden notation
						for ($i = 0; $i < $no_of_requests_to_display; ++ $i)
							$auftragsdaten .= '<input type="hidden" name="request_type_id$' . $i . '" value="NULL"/>';					
					}	
// Auftragsnummer
					$auftragsdaten .=  $changeTable->displayRowSimple($format) . 
						$changeTable->displayColumnNoBorder($language->get_text_msg ('request_number', 'html'), $format);
					for ($i=0; $i<$no_of_requests_to_display; ++$i)
						$auftragsdaten .= $changeTable->displayColumnNoBorder('<input type="text" name="request$' . $i .
						'" value="" size="10" maxlength="15" class="diff_nosize" />',$format);
					$auftragsdaten .= '</table></FIELDSET>';
					echo $changeTable->displayColumnNoBorder($auftragsdaten,$format);
					echo $changeTable->displayRowSimple($format);
					$comment_daten = '<table>' . $changeTable->displayRowSimple($format) . 
						$changeTable->displayColumnNoBorder($language->get_text_msg ('comment', 'html'), $format);
					$comment_daten .= $changeTable->displayColumnNoBorder('<textarea name="comment" cols="60" rows="2" class="diff_nosize"></textarea>',$format);
					$comment_daten .= '</table>';
					echo $changeTable->displayColumnNoBorder($comment_daten,$format);
				
					$submit_buttons =	'<input type="submit" value="' . $language->get_text_msg ('form_send', 'html') . '" class="button">' . 
										'&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;' . '<INPUT type="reset" value="' .  $language->get_text_msg ('form_reset', 'html') . '" class="button">';
					echo $changeTable->displayColumnNoBorder($submit_buttons,$format);
				echo '</table><p>';
		echo "</div>";
	echo "</FORM>";
?>

<iframe id="ChangeFrame" name="ChangeFrame" src="leer.php">
	[<?php echo $language->get_text_msg ('no_frames', 'html');?>]
</iframe>

<?php include ("leer.inc.php"); ?>
<script language="javascript" type="text/javascript">
	Hoehe_Frame_setzen("ChangeFrame");
</script>	
</body>
</html>
