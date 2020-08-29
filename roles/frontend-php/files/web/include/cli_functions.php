<?php
// $Id: cli_functions.php,v 1.1.2.7 2011-05-02 09:24:48 tim Exp $
// $Source: /home/cvs/iso/package/web/include/Attic/cli_functions.php,v $
/*
 * Created on 11.04.2011
 */
require_once ("db-base.php");

	function getArgs2($args) {  // works with php7
		$out = array();
		for($i = 1; $i < sizeof($args); $i++) { // leaving out argv[0], i.e. php script name
			list($key, $val) = explode("=", $args[$i]);
			$out[$key] = $val;
		}
		return $out;
	}

	function getArgs($args) {  // does not seem to work with php7
	 $out = array();
	 $last_arg = null;
	    for($i = 1, $il = sizeof($args); $i < $il; $i++) {
	        if( (bool)preg_match("/^--(.+)/", $args[$i], $match) ) {
	         $parts = explode("=", $match[1]);
	         $key = preg_replace("/[^a-z0-9]+/", "", $parts[0]);
	            if(isset($parts[1])) {
	             $out[$key] = $parts[1];   
	            }
	            else {
	             $out[$key] = true;   
	            }
	         $last_arg = $key;
	        }
	        else if( (bool)preg_match("/^-([a-zA-Z0-9]+)/", $args[$i], $match) ) {
	            for( $j = 0, $jl = strlen($match[1]); $j < $jl; $j++ ) {
	             $key = $match[1]{$j};
	             $out[$key] = true;
	            }
	         $last_arg = $key;
	        }
	        else if($last_arg !== null) {
	         $out[$last_arg] = $args[$i];
	        }
	    }
	 return $out;
	} 
	function getDevName ($devId, $user, $pw) {
		$e = new PEAR();
		$DbConf = new DbConfig($user,$pw); // enters database connection info into session
		$conn = new DbConnection($DbConf);
		$result = $conn->fworch_db_query("select dev_name from device where dev_id=$devId");
		if (!$e->isError($result) and isset($result->data[0]['dev_name'])) return $result->data[0]['dev_name'];	
		else return "";
	}
	function getDevId ($devName, $user, $pw) {
		$e = new PEAR();
		$DbConf = new DbConfig($user,$pw); // enters database connection info into session
		$conn = new DbConnection($DbConf);
		$result = $conn->fworch_db_query("select dev_id from device where dev_name='$devName'");
		if (!$e->isError($result) and isset($result->data[0]['dev_id'])) return $result->data[0]['dev_id'];	
		else return "";
	}
	function getMgmNameFromDevId ($devId, $user, $pw) {
		$e = new PEAR();
		$DbConf = new DbConfig($user,$pw); // enters database connection info into session
		$conn = new DbConnection($DbConf);
		$result = $conn->fworch_db_query("select mgm_name from device LEFT JOIN management using (mgm_id) where dev_id=$devId");
		if (!$e->isError($result) and isset($result->data[0]['mgm_name'])) return $result->data[0]['mgm_name'];	
		else return "";
	}
	function convert_report_date_to_postgres($report_date) {	// convert report time from format '2011-04-12-112233' into postgresql '2011-04-11 11:22:33' 
		if (preg_match('/^(?P<date>\d+\-\d+\-\d+)\-(?P<time>\d+)$/', $report_date, $matches)) {
			$report_date_without_time = $matches[1];	
			$report_time = $matches[2];	
			$report_time = substr($report_time, 0, 2) . ':' . substr($report_time, 2, 2) . ':' . substr($report_time, 4, 2);
			$report_date = "$report_date_without_time $report_time";
		}
		return $report_date;	
	}
	function output_html_header_config_report($stamm) {
		echo '<!DOCTYPE HTML PUBLIC "-//W3C//DTD HTML 4.01 Transitional//EN">' ."\n". '<html><head><title>fworch Report</title>' . "\n".
			'<meta name="robots" content="index,follow"><meta http-equiv="cache-control" content="no-cache">' ."\n". '<meta name="revisit-after" content="2 days"><meta http-equiv="content-language" content="de">' . "\n" .
			'<meta http-equiv="Content-Type" content="text/html; charset=UTF-8">';
		echo  "\n" . '<script type="text/javascript" src="' . $stamm . 'js/browser.js"></script>' .
			 "\n" . '<script type="text/javascript" src="' . $stamm . 'js/script.js"></script>' . "\n";
		echo '<style type="text/css">';
		include ($stamm . 'css/firewall_print.css');	// style info is directly included in report instead of linked
		echo '</style></head>';
		echo "\n" . '<body class="iframe" onLoad="javascript:parent.document.getElementById(\'leer\').style.visibility=\'hidden\';">' . 
			 "\n" . '<div id="inhalt1">' . "\n" . '<form id="reporting_result" name="reporting_result" action="" method="post">' . "\n";
	}	
 ?>
