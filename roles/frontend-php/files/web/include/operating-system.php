<?php
	class LogConnection {
		var $system_syslogfacility;
		var $system_logtarget;
		var $system_loglevel;
		
		function __construct() {
			if (!isset($_SESSION['loglevel'])) $this->system_loglevel  = 0;
			else $this->system_loglevel  = $_SESSION['loglevel'];
	
			if (!isset($_SESSION['logtarget'])) $this->logtarget = 'syslog';
			else $this->system_logtarget  = $_SESSION['logtarget'];
	
			if (!isset($_SESSION['logfacility'])) $this->system_syslogfacility = LOG_LOCAL6;
			else $this->system_syslogfacility  = constant('LOG_' . strtoupper($_SESSION['logfacility'])); 
		}
		function log($log_level, $text) {
			if (!strpos(strtolower($text), "password")===false OR !strpos(strtolower($text), "pw")===false OR !strpos(strtolower($text), "pass")===false)
				$text = "<log text might have contained password in cleartext: this will not be logged>";			
			if ($this->getLogTarget() == 'syslog' AND ($log_level+0)<=(0+$this->getLogLevel())) {
				openlog("fworch php", LOG_PID, $this->getFacility());
				syslog($log_level, $text);
				closelog();
			}
		}
		function log_error($text) {
			$this->log(LOG_ERR, 'ERROR: ' . $text);
		}	
		function log_debug($text) {
			if ($this->getLogLevel()>=LOG_DEBUG) $this->log(LOG_DEBUG, 'DEBUG: ' . $text);
		}
		function log_login($text) {
			// substitute pass with kenn to make sure this gets logged and is not masked by log function:
			$kenn_text = str_replace("pass", "kenn", strtolower($text));
			if ($this->getLogLevel()>=LOG_INFO) $this->log(LOG_INFO,'INFO: ' . $kenn_text);
		}
		function getLogLevel() {
			return $this->system_loglevel;
		}
		function getLogTarget() {
			return $this->system_logtarget;
		}
		function getFacility() {
			return $this->system_syslogfacility;
		}
	}
?>