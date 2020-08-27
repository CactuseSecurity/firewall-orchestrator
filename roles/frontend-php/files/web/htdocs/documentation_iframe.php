<?php
// $Id: documentation_iframe.php,v 1.1.2.4 2011-05-20 12:45:25 tim Exp $
// $Source: /home/cvs/iso/package/web/htdocs/Attic/documentation_iframe.php,v $
	require_once("check_privs.php");
	require_once("display-filter.php");
	require_once ("db-input.php");
	$cleaner = new DbInput();  // for clean-function
	setlocale(LC_CTYPE, "de_DE.UTF-8");
	$request = $cleaner->clean_structure($_REQUEST);
	$session = $cleaner->clean_structure($_SESSION);
	if (!$allowedToChangeDocumentation) { header("Location: ".$stamm."index2.php"); }
?>
<!DOCTYPE HTML PUBLIC "-//W3C//DTD HTML 4.01 Transitional//EN">
<html>
<head>
<meta http-equiv="Content-Type" content="text/html; charset=UTF-8">
<?php
 $stamm="";
 $linie="<table width='730' cellspacing='0' cellpadding='0' style='margin:6px 0px;'><tr>\n
 <td style='background-color:#FFD;'><img src='".$stamm."img/1p_tr.gif' width='730' height='2' alt=''></td>\n
 </tr></table>";
?>

<script type="text/javascript" src="<?php echo $stamm ?>js/browser.js"></script>
<script type="text/javascript" src="<?php echo $stamm ?>js/script.js"></script>
<link rel="stylesheet" type="text/css" href="<?php echo $stamm ?>css/firewall.css">
<script language="javascript" type="text/javascript">
	if(is_ie) document.write("<link rel='stylesheet' type='text/css' href='<?php echo $stamm ?>css/firewall_ie.css'>");
	
	function doSelectTable(table_number) {
		var start_inversion = false;
		var next_table_number = 1 + Number(table_number);

		for (var i = 0; i < document.forms.selectchange.elements.length; i++) {
			if( document.forms.selectchange.elements[i].type == 'checkbox' ) {
				if (start_inversion && document.forms.selectchange.elements[i].name == 'alle_auswaehlen_' + next_table_number ) {
					break;
				}
				if (document.forms.selectchange.elements[i].name == 'alle_auswaehlen_' + table_number) {
					start_inversion = true;
				} else {
					if ( start_inversion) {
						document.forms.selectchange.elements[i].checked = !(document.forms.selectchange.elements[i].checked);
					}
				}
			}
		}
	}  
</script>
</head>

<body class="iframe" onLoad="javascript:parent.document.getElementById('leer').style.visibility='hidden';">

<div id="inhalt1">

	<?php
		$mgm_id_filter = 'TRUE'; $request_nr_filter = 'TRUE'; $comment_filter = 'TRUE'; $obj_comment_filter = 'TRUE';
		if (isset($request['comment_filter']) and $request['comment_filter']<>'')
			$comment_filter	= "change_comment LIKE '%" . $request['comment_filter'] . "%'";
		if (isset($request['obj_comment_filter']) and $request['obj_comment_filter']<>'')
			$obj_comment_filter	= "obj_comment LIKE '%" . $request['obj_comment_filter'] . "%'";
		if (isset($request['request_nr_filter']) and $request['request_nr_filter']<>'')
			$request_nr_filter	= "change_request_info LIKE '%" . $request['request_nr_filter'] . "%'";
		if (isset($request['mgm_id']) and $request['mgm_id']<>'NULL' and $request['mgm_id']<>'')
			$mgm_id_filter	= "mgm_id=" . $request['mgm_id'];
		if (isset($request['change_docu'])) $page = 'docu_change';
		else $page	= 'doc';
//		$felder = $_POST;  ksort($felder);  reset($felder); while (list($feldname, $val) = each($felder)) {	echo "found $feldname: $val<br>"; } reset($felder); 
		require_once ("display-filter.php");
		require_once ("db-change.php");
		require_once ("display_changes.php");
		
		$additional_filter = "security_relevant AND $mgm_id_filter AND $comment_filter AND $obj_comment_filter AND $request_nr_filter";
		if ($page=='doc') {
			$ruleFilter = new RuleChangesFilter($request,$session,'document');
			$additional_filter .= " AND NOT change_documented";
		} else  {
			$ruleFilter = new RuleChangesFilter($request,$session,'change_documentation');
			$additional_filter .= " AND change_documented";
		}
		$management_filter_4_view = $ruleFilter->getMgmFilter4View();
		$additional_filter .= " AND " . $management_filter_4_view;
		$columns = array('','Auftrag', 'Kommentar');
		if ($allowedToViewAdminNames) {
			$columns []= 'Change Admin';
			$columns []= 'Doku Admin';
		}
		$columns []= 'Typ';	$columns []= 'Betroffenes Element';	$columns []= 'Details';	$columns []= 'Quelle';
		$columns []= 'Ziel'; $columns []= 'Dienst'; $columns []= 'Aktion'; $columns []= 'Kommentar des Objekts';
		
		$db_connection = new DbConnection(new DbConfig($session["dbuser"],$session["dbpw"]));
		
		if (isset($request['tenant_id$0'])) $request_filter = $request['tenant_id$0'];
		//	if (isset($request_filter)) $additional_filter .= " AND tenant_id=$request_filter";
		if ($page=='doc') $changes = new ChangeList($ruleFilter,$additional_filter,'view_undocumented_changes');
		else $changes = new ChangeList($ruleFilter,$additional_filter,'view_reportable_changes');
		$change_number = $changes->get_displayed_change_number();
		if ($change_number==0)
			echo "Keine &Auml;nderungen zu den angegebenen Filterkriterien gefunden.<br>";
		else {
			$changeTable = new DisplayChangeTable($columns, $changes);
			echo $changeTable->displayChanges($ruleFilter,$management_filter_4_view,$change_docu_allowed = true);
		}
	?>
	<br>
</div>
</body>
</html>
