<?php

/*
 * $Id: display_service_config.php,v 1.1.2.12 2011-05-26 07:55:12 tim Exp $
 * $Source: /home/cvs/iso/package/web/include/Attic/display_service_config.php,v $
 * Created on 28.10.2005
 *
 */
require_once ("db-service.php");
require_once ("display-table.php");

class ServiceConfigTable extends DisplayTable{
	
	var $serviceList;
	var $tableString;
	var $displayRowsInTable;
	
	function __construct($headers,$serviceList) {
		$this->name 	= "serviceTable";
		$this->headers 	= $headers;
		$this->serviceList = $serviceList;
		
		($this->serviceList->getRows() == 0) ? $this->displayRowsInTable = false : $this->displayRowsInTable = true;
		$this->tableString = NULL;
	}

	function display($filter, $report_format) {
		$this->filter = $filter;
		if (is_null($this->tableString)) {
			$this->tableString	= $this->displayTableHideShow($this->filter->getStamm(),"service_id","service_id_min","Dienste", $report_format) .
									$this->displayShowHideColumn("colIndex_svc", $report_format) .
									$this->displayTableOpen($report_format) .
									$this->displayTableHeaders($report_format) .
									$this->displayServices($this->filter, $report_format) .
									$this->displayTableClose($report_format) .
									$this->displayTableShowHide($this->filter->getStamm(),"service_id","service_id_min","Dienste", $report_format);
		}
		return $this->tableString;
	}

	function filter($filter,$service) {
		if($filter->showRuleObjectsOnly()) {
			$isFiltered = true;
		} else {
			$isFiltered = $filter->filterService($service->getName(),$service->getIpId(),$service->getDestinationPort());
		}
		return $isFiltered;
	}
	function displayServices($filter, $report_format) {
		$serviceTable = ""; $svc_simple = ''; $svc_set = '';
		######## Begin Main-Loop ################################
		$no_of_services = 0;
		$rowsFiltered   = 0;
		if (!$this->displayRowsInTable) $serviceTable .= $this->displayCommentLine('Keine Services gefunden.', $report_format);
		else {
			foreach ($this->serviceList->getServices() as $service) {
				if (!$this->filter($filter,$service)) {	$rowsFiltered++; continue; }
				$service_type = $service->getType();
				if ($report_format == 'junos') {
					if ($service_type=='simple') $svc_simple .= $this->displayServiceJunos($service, $report_format);
					else $svc_set .= $this->displayServiceJunos($service, $report_format);
				} else {
					$chNr = 'svc' . $service->getId();
					$serviceTable .= $this->displayRow($chNr, $report_format);
					$serviceTable .= $this->displayColumn($service->getName(), $report_format).
						$this->displayColumn($service->getType(), $report_format).
						$this->displayColumn($this->getMemberString($service->getMembers()), $report_format).
						$this->displayColumn($this->getIpString($service->getIp(),$service->getIpId()), $report_format).
						$this->displayColumn($this->getPortString($service->getDestinationPort(),$service->getDestinationPortEnd()), $report_format) .
						$this->displayColumn($this->getPortString($service->getSourcePort(),$service->getSourcePortEnd()), $report_format) .
						$this->displayColumn($service->getTimeout(), $report_format) .
						$this->displayColumn($service->getUid(), $report_format) .
						$this->displayColumn($service->getComment(), $report_format);
				}
				++ $no_of_services;
				if ($this->isHtmlFormat($report_format)) $serviceTable .= "</tr>";
			} // naechste Regel
			if ($rowsFiltered == count($this->serviceList->getServices())) 	$serviceTable .= $this->displayCommentLine('Keine Services gefunden (Filter)', $report_format);
		}
		return $svc_simple . $svc_set . $serviceTable;
	}
	function displayServiceJunos ($service, $report_format) {
		$service_comments = '';
		$serviceTable = '';
		$svc_annotate = $service->getComment();
		$service_name = $this->displayJunosToken($service->getName());
		$service_type = $service->getType();
		if (isset($svc_annotate) and $svc_annotate <> '') $serviceTable .= "\t/* $svc_annotate */\n";
		if ($service_type == 'simple' || $service_type == 'rpc') {	// simple application - no group
			$serviceTable .= "\tapplication $service_name {\n";
			if ($service->getIp() <> '') {
				$proto = strtolower($service->getIp());
				if (!in_array($proto, array('tcp', 'udp', 'icmp'))) {
					$proto = $service->getIpId();
				}
				$serviceTable .= "\t\tprotocol " . $this->displayJunosToken($proto) . ";\n";
			} else {
				$serviceTable .= "\t\tprotocol tcp;\n";
				$serviceTable .= $this->displayCommentLine("warning: no protocol specified for service $service_name, guessing tcp", $report_format);
			}
			if ($service->getSourcePort() <> '') {
				$sport = $service->getSourcePort();
				if ($service->getSourcePortEnd() <> '')
					$sport_end = $service->getSourcePortEnd();
				else $sport_end = $sport;
				$serviceTable .= "\t\tsource-port $sport-$sport_end;\n";
			}
			if ($service->getDestinationPort() <> '') {
				$dport = $service->getDestinationPort();
				if ($service->getDestinationPortEnd() <> '')
					$dport_end = $service->getDestinationPortEnd();
				else $dport_end = $dport;
				$serviceTable .= "\t\tdestination-port $dport-$dport_end;\n";
			}
			if ($service->getTimeout() <> '') {
				$timeout = $service->getTimeout();
				if ($timeout>86400) $timeout = 86400;
				$serviceTable .= "\t\tinactivity-timeout $timeout;\n";
			}
			if ($service->getRpc() <> '') {
				$rpc = $service->getRpc();
				if (preg_match("/^\d+$/", $rpc) or preg_match("/^d+\-\d+$/", $rpc))
					$serviceTable .= "\t\trpc-program-number $rpc;\n";
				else $serviceTable .= "\t\tuuid $rpc;\n";
			}
			$serviceTable .= "\t}\n";
		} elseif ($service_type == 'group') {	// 	group handling
			$serviceTable = "\tapplication-set $service_name {\n";
			$members = $service->getMembers();
			foreach ($members as $member) $serviceTable .= "\t\tapplication " . $this->displayJunosToken($member->getName()) . ";\n";
			$serviceTable .= "\t}\n";
		} else {
			$serviceTable .= $this->displayCommentLine("warning: unknown service type $service_type found for service $service_name", $report_format);
		}
		if ($service_comments <> '') $ruleTable .= $this->displayCommentLine($service_comments, $report_format); // vorher noch ", " abschneiden
		return $serviceTable; 
	}
	function getIpString($svc_ip,$svc_ip_id) {
		$svc_ip_str = "$svc_ip ($svc_ip_id)";
		if ($svc_ip_id == 0) {
			$svc_ip_str="";
		}
		if ($svc_ip_str == '') {
			$svc_ip_str = "<br>";
		}
		return $svc_ip_str;
	}
	function getPortString($port,$port_end) {
		$port_str = $port;
		if (!is_null($port_end) && $port_end<>$port) {
			$port_str .= " - $port_end";
		}
		if ($port_str == '') {
			$port_str = "<br>";
		}
		return $port_str;
	}
	function getMemberString($members) {
		$member_str = '';
		foreach ($members as $member) {
			$member_str .= $this->displayReference("svc",$member->getId(),$member->getName());
			$member_str .= '<br>';
		}
		if ($member_str == '') {
			$member_str = '<br>';
		}		
		return $member_str;
	}
}
?>
