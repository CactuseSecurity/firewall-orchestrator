<?php

/*
 * $Id: display_user_config.php,v 1.1.2.5 2011-04-19 00:28:05 tim Exp $
 * $Source: /home/cvs/iso/package/web/include/Attic/display_user_config.php,v $
 * Created on 28.10.2005
 *
 */
require_once ("db-user.php");
require_once ("display-table.php");

class UserConfigTable extends DisplayTable{
	var $userList;
	var $tableString;
	var $displayRowsInTable;
	
	function __construct($headers,$userList) {
		$this->name 	= "userTable";
		$this->headers 	= $headers;
		$this->userList = $userList;
		
		($this->userList->getRows() == 0) ? $this->displayRowsInTable = false : $this->displayRowsInTable = true;
		$this->tableString = NULL;
	}
	function display($filter, $report_format) {
		$this->filter = $filter;
		if (is_null($this->tableString)) {
			$this->tableString	= $this->displayTableHideShow($this->filter->getStamm(),"user_id","user_id_min","Benutzer", $report_format) .
									$this->displayShowHideColumn("colIndex_usr", $report_format) .
									$this->displayTableOpen($report_format) .
									$this->displayTableHeaders($report_format) .
									$this->displayUsers($this->filter, $report_format) .
									$this->displayTableClose($report_format) .
									$this->displayTableShowHide($this->filter->getStamm(),"user_id","user_id_min","Benutzer", $report_format);
		}
		return $this->tableString;
	}
	function filter($filter,$user) {
		$foreign_username_pattern = $filter->get_foreign_username_pattern();
		if ($foreign_username_pattern) 
			return $filter->filterUser($user->getName(),NULL,NULL) && !((ereg($foreign_username_pattern, $user->getName())==1)==1);
		else 
			return $filter->filterUser($user->getName(),NULL,NULL);
	}
	function displayUsers($filter, $report_format) {
		$userTable = "";
		$no_of_users = 0;
		$rowsFiltered= 0;
		if (!$this->displayRowsInTable) {
			$userTable .= '<td class="celldev" colspan="'.count($this->headers).'">Die Anfrage f&uuml;hrte zu keinem Ergebnis</td>';
		} else {
			foreach ($this->userList->getUsers() as $user) {
				if (!$this->filter($filter,$user)) {
					$rowsFiltered++;
					continue;
				}
				$chNr = 'usr' . $user->getId();
				$userTable .= $this->displayRow($chNr, $report_format);
				$userTable .= $this->displayColumn($user->getName(), $report_format).
					$this->displayColumn($user->getType(), $report_format).
					$this->displayColumn($user->getUid(), $report_format) .
					$this->displayColumn($user->getComment(), $report_format);
				$userMembers = $user->getMembers();
				$userMemberString = $this->getMemberString($userMembers);
//				$log = new LogConnection(); $log->log_debug("display_user_config.php: user " . $user->getName() . ": members: " . $userMemberString);
				$userTable .= $this->displayColumn($userMemberString, $report_format);
				++ $no_of_users;
				
				$userTable .= "</tr>";
			} # naechste Regel
			if ($rowsFiltered == count($this->userList->getUsers())) {
				if ($this->isHtmlFormat($report_format))
					$userTable .= '<td class="celldev" colspan="'.count($this->headers).'">Die Anfrage f&uuml;hrte zu keinem Ergebnis</td>';
				else
					$userTable .= $this->displayComment('no network services found', $report_format);
			}
		}
		return $userTable;
	}
	function getMemberString($members) {
		$member_str = '';
		foreach ($members as $member) {
			$member_str .= $this->displayReference("usr",$member->getId(),$member->getName());
			$member_str .= '<br>';
		}
		if ($member_str == '') {
			$member_str = '<br>';
		}		
		return $member_str;
	}	
}
?>