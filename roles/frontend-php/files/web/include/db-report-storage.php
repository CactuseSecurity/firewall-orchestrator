<?php
//	echo "starting db-report-storage.ph";
	require_once("db-report.php");
	
	class ReportStorage extends Report {
		function __construct($report, $filter) {
			$this->error = new PEAR();
			$this->filter = $filter;
			$this->report = $report;
//			$this->report_time = date('Y-m-d H:i:s');
		}
		public function reportExists($db_type, $connect_str, $report_type, $dev_id, $time1, $time2, $tenant_id, $collection, $session) {
			$log = new LogConnection();
//			$log->log_debug("db-report-storage::reportExists params: $db_type, $connect_str, $report_type, $dev_id, $time1, $time2, $tenant_id, $collection");
			if ($db_type=='mongo') {
				$filter = [
						"report_type" => $report_type,
						"dev_id" => $dev_id,
						"report_start_time" => $time1,
//						"report_end_time" => $time2,
//						"tenant_id" => $tenant_id,
				];
				$options = [
						'projection' => ['_id' => 0],
				];
				$fworchdb = new MongoDB\Driver\Manager($connect_str);
				$query = new MongoDB\Driver\Query($filter, $options);
				$cursor = $fworchdb->executeQuery($collection, $query);
				foreach ($cursor as $document) {
					$log->log_debug("db-report-storage::reportExists found existing report in DB");
					return true;
				}
				$log->log_debug("db-report-storage::reportExists miss: no existing report in DB");
				return false;
			}
			if ($db_type=='postgres') {
				require_once 'db-base.php';
				require_once 'db-gui-config.php';
				$db_connection = new DbConnection(new DbConfig($session["dbuser"], $session["dbpw"]));
				$tenant_id_filter = ' (TRUE) ';
				if (isset($tenant_id)) { $tenant_id_filter = 'tenant_id=$tenant_id'; }

				if (preg_match("/config/", $report_type)) { // standard config report
					$db_connection = new DbConnection(new DbConfig($session["dbuser"], $session["dbpw"]));
					$sql_code = "SELECT * FROM get_import_id_for_dev_at_time($dev_id, '$time1')"; // get_import_id_for_dev_at_time
					$result = $db_connection->fworch_db_query($sql_code);
					if (isset($result->data[0]['get_import_id_for_dev_at_time'])) {
						$first_import_id = $result->data[0]['get_import_id_for_dev_at_time'];
						$second_import_id = 'NULL';
						if (!isset($time2) or $time2=='') { $time2 = 'NULL'; } else {
							$sql_code = "SELECT * FROM get_import_id_for_dev_at_time($dev_id, '$time2')";
							$result = $db_connection->fworch_db_query($sql_code);
							$second_import_id = $result->data[0]['get_import_id_for_dev_at_time'];
						}
						// TODO: report_typ_id --> not to be set fixed
						$sql_code =
						"SELECT report_id FROM report WHERE report_typ_id=1 AND start_import_id=$first_import_id";
						$sql_code .= " AND dev_id=$dev_id";
						if (isset($tenant_id) and $tenant_id!='') {
							if (preg_match("/^null$/i", $tenant_id)) $sql_code .= " AND tenant_id IS NULL";
							else $sql_code .= " AND tenant_id=$tenant_id";
						}
						$log->log_debug("db-report-storage::reportExists SQL code: $sql_code");
						$result = $db_connection->fworch_db_query($sql_code);
						return (isset($result->data[0]));
					} else {
						return false;
					}
				}
				if (preg_match("/audit/", $report_type)) { // standard config report
					$db_connection = new DbConnection(new DbConfig($session["dbuser"], $session["dbpw"]));
					$sql_code = "SELECT * FROM get_import_id_for_dev_at_time($dev_id, '$time1')"; // get_import_id_for_dev_at_time
					$result = $db_connection->fworch_db_query($sql_code);
					if (isset($result->data[0]['get_import_id_for_dev_at_time'])) {
						$first_import_id = $result->data[0]['get_import_id_for_dev_at_time'];
						$second_import_id = 'NULL';
						if (!isset($time2) or $time2=='') { $time2 = 'NULL'; } else {
							$sql_code = "SELECT * FROM get_import_id_for_dev_at_time($dev_id, '$time2')";
							$result = $db_connection->fworch_db_query($sql_code);
							$second_import_id = $result->data[0]['get_import_id_for_dev_at_time'];
						}
						$sql_code = 
							"SELECT report_id FROM report WHERE (report_typ_id=5 OR report_typ_id=6) AND dev_id=$dev_id AND start_import_id=$first_import_id";
						if (isset($second_import_id) and $second_import_id!='') {
							if (!preg_match("/^null$/i", $second_import_id)) $sql_code .= " AND stop_import_id=$second_import_id ";
						}
						if (isset($tenant_id) and $tenant_id!='') {
							if (preg_match("/^null$/i", $tenant_id)) $sql_code .= " AND tenant_id IS NULL";
							else $sql_code .= " AND tenant_id=$tenant_id";
						}
						$log->log_debug("db-report-storage::reportExists SQL code: $sql_code");
						$result = $db_connection->fworch_db_query($sql_code);
						return (isset($result->data[0]));
					} else {
						return false;
					}
				}
			}
			$log->log_debug("db-report-storage::reportExists no matching db_type found: $db_type");
			return false;
		}
		public function readReport($db_type, $connect_str, $report_type, $dev_id, $time1, $time2, $tenant_id, $collection, $session) { 
			//	retrieve report from databse
			$log = new LogConnection();
//			$log->log_debug("db-report-storage::readReport params: $db_type, $connect_str, $report_type, $dev_id, $time1, $time2, $tenant_id, $collection");
			if ($db_type=='mongo') {
				$filter = [
						"report_type" => $report_type,
						"dev_id" => $dev_id,
						"report_start_time" => $time1,
//						"report_end_time" => $time2,
//						"tenant_id" => $tenant_id,
				];
				$options = [
						'projection' => ['_id' => 0],
				];
				$fworchdb = new MongoDB\Driver\Manager($connect_str);
				$query = new MongoDB\Driver\Query($filter, $options);
				$cursor = $fworchdb->executeQuery($collection, $query);
				foreach ($cursor as $document) {
					$log->log_debug("db-report-storage::readReport retrieved stored report");
					return json_encode($document->report);
				}
				$log->log_debug("db-report-storage::readReport ERROR: did not find promised stored report");
			}
			if ($db_type=='postgres') {
				require_once 'db-base.php';
				require_once 'db-gui-config.php';
				$db_connection = new DbConnection(new DbConfig($session["dbuser"], $session["dbpw"]));
				$tenant_id_filter = ' (TRUE) ';
				if (isset($tenant_id)) { $tenant_id_filter = 'tenant_id=$tenant_id'; } // TODO

				if (preg_match("/config/", $report_type)) { // standard config report
					$db_connection = new DbConnection(new DbConfig($session["dbuser"], $session["dbpw"]));
					$sql_code = "SELECT * FROM get_import_id_for_dev_at_time($dev_id, '$time1')"; // get_import_id_for_dev_at_time
					$result = $db_connection->fworch_db_query($sql_code);
					$first_import_id = $result->data[0]['get_import_id_for_dev_at_time'];
					$second_import_id = 'NULL';
					if (!isset($time2) or $time2=='') { $time2 = 'NULL'; } else {
						$sql_code = "SELECT * FROM get_import_id_for_dev_at_time($dev_id, '$time2')";
						$result = $db_connection->fworch_db_query($sql_code);
						$second_import_id = $result->data[0]['get_import_id_for_dev_at_time'];
					}
					// TODO: report_typ_id --> not to be set fixed to config (1)
					$sql_code = "SELECT report_document FROM report WHERE report_typ_id=1 AND dev_id=$dev_id AND start_import_id=$first_import_id";
					if ($second_import_id!='NULL') { $sql_code .= " AND stop_import_id=$second_import_id "; }
					if (isset($tenant_id) and $tenant_id!='') {
						if (preg_match("/^null$/i", $tenant_id)) $sql_code .= " AND tenant_id IS NULL";
						else $sql_code .= " AND tenant_id=$tenant_id";
					}
					$log->log_debug("db-report-storage::readReport SQL code: $sql_code");
					$result = $db_connection->fworch_db_query($sql_code);
					return ($result->data[0]['report_document']);
				} else { // audit/change reports: TODO, not implemented / not needed yet
				}
			}
			return false;
		}
		public function dumpReport($db_type, $connect_str, $report_type, $dev_id, $time1, $time2, $tenant_id, $collection, $session) { 
			//	write report to database
			$log = new LogConnection();
			if ($db_type=='mongo') {
				$fworchdb = new MongoDB\Driver\Manager($connect_str);
				$bulk = new MongoDB\Driver\BulkWrite;
				$bulk->insert([
						"report_time" => $this->report_time, 
						"report_type" => $report_type,
						"dev_id" => $dev_id,
						"report_start_time" => $time1,
						"report_end_time" => $time2,
						"tenant_id" => $tenant_id,
						"report" => $this->report
				]);
				$fworchdb->executeBulkWrite($collection, $bulk);
			}
			if ($db_type=='postgres') {
				require_once 'db-base.php';
				$report_typ_id = 1;
				if (preg_match("/audit/i", $report_type)) { $report_typ_id=6; }
//				if (preg_match("/audit changes details/i", $report_type)) { $report_typ_id=6; }
//				elseif (preg_match("/audit changes/i", $report_type)) { $report_typ_id=5; }
				$db_connection = new DbConnection(new DbConfig($session["dbuser"], $session["dbpw"]));
				$sql_code = "SELECT * FROM get_import_id_for_dev_at_time($dev_id, '$time1')"; // get_import_id_for_dev_at_time
				$result = $db_connection->fworch_db_query($sql_code);
//				if (isset($result->data[0]['get_import_id_for_dev_at_time'])) {			
				$first_import_id = $result->data[0]['get_import_id_for_dev_at_time'];
				$second_import_id = 'NULL';
				if (!isset($time2) or $time2=='') { $time2 = 'NULL'; } else {
					$sql_code = "SELECT * FROM get_import_id_for_dev_at_time($dev_id, '$time2')";
					$result = $db_connection->fworch_db_query($sql_code);
					$second_import_id = $result->data[0]['get_import_id_for_dev_at_time'];
				}
				if (isset($first_import_id)) {
					$sql_code = 
						"INSERT INTO report (report_typ_id, start_import_id, stop_import_id, dev_id, report_start_time, report_end_time, tenant_id, report_document)" .
						" VALUES ($report_typ_id, $first_import_id, $second_import_id, $dev_id, '$time1', ";
					if ($time2=='NULL') { $sql_code .= 'NULL'; } 
					else { $sql_code .= "'$time2'"; }
					if (isset($tenant_id) and $tenant_id!='') {
						if (preg_match("/^null$/i", $tenant_id)) $sql_code .= ', NULL';
						else $sql_code .= ", $tenant_id"; ;
					} else $sql_code .= ', NULL';
					$sql_code .= ", '" . $this->getReportJson() . "')";
//					$log->log_debug("db-report-storage::dumpReport INSERT code: $sql_code");
					$result = $db_connection->fworch_db_query($sql_code);
				}
			}
		}
	}
?>