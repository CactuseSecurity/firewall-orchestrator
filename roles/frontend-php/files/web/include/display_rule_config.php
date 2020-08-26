<?php

/*
 * $Id: display_rule_config.php,v 1.1.2.28 2012-05-28 10:36:44 tim Exp $
 * $Source: /home/cvs/iso/package/web/include/Attic/display_rule_config.php,v $
 * Created on 28.10.2005
 *
 */
 
require_once ("db-rule.php");
require_once ("db-tenant.php");
require_once ("db-nwobject.php");
require_once ("display-table.php");

class RuleConfigTable extends DisplayTable{

	var $ruleList;
	var $tableString;
	var $displayRowsInTable;
	var $filtered_rule_ids;
	var $error;

	function __construct($headers,$ruleList) {
		$this->error = new PEAR();
		$this->name 	= "ruleTable";
		$this->headers 	= $headers;
		if($this->error->isError($ruleList)) {
			$error = $ruleList;
			$this->error->raiseError($error->getMessage());
		} else {
			$this->ruleList = $ruleList;
		}

		($this->ruleList->getRows() == 0) ? $this->displayRowsInTable = false : $this->displayRowsInTable = true;
		$this->tableString = NULL;
	}
	function display($filter, $report_format,$import_ids) {
		$this->filter = $filter;
		$this->tableString =	$this->displayTableHideShow($this->filter->getStamm(),"rule_id","rule_id_min","Regeln",$report_format) .
								$this->displayShowHideColumn("colIndex_rule",$report_format) .
								$this->displayTableOpen($report_format) .
								$this->displayRules($this->filter,$report_format,$import_ids) .
								$this->displayTableClose($report_format) .
								$this->displayTableShowHide($this->filter->getStamm(),"rule_id","rule_id_min","Regeln",$report_format);
		return $this->tableString;
	}
	function getFilteredRuleIds() {
		return $this->filtered_rule_ids;
	}
	function displayRules($filter, $report_format, $import_ids) {
		$ruleTable = "";
		$rule_zone_Table = '';
		$zones = array();
		$filter->emptyFilteredRowIds();
		$no_of_rules = 0;
		$rowsFiltered= 0;
		$filtered_rules = $this->ruleList->getRules($filter->getReportTime(), $filter->getReportId()); 
		if($this->error->isError($filtered_rules)) {
			$error = $filtered_rules;
			return $this->displayCommentLine("A technical problem occured:" . $error->getMessage(), $report_format); 
		}
		if (!$this->displayRowsInTable) $ruleTable .= $this->displayTableHeaders($report_format). $this->displayCommentLine('Keine Regeln gefunden.', $report_format);
		else {	// Auslesen der max. Anzahl anzeigbarer Regeln aus gui.conf --> sollte groesser als max rulenumber des groessten Regelwerks sein
				// da diese Beschraenkung jetzt auch fuer Konfiguration-Reports gilt, empfohlener Wert: 1500
			$displ_conf = new DisplayConfig();
			if ($this->isHtmlFormat($report_format)) $max_rules_to_display = 1000000000;
			else $max_rules_to_display = $displ_conf->getMaxRuleDisplayNumber();
			$device_of_last_rule = NULL;
			foreach ($filtered_rules as $rule) {
				if (!is_null($max_rules_to_display) and $no_of_rules < $max_rules_to_display) {					
					if (!$this->filter($filter,$rule)) {
						continue;
					}
					else {
						$filter->addFilteredRow($rule->getRuleId());	
					}
					if ($device_of_last_rule != $rule->rule_dev_id) { // bei Beginn eines neuen Devices --> neue Tabelle
						$ruleTable .= $this->displayRuleDeviceHeading($rule, $report_format);
						$ruleTable .= $this->displayTableHeaders($report_format);
					}
					$device_of_last_rule = $rule->rule_dev_id;
					if ($report_format == 'junos') {
						$fromZone = $rule->getFromZoneName();
						$toZone = $rule->getToZoneName();
						if (!isset($fromZone) or $fromZone == '') $fromZone = 'dummyZone';
						if (!isset($toZone) or $toZone == '') $toZone = 'dummyZone';
						$rule_zones = "from-zone $fromZone to-zone $toZone";
						if (!in_array($rule_zones, $zones)) {	// adding new zone tuple
							$zones[]= $rule_zones; // add zone tuple to array
							$rule_zone_Table [$rule_zones] = '';
						}
					}
					if ( $rule->isRuleHeader() or $rule->is_zone_header()) { // display header text only if it exists
						if (is_null($filter->gettenantId()) or $rule->is_zone_header() or // only show zone headers, when using tenant-filter
							(!is_null($filter->tenant_id) and $filter->gettenantId()==0)) {  // show headers for standard tenant
							if ($this->isHtmlFormat($report_format)) $ruleTable .= $this->displayRuleHeaderHtml($rule);
							else {
								if (isset($rule_zones))
									$rule_zone_Table [$rule_zones] .= $this->displayCommentLine("rule heading " . $rule->getRuleHeadText(), $report_format);
							}
						}
					} else { // normal rule
						if ($report_format == 'junos')
							$rule_zone_Table[$rule_zones] .=  $this->displayRuleJunos($rule, $no_of_rules, $report_format, $import_ids);
						else if ($report_format == 'csv' or $report_format == 'ARS.csv' or $report_format == 'ARS.noname.csv') {
							$ruleTable .=  $this->displayRuleCsv($rule, $no_of_rules, $report_format, $filter, $import_ids);  
						} 
						else if ($report_format == 'json') {
							$ruleTable .=  $this->displayRuleJson($rule, $no_of_rules, $report_format, $filter, $import_ids);  
						} else
							$ruleTable .= $this->displayRuleHtml($rule, $no_of_rules, $report_format);	// output html and simple.html rule
						if (!($report_format === 'ARS.csv' or $report_format == 'csv' or $report_format == 'ARS.noname.csv'))
							++ $no_of_rules; 	// bei ARS.csv kann es notwendig sein, aus einer Regel mehrere zu machen!
					}
					if ($this->isHtmlFormat($report_format)) $ruleTable .= "</tr>";
				} else ++ $no_of_rules;  // already too many rules for displaying
			} # naechste Regel
			if ($report_format == 'json') $ruleTable .= "]\n";
			if ($this->isHtmlFormat($report_format) and !is_null($max_rules_to_display) and $no_of_rules > $max_rules_to_display) { // no rule cutoff for ascii reports
				$warning_too_many_rules_found = '<td class="celldev" colspan="'.count($this->headers).
					'">Es wurden ' . $no_of_rules . ' (> ' . $max_rules_to_display . ') passende Regeln gefunden. Ausgabe abgeschnitten.</td>';
				$ruleTable = $warning_too_many_rules_found . $ruleTable . $warning_too_many_rules_found;
			}
			if (count($filter->getFilteredRowIds()) == 0) {
				$ruleTable .= $this->displayCommentLine('Keine Regeln gefunden (Filter)', $report_format);
			}
		}
		$this->filtered_rule_ids = $filter->getFilteredRowIdsArrayString();
		if ($report_format == 'junos') foreach ($zones as $zone) $ruleTable .= "\t\t$zone {\n" . $rule_zone_Table[$zone] . "\t\t}\n";
		return $ruleTable;
	}
	function displayRuleJunos ($rule, $no_of_rules, $report_format) {
		$rule_comments = '';
		$ruleTable = '';
		$rule_annotate = $rule->getComment();
		if (isset($rule_annotate) and $rule_annotate <> '') $ruleTable .= "\t\t\t/* $rule_annotate */\n";
		$ruleTable .= "\t\t\t";
		if (!($rule->isRuleDisabled())) $ruleTable .= "inactive: ";			#### das geht noch nicht, sollte nicht auftreten, da nur aktive Regeln exportiert werden (sollen)
		$ruleTable .= "policy $no_of_rules {\n";
		$ruleTable .= "\t\t\t\tmatch {\n";
		$ruleTable .= $this->displayJunosNWObjLine("source-address", $rule->getRuleSource());
		$ruleTable .= $this->displayJunosNWObjLine("destination-address", $rule->getRuleDestination());
		$ruleTable .= $this->displayJunosSvcObjLine("application", $rule->getRuleService());
		$ruleTable .= "\t\t\t\t}\n\t\t\t\tthen {\n";
// 	action handling
		$orig_action = $rule->getAction();
		switch ($orig_action) {
			case 'accept': case 'access': case 'permit': 
				$action = 'permit'; break;
			case 'tenant encrypt': case 'tenant auth': case 'auth': case 'encrypt': case 'user auth': case 'session auth': case 'actionlocalredirect':
			case 'permit webauth': case 'redirect': case 'map': case 'permit auth': case 'tunnel l2tp': case 'tunnel vpn-group': case 'tunnel vpn':  
				$action = 'permit';
				$rule_comments .= "original action of the following rule was $orig_action, converted to $action, ";
				break;
			case 'deny': case 'drop': $action = 'deny'; break;
			case 'reject': $action = 'reject'; break;
			default: $rule_comments .= "warning: unknown action found: $orig_action, "; $action = 'undefined-action';
		}
		$ruleTable .= "\t\t\t\t\t$action;\n";
		$orig_track = strtolower($rule->getTrack());
		if ($orig_track <> 'none') {
			$track = '';
			if (in_array($orig_track, array('log', 'alert', 'userdefined', 'userdefined 1', 'userdefined 2', 'userdefined 3', 'userdefined 4', 'snmptrap',
					'log alert', 'log count', 'log alert count', 'log alert count alarm', 'log count alarm'))) 
				$track .= "\t\t\t\t\tlog {\n\t\t\t\t\t\tsession-close;\n\t\t\t\t\t}\n";
			if (in_array($orig_track, array('account', 'count', 'count alarm', 'log count', 'log alert count', 'log alert count alarm', 'log count alarm')))
				$track .= "\t\t\t\t\tcount;\n";
			if ($track == '') $rule_comments .= "warning: unknown track found: '$orig_track', ";
			$ruleTable .= $track;
		}
		$ruleTable .= "\t\t\t\t}\n\t\t\t}\n";
		if ($rule->isRuleSourceNegated()) $rule_comments .= "source is negated in original rule, feature not available in JUNOS, ";
		if ($rule->isRuleDestinationNegated()) $rule_comments .= "destination is negated in original rule, feature not available in JUNOS, ";
		if ($rule->isRuleServiceNegated()) $rule_comments .= "service is negated in original rule, feature not available in JUNOS, ";
		if ($rule_comments <> '') { 
			$rule_comment = substr($rule_comments, -2); // letztes ", " abschneiden
			$ruleTable = $this->displayCommentLine($rule_comments, $report_format) . $ruleTable;
		}
		return $ruleTable; 
	}
	function displayRuleCsv ($rule, &$rule_number, $report_format, $filter, $import_ids) {
		$db_conn = $rule->db_connection;
		$rule_comments = '';
		$ruleTable = '';
		if ($report_format == 'csv') {
			$rule_postfix = '"","","","' . $rule->getComment() . '"' . "\n";
		} else {
			$rule_postfix = '"","","","' . '"' . "\n";		
		}
		$orig_action = $rule->getAction();
		switch ($orig_action) {
			case 'accept': case 'access': case 'permit': 
				$action = 'Erlauben'; break;
			case 'tenant encrypt': case 'tenant auth': case 'auth': case 'encrypt': case 'user auth': case 'session auth': case 'actionlocalredirect':
			case 'permit webauth': case 'redirect': case 'map': case 'permit auth': case 'tunnel l2tp': case 'tunnel vpn-group': case 'tunnel vpn':  
				$action = 'Erlauben';
				$rule_comments .= "original action of the following rule was $orig_action, converted to $action, ";
				break;
			case 'deny': case 'drop': case 'reject': $action = 'Verbieten'; break;
			default: $rule_comments .= "warning: unknown action found: $orig_action, "; $action = 'undefined-action';
		}
		$rule_src = $rule->getRuleSource();
		$rule_dst = $rule->getRuleDestination();
		$source			= $this->displayCsvResolveNWObjects($rule_src, $rule->isRuleSourceNegated(), $report_format, $filter, $import_ids);
		$destination	= $this->displayCsvResolveNWObjects($rule_dst, $rule->isRuleDestinationNegated(), $report_format, $filter, $import_ids);
		$service		= $this->displayCsvResolveSvcObjects($rule->getRuleService(), $rule->isRuleServiceNegated(), $report_format, $filter, $import_ids);
		$action = '"' . $action . '"';
		$id_field = '';
		if ($report_format == 'csv') {
			$name = $rule->getRuleName();
			$uid = $rule->getRuleUid();
			$id_field = ',"';
//			if (isset($rule_annotate) and is_numeric($rule_annotate)) $uid = $rule_annotate;
			if (isset($uid) and !($uid == '')) $id_field .= $uid;
			if (isset($uid) and !($uid == '') and isset($name) and !($name == '')) $id_field .= '/';
			if (isset($name) and !($name == '')) $id_field .= $name;
			$id_field .= '"';
		}

//		do tenant filtering within groups (CSV report format only)
		if (($report_format === 'csv' or $report_format === 'ARS.csv' or $report_format == 'ARS.noname.csv') and 
			isset($filter->tenant_filter_expr) and stripos($filter->tenant_filter_expr, 'TRUE') === false) { // then flatten and tenant-filter groups
				// NB: tenant_filter_expr contains 'false' (user not able to see any tenants) is not in the filter: this should not occur
//			$log = new LogConnection(); $log->log_debug("tenant_filter_expr: " . $filter->tenant_filter_expr);
			$rule_src_ar = $this->getIpArray($source, ',', '<br>', $report_format);
			$rule_dst_ar = $this->getIpArray($destination, ',', '<br>', $report_format);
			$tenant_network_table	= new tenantNetList($filter,$db_conn,'true');
			$tenant_network_ar = $tenant_network_table->tenant_net_ar;
	 		if (!$this->IpOverlapsAr($rule_dst_ar, $tenant_network_ar)) {
				$source = $this->tenantFilter($source, $tenant_network_ar, ',', '<br>', $report_format);
			}
			if (!$this->IpOverlapsAr($rule_src_ar, $tenant_network_ar)) {
				$destination = $this->tenantFilter($destination, $tenant_network_ar, ',', '<br>', $report_format);
			}
		}
		
//		split single rule into multiple rules if fields are too long:
		if ($report_format === 'ARS.csv' or $report_format == 'ARS.noname.csv') $max_field_length = 254;
		else $max_field_length = 999999;
		$source_ar = $this->splitLongField($source, ',', '<br>', $max_field_length, $report_format);
		$destination_ar = $this->splitLongField($destination, ',', '<br>', $max_field_length, $report_format);
		$service_ar = $this->splitLongField($service, ',', '<br>', $max_field_length, $report_format);
//		end of split
		foreach ($source_ar as $source)
			foreach ($destination_ar as $destination)
				foreach ($service_ar as $service) $ruleTable .= '"' . ++$rule_number . '"' . $id_field . ",$action,$source,$destination,$service,$rule_postfix";
		return $ruleTable; 
	}
	function displayRuleJson ($rule, &$rule_number, $report_format, $filter, $import_ids) {
		// remove filter, error, import_id first
		$short_rule = $rule;
		unset($short_rule->db_connection);	unset($short_rule->import_ids); unset($short_rule->filter);
		unset($short_rule->error); 
		foreach ($short_rule->rule_src as $el) unset ($el->error);
		foreach ($short_rule->rule_dst as $el) unset ($el->error);
		foreach ($short_rule->rule_svc as $el) unset ($el->error);
		
		return json_encode($short_rule,JSON_PRETTY_PRINT) . "\n";
	}
	function IpOverlapsAr($ip_ar1, $ip_ar2) {
		foreach ($ip_ar2 as $ip2) {
			foreach ($ip_ar1 as $ip1) if ($this->filter->ips_overlap($ip2, $ip1)) return true;
		}
		return false;
	}
	function displayCsvResolveNWObjects($elements, $negated, $report_format, $filter, $import_ids) {
		$result = '';
 		foreach ($elements as $element) {
			$result .= $this->displayCsvResolveNwObjectName($element->getObjectName(), $element->getObjectId(), $filter, $import_ids);
			$result .= "<br>";
		}
		$result = substr($result,0,strlen($result)-4);
		$result .= ',';
		foreach ($elements as $element) {
			$result .= $this->displayCsvResolveNwObjectIp($element->getObjectIp(), $element->getObjectId(), $filter, $import_ids);
			$result .= "<br>";
		}
		$result = substr($result,0,strlen($result)-4);
		return $result;
	}
	function displayCsvResolveSvcObjects($elements, $negated, $report_format, $filter, $import_ids) {  // hier werden auch Gruppen aufgeloest
		$result = '';
		foreach ($elements as $element) {
			$result .= $this->displayCsvResolveSvcObjectProto($element->getIpProtoId(), $element->getId(), $filter, $import_ids);
			$result .= "<br>";
		}
		$result = substr($result,0,strlen($result)-4);
		$result .= ',';
		foreach ($elements as $element) {
			$result .= $this->displayCsvResolveSvcObjectPortName($element->getPort(), $element->getName(), $element->getId(), $filter, $import_ids);
			$result .= "<br>";
		}
		$result = substr($result,0,strlen($result)-4);
		return $result;
	}
	// TODO: laengenbegrenzungen, was soll bei Any Service als IP proto / Port stehen (momentan undefined)
	function displayCsvResolveNwObjectName($name, $id, $filter, $import_ids) { // hier werden auch Gruppen aufgeloest
		$result = '';
		$flat_group = new NWObjectGroupFlatNoGroups($id, $filter, $import_ids);
		foreach ($flat_group->getObjectNames() as $flat_member) $result .= "$flat_member<br>";
		$result = substr($result,0,strlen($result)-4);
		return $result;
	}
	function displayCsvResolveNwObjectIp($ip, $id, $filter, $import_ids) { // hier werden auch Gruppen aufgeloest
		$result = '';
		$flat_group = new NWObjectGroupFlatNoGroups($id, $filter, $import_ids);
		foreach ($flat_group->getObjectIps() as $flat_member) $result .= "$flat_member<br>";
		$result = substr($result,0,strlen($result)-4);
		return $result;
	}
	function getIpArray($nw_objs, $field_separator, $element_separator, $report_format) {
		list($obj_names, $obj_ips) = explode("$field_separator", $nw_objs);
		return explode ("$element_separator", $obj_ips);
	}
	function tenantFilter($nw_objs, $tenant_net_ar, $field_separator, $element_separator, $report_format) {
		list($obj_names, $obj_ips) = explode("$field_separator", $nw_objs);
		$obj_name_ar = explode ("$element_separator", $obj_names);
		$obj_ip_ar = explode ("$element_separator", $obj_ips);

		// hier kommt das Filtern
		$obj_ip_ar2 = array();
		$obj_name_ar2 = array();
		for ($i=0; $i<count($obj_ip_ar); $i++) {
			$obj_ip = $obj_ip_ar[$i];
			$obj_name = $obj_name_ar[$i];
			if ($this->IpOverlapsAr(array($obj_ip), $tenant_net_ar)) {
				$obj_ip_ar2[] = $obj_ip;
				$obj_name_ar2[] = $obj_name;
			}
		}
		$obj_str = '';
		if (!($report_format == 'ARS.noname.csv')) { 
			for ($i=0; $i<count($obj_name_ar2); $i++)  $obj_str .= $obj_name_ar2[$i] . "$element_separator";	
			$obj_str = substr($obj_str,0,strlen($obj_str)-strlen($element_separator));
		}
		$obj_str .= ',';
		for ($i=0; $i<count($obj_ip_ar2); $i++) $obj_str .= $obj_ip_ar2[$i] . "$element_separator";	
		$obj_str = substr($obj_str,0,strlen($obj_str)-strlen($element_separator));
		$obj_str .= ',"';
		return $obj_str;
	}
	function splitLongField($field, $field_separator, $element_separator, $max_field_length, $report_format) {
		list($field_names, $field_data) = explode(",", $field);
//		if (strlen($field_names)>$max_field_length) echo ("field too long: $field_names\n");
		$field_name_ar = explode ("<br>", $field_names);
		$field_data_ar = explode ("<br>", $field_data);
		$target_id = 0;
		$target_name[$target_id] = $field_name_ar[0];
		$target_data[$target_id] = $field_data_ar[0];
		for ($i=1; $i<count($field_name_ar); $i++) {
			if (strlen($target_name[$target_id])+strlen($field_name_ar[$i])<$max_field_length-5 and
				strlen($target_data[$target_id])+strlen($field_data_ar[$i])<$max_field_length-5) {
				$target_name[$target_id] .= "<br>" . $field_name_ar[$i];
				$target_data[$target_id] .= "<br>" . $field_data_ar[$i];
			} else {
				$target_id++;
				$target_name[$target_id] = $field_name_ar[$i];
				$target_data[$target_id] = $field_data_ar[$i];
			}
		}
		for ($i=0; $i<count($target_name); $i++) {
			if ($report_format == 'ARS.noname.csv') $target[$i] = '"' . '","'. $target_data[$i] . '"';
			else $target[$i] = '"' . $target_name[$i] . '","'. $target_data[$i] . '"';
		}
		return $target;
	}
	function displayCsvResolveSvcObjectPortName($port, $name, $id, $filter, $import_ids) {
		$result = ''; $ports = ''; $names = '';
		$flat_group = new ServiceGroupFlat($id, $filter, $import_ids);
		foreach ($flat_group->getServiceNames() as $flat_member) $names .= "$flat_member|";
		foreach ($flat_group->getPorts() as $flat_member) $ports .= "$flat_member|";
		$ports_array = explode('|', substr($ports,0,strlen($ports)-1));
		$names_array = explode('|', substr($names,0,strlen($names)-1));
		for ($i=0; $i<count($ports_array); $i++) {
			$port = $ports_array[$i];
			$name = $names_array[$i];
			$service = "$port/$name";
			if ($port=='' and !($name==='') ) $service = "$name";
			if ($port=='' and $name=='') $service = "undefined";
			$result .= "$service<br>";
		}
		$result = substr($result,0,strlen($result)-4);
		return $result;
	}
	function displayCsvResolveSvcObjectProto($proto, $id, $filter, $import_ids) {
		$result = ''; $protos = '';
		$flat_group = new ServiceGroupFlat($id, $filter, $import_ids);
		foreach ($flat_group->getServiceProtoIds() as $flat_member) {
			switch ($flat_member) {
				case 1 : $proto_string = 'icmp'; break;
				case 6 : $proto_string = 'tcp'; break;
				case 17 : $proto_string = 'udp'; break;
				default : $proto_string = $flat_member; break;
			}
			$protos .= "$proto_string|";
		}
		$protos_array = explode('|', substr($protos,0,strlen($protos)-1));
		for ($i=0; $i<count($protos_array); $i++) {
			$proto = $protos_array[$i];
			if ($proto=='') $proto = "undefined";
			$result .= "$proto<br>";
		}
		$result = substr($result,0,strlen($result)-4);
		return $result;
	}
	function displayRuleHeaderHtml ($rule) {
		$ruleTable = '';
		$lastElement = count($this->headers);
		$ii=1;
		foreach ($this->headers as $header) {
			if ($header == "Nr") {
				$ruleTable .= '<td class="fw_headerdev" height="21">' . 
					'<div style="position:relative;top:1px;left:3px;width:100%;height:21px;">' . 
					'<div style="position:absolute;white-space:nowrap;border:0px none;">' .
					$rule->getRuleHeadText().'</div></div></td>';
			} else if ($ii != $lastElement) {
				$ruleTable .= '<td class="fw_headerdev">&nbsp;</td>';
			} else if ($ii == $lastElement) {
			  $ruleTable .= '<td class="fw_headerdev_right">&nbsp;</td>';
			}
			$ii++;
		}
		return $ruleTable;
	}
	function displayRuleHtml ($rule, $no_of_rules, $report_format) {
		$chNr = 'ch'. ($no_of_rules);
//		if ($this->getSourceString($rule->getRuleSource(), $rule->isRuleSourceNegated()) === '' and 
//			$this->getDestinationString($rule->getRuleDestination(), $rule->isRuleDestinationNegated()) === '') {
//			return '';
//			// do nothing
//		} else {
			$ruleTable = $this->displayRow($chNr,$report_format);
			$ruleTable .= ($this->displayColumn( ($no_of_rules + 1).
				$this->getDisabledString($rule->isRuleDisabled()), $report_format).
				$this->displayColumn($this->getIdField($rule->getRuleId(),$rule->getRuleUid(),$rule->getRuleRuleId()), $report_format).
				$this->displayColumn($this->getSourceString($rule->getRuleSource(), $rule->isRuleSourceNegated()), $report_format).
				$this->displayColumn($this->getDestinationString($rule->getRuleDestination(), $rule->isRuleDestinationNegated()), $report_format).
				$this->displayColumn($this->getServiceString($rule->getRuleService(), $rule->isRuleServiceNegated()), $report_format).
				$this->displayColumn($rule->getAction(), $report_format).
				$this->displayColumn($rule->getTrack(), $report_format)).
				$this->displayColumn($rule->getInstallOn(), $report_format).
				$this->displayColumn($rule->getComment(), $report_format).
				$this->displayColumn($rule->getRuleName(), $report_format);
			return $ruleTable;
//		}
	}
	function filter($filter,$rule) {	// returns false if rule shall not be displayed, otherwise true
		if ($filter->tenant_filter_is_set() and $rule->isRuleHeader())
			return false;
		if ($filter->report_type == 'rulesearch' and !$rule->is_pass_rule()) 
			return false;
		$isDisabled = true;
		if(!$filter->showDisabled() && !$rule->isRuleDisabled() && !$rule->isRuleHeader()) {
			$isDisabled = false;
		}
		$isComment = $filter->filterComment($rule->getComment());
		$isService = false; $isSource = false; $isDestination = false;
		if(!$this->error->isError($rule_services = $rule->getRuleService())) {
			$name_filter = $filter->getFilter($filter->svc_name);
			$proto_filter = $filter->getProtoFilter($filter->svc_proto);
			$port_filter = $filter->getPortFilter($filter->svc_dst_port);
			if ($name_filter OR $proto_filter OR $port_filter) {
				if (count($rule_services) == 0) $isService = false;
				else {
					foreach ($rule_services as $service) {
						if ($service->getServiceType() == 'group') { 	# wenn service_type <> group --> direkt weiter, ansonsten die Gruppe abarbeiten
							$svc_group = new ServiceGroupFlat ($service->getServiceId(), $filter);
							$isMatch = $filter->filterServiceGroup($svc_group->getServiceNames(), $svc_group->getServiceProtoIds(), $svc_group->getPorts());
						} else { // just a simple object
							$isMatch = $filter->filterService($service->getName(),$service->getIpProtoId(),$service->getPort());
						}
						$isService = ($isService || $isMatch);
					}
				}
			} else
				$isService = true;
		}
		if(!$this->error->isError($rule_sources = $rule->getRuleSource())) {
			if ($filter->tenant_filter_is_set() and count($rule_sources)==0) $isSource = false; // source or destination is empty --> filtered by tenant filter 
			else {
				$name_filter = $filter->getFilter($filter->src_name);
				$ip_filter = $filter->getIpFilter($filter->src_ip);
				if ($name_filter OR $ip_filter OR $filter->tenant_filter_is_set()) {
					if ($rule->isRuleHeader())
						return false;
					else {
						if (count($rule_sources)==0 || // empty source or any-match
							($ip_filter && !$filter->showAnyRules() && $rule_sources[0]->getObjectIp() == '0.0.0.0/0'
								|| $ip_filter && !$filter->showNegRules() && $rule->isRuleSourceNegated()
						    	|| $filter->tenant_filter_is_set() && !$filter->showAnyRules() && $rule_sources[0]->getObjectIp() == '0.0.0.0/0'
							)) // just checking first obj!
							$isSource = false;
						else {
							foreach ($rule_sources as $source) {
								if ($source->getObjectType() == 'group') { 	# wenn object_type <> group --> direkt weiter, ansonsten die Gruppe abarbeiten
									$nw_group = new NWObjectGroupFlat ($source->getObjectId(), $filter);
									$isMatch1 = $filter->filterSourceObjectGroup($nw_group->getObjectNames(), $nw_group->getObjectIps(), $source->getZoneId());
								} else // just a simple object
									$isMatch1 = $filter->filterSourceObject($source->getObjectName(),$source->getObjectIp(),$source->getZoneId());
								// if Usergroups are consistent, parse these as well (later)
								$isMatch2 = $filter->filterUser($source->getUserName(),"","");
								$isSource = ($isSource || ($isMatch1 && $isMatch2));
							}
						}
					}
				} else 
					$isSource = true;
			}
		}
		if(!$this->error->isError($rule_destinations = $rule->getRuleDestination())) {
			if ($filter->tenant_filter_is_set() and count($rule_destinations)==0) $isDestination = false;
				else {
				$name_filter = $filter->getFilter($filter->dst_name);
				$ip_filter = $filter->getIpFilter($filter->dst_ip);
				if ($name_filter OR $ip_filter OR $filter->tenant_filter_is_set()) {
					if ($rule->isRuleHeader())
						return false;
					else {
						if (count($rule_destinations)==0 || // empty destination or any-match, just checking first obj! unscharf bei userauth regeln
							($ip_filter && !$filter->showAnyRules() && $rule_destinations[0]->getObjectIp() == '0.0.0.0/0'
							 || $ip_filter && !$filter->showNegRules() && $rule->isRuleDestinationNegated()
							 || $filter->tenant_filter_is_set() && !$filter->showAnyRules() && $rule_destinations[0]->getObjectIp() == '0.0.0.0/0'
						))
							$isDestination = false;
						else {
							foreach ($rule_destinations as $destination) {
								if ($destination->getObjectType() == 'group') { # wenn object_type <> group --> direkt weiter, ansonsten die Gruppe abarbeiten
									$nw_group = new NWObjectGroupFlat ($destination->getObjectId(), $filter);
									$isMatch = $filter->filterDestinationObjectGroup($nw_group->getObjectNames(), $nw_group->getObjectIps(), $destination->getZoneId());
								} else
									$isMatch = $filter->filterDestinationObject($destination->getObjectName(),$destination->getObjectIp(),$destination->getZoneId());
								$isDestination = ($isDestination || $isMatch);
							}
						}
					}
				} else {
					$isDestination = true;
				}
			}
		}
//		$isIpMatch = ($isSource || $isDestination);
//		if($isDisabled && $isIpMatch && $isService && $isComment) {
		if($isDisabled && $isDestination && $isSource && $isService && $isComment) {
			return true;
		} else {
			return false;
		}
	}
	function getSourceString($sources, $negated) {
		$src_str = '';
		if($this->error->isError($sources)) return $sources->getMessage();
		if ($negated) $src_str .= "[NOT]<br>";
		foreach ($sources as $object) {
			if (!is_null($object->getUserName())) {
				if ($object->getUserName()=='[ANONYMISIERT]') {
					$src_str .= $this->displayReference("usr",'anon',$object->getUserName());
				} else 
					$src_str .= $this->displayReference("usr",$object->getUserId(),$object->getUserName());
				$src_str .= " @ ";
			}
			$src_str .= $this->displayReference("obj",$object->getObjectId(),$object->getObjectName());
			$src_str .= '<br>';
		}
//		if ($src_str == '') $src_str = '<br>';
		return $src_str;
	}
	function getDestinationString($destinations, $negated) {
		$dst_str = '';
		if($this->error->isError($destinations)) { return $destinations->getMessage(); }
		if ($negated) $dst_str .= "[NOT]<br>";
		foreach ($destinations as $object) {
			$dst_str .= $this->displayReference("obj",$object->getObjectId(),$object->getObjectName());
			$dst_str .= '<br>';
		}
//		if ($dst_str == '') $dst_str = '<br>';
		return $dst_str;
	}
	function getServiceString($services, $negated) {
		$svc_str = '';
		if($this->error->isError($services)) { return $services->getMessage(); }
		if ($negated) $svc_str .= "[NOT]<br>";
		foreach ($services as $service) {
			$svc_str .= $this->displayReference("svc",$service->getId(),$service->getName());
			$svc_str .= '<br>';
		}
		if ($svc_str == '') {
			$svc_str = '<br>';
		}
		return $svc_str;
	}
	function getSourceNormalString($sources) {
		$src_str = '';
		$cnt = 0;
		if($this->error->isError($sources)) {
			return $sources->getMessage();
		}
		foreach ($sources as $object) {
			if($cnt > 0) {
				$src_str .= '|';
			}
			if (!is_null($object->getUserName())) {
				$src_str .= $object->getUserName();
				$src_str .= " @ ";
			}
			$src_str .= $object->getObjectName();
			$cnt++;
		}
		if ($src_str == '') {
			$src_str = '';
		}
		return $src_str;
	}
	function getDestinationNormalString($destinations) {
		$dst_str = '';
		$cnt = 0;
		if($this->error->isError($destinations)) {
			return $destinations->getMessage();
		}
		foreach ($destinations as $object) {
			if($cnt > 0) {
				$dst_str .= '|';
			}
			$dst_str .= $object->getObjectName();
			$cnt++;
		}
		if ($dst_str == '') {
			$dst_str = '';
		}
		return $dst_str;
	}
	function getServiceNormalString($services) {
		$svc_str = '';
		$cnt = 0;
		if($this->error->isError($services)) {
			return $services->getMessage();
		}
		foreach ($services as $service) {
			if($cnt > 0) {
				$svc_str .= '|';
			}
			$svc_str .= $service->getName();
			$cnt++;
		}
		if ($svc_str == '') {
			$svc_str = '';
		}
		return $svc_str;
	}
	function getIdField($rule_id,$rule_uid,$rule_ruleid) {
		$id_field = "";
//		if (strlen($rule_uid)<10 and strlen($rule_uid)>0) { # nicht check point rule-uid
		if (strlen($rule_uid)>0) { # nicht check point rule-uid
			if (preg_match("/__uid__(.+)/", $rule_uid, $matches))
				$id_field .= "uid: " . $matches[1] . "<br>";
			else 
				$id_field .= "uid: $rule_uid<br>";
		}
		if (strlen($rule_ruleid)>0) { # nicht check point rule-uid
			$id_field .= "ruleid: $rule_ruleid";
		}
		if (strlen($id_field) == 0) {
//			$id_field = "db-id: $rule_id";
			$id_field = "&nbsp;";
		}
		return $id_field;
//		return $id_field . "db_id: $rule_id";
	}

	function getDisabledString($rule_disabled) {
		$disabled = "";
		if (!$rule_disabled) {
			$disabled .= "<br>[inaktiv]";
		}
		return $disabled;
	}
}
?>
