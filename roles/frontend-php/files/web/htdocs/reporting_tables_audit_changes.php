<?php
	if (!isset($_SESSION)) session_start();
	require_once ("cli_functions.php");
	require_once ("db-input.php");
	require_once ("check_privs.php");
	require_once ("display-filter.php");
	require_once ("db-rule.php");
	require_once ("db-nwobject.php");
	require_once ("db-service.php");
	require_once ("db-user.php");
	require_once ("db-report-audit.php");
	
	// display_xxx functions also needed (for filtering :-( )
	require_once ("display_rule_config.php");
	require_once ("display_nwobject_config.php");
	require_once ("display_service_config.php");
	require_once ("display_user_config.php");

	if (sizeof($args)==0) {	# not called via cli but via web
		$args = $cleaner->clean_structure($_REQUEST);
		$args['devid'] = $args['devId'];
		$args['reportdate'] = $args['zeitpunktalt'];
		$args['reportdate2'] = $args['zeitpunktneu'];
		$args['reportformat'] = $args['reportFormat_7'];
		$session = $cleaner->clean_structure($_SESSION);
		$args['mgmfilter'] = $session['ManagementFilter'];
		$args['stamm'] = '';
		$report_format = $request['reportFormat_2'];
	}
	/*
	 $request = $cleaner->clean_structure($_REQUEST);
	 $session = $cleaner->clean_structure($_SESSION);
	 echo "request:<br>\n"; print_r($request);
	 echo "session:<br>\n"; print_r($session);
	 $felder = $args;  ksort($felder);  reset($felder);
	 while (list($feldname, $val) = each($felder)) { echo ("<br>found $feldname: $val"); } reset($felder);
	 */
	$dev_id			= $args['devid'];
	$report_format	= $args['reportformat'];
	$mgm_filter		= $args['mgmfilter'];
	$tenant_id		= $args['tenant_id'];
	$stamm			= $args['stamm'];
	
	$report_date	= convert_report_date_to_postgres($args['reportdate']);
	$report_date2	= convert_report_date_to_postgres($args['reportdate2']);
	$config = new Config();
	$_SESSION["loglevel"] = $config->getLogLevel();
	$_SESSION["logtarget"] = $config->getLogTarget();
	$_SESSION["logfacility"] = $config->getLogFacility();

	$start = explode(" ", microtime());	$stime = $start[0] + $start[1];
	if (is_numeric($dev_id)) {
		$dev_name = getDevName ($dev_id, 'confexporter', '');
		if (!isset($dev_name) or $dev_name == 'NULL' or $dev_name == '') { echo "ERROR: device with ID $dev_id not found - aborting\n"; exit (1); }	
	} else {
		$dev_name = strtolower($dev_id);
		$dev_id = getDevId ($dev_name, 'confexporter', '');	
		if (!isset($dev_id) or $dev_id == 'NULL' or $dev_id == '') { echo "ERROR: device with name $dev_name not found - aborting\n"; exit (1); }	
	}
	$_REQUEST['Device'] = $dev_name;
	$_REQUEST['ManSystem'] = getMgmNameFromDevId ($dev_id, 'fworch', '');
	$_SESSION['ManagementFilter'] = $mgm_filter;
	if (isset($tenant_id) and !($tenant_id == '')) $_SESSION['tenantFilter'] = " (tenant_id=$tenant_id) ";
	$_REQUEST['devId'] = $dev_id;
	$_REQUEST['zeitpunkteins'] = $report_date;	
	$_REQUEST['zeitpunktzwei'] = $report_date2;	
	$_REQUEST['inactive'] = "0";
	$_REQUEST['notused'] = "1";
	$_REQUEST['tenant_id'] = $tenant_id;
	$request = $cleaner->clean_structure($_REQUEST);
	$session = $cleaner->clean_structure($_SESSION);
	if (!isset($tenant_id) or $tenant_id=='') { $tenant_id = 'null'; }  // very important, just do not know why yet!!!
