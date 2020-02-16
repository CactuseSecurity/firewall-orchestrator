<?php

/*
 * $Id: display_nwobject_config.php,v 1.1.2.12 2012-10-21 15:15:32 tim Exp $
 * $Source: /home/cvs/iso/package/web/include/Attic/display_nwobject_config.php,v $
 * Created on 28.10.2005
 *
 */
require_once ("db-nwobject.php");
require_once ("display-table.php");

class NwObjectConfigTable extends DisplayTable{
	
	var $nwobjectList;
	var $tableString;
	var $displayRowsInTable;
	
	function __construct($headers,$nwobjectList) {
		$this->name 	= "objectTable";
		$this->headers 	= $headers;
		$this->nwobjectList = $nwobjectList;
		
		($this->nwobjectList->getRows() == 0) ? $this->displayRowsInTable = false : $this->displayRowsInTable = true;
		$this->tableString = NULL;
	}

	function display($filter, $report_format) {
		$this->filter = $filter;
		if (is_null($this->tableString)) {
			$this->tableString	= $this->displayTableHideShow($this->filter->getStamm(),"object_id","object_id_min","Netzwerkobjekte", $report_format) .
									$this->displayShowHideColumn("colIndex_obj", $report_format) .
									$this->displayTableOpen($report_format) .
									$this->displayTableHeaders($report_format) .
									$this->displayObjects($this->filter, $report_format) .
									$this->displayTableClose($report_format) .
									$this->displayTableShowHide($this->filter->getStamm(),"object_id","object_id_min","Netzwerkobjekte", $report_format);
		}
		return $this->tableString;
	}

	function filter($filter,$object) {
		if($filter->showRuleObjectsOnly()) {
			$isFiltered = true;
		} else {
			$isMatch1 = $filter->filterSourceObject($object->getName(),$object->getIp(),$object->getZoneId());
			$isMatch2 = $filter->filterDestinationObject($object->getName(),$object->getIp(),$object->getZoneId());
			$isFiltered = $isMatch1 && $isMatch2;
		}
		return $isFiltered;
	}
	function displayObjects($filter, $report_format) {
		if (!isset($report_format)) $report_format = 'html';
		$objectTable = "";
		$no_of_objects = 0;
		$rowsFiltered  = 0;
		if (!$this->displayRowsInTable) $objectTable .= $this->displayCommentLine('Keine Netzwerkobjekte gefunden.', $report_format);
		else {
			$zones = array();
			foreach ($this->nwobjectList->getNetworkObjects() as $object) {
				if (!$this->filter($filter,$object)) { $rowsFiltered++; continue; }
				if ($report_format == 'junos') {
					$objectZone = $object->getZone();
					if (!isset($objectZone) or $objectZone == '') $objectZone = 'dummyZone';
					if (!in_array($objectZone, $zones)) {	// adding new zone
						$zones[]= $objectZone; // add zone to array
						$nwobject_zone_Table ["$objectZone"] = '';
					}
					$nwobject_zone_Table[$objectZone] .= $this->displayNWObjectJunos($object, $report_format);   	
				} else {
					$chNr = 'obj' . $object->getId();
					$objectTable .= $this->displayRow($chNr,$report_format);
					$objectTable .= $this->displayColumn($object->getName(),$report_format).
						$this->displayColumn($object->getZone(),$report_format).
						$this->displayColumn($object->getType(),$report_format).
						$this->displayColumn($this->getIpString($object->getIp(),$object->getIpEnd()),$report_format).
						$this->displayColumn($this->getMemberString($object->getMembers()),$report_format).
						$this->displayColumn($object->getUid(),$report_format) .
						$this->displayColumn($object->getComment(),$report_format);
				}				
				++ $no_of_objects;
				if ($this->isHtmlFormat($report_format)) $objectTable .= "</tr>";
				//////////////////////////////////////////////////////
			} # naechste Regel
			if ($report_format == 'junos') foreach ($zones as $zone)
				$objectTable .= "\t\tsecurity-zone $zone {\n\t\t\taddress-book {\n" . $nwobject_zone_Table[$zone] . "\t\t\t}\n\t\t}\n";
			if ($rowsFiltered == count($this->nwobjectList->getNetworkObjects())) $serviceTable .= $this->displayCommentLine('Keine Netzwerkobjekte gefunden (Filter)', $report_format);
		}
		return $objectTable;
	}
	function displayNWObjectJunos ($nwobject, $report_format) {
		$nwobject_comments = '';
		$nwobjectTable = '';
		$nwobject_type = $nwobject->getType();
		$nwobject_annotate = $nwobject->getComment();
		$nwobject_name = $this->displayJunosToken($nwobject->getName());
		if (isset($nwobject_annotate) and $nwobject_annotate <> '') $nwobjectTable .= "\t\t\t\t/* $nwobject_annotate */\n";
		
		if ($nwobject_type == 'host' or $nwobject_type == 'network' or $nwobject_type == 'gateway') {	// simple nw-object- no group
			$ip_addr = strtolower($nwobject->getIp());
			$nwobjectTable .= "\t\t\t\taddress $nwobject_name $ip_addr;\n";
		} elseif ($nwobject_type == 'ip_range') {	// ip range (check point)
			$ip_addr = strtolower($nwobject->getIp());
			$ip_addr_end = strtolower($nwobject->getIpEnd());
			$nwobjectTable .= $this->displayCommentLine("warning: object $nwobject_name was originally an ip range with start ip=$ip_addr and end ip=$ip_addr_end", $report_format);
			$nwobjectTable .= "\t\t\t\taddress $nwobject_name $ip_addr;\n";
		} elseif ($nwobject_type == 'group') {	// 	group handling, junos cannot deal with groups within groups, so flatten them
			$nwobjectTable .= "\t\t\t\taddress-set $nwobject_name {\n";
			$flat_group = new NWObjectGroupFlatNoGroups($nwobject->getId(), $nwobject->filter); 
			foreach ($flat_group->getObjectNames() as $flat_member) $nwobjectTable .= "\t\t\t\t\taddress " . $this->displayJunosToken($flat_member) . ";\n";
			$nwobjectTable .= "\t\t\t\t}\n";
		} else {
			$nwobjectTable .= $this->displayCommentLine("warning: unknown nwobject type $nwobject_type for for nwobject $nwobject_name", $report_format);
		}
		if ($nwobject_comments <> '') $ruleTable .= $this->displayCommentLine($nwobject_comments, $report_format); // vorher noch ", " abschneiden
		return $nwobjectTable; 
	}
	function getIpString($obj_ip,$obj_ip_end) {
		$obj_ip_str = "";
		if (!is_null($obj_ip_end) && $obj_ip <> $obj_ip_end) {
			$obj_ip_str .= ($obj_ip . "-" . $obj_ip_end);
		} else {
			$obj_ip_str .= $obj_ip;
		}
		if ($obj_ip_str == '') {
			$obj_ip_str = '<br>';
		}		
		return $obj_ip_str;
	}
	function getMemberString($members) {
		$member_str = '';
		if (isset($members)) foreach ($members as $member) {
			$member_str .= $this->displayReference("obj",$member->getId(),$member->getName());
			$member_str .= '<br>';
		}
		if ($member_str == '') {
			$member_str = '<br>';
		}		
		return $member_str;
	}	
}
?>
