<?php
	require_once ("db-report-rule.php");

// AuditReport contains audit report specific functionality
//  input: JSON encoded report 
class AuditReport extends RuleBasedReport {
	
	function __construct($report) {
		$this->error = new PEAR();
		$this->report = json_decode($report, true);
		$this->report_time = date('Y-m-d H:i:s');
		$this->normalize();
		$this->removeFilteredRules();
	}
	
	private function addLabel($obj_ar, $label, $value, $dividing_index) {
		foreach (array_keys($obj_ar) as $idx) {
			if ($idx>$dividing_index)
				$obj_ar[$idx][$label] = $value;
		}
		return $obj_ar;
	}
	
	public function compareRulesets($rep_in) {
		// return a new report containing all data from 2 input reports and indicating differences
		// this = alter stand
		// $rep_in = neuer stand
		// 1st step: delete all disabled rules (neglecting those for audit reports)
		$log = new LogConnection();
		$log->log_debug("reporting_tables_audit_changes::compareRulesets starting");
		$rep_in->removeDisabledRules();
		$this->removeDisabledRules();
		$report = $rep_in->getCleanReportArray(); // copies everything useful from 2nd report (rep_in) to new report
		$log->log_debug("reporting_tables_audit_changes::compareRulesets after clean reports");
		$report["report_start_time"] = $this->report["report_time"];
		$report["report_end_time"] = $report["report_time"];
		unset($report["report_time"]);
		$log->log_debug("reporting_tables_audit_changes::compareRulesets before merge_rules");
		$report["rules"] = $this->merge_rules($rep_in); // main functionality: combine two rulesets
		$log->log_debug("reporting_tables_audit_changes::compareRulesets after  merge_rules");
		$rep_new = new AuditReport(json_encode($report));

		$this->report["network_objects"] = $this->reindex($this->report["network_objects"], 'obj_id', null);	
		$rep_in->report["network_objects"] = $this->reindex($rep_in->report["network_objects"], 'obj_id', null);
		$rep_in->report["network_objects"] = $this->addLabel($rep_in->report["network_objects"], 'origin', 'neu',
				max(array_keys($this->report["network_objects"])));
		$this->report["users"] = $this->reindex($this->report["users"], 'usr_id', null);
		$rep_in->report["users"] = $this->reindex($rep_in->report["users"], 'usr_id', null);
		if (count($this->report["users"]))
			$rep_in->report["users"] = $this->addLabel($rep_in->report["users"], 'origin', 'neu', max(array_keys($this->report["users"])));
		else 
			$rep_in->report["users"] = $this->addLabel($rep_in->report["users"], 'origin', 'neu', 0);

		if (count($this->report["users"])) {
			$this->report["users"] = $this->reindex($this->report["users"], 'usr_id', null);
			$rep_in->report["users"] = $this->reindex($rep_in->report["users"], 'usr_id', null);
			$rep_in->report["users"] = $this->addLabel($rep_in->report["users"], 'origin', 'neu', max(array_keys($this->report["users"])));			
		} else {			
			$rep_in->report["users"] = $this->addLabel($rep_in->report["users"], 'origin', 'neu', 0);
		}
		$rep_new->setNwObjects($this->mergeObjArray($this->report["network_objects"], $rep_in->report["network_objects"]));
		$rep_new->setNwServices($this->mergeObjArray($this->report["network_services"], $rep_in->report["network_services"]));
		$rep_new->setUsers($this->mergeObjArray($this->report["users"], $rep_in->report["users"]));

		$log->log_debug("reporting_tables_audit_changes::compareRulesets before sortReport");
		$rep_new->sortReport();
		$rep_new->finalizeComments();
		$log->log_debug("reporting_tables_audit_changes::compareRulesets leaving ...");
		return $rep_new;
	}
	private function mergeObjArray($objArr1, $objArr2) {
		if (!count($objArr1) or !is_array($objArr1)) {
			if (!count($objArr2) or !is_array($objArr2)) // throw error?
				return array();
			else 
				return $objArr2;
		} elseif (!count($objArr2) or !is_array($objArr2))
			return $objArr1;
		else 
			return array_merge($objArr1, $objArr2);
	}
	private function removeDisabledRules() {
		// remove all disabled rules (except for header rules)
		if (count($this->report["rules"])) {
			foreach (array_keys($this->report["rules"]) as $rule_id) {
				if ($this->report["rules"][$rule_id]["rule_disabled"] === 't' and !isset($this->report["rules"][$rule_id]["rule_head_text"])) 
					unset ($this->report["rules"][$rule_id]);
			}
		}
	}
	private function compareRuleObjects ($ar1, $ar2) {
		return array_diff_key($ar1, $ar2);
	}
	private function arrayRecursiveDiff($aArray1, $aArray2) {
		$aReturn = array();
	
		foreach ($aArray1 as $mKey => $mValue) {
			if (array_key_exists($mKey, $aArray2)) {
				if (is_array($mValue)) {
					$aRecursiveDiff = arrayRecursiveDiff($mValue, $aArray2[$mKey]);
					if (count($aRecursiveDiff)) { $aReturn[$mKey] = $aRecursiveDiff; }
				} else {
					if ($mValue != $aArray2[$mKey]) {
						$aReturn[$mKey] = $mValue;
					}
				}
			} else {
				$aReturn[$mKey] = $mValue;
			}
		}
		return $aReturn;
	}
	private function isRealChange($change_string) {
		if ($change_string!="\n" and $change_string!="" and $change_string!="<br>" and $change_string!="<BR>")
			return true;
		else 
			return false;
	}
	private function compare_2_rules($rule1, $rule2) {
		// compares two rules and returns a combined rule containing all changes 
		$rule2["rule_audit_old_comment"] = $rule1["rule_comment"]; // preserving oldest comment
//		TODO: maybe ignore name changes if UID unchanged (for K0! warnings)		
		// calculate differences:
		$log = new LogConnection();
		$rule_diffs_del = array_diff($rule1,$rule2);		
		$rule_diffs_add = array_diff($rule2,$rule1);
		$rule2["rule_audit_state"] = "[R0]";  // starting with assuming no relevant change is found
		$rule2["rule_audit_comment_change_type"] = "";
		
		// diff src/dst/svc based on json representation?
		$src_diffs_del = $this->compareRuleObjects($rule1["rule_src"], $rule2["rule_src"]);
		$src_diffs_add = $this->compareRuleObjects($rule2["rule_src"], $rule1["rule_src"]);
		$dst_diffs_del = $this->compareRuleObjects($rule1["rule_dst"], $rule2["rule_dst"]);
		$dst_diffs_add = $this->compareRuleObjects($rule2["rule_dst"], $rule1["rule_dst"]);
		$svc_diffs_del = $this->compareRuleObjects($rule1["rule_svc"], $rule2["rule_svc"]);
		$svc_diffs_add = $this->compareRuleObjects($rule2["rule_svc"], $rule1["rule_svc"]);
		
		// unsetting all fields that might contain irrelevant diffs:
		unset ($rule_diffs_add["rule_installon"],	$rule_diffs_del["rule_installon"]);
		unset ($rule_diffs_add["rule_name"],		$rule_diffs_del["rule_name"]);
		unset ($rule_diffs_add["rule_id"],			$rule_diffs_del["rule_id"]);
		unset ($rule_diffs_add["rule_audit_state"],	$rule_diffs_del["rule_audit_state"]);
		unset ($rule_diffs_add["rule_num"],			$rule_diffs_del["rule_num"]);
		unset ($rule_diffs_add["rule_audit_num"],	$rule_diffs_del["rule_audit_num"]);
				
		if (!(count($rule_diffs_add) or count($rule_diffs_del) or count($src_diffs_add) or count($src_diffs_del) 
			or count($dst_diffs_add) or count($dst_diffs_del) or count($svc_diffs_add) or count($svc_diffs_del))) {
			$log->log_debug("WARNING: compare_2_rules; found changed rule with no differences; uid=" . $rule1["rule_uid"]);
		} else {
//			// first case - an object within a rule has been changed but the comment was not changed:
//			if ((count($src_diffs_add) or count($src_diffs_del) or count($dst_diffs_add) or count($dst_diffs_del)
//				or count($svc_diffs_add) or count($svc_diffs_del)) and !(count($rule_diffs_add) or count($rule_diffs_del))) {
//				$rule2["rule_audit_state"] = "[delta_R%]";
//				$rule2["rule_audit_comment_change_type"] = "[delta_K0!]";
//			}
			//////////// now checking diffs in src / dst / svc //////////////////////////////////7
			if (count($src_diffs_add)) {
				foreach (array_keys($src_diffs_add) as $change_id) $rule2["rule_src"][$change_id]["changed"] = "[+]";
				$rule2["rule_audit_state"] = "[delta_R%]";
				$rule2["rule_audit_src_diffs_add"] = $src_diffs_add;
			}
			if (count($src_diffs_del)) {
				$deleted_objects = objectToArray($src_diffs_del);
				// add deleted objects back in
				foreach (array_keys($deleted_objects) as $change_id) {
					$rule2["rule_src"][$change_id] = $deleted_objects[$change_id];
					$rule2["rule_src"][$change_id]["changed"] = "[-]";
				}
				$rule2["rule_audit_state"] = "[delta_R%]";
				$rule2["rule_audit_src_diffs_del"] = $src_diffs_del;
			}
			if (count($dst_diffs_add)) {
				foreach (array_keys($dst_diffs_add) as $change_id) $rule2["rule_dst"][$change_id]["changed"] = "[+]";
				$rule2["rule_audit_state"] = "[delta_R%]";
				$rule2["rule_audit_dst_diffs_add"] = $dst_diffs_add;
			}
			if (count($dst_diffs_del))  {
				$deleted_objects = objectToArray($dst_diffs_del);
				// add deleted objects back in
				foreach (array_keys($deleted_objects) as $change_id) {
					$rule2["rule_dst"][$change_id] = $deleted_objects[$change_id];
					$rule2["rule_dst"][$change_id]["changed"] = "[-]";
				}
				$rule2["rule_audit_state"] = "[delta_R%]";
				$rule2["rule_audit_dst_diffs_del"] = $dst_diffs_del;
			}
			if (count($svc_diffs_add)) {
				foreach (array_keys($svc_diffs_add) as $change_id) {
					$rule2["rule_svc"][$change_id] = $svc_diffs_add[$change_id];
					$rule2["rule_svc"][$change_id]["changed"] = "[+]";
				}
				$rule2["rule_audit_state"] = "[delta_R%]";
				$rule2["rule_audit_svc_diffs_add"] = $svc_diffs_add;
			}
			if (count($svc_diffs_del)) {
				$deleted_services = objectToArray($svc_diffs_del);
				// add deleted objects back in
				foreach (array_keys($deleted_services) as $change_id) {
					$rule2["rule_svc"][$change_id] = $deleted_services[$change_id];
					$rule2["rule_svc"][$change_id]["changed"] = "[-]";
				}
				$rule2["rule_audit_state"] = "[delta_R%]";
				$rule2["rule_audit_svc_diffs_del"] = $svc_diffs_del;
			}
			
			// now checking top level rule changes and comment changes
			if (count($rule_diffs_add)) { // relevant changes (or comment change) to rule exist
				if (count($rule_diffs_add)==1 and isset($rule_diffs_add["rule_comment"])) {  // standard case: comment was changed
					// careful: objct changes within rule do not count here!
					$log->log_debug("compare_2_rules; count=1 and isset rule_comment; count(rule_diffs_add)=" . count($rule_diffs_add));
					//					$rule2["rule_audit_state"] = "[delta_R%]";
					$old_comment = preg_quote($rule1["rule_comment"], '/');
					$new_comment = preg_quote($rule_diffs_add["rule_comment"], '/');
					if (preg_match("/$old_comment(.+)/", $new_comment, $matches)) { // default, text was added to the comment field
						$cct = "[delta_K+]"; $comment_diff = "[+]&nbsp;" . $matches[1]; 
						$log->log_debug("compare_2_rules; extended comment: " . $comment_diff);
					} elseif (preg_match("/$new_comment(.+)/", $old_comment, $matches)) { 
						$cct = "[delta_K-]"; $comment_diff = "[-]&nbsp;" . $matches[1]; 
						$log->log_debug("compare_2_rules; shortened comment: " . $comment_diff);
					} elseif ($old_comment == $new_comment) { // no comment change
						$cct = '';
						$comment_diff = '';
						if (preg_match("/^\[delta\_R/", $rule2["rule_audit_state"])) { $cct = "[delta_K0!]"; }
					} else { // freestyle comment change (not standard extension of text)
						$cct = "[delta_K%]";
						$comment_diff =  // "alt: " . $old_comment . 
							"neu: " . $new_comment;
						// check if [delta R, if yes: (this should not happen) set [delta K0!] and thereby also trigger colour change
						$log->log_debug("compare_2_rules; freestyle comment change; old=" . $old_comment . "; new=" . $new_comment);
					}
					$rule2["rule_audit_rule_diffs_add"] = $comment_diff;
					$rule2["rule_audit_comment_change_type"] = $cct;
				} else { // case with more than one top-level change / or single change and not comment - (e.g. accept --> deny)
					if (count($rule_diffs_add)>1) {
						// ignore this case for the time being; TODO: include these more exotic changes (e.g. action, zone) later
						if (!isset($rule2["rule_head_text"])) {	// ignoring changes to header rules
							// echo "WARNING: found more than one rule_audit_rule_diffs_add:<br>";
							foreach (array_keys($rule_diffs_add) as $rule_diff_field) { 
								if ($rule_diff_field=='rule_action') {
									$rule2["rule_action"] = "[-]&nbsp;" . $rule1["rule_action"] . "<br>";
									$rule2["rule_action"] .= "[+]&nbsp;" . $rule_diffs_add[$rule_diff_field];
									$rule2["rule_audit_state"] = "[delta_R%]";
								}
								if ($this->isRealChange($rule2["rule_audit_rule_diffs_add"])) {
									$rule2["rule_audit_rule_diffs_add"] .= $rule_diffs_add[$rule_diff] . "<br>"; 
									$rule2["rule_audit_state"] = "[delta_R%]";
									$log->log_debug("add; rule_diffs_add>1; field: $rule_diff_field, value: " . $rule2["rule_audit_rule_diffs_add"] . ";");
								}
							}
						}
					}
					if (!isset($rule2["rule_head_text"])) {	// ignorning changes to header rules
						// echo "WARNING: found more than one rule_audit_rule_diffs_add:<br>";
						foreach (array_keys($rule_diffs_del) as $rule_diff_field) { 
							if ($rule_diff_field=='rule_action') {
								$rule2["rule_audit_state"] = "[delta_R%]";
								$rule2["rule_action"] = "[-]&nbsp;" . $rule1["rule_action"] . "<br>";
								$rule2["rule_action"] .= "[+]&nbsp;" . $rule_diffs_add[$rule_diff_field];
								$rule2["rule_audit_state"] = "[delta_R%]";
							}
							$rule2["rule_audit_rule_diffs_del"] .= $rule_diffs_add[$rule_diff] . "<br>"; 
							$log->log_debug("del; field: $rule_diff_field, value: " . $rule2["rule_audit_rule_diffs_del"] . ";");
						}
					}
				}
				if (count($rule_diffs_del)>1) {
					$rule2["rule_audit_rule_diffs_del"] = $rule_diffs_del; 
					$rule2["rule_audit_state"] = "[delta_R%]";
				}
			}
		}
		if ($rule2["rule_audit_state"] == "[delta_R%]" and !preg_match("/^\[delta\_K/", $rule2["rule_audit_comment_change_type"]))
			$rule2["rule_audit_comment_change_type"] = "[delta_K0!]";
		return $rule2;
	}
	private function merge_rules($rep_in) { // creates a set of all rules (all types of changes)
		// $this->repport = start date
		// $rep_in = stop date
		$log = new LogConnection();
		$log->log_debug("reporting_tables_audit_changes::merge_rules starting");
		
		$rep1 = $this->objectToArray($this->report);
		$rep2 = $this->objectToArray($rep_in->report);
		$rules1 = $rep1["rules"];
		$rules2 = $rep2["rules"];
		$rules_combined = $rules2;
		if (count($rules2)) {
			$id = 0;
			foreach (array_keys($rules2) as $rule_id) {  // fill array with rules from end state 2
				$rules_combined[$rule_id]["rule_audit_state"] = "[delta_R+]";
				$rules_combined[$rule_id]["rule_audit_num"] = "$id";
				// 2017-08-07 - if new rule is added without comment, set MTK to [delta_K0!]
//				$log->log_debug("reporting_tables_audit_changes::merge_rules delta_R+ & delta_K0!; comment: " .
//						$rules_combined[$rule_id]["rule_comment"]);
//				if ($rules_combined[$rule_id]["rule_comment"] == '') {
//					$rules_combined[$rule_id]["rule_audit_comment_change_type"] = "[delta_K0!]";
//				}
				$id++;
			}
		}
		if (count($rules1)) {
			foreach (array_keys($rules1) as $rule_id) { // add rules that where changed or deleted (start state 1)
				if (!isset($rules_combined["$rule_id"])) { // rule was deleted or changed
					$rules_combined[$rule_id] = $rules1[$rule_id];
					$rules_combined[$rule_id]["rule_audit_state"] = "changed_or_deleted";
					$rules_combined[$rule_id]["rule_audit_num"] = "tbd_changed_or_deleted";
					$rules_combined[$rule_id]["rule_audit_old_comment"] = $rules1[$rule_comment];
				} else {
					$rules_combined[$rule_id]["rule_audit_state"] = "[R0]"; // unchanged
				}
			}
		}
		$log->log_debug("reporting_tables_audit_changes::merge_rules before refine_rules");
		return $this->refine_rules($rules_combined);
	}
	// returns the rule_uid of that rule within the start state ruleset that was directly before a deleted rule
	private function getPredecessorUid($deleted_rule_id, $start_rules_pred_uid) {
		if (isset($start_rules_pred_uid[$deleted_rule_id]))
			return $start_rules_pred_uid[$deleted_rule_id];
		else 
			return null;
	}
	private function getPredecessorRuleId($predecessor_uid, $stop_rules) {
		if (isset($predecessor_uid))
			return $this->ruleIdForUid($predecessor_uid, $stop_rules);
		else 
			return null;
	}
	private function refine_rules($rules_combined) { // mark rules as either changed or deleted and set rule_audit_num of deleted rules
		$log = new LogConnection();
		$log->log_debug("reporting_tables_audit_changes::refine_rules starting, found combined rules: " . count($rules_combined));
		if (count($rules_combined)) {
			$intermediate_rules = array();
			$counter = 0;
			// prepare array of rule predecessor uids in start state:
			$start_rules = $this->report["rules"];
			$start_rules_pred_uid = array();
			$pred_uid = null;
			if (isset($start_rules)) {
				foreach (array_keys($start_rules) as $rule_id) {
					$uid = $start_rules[$rule_id]["rule_uid"];
					$start_rules_pred_uid[$rule_id] = $pred_uid;
					$pred_uid = $uid;
				}
			}
			// end of prepare
			foreach (array_keys($rules_combined) as $id) { // mark changed rules as changed (not deleted)
				// TODO: make sure to start with the smallest id of the rule!
				// if rule_uid exists more than once with differing rule_ids
				if ($rules_combined[$id]["rule_audit_state"] == "changed_or_deleted") {
					$rule_uid = $rules_combined[$id]["rule_uid"];
					foreach (array_keys($rules_combined) as $id2) { // find other rule with same rule_uid
						if ($rules_combined[$id2]["rule_uid"] == $rule_uid && $id<>$id2) {
							//only show diffs between oldest and newest rule version
							if (!isset($rules_combined[$id]["rule_audit_latest_id"]) || $id2>$rules_combined[$id]["rule_audit_latest_id"]) {
								$rules_combined[$id]["rule_audit_state"] = "[delta_R%]";
								$rules_combined[$id]["rule_audit_num"] = "tbd_changed";
								$rules_combined[$id]["rule_audit_latest_id"] = "$id2";
							} else {
								$rules_combined[$id]["rule_audit_state"] = "intermediate state";
								$rules_combined[$id]["rule_audit_num"] = "tbd_intermediate state";
								$intermediate_rules[]= $id2;
							}
						}
					}
					if (!isset($rules_combined[$id]["rule_audit_latest_id"])) { // no rule with same rule_uid found: rule was deleted
						$rules_combined[$id]["rule_audit_state"] = "[delta_R-]";
						$predecessor_uid = $this->getPredecessorUid($id, $start_rules_pred_uid);
						$predecessor = $this->getPredecessorRuleId($predecessor_uid, $rules_combined);
						$rules_combined[$id]["rule_audit_predecessor"] = $predecessor;
						$rules_combined[$id]["rule_audit_num"] = $rules_combined[$predecessor]["rule_audit_num"] + 0.5;
						// make sure deleted rule is inserted after its former predecessor
					} else { // intermediate rule state (also to be deleted)
						$latest_id = $rules_combined[$id]["rule_audit_latest_id"];
						// echo "comparing rule $id with $latest_id<br>";
						$rules_combined[$latest_id] = $this->compare_2_rules($rules_combined[$id], $rules_combined[$latest_id]);
						unset($rules_combined[$id]); // deleting old rule
					}
				}
				$counter++;
			}
			$log->log_debug("reporting_tables_audit_changes::refine_rules after main foreach with $counter combined rules");
			$log->log_debug("reporting_tables_audit_changes::refine_rules found " . $intermediate_rules . " intermediate rules");
			foreach ($intermediate_rules as $intermediate_id) { // TODO: might also display intermediate steps later
				$log->log_debug("reporting_tables_audit_changes::refine_rules deleting intermediate rule with id $intermediate_id");
				unset($rules_combined[$intermediate_id]);
			}
		}
		$log->log_debug("reporting_tables_audit_changes::refine_rules exiting");
		return $rules_combined;
	}
	private function finalizeComments() { // create single column containing all comment data
		$log = new LogConnection();
		$report = $this->getReportObject();
		foreach (array_keys($report["rules"]) as $rule_id) {
			if (isset($report["rules"][$rule_id]["rule_audit_old_comment"])) {
				$report["rules"][$rule_id]["rule_audit_full_comment"] = '';
				if (isset($report["rules"][$rule_id]["rule_audit_rule_diffs_add"]))
					$report["rules"][$rule_id]["rule_audit_full_comment"] .= 'alt: ';
				$report["rules"][$rule_id]["rule_audit_full_comment"] .= 
					$report["rules"][$rule_id]["rule_audit_old_comment"] . "<br>" .
					$report["rules"][$rule_id]["rule_audit_rule_diffs_add"]; 
					$log->log_debug("reporting_tables_audit_changes::finalizeComments: " . 
							" isset(report[rules][$rule_id][rule_audit_old_comment])" . 
//							$report["rules"][$rule_id]["rule_audit_old_comment"] .
							"; rule_audit_rule_diffs_add=" .
							$report["rules"][$rule_id]["rule_audit_rule_diffs_add"])
					;
			} else {
				$report["rules"][$rule_id]["rule_audit_full_comment"] = $report["rules"][$rule_id]["rule_comment"];
//				$log->log_debug("reporting_tables_audit_changes::finalizeComments: " .
//						" !!!isset(report[rules][$rule_id][rule_audit_old_comment])" .
//						$report["rules"][$rule_id]["rule_comment"]);
			}
		}
		// 2017-08-07 - if new rule is added without comment, set MTK to [delta_K0!]
		foreach (array_keys($report["rules"]) as $rule_id) {
			if ($report["rules"][$rule_id]["rule_audit_full_comment"] == '' &&
					$report["rules"][$rule_id]["rule_audit_state"] == "[delta_R+]") {
						$report["rules"][$rule_id]["rule_audit_comment_change_type"] = "[delta_K0!]";
//					$log->log_debug("reporting_tables_audit_changes::merge_rules delta_R+ & delta_K0!; set rule_audit_comment_change_type to [delta_K0!]);
			}

		}
		$this->report = $report;
	}
	private function sortReport() { // sorts combined rules in correct order
		// sort rules (add deleted in right place)
		// move all meta info to the top (start_time)
		// sort objects within rules alphabetically
		// sorting of list of nw_objets/services/users is handled by datatables
		// remove all duplicates from nw, svc, user objects and sort them
		$report = $this->getReportObject();
		$new_order["Management-System"] = $report["Management-System"];
		$new_order["Device"] = $report["Device"];
		$new_order["device_id"] = $report["device_id"];
		$new_order["tenant_id"] = $report["tenant_id"];
		$new_order["report_start_time"] = $report["report_start_time"];
		$new_order["report_stop_time"] = $report["report_stop_time"];
		$new_order["rules"] = $this->array_sort($report["rules"],"rule_audit_num");
		$id=1; 
		foreach ($new_order["rules"] as $rule) {
			$new_order["rules"][$rule["rule_id"]]["rule_audit_num1"] = $id++; // add proper rule num starting from 1

			// checkpoint: remove everything up to __uid__ in rule_uid for sake of rule_uid column width
			$rule_id = $new_order["rules"][$rule["rule_id"]]["rule_uid"];
			if (preg_match("/__uid__(.+)/", $rule_id, $matches)) {
				$new_order["rules"][$rule["rule_id"]]["rule_uid"] = $matches[1];
			}
			// junos: from_zone__application-tier__to_zone__application-tier__
			if (preg_match("/from_zone__(.+?)__to_zone__(.+?)__(.+)/", $rule_id, $matches)) {
				$new_order["rules"][$rule["rule_id"]]["rule_uid"] = $matches[3];
			}
				
		}
 		// sorting objects within rules --> moved to handlebars eachSort function
		$report["rules"] = $new_order["rules"];
		$new_order["network_objects"] = $this->remove_duplicates($report["network_objects"]);
		$report["network_objects"] = $new_order["network_objects"]; 
		$new_order["network_services"] = $this->remove_duplicates($report["network_services"]);
		$report["network_services"] = $new_order["network_services"]; 
		$new_order["users"] = $this->remove_duplicates($report["users"]);
		$report["users"] = $new_order["users"]; 
		$this->report = $report;
	}
}
?>