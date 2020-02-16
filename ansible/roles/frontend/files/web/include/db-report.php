<?php
//	echo "starting db-report.ph";
	require_once("db-base.php");
	
	// Report contains basic generic report stuff
	class Report  {
		private $db_connection;
		private $error;
		private $filter;
		protected $report;
		protected $report_time;
	
		function __construct($report, $filter) {
			$this->error = new PEAR();
			$this->filter = $filter;
			$this->report = $report;
			$this->report_time = date('Y-m-d H:i:s');
		}
		// getters:
		public function getReportObject() {
			return $this->report;
		}
		public function getReportJson() {
			return json_encode($this->report);
		}
		public function array_sort($array, $on, $order=SORT_ASC)
		{
			$new_array = array();
			$sortable_array = array();
	
			if (count($array) > 0) {
				foreach ($array as $k => $v) {
					if (is_array($v)) {
						foreach ($v as $k2 => $v2) {
							if ($k2 == $on) {
								$sortable_array[$k] = $v2;
							}
						}
					} else {
						$sortable_array[$k] = $v;
					}
				}
				switch ($order) {
					case SORT_ASC:
						asort($sortable_array, SORT_NATURAL | SORT_FLAG_CASE);
						break;
					case SORT_DESC:
						arsort($sortable_array, SORT_NATURAL | SORT_FLAG_CASE);
						break;
				}
				foreach ($sortable_array as $k => $v) {
					$new_array[$k] = $array[$k];
				}
			}
			return $new_array;
		}
		protected function remove_duplicates($ar_in) {
			if (isset($ar_in))
				return array_unique($ar_in, SORT_REGULAR);
				else
					return null;
		}
		public function objectToArray ($object) {
			if(!is_object($object) && !is_array($object)) return $object;
			//		return array_map($this->callMethod('getProp'), (array) $object);
			return array_map('objectToArray', (array) $object);
		}
	}
?>