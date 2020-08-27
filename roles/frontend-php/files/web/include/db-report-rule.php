<?php
//	echo "starting db-report-rule.ph";
	require_once("db-report-storage.php");

	// class for all functiality common to reports dealing with (firewall) rules
	class RuleBasedReport extends ReportStorage {
	
		function __construct($report, $filter) {
			// $this->error = new PEAR();
			$this->filter = $filter;
			$this->report = $report;
			$this->report_time = date('Y-m-d H:i:s');
		}
	
		protected function removeFilteredRules() { // use rule_id as index for rules and objectIds as index for object arrays
			$rules = array();
			if (isset($this->report["rules"])) {
				foreach ($this->report["rules"] as $rule) {
					if (!($this->IsRuleFilteredOut($rule))) {
						$rules[$rule["rule_id"]]= $rule;
					}
				}
				$this->report["rules"] = $rules;
			}
		}
		protected function normalize() { // use rule_id as index for rules
			$rules = array();
			if (isset($this->report["rules"])) {
				foreach ($this->report["rules"] as $rule) {
					$rule_reindexed = $this->convert_rule($rule);
					$rules[$rule_reindexed["rule_id"]]= $rule_reindexed;
				}
				$this->report["rules"] = $rules;
			}
		}
		protected function convert_rule($rule) {
			// rebuild src, dst, svc arrays from 0..n index to objectId/id index
			$rule["rule_src"] = $this->reindex($rule["rule_src"], "objectId", "userId");
			$rule["rule_dst"] = $this->reindex($rule["rule_dst"], "objectId", null);
			$rule["rule_svc"] = $this->reindex($rule["rule_svc"], "id", null);
			return $rule;
		}
		public function reindex($array, $newidx, $newidx2) { // reindex array with possibly two indices combined (e.g. for userid@objid)
			$newarray = array();
			
			if (isset($array)) {
				foreach ($array as $element) {
					if (isset($newidx2) && isset($element["$newidx2"])) {
						$newarray[$element["$newidx2"] . "@" . $element["$newidx"]] = $element;
					} elseif (isset($element["$newidx"])) {
						$newarray[$element["$newidx"]] = $element;
					} else {
						print("ERROR in RuleBasedReport::reindex: newidx $newidx does not exist in the following array:<p>");
						print_r($array);
					}
				}
			}
			return $newarray;
		}
		protected function IsRuleFilteredOut($rule) {
			// e.g. tenant filtering leads to rule being incomplete --> remove rule completely
			return (count($rule["rule_src"])==0 and count($rule["rule_dst"])==0);
		}
	
		// getters
		public function getReportBasicData() {
			$data = '';
			$data .= "dev_id: " . $this->getDevId() . "<br>";
			$data .= "dev_name: " . $this->getDevName() . "<br>";
			$data .= "Mgm-Name: " . $this->getMgmName() . "<br>";
			$data .= "tenant_id: " . $this->gettenantId() . "<br>";
			return $data;
		}
		public function getDevId() {
			return $this->report["device_id"];
		}
		public function gettenantId() {
			return $this->report["tenant_id"];
		}
		public function getDevName() {
			return $this->report["Device"];
		}
		public function getMgmName() {
			return $this->report["Management-System"];
		}
		public function getNetworkObjects() {
			$nw_objects = array();
			if (isset($this->report["network_objects"]))
				foreach ($this->report["network_objects"] as $obj) $nw_objects []= $obj;
				return json_encode($nw_objects);
		}
		public function getRules() {
			$rules = array();
			if (isset($this->report["rules"]))
				foreach ($this->report["rules"] as $rule) $rules []= $rule;
				return json_encode($rules);
		}
		public function getNetworkServices() {
			$nw_services = array();
			if (isset($this->report["network_services"]))
				foreach ($this->report["network_services"] as $obj) $nw_services []= $obj;
				return json_encode($nw_services);
		}
		public function getUsers() {
			$users = array();
			if (isset($this->report["users"]))
				foreach ($this->report["users"] as $obj) $users []= $obj;
				return json_encode($users);
		}
		protected function getCleanReportArray() {	// remove object garbage from array and add useful information
			$rep_ar = $this->objectToArray($this->getReportObject());
			if (isset($rep_ar)) {
				foreach (array_keys($rep_ar) as $field)
					if ($field<>"db_connection" && $field<>"error" && $field<>"filter")
						$report["$field"] = $rep_ar["$field"];
						return $report;
			}
		}
	
		protected function ruleIdForUid ($uid, $rules) {
			foreach ($rules as $rule) {
				if ($rule["rule_uid"] === $uid)
					return $rule["rule_id"];
			}
			return null;
		}
	
		// setters:
		protected function setNwObjects($nwobjects) {
			$this->report["network_objects"] = $nwobjects;
		}
		protected function setNwServices($nwservices) {
			$this->report["network_services"] = $nwservices;
		}
		protected function setUsers($users) {
			$this->report["users"] = $users;
		}
	}
?>