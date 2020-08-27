<?php

/*
 * $Id: display_changes.php,v 1.1.2.5 2012-03-05 17:11:48 tim Exp $
 * $Source: /home/cvs/iso/package/web/include/Attic/display_changes.php,v $
 * Created on 01.01.2006
 *
 */
 
require_once ("db-change.php");
require_once ("display-table.php");
require_once ("db-nwobject.php");
require_once ("db-service.php");
require_once ("db-user.php");

class DisplayChangeTable extends DisplayTable {
	var $columns;

	function __construct($columns,$ChangeList) {
		$this->error = new PEAR();
		$this->columns 	= $columns;
		if($this->error->isError($ChangeList)) {
			$error = $ChangeList;
			$this->error->raiseError($error->getMessage());
		} else {
			$this->ChangeList = $ChangeList;
		}
		($this->ChangeList->getRows() == 0) ? $this->displayRowsInTable = false : $this->displayRowsInTable = true;
		$this->tableString = NULL;
	}
 	function allowedToViewAdminNames() {
		return (in_array('Doku Admin', $this->columns));
	}
	function displayChanges($filter,$management_filter,$change_docu_allowed) {
		$changeTable = "";
		$filter->emptyFilteredRowIds();
		$this->tenants = $filter->gettenants();
		$change_number = 0;
		$rowsFiltered= 0;
		if ($change_docu_allowed)
			$changeTable .= '<FORM name="selectchange" action="" method="POST" target="_top">'; // wird nie abgeschickt sondern �ber parent form abgefragt
		if ($this->error->isError($this->ChangeList->getChanges())) {
			$error = $this->ChangeList->getChanges();
			$str = ('<td class="celldev" colspan="'.count($this->headers).'">' .
					"Technical Problem occured:" . $error->getMessage() .
					'</td>');
			return $str;
		}
		$table_number = 0;
		$old_header = '';
		if ($filter->TypeOfChangeTable()=='document') {
			$display_config = new DisplayConfig();
			$maxrows = $display_config->getMaxRowsRuleChanges();
			$maxcols = $display_config->getMaxColsRuleChanges();
		} else {
			$maxrows = 1000000;
			$maxcols = 1000000;
		}
		foreach ($this->ChangeList->getChanges() as $change) {
			$header_string = $change->change_header; //BBC
	//		$header_string = "Regel&auml;nderungen"; // BBC
			if ($filter->TypeOfChangeTable()=='document') $header_string .= $change->change_admin_str;
			if ($header_string != $old_header) { // neue Ueberschrift
				$old_header = $header_string; // Vergleichsueberschrift setzen
				if ($table_number > 0) {
					$changeTable .= '</table><br>';
				}
				$table_number ++;
				if ($filter->TypeOfChangeTable()=='document')
					$changeTable .= $this->displayChangeTableHeader($header_string, $table_number);
				else
					$changeTable .= $this->displayReportChangeTableHeader($header_string);
			}
			$change_number++;
			if ($filter->TypeOfChangeTable()=='document')
				$changeTable .= $this->displayChangeDetails($change,$filter,$maxrows,$maxcols);
			else
				$changeTable .= $this->displayReportChangeDetails($change,$filter,$maxrows,$maxcols,$change_docu_allowed);
		} // naechster Change
		if ($change_number==0) {
			$changeTable .= '<td class="celldev" colspan="'.count($this->headers).
				'">Die Anfrage f&uuml;hrte zu keinem Ergebnis</td>';
		}
		$changeTable .= '</table>';
		if ($change_docu_allowed) $changeTable .= '</FORM>';
		return $changeTable;
	}
	function displayChangeTableHeader($header_string, $table_number) {
		$changeTableHeader = '<h2>' . $header_string . '</h2><br>';
		$changeTableHeader .= '<table cellpadding="0" cellspacing="0" class="tab-border" style="margin:0px 10px;"><tr>';
		$changeTableHeader .= '<td class="headerdev"><input type="checkbox" name="alle_auswaehlen_' . $table_number . 
			'" onclick="doSelectTable(\'' . $table_number . '\');"> alle</td>';
		$changeTableHeader .= '<td class="headerdev">Zeit</td>';
		$changeTableHeader .= '<td class="headerdev">Typ</td>';
		$changeTableHeader .= '<td class="headerdev" width="20">Betroffenes Element</td>';
		$changeTableHeader .= '<td class="headerdev">Details</td>';
		$changeTableHeader .= '<td class="headerdev">Quelle<td class="headerdev">Ziel<td class="headerdev">Dienst' . 
								'<td class="headerdev">Aktion<td class="headerdev">Kommentar'; 
		return $changeTableHeader;
	}
	function displayReportChangeTableHeader($header_string) {
		$changeTableHeader = '<h2>' . $header_string . '</h2><br>';
		$changeTableHeader .= '<table cellpadding="0" cellspacing="0" class="tab-border" style="margin:0px 10px;"><tr>';
		foreach ($this->columns as $column) {
			if ($column == 'Kommentar') 
				$changeTableHeader .= '<td class="headerdev_komm">' . $column . '</td>';
			else
				$changeTableHeader .= '<td class="headerdev">' . $column . '</td>';
		} 
		return $changeTableHeader;
	}
	function displayChangeDetails($change,$filter,$maxrows,$maxcols) {
		$abs_change_id = $change->abs_change_id;
		$local_change_id = $change->local_change_id;
		$base_table = $change->base_table;
		$unique_element_name = $change->unique_element_name;
		$change_action = $change->change_action;
		$old_id = $change->old_id;
		$new_id = $change->new_id;
				
		$kurzbeschreibung = '';
		if ($change_action == 'I')	$change_symbol = '+';
		if ($change_action == 'D')	$change_symbol = '-';
		if ($change_action == 'C')	$change_symbol = '&Delta;';
		if ($base_table == 'rule')		$kurzbeschreibung .= 'Regel';
		if ($base_table == 'usr')		$kurzbeschreibung .= 'Benutzer(gruppe)';
		if ($base_table == 'object')	$kurzbeschreibung .= 'Netzwerkobjekt';
		if ($base_table == 'service')	$kurzbeschreibung .= 'Netzwerkdienst';
		$kurzbeschreibung .= '<b> ';
		$kurzbeschreibung .= "$unique_element_name "; 
		$kurzbeschreibung .= ' </b>';
		$changeStr = '<tr><td class="celldev"><center><input type="checkbox" name="validfor$' .
			$base_table . '$' . $local_change_id . '"/></center>';
		$change_symbol = '<center><b>' . $change_symbol . '</b></center>';
//		$changeStr .= ('<td class="celldev">' . $change->change_time); // BBC
		$changeStr .= ('<td class="celldev">' . $change_symbol);
		$changeStr .= ('<td class="celldev_wrap">' . $kurzbeschreibung); 
		$changeStr .= '<td class="celldev_wrap">';
		if ($base_table == 'object')
			$changeStr .= $this->displayNwObjectChangeDetails($filter,$change_action,$old_id,$new_id);
		if ($base_table == 'service')
			$changeStr .= $this->displayServiceChangeDetails($filter,$change_action,$old_id,$new_id);
		if ($base_table == 'usr')
			$changeStr .= $this->displayUserChangeDetails($filter,$change_action,$old_id,$new_id);
		if ($base_table == 'rule')
			$changeStr .= $this->displayRuleChangeDetails($filter,$change_action,$old_id,$new_id,$maxrows,$maxcols, true);
				// true --> Kommentar anzeigen
		else // f�r nicht Regel-Zeilen die Regelinfos mit leeren Feldern auff�llen 
			$changeStr .= '<td colspan="5" class="celldev">&nbsp;';
		return $changeStr;
	}
	function displayReportChangeDetails($change,$filter,$maxrows,$maxcols,$change_docu_allowed) {
		$abs_change_id = $change->abs_change_id;
		$local_change_id = $change->local_change_id;
		$base_table = $change->base_table;
		$unique_element_name = $change->unique_element_name;
		$change_action = $change->change_action;
		$old_id = $change->old_id;
		$new_id = $change->new_id;
		if (!isset($format)) $format = 'html';
				
		$kurzbeschreibung = '';
		if ($change_action == 'I')	$change_symbol = '+';
		if ($change_action == 'D')	$change_symbol = '-';
		if ($change_action == 'C')	$change_symbol = '&Delta;';
		if ($base_table == 'rule')		$kurzbeschreibung .= 'Regel';
		if ($base_table == 'usr')		$kurzbeschreibung .= 'Benutzer(gruppe)';
		if ($base_table == 'object')	$kurzbeschreibung .= 'Netzwerkobjekt';
		if ($base_table == 'service')	$kurzbeschreibung .= 'Netzwerkdienst';
		$kurzbeschreibung .= '<b> ';
		$kurzbeschreibung .= "$unique_element_name "; 
		$kurzbeschreibung .= ' </b>';
		$changeStr = '<tr><td class="celldev">';
		if ($change_docu_allowed) {
			$changeStr .= '<INPUT type="checkbox" name="validfor$' .
				$base_table . '$' . $local_change_id . '"><td class="celldev">';
		}
		$changeStr .= ((isset($change->change_request) and $change->change_request<>'') ? $change->change_request:'&nbsp;');
		if ($this->allowedToViewAdminNames()) {
			$changeStr .= $this->displayColumn_wrap($change->change_comment);
			$changeStr .= '<td class="celldev">' .  (isset($change->change_admin)?$change->change_admin:'&nbsp;');
			$changeStr .= '<td class="celldev">' .  (isset($change->doku_admin)?$change->doku_admin:'&nbsp;');
		} else { // die beiden Spalten change_admin u. doku_admin ausblenden und aus dem change_comment-Feld noch die "Administrator:"-Zeilen entfernen
			$ch_cmnt_arr = explode("\n", $change->change_comment);
			$change_comment = '';
			foreach ($ch_cmnt_arr as $line) if (strpos($line, 'Administrator:')===false) $change_comment .= "$line\n";
			$changeStr .= $this->displayColumn_wrap($change_comment,$format);
		}
		$change_symbol = '<center><b>' . $change_symbol . '</b></center>';
//		$changeStr .= ('<td class="celldev">' . $change->change_time); // BBC
		$changeStr .= ('<td class="celldev">' . $change_symbol);
		$changeStr .= ('<td class="celldev_wrap">' . $kurzbeschreibung); 
		$changeStr .= '<td class="celldev_wrap">';
		if ($base_table == 'object')
			$changeStr .= $this->displayNwObjectChangeDetails($filter,$change_action,$old_id,$new_id);
		if ($base_table == 'service')
			$changeStr .= $this->displayServiceChangeDetails($filter,$change_action,$old_id,$new_id);
		if ($base_table == 'usr')
			$changeStr .= $this->displayUserChangeDetails($filter,$change_action,$old_id,$new_id);
		if ($base_table == 'rule')
			$changeStr .= $this->displayRuleChangeDetails($filter,$change_action,$old_id,$new_id,$maxrows,$maxcols,true);
			// false bezieht sich auf das Anzeigen des Kommentars
		else // f�r nicht Regel-Zeilen die Regelinfos mit leeren Feldern auff�llen 
			$changeStr .= '<td colspan="4" class="celldev">&nbsp;';
		return $changeStr;
	}
	function displayNwObjectChangeDetails($filter,$change_action,$old_id,$new_id) {
		if ($change_action == 'D' or $change_action == 'I') {
			if ($change_action == 'D') $obj_id = $old_id;
			if ($change_action == 'I') $obj_id = $new_id;
			$nwobj = new NetworkObjectSingle($obj_id,$filter);
			$obj_name   = $nwobj->obj_name;
			$obj_zone   = $nwobj->obj_zone;
			$obj_type   = $nwobj->obj_typ;
			$obj_ip     = $nwobj->obj_ip;
			$obj_member = $nwobj->members;
			
			$change_descr_line = array();
			$change_descr_line[0] =  $obj_name . ' (' . $obj_type;
			if (defined($obj_zone)) $change_descr_line[0] .= ', Zone: $obj_zone';
			$change_descr_line[0] .= ')';
//			echo "obj_type: $obj_type, obj_member: $obj_member<br>";
			if ($obj_type == 'group') {
				$change_descr_line[1] = implode(', ', explode('|', $obj_member));
			} else {
				$change_descr_line[1] = $obj_ip;
			}
			return $change_descr_line[0] . '<br>' . $change_descr_line[1] ; 
		} else if ($change_action == 'C') {
			$changes = new NetworkObjectCompare($old_id,$new_id,$filter);
			$diff_str = '';
			foreach ($changes->diffs as $diff) {
				$diff_str .= "$diff<br>";
			}
			return $diff_str;
		}
	}
	function displayServiceChangeDetails($filter,$change_action,$old_id,$new_id) {
		if ($change_action == 'D' or $change_action == 'I') {
			if ($change_action == 'D') $svc_id = $old_id;
			if ($change_action == 'I') $svc_id = $new_id;
			$service = new ServiceSingle($svc_id,$filter);
			$svc_name   = $service->svc_name;
			$svc_dport   = $service->svc_dport;
			$svc_type   = $service->svc_typ;
			$svc_proto     = $service->svc_proto;
			$svc_member = $service->members;
			
			$change_descr_line = array();
			$change_descr_line[0] =  $svc_name . ' (' . $svc_type;
			$change_descr_line[0] .= ')';
			if ($svc_type == 'group') {
				$change_descr_line[1] = implode(', ', explode('|', $svc_member));
			} else {
				$change_descr_line[1] = $svc_dport . '/' . $svc_proto;
			}
			return $change_descr_line[0] . '<br>' . $change_descr_line[1] ; 
		} else if ($change_action == 'C') {
			$changes = new ServiceCompare($old_id,$new_id,$filter);
			$diff_str = '';
			foreach ($changes->diffs as $diff) {
				$diff_str .= "$diff<br>";
			}
			return $diff_str;
		}
	}
	function displayUserChangeDetails($filter,$change_action,$old_id,$new_id) {
		if ($change_action == 'D' or $change_action == 'I') {
			if ($change_action == 'D') $user_id = $old_id;
			if ($change_action == 'I') $user_id = $new_id;
			$usr			= new UserSingle($user_id,$filter);
			$usr_name   	= $usr->user_name;
			$usr_typ	   	= $usr->user_typ;
			$usr_firstname	= $usr->user_firstname;
			$usr_lastname	= $usr->user_lastname;
			$usr_member		= $usr->members;
			
			$change_descr_line = array();
			$change_descr_line[0] =  $usr_name . ' (' . $usr_typ;
			$change_descr_line[0] .= ')';
			if ($usr_typ == 'group') {
				$change_descr_line[1] = implode(', ', explode('|', $usr_member));
			} else {
				$change_descr_line[1] = $usr_firstname . ' ' . $usr_lastname;
			}
			return $change_descr_line[0] . '<br>' . $change_descr_line[1] ; 
		} else if ($change_action == 'C') {
			$changes = new UserCompare($old_id,$new_id,$filter);
			$diff_str = '';
			foreach ($changes->diffs as $diff) {
				$diff_str .= "$diff<br>";
			}
			return $diff_str;
		}
	}
	function displayRuleChangeDetails($filter,$change_action,$old_id,$new_id,$maxrows,$maxcols,$show_comment) {
		$diff_str = '';
		if ($change_action == 'C') {
			$changes = new RuleCompare($old_id,$new_id,$filter);
			foreach ($changes->diffs as $diff) {
				$diff_str .= "$diff<br>";
			}
		} else {
			$diff_str .= "&nbsp;";
		}
		if ($change_action == 'D')	{ $id = $old_id; }
		else						{ $id = $new_id; }
		$rule = new RuleSingle($id,$filter);
		$rule_header = $rule->rule_header;
		if (!$rule_header) {
			$diff_str .= $this->display_rule_field($rule->rule_src, $maxrows, $maxcols, '|', $rule->isRuleSourceNegated(), $rule->isRuleDisabled());
			$diff_str .= $this->display_rule_field($rule->rule_dst, $maxrows, $maxcols, '|', $rule->isRuleDestinationNegated(), $rule->isRuleDisabled());
			$diff_str .= $this->display_rule_field($rule->rule_svc, $maxrows, $maxcols, '|', $rule->isRuleServiceNegated(), $rule->isRuleDisabled());
			$diff_str .= '<td class="celldev">';
			if ($rule->isRuleDisabled()) $diff_str .= '<strike>';
			$diff_str .= $rule->rule_action;
			if ($rule->isRuleDisabled()) $diff_str .= '</strike>';
			if ($show_comment) {
				$diff_str .= '<td class="celldev">';
				if (empty($rule->rule_comment))
					$diff_str .= '&nbsp;';
				else {
					if ($rule->isRuleDisabled()) $diff_str .= '<strike>';
					$diff_str .= $rule->rule_comment;
					if ($rule->isRuleDisabled()) $diff_str .= '</strike>';
				}
			}
		} else {
			if ($show_comment) $row_number=5;
			else $row_number=4;
			if ($change_action == 'C') {
				$diff_str .= '<td colspan="' . $row_number . '">&nbsp;</td>';
				
			} else {
				$diff_str = ('Regel&uuml;berschrift: ' . $rule_header . '<td colspan=" ' . $row_number . '">&nbsp;</td>');
			}
		}
		return $diff_str;			
	}
	function display_rule_field($field_str, $maxrows, $maxcols, $separator, $negated, $rule_disabled) {
		$field_ar = explode ($separator, $field_str);
		$diff_str = '<td class="celldev">';
		if ($rule_disabled) $diff_str .= '<strike>';
		if ($negated) {
			$diff_str .= "[NOT]<br>";
		}
		for ($i=0; $i<min($maxrows,count($field_ar)); ++$i) { // die ersten drei Quellen ausgeben
			$diff_str .= (substr($field_ar[$i],0,$maxcols));
			if (strlen($field_ar[$i])>$maxcols) {
				$diff_str .= '...';
			}			
			$diff_str .= '<br>';
		}
		if (count($field_ar)>$maxrows) {
			$diff_str .= '...';
		}
		if ($rule_disabled) $diff_str .= '</strike>';
		return $diff_str;
	}
}
?>