?>	
<!doctype html>
<html>
	<head>
	    <meta charset="utf-8">
	    <?php echo "<title>Audit Report - &Auml;nderungen $dev_name, $report_date - $report_date2 </title>" ?>
	    
		<!-- ********************** including scripts and css ************************************ -->
		<link rel="stylesheet" type="text/css" href="css/firewall_print.css"/>
	 	<link rel="stylesheet" type="text/css" href="os-js/jquery-datatables/css/jquery.dataTables.min.css"/>
		<link rel="stylesheet" type="text/css" href="os-js/jquery-datatables/css/dataTables.jqueryui.min.css"/>	
	
		<script src="os-js/jquery/jquery.min.js"></script>
		<script src="os-js/jquery-datatables/jquery.dataTables.min.js"></script>
		<script src="os-js/handlebars/handlebars.js"></script>
		<script src="os-js/underscore/underscore.min.js"></script>  <!-- only for _.sortBy function in eachSort -->

	<!-- ********************** handlebars functions and templates ************************************ -->

	<script>		
		// used for sorting objects within rules (src,dst,svc) (in handlebars)
		Handlebars.registerHelper('eachSort', function(array, key, opts) {
		    for (var i = 0; i < array.length; i++) { array[i].originalIndex = i } // append metadata into data
		    var sorted = _.sortBy(array, function (i) { return i[key].toLowerCase(); })	// sort
		    return Handlebars.helpers.each(sorted, opts) // default each
		})
		
		// define Handlebars helper for comparing two values
		Handlebars.registerHelper('compare', function (lvalue, operator, rvalue, options) {
		    var operators, result;
		
		    if (arguments.length < 3) {
		        throw new Error("Handlerbars Helper 'compare' needs 2 parameters");
		    }
		
		    if (options === undefined) {
		        options = rvalue;
		        rvalue = operator;
		        operator = "===";
		    }
		
		    operators = {
		        '==': function (l, r) { return l == r; },
		        '===': function (l, r) { return l === r; },
		        '!=': function (l, r) { return l != r; },
		        '!==': function (l, r) { return l !== r; },
		        '<': function (l, r) { return l < r; },
		        '>': function (l, r) { return l > r; },
		        '<=': function (l, r) { return l <= r; },
		        '>=': function (l, r) { return l >= r; },
		        'typeof': function (l, r) { return typeof l == r; }
		    };
		
		    if (!operators[operator]) {
		        throw new Error("Handlerbars Helper 'compare' doesn't know the operator " + operator);
		    }
		
		    result = operators[operator](lvalue, rvalue);
		
		    if (result) {
		        return options.fn(this);
		    } else {
		        return options.inverse(this);
		    }
		});
	</script>

	<script id="rules-template" type="text/x-handlebars-template">
		<table id="ruletable" class="display compact cell-border">
			<thead>
				<tr>
				<th>MTR</th>
				<th>MTK</th>
				<th>Nr.</th>
				<th>Regel ID</th>
				<th>Quellzone</th>
				<th>Quelle</th>
				<th>Zielzone</th>
				<th>Ziel</th>
				<th>Dienst</th>
				<th>Aktion</th>
				<th>Kommentar</th>
				</tr>
			</thead>
			<tfoot>
				<tr>
				<th>MTR</th>
				<th>MTK</th>
				<th>Nr.</th>
				<th>Regel ID</th>
				<th>Quellzone</th>
				<th>Quelle</th>
				<th>Zielzone</th>
				<th>Ziel</th>
				<th>Dienst</th>
				<th>Aktion</th>
				<th>Kommentar</th>
				</tr>
			</tfoot>
			<tbody>
				{{#each rules}}
 					<tr>
					{{#if rule_head_text}}
 						<td>&nbsp;</td>
						<td>&nbsp;</td>
						<td>{{rule_audit_num1}}</td>
						<td>{{rule_head_text}}</td>
						<td>&nbsp;</td>
						<td>&nbsp;</td>
						<td>&nbsp;</td>
						<td>&nbsp;</td>
						<td>&nbsp;</td>
						<td>&nbsp;</td>
						<td>&nbsp;</td>
					{{else}}
 						<td>{{rule_audit_state}}</td>
						<td>{{rule_audit_comment_change_type}}</td>
						<td>{{rule_audit_num1}}</td>
						<td><p class="break">{{rule_uid}}</p></td>
						<td>{{#if rule_from_zone_name}}{{rule_from_zone_name}}{{else}}[global]{{/if}}</td>
						<td>{{#compare rule_src_neg "!=" "f"}}[NEG]<br>{{/compare}}
							{{#eachSort rule_src 'objectName'}}
								<p class="nobreak">{{#if userName}}<a href="#usr{{userId}}">{{userName}}</a> @ {{/if}}
								{{#if changed}}{{changed}}&nbsp;{{/if}}<a href="#obj{{objectId}}">{{objectName}}</a></p>
							{{/eachSort}}
						</td>
						<td>{{#if rule_to_zone_name}}{{rule_to_zone_name}}{{else}}[global]{{/if}}</td>
						<td>{{#compare rule_dst_neg "!=" "f"}}[NEG]<br>{{/compare}}
							{{#eachSort rule_dst 'objectName'}}
								<p class="nobreak">{{#if changed}}{{changed}}&nbsp;{{/if}}<a href="#obj{{objectId}}">{{objectName}}</a></p>
							{{/eachSort}}
						</td>
						<td>{{#compare rule_svc_neg "!=" "f"}}[NEG]<br>{{/compare}}
							{{#eachSort rule_svc 'name'}}
								<p class="nobreak">{{#if changed}}{{changed}}&nbsp;{{/if}}<a href="#svc{{id}}">{{name}}</a></p>
							{{/eachSort}}
						</td>
						<td>{{{rule_action}}}</td>
						<td>{{#if rule_audit_full_comment}}<p class="break">{{{rule_audit_full_comment}}}</p>{{/if}}</td>
					{{/if}}
					</tr>
				{{/each}}
			</tbody>
		</table>
	</script>
	<script id="network_objects-template" type="text/x-handlebars-template">
		<table id="nwobjecttable" class="display compact cell-border">
			<thead>
				<tr><th>Netzwerkobjektname</th>
					<th>UID</th>
					<th>IP-Adresse</th>
					<th>Typ</th>
					<th>Zone</th>
					<th>Gruppenmitglieder</th>
					<th>Kommentar</th>
				</tr>
			</thead>
			<tfoot>
				<tr><th>Netzwerkobjektname</th>
					<th>UID</th>
					<th>IP-Adresse</th>
					<th>Typ</th>
					<th>Zone</th>
					<th>Gruppenmitglieder</th>
					<th>Kommentar</th>
				</tr>
			</tfoot>
			<tbody>
				{{#each network_objects}}
  					<tr id="obj{{obj_id}}">
						<td>{{obj_name}}{{#if origin}} ({{origin}}){{/if}}</td>
						<td>{{obj_uid}}</td>
						<td>{{obj_ip}}</td>
						<td>{{obj_typ}}</td>
						<td>{{#if obj_zone}}{{obj_zone}}{{/if}}</td>
						<td>{{#if members}}
							{{#each members}}
								<a href="#obj{{obj_id}}">{{obj_name}}</a><br>
							{{/each}}
							{{/if}}</td>
						<td>{{#if obj_comment}}{{obj_comment}}{{/if}}</td>
					</tr>
				{{/each}}
			</tbody>
		</table>
	</script>
	<script id="network_services-template" type="text/x-handlebars-template">
		<table id="nwservicetable" class="display compact cell-border">
			<thead>
				<tr><th>Netzwerkservicename</th>
					<th>UID</th>
					<th>Service-Daten</th>
					<th>Typ</th>
					<th>Timeout</th>
					<th>Gruppenmitglieder</th>
					<th>Kommentar</th>
				</tr>
			</thead>
			<tfoot>
				<tr><th>Netzwerkservicename</th>
					<th>UID</th>
					<th>Service-Daten</th>
					<th>Typ</th>
					<th>Timeout</th>
					<th>Gruppenmitglieder</th>
					<th>Kommentar</th>
				</tr>
			</tfoot>
			<tbody>
				{{#each network_services}}
  					<tr id="svc{{svc_id}}">
						<td>{{svc_name}}{{#if origin}} ({{origin}}){{/if}}</td>
						<td>{{svc_uid}}</td>
						<td>{{svc_dstport}}{{#if svc_dstport_end}}{{#compare svc_dstport "!=" svc_dstport_end}}-{{svc_dstport_end}}{{/compare}}{{/if}}
							{{#if svc_ip}}/ {{svc_ip}}{{/if}}
						</td>
						<td>{{svc_typ}}</td>
						<td>{{#if svc_timeout}}{{svc_timeout}}{{/if}}</td>
						<td>{{#if members}}
							{{#each members}}<a href="#svc{{svc_id}}">{{svc_name}}</a><br>{{/each}}
							{{/if}}</td>
						<td>{{#if svc_comment}}{{svc_comment}}{{/if}}</td>
					</tr>
				{{/each}}
			</tbody>
		</table>
	</script>
	<script id="users-template" type="text/x-handlebars-template">
		<table id="usertable" class="display compact cell-border">
			<thead>
				<tr><th>Username</th>
					<th>Typ</th>
					<th>Gruppenmitglieder</th>
					<th>Kommentar</th>
				</tr>
			</thead>
			<tfoot>
				<tr><th>Username</th>
					<th>Typ</th>
					<th>Gruppenmitglieder</th>
					<th>Kommentar</th>
				</tr>
			</tfoot>
			<tbody>
				{{#each users}}
  					<tr id="usr{{usr_id}}">
						<td>{{usr_name}}{{#if origin}} ({{origin}}){{/if}}</td>
						<td>{{usr_typ}}</td>
						<td>{{#if members}}
							{{#each members}}
								<a href="#usr{{usr_id}}">{{usr_name}}</a><br>
							{{/each}}
							{{/if}}</td>
						<td>{{#if usr_comment}}{{usr_comment}}{{/if}}</td>
					</tr>
				{{/each}}
			</tbody>
		</table>
	</script>

	<!-- ********************** datatables scripts/formatting ************************************ -->
 	<script>
		$(document).ready(function() {
//			console.log("starting document ready function");
			// handlebars:
			show_rules();
			show_nw_objects();
			show_nw_services();
			show_users();
//			console.log("document ready function after calling show_xxx");

			// datatables formatting
		    $('#ruletable').DataTable( {
		    	"paging": false,
<?php 
		$report_type = $_REQUEST['repTyp'];		
		if ($report_type == 'auditchangesdetails') 
			{ echo "\"dom\": '<f<t>i>',"; } // display search field
		elseif ($report_type == 'auditchanges')
			{ echo "\"dom\": '<<t>>',"; } // do not display search field or information
?>
				"search": { "search": "[delta" },
				"columnDefs": [ { "searchable": false, "targets": 3 } ],
//		        buttons: [ 'colvis', 'print', 'pdf' ],
//		        buttons: [ 'colvis' ],
		    	"order": [[ 2, 'asc' ]],
		    	"columnDefs":	[
					<?php if ($report_type == 'auditchanges') echo '{ "visible": false, "targets": [2] },'?>  // ??? makes column infos invisible
					{ "orderable": false, "targets": [4,5,6] }
				],
				"fnCreatedRow": function(nRow, aData, iDataIndex) {
					if (aData[0].match(/^\&nbsp\;$/))		{ $('td', nRow).css({"background-color": "#C3BDBD"}); }
					if (aData[0].match(/\[delta\_R\-\]/))	{ $('td', nRow).css({"background-color": "#E96060"}); }
					if (aData[0].match(/\[delta\_R\+\]/))	{ $('td', nRow).css({"background-color": "#49D549"}); }
					if (aData[0].match(/\[delta\_R\%\]/))	{ $('td', nRow).css({"background-color": "#FAF7B2"}); }
					if (aData[1].match(/\[delta\_K0\!\]/))	{ $('td', nRow).css({"background-color": "#FFFF00"}); }
				}
		    } );
		    $('#nwobjecttable').DataTable( {
		    	"paging": false,
		    	"order": [[ 0, 'asc' ]]
		    } );
		    $('#nwservicetable').DataTable( {
		    	"paging": false,
		    	"order": [[ 0, 'asc' ]]
		    } );
		    $('#usertable').DataTable( {
		    	"paging": false,
		    	"order": [[ 0, 'asc' ]]
		    } );
//			console.log("exiting document ready function");
		} );    
    </script>
</head>
<body>
<?php	
	$log = new LogConnection();
	$cleaner = new DbInput();  // for clean-function
	$db_connection = new DbConnection(new DbConfig('confexporter',''));
	setlocale(LC_CTYPE, "de_DE.UTF-8");
	$args = $cleaner->clean_structure(getArgs2($_SERVER['argv']));	

// objectToArray is currently called from Report::objectToArray (because I do not know how to call the class method 
function objectToArray ($object) {
	if(!is_object($object) && !is_array($object)) return $object;
	return array_map('objectToArray', (array) $object);
}

function create_json_config_report($request, $session, $dev_id, $report_format, $attributes) {
	$e = new PEAR();
//	$config = new Config();
	$cleaner = new DbInput();  // for clean-function
	setlocale(LC_CTYPE, "de_DE.UTF-8");
	$log = new LogConnection();
	$log->log_debug("reporting_tables_auditchanges::create_json_config_report:: starting report generation for device $dev_name " . 
			"(id=$dev_id, format: $report_format)");
	$ruleFilter = new RuleConfigurationFilter($request,$session);
	$import_ids = new ImportIds($ruleFilter); // generating relevant import ids per mgmt in temp table  // to optimize for a single device
	$import_ids->set_all_tables();
	$rule_list = new RuleList($ruleFilter, $import_ids);
	$ruleTable = new RuleConfigTable($headers = 
			array("Nr","ID","Quelle","Ziel","Dienst","Aktion","Tracking","Install on","Kommentar","Name"), $rule_list);
	if ($e->isError($ruleTable)) { $err = $ruleTable; echo "An error occured." . $err->getMessage(); }
	else {
		// die display-Funktion hat einen Seiteneffekt!! ohne diese Aufruf liefert $ruleTable->getFilteredRuleIds() nichts zurück
		// offenbar wird das Filtern erst in der display-Funktion ausgefuert!
		$rules_out = $ruleTable->display($ruleFilter, $report_format, $import_ids);
		$filtered_rule_id = $ruleTable->getFilteredRuleIds();
		if (!($filtered_rule_id==="'{}'")) {
			$ruleFilter->setFilteredRuleIds($filtered_rule_id);
			$objectTable = new NwObjectConfigTable($headers = array("Name","Zone","Typ","IP","Member","UID","Kommentar"), 
					new NetworkObjectList($ruleFilter, $order=NULL, $import_ids));
			$nwobjects_out = $objectTable->display($ruleFilter, $report_format);
			$headers = array("Name","Typ","Member","IP-Proto.","Zielport","Quellport","Timeout<br>(sec)","UID","Kommentar");
			$serviceTable = new ServiceConfigTable($headers,new ServiceList($ruleFilter, $order=NULL, $import_ids));
			$services_out = $serviceTable->display($ruleFilter, $report_format);
			$userTable = new UserConfigTable($headers = array("Name","Typ","Uid","Kommentar","Member"), new UserList($ruleFilter, $order=NULL, $import_ids));
			$users_out = $userTable->display($ruleFilter, $report_format);
			$report = '';
			// rules:
			$tmp = $ruleTable->ruleList->rule_list;
			// TODO: import_ids, filter, display aus einzelnen rule_lists loeschen
			
			unset($tmp->db_connection);	unset($tmp->import_ids); unset($tmp->filter); unset($tmp->error);
			foreach ($tmp as $el) {
				unset ($el->error); unset ($el->db_connection); unset ($el->display); unset ($el->filter);
				unset($el->import_ids);
				foreach ($el->rule_src as $el2) unset ($el2->error);
				foreach ($el->rule_dst as $el2) unset ($el2->error);
				foreach ($el->rule_svc as $el2) unset ($el2->error);
			}
			$report .= '"rules": ' . json_encode($tmp,$attributes) . ",\n";
			// nwobjects:
			$tmp = $objectTable->nwobjectList->obj_list;
			unset($tmp->db_connection);	unset($tmp->import_ids); unset($tmp->filter); unset($tmp->error);
			foreach ($tmp as $el) { unset ($el->error); unset ($el->db_connection); unset ($el->display); unset ($el->filter); }
			$report .= '"network_objects": ' . json_encode($tmp,$attributes) . ",\n";
			// nwservices:
			$tmp = $serviceTable->serviceList->service_list;
			unset($tmp->db_connection);	unset($tmp->import_ids); unset($tmp->filter); unset($tmp->error);
			foreach ($tmp as $el) { unset ($el->error); unset ($el->db_connection); unset ($el->display); unset ($el->filter); }
			$report .= '"network_services": ' . json_encode($tmp,$attributes) . ",\n";
			// users:
			$tmp = $userTable->userList->user_list;
			unset($tmp->db_connection);	unset($tmp->import_ids); unset($tmp->filter); unset($tmp->error);
			foreach ($tmp as $el) { unset ($el->error); unset ($el->db_connection); unset ($el->display); unset ($el->filter); }
			$report .= '"users": ' . json_encode($tmp,$attributes);
			$report .= "\n";
		} else { // found not a single rule (device did not exist then?)		
			$report = '"rules": ' . json_encode(null,$attributes) . ",\n" .
					'"network_objects": ' . json_encode(null,$attributes) . ",\n" .
					'"network_services": ' . json_encode(null,$attributes) . ",\n" .
					'"users": ' . json_encode(null,$attributes);
		}
		return $report;
	}
	//	cleanup
	$import_ids->delete_relevant_import_times_from_temp_table();
	$rule_list->deleteTempReport($ruleFilter->getReportId());
}

	$log->log_debug("Starting reporting_tables_audit_changes.php (dev_id=$dev_id, dev_name=$dev_name, tenant_id=$tenant_id, date1=$report_date, date2=$report_date2; report_typ=$report_type)");
	$attributes = JSON_PRETTY_PRINT;
	$connection_string = "mongodb://localhost:27017/";
	$collection = "fworch.reports";
//	$db_type = "mongo";
	$db_type = "postgres";
	
	$rep_storage_checker = new ReportStorage(null, null);
	if ($rep_storage_checker->reportExists($db_type, $connection_string, "config", $dev_id, $report_date, null, $tenant_id, $collection, $session)) {
//		load report from database
		$json_rep = $rep_storage_checker->readReport($db_type, $connection_string, 'config', $dev_id, $report_date, null, $tenant_id, $collection, $session);
		$rep1 = new AuditReport($json_rep);
	} else {	// create 1st config report
		$report_header = '"Management-System":"' . $_REQUEST['ManSystem'] . "\",\n" . '"Device":"' . $dev_name . "\",\n" .
			"\"device_id\":" . $dev_id . ",\n" . '"report_time":' ."\"$report_date\",\n".'"tenant_id":' . $tenant_id . ",\n";
		$log->log_debug("reporting_tables_audit_changes::before first create_json_config_report");
		$r1 = "{" . $report_header . create_json_config_report($request, $session, $dev_id, $report_format, $attributes) . "}";
		$log->log_debug("reporting_tables_audit_changes::before first AuditReport()");
		$rep1 = new AuditReport($r1);
		$log->log_debug("reporting_tables_audit_changes::before first dumpReport()");
		$rep1->dumpReport($db_type, $connection_string, "config", $dev_id, $report_date, null, $tenant_id, $collection, $session);
	}
	if ($rep_storage_checker->reportExists($db_type, $connection_string, "config", $dev_id, $report_date2, null, $tenant_id, $collection, $session)) {
//		load 2nd report from database
		$rep2 = new AuditReport($rep_storage_checker->readReport($db_type, $connection_string, "config", $dev_id, 
				$report_date2, null, $tenant_id, $collection, $session));
	} else {	// create 2nd config report
		$_REQUEST['zeitpunkteins'] = $report_date2;	
		$request = $cleaner->clean_structure($_REQUEST);
		$report_header = '"Management-System":"' . $_REQUEST['ManSystem'] . "\",\n" . '"Device":"' . $dev_name . "\",\n" . 
			"\"device_id\":" . $dev_id . ",\n" . '"report_time":' ."\"$report_date2\",\n".'"tenant_id":' . $tenant_id . ",\n";
		$log->log_debug("reporting_tables_audit_changes::before second create_json_config_report");
		$r2 = "{" .$report_header . create_json_config_report($request, $session, $dev_id, $report_format, $attributes) . "}";
		$log->log_debug("reporting_tables_audit_changes::before second AuditReport()");
		$rep2 = new AuditReport($r2);
		$rep2->dumpReport($db_type, $connection_string, "config", $dev_id, $report_date2, null, $tenant_id, $collection, $session);
	}	
	$log->log_debug("reporting_tables_audit_changes::before compareRulesets()");
	$rep3 = $rep1->compareRulesets($rep2);
?>
	
    <script type="text/javascript">
	    (function($) {
			var compiled = {};
			$.fn.handlebars = function(template, data) {
				if (template instanceof jQuery) {
					template = $(template).html();
				}
				compiled[template] = Handlebars.compile(template);
				this.html(compiled[template](data));
			};
		})(jQuery);

		function show_rules() {
			var rules		= {"rules": <?php echo $rep3->getRules(); ?> };
			$('#rulediv').handlebars($('#rules-template'), rules);
		}
		function show_nw_objects() {
			var nw_objs		= {"network_objects": <?php echo $rep3->getNetworkObjects(); ?> };
			$('#nwobjdiv').handlebars($('#network_objects-template'), nw_objs);
		}
		function show_nw_services() {
			var nw_svcs		= {"network_services": <?php echo $rep3->getNetworkServices(); ?> };
			// console.log(JSON.stringify(nw_svcs, null, 5));
			$('#nwsvcdiv').handlebars($('#network_services-template'), nw_svcs);
		}
		function show_users() {
			// console.log("starting show_users");
			var users		= {"users": <?php echo $rep3->getUsers(); ?> };
			// console.log(JSON.stringify(users, null, 5));
			$('#userdiv').handlebars($('#users-template'), users);
		}
	</script>

<?php 
	// starting output:
	include("report_header.inc.php");

	// prepare empty divs for rules and objects:
?>
	<br><div id="rulediv"></div>
	<br><div id="legend">
		<h3>Legende:</h3>
		<table>
		<tr><td>[delta]</td><td>generell: &Auml;nderung</td><td>&nbsp;</td><td></td><td></td></tr>
		<tr><td>MTR</td><td>Modifikationstyp Regel</td><td>&nbsp;</td><td>MTK</td><td>Modifikationstyp Kommentar</td></tr>
		<tr><td>[R]</td><td>Regel&auml;nderung</td><td>&nbsp;</td><td>[K]</td><td>Kommentar&auml;nderung</td></tr>
		<tr><td>[R+]</td><td>Regel wurde hinzugef&uuml;gt</td><td>&nbsp;</td><td>[K+]</td><td>Kommentar wurde erg&auml;nzt</td></tr>
		<tr><td>[R-]</td><td>Regel wurde gel&ouml;scht</td><td>&nbsp;</td><td>[K-]</td><td>Kommentar wurde verk&uuml;rzt</td></tr>
		<tr><td>[R%]</td><td>Regel wurde ge&auml;ndert</td><td>&nbsp;</td><td>[K0!]</td><td>unkommentierte Regel&auml;nderung</td></tr>
		<tr><td>[R0]</td><td>Regel unver&auml;ndert</td><td>&nbsp;</td><td></td><td></td></tr>
		</table>
	</div>
	<br><br><div id="nwobjdiv"></div> 
	<br><br><div id="nwsvcdiv"></div> 
	<br><br><div id="userdiv"></div>

<?php 
	//	dumping to database
	$log->log_debug("reporting_tables_audit_changes::before dumpReport()");
	if (!($rep_storage_checker->reportExists($db_type, $connection_string, "audit", $dev_id, $report_date1, $report_date2, $tenant_id, $collection, $session))) {
		$rep3->dumpReport($db_type, $connection_string, "audit", $dev_id, $report_date1, $report_date2, $tenant_id, $collection, $session);
	}
	$endtime = explode(" ", microtime()); $etime = $endtime[0] + $endtime[1];
	$log->log_debug("report generation for device $dev_name (id=$dev_id, format: $report_format) took " . sprintf('%.2f', $etime - $stime) . " seconds.");
	echo "<br>" . date('Y-m-d H:i:s') . ": report generation for device $dev_name took " . sprintf('%.2f', $etime - $stime) . " seconds.";
	?>
</body>
</html>