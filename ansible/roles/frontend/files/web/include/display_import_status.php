<?php

/*
 * $Id: display_import_status.php,v 1.1.2.3 2011-05-19 06:34:44 tim Exp $
 * $Source: /home/cvs/iso/package/web/include/Attic/display_import_status.php,v $
 * Created on 10.11.2007
 *
 */
 
require_once ("db-import-status.php");

class DisplayImportStatusTable {
	var $col_width = array(10,6,18,16,17,17,17,16,16,20,10);
	var $headers   = array("Status","ID", "Managementname", "Device-Typ", "Import aktiv?", "erfolgreich", "mit Fehler", "erfolgreich", "mit Fehler", "Importfehler");
	
	function __construct($ImportStatusList) {
		if(PEAR::isError($ImportStatusList)) {
			$error = $ImportStatusList;
			PEAR::raiseError($error->getMessage());
		} else {
			$this->ImportStatusList = $ImportStatusList;
		}
	}
	function displayImportStatus($output_mode) {
		if ($output_mode == 'text') {
			$linebreak = "\n";;
			$start_tag = '';
			$end_tag = '';
		} else {
			$linebreak = '<br>';
			$start_tag = '<br><pre>';
			$end_tag = '</pre>';
		}
		if (defined($output_mode)) {
			echo "output_mode = $output_mode$linebreak";
		}
		$importStatusTable = "$start_tag$linebreak" . 
			"                                                                         Letzter Import                 Anzahl Imports (24h)$linebreak";
		$table_width = 0;
		for ($i=0; $i<count($this->col_width); ++$i) {
			$importStatusTable .= sprintf('%-' . $this->col_width[$i] . "s", $this->headers[$i]);
			$table_width += $this->col_width[$i];
		}
		$importStatusTable .= "\n";
		for ($i=0; $i<$table_width; ++$i) $importStatusTable .= "-";
		$importStatusTable .= "\n";
		$mgm_number = 0;
		foreach ($this->ImportStatusList->getStati() as $mgm_import_status) {
			$mgm_number++;
			$i = 0;
			$importStatusTable .= sprintf('%-' . $this->col_width[$i++] . "s", $mgm_import_status->status);
			$importStatusTable .= sprintf('%-' . $this->col_width[$i++] . "s", $mgm_import_status->mgm_id);
			$importStatusTable .= sprintf('%-' . $this->col_width[$i++] . "s", $mgm_import_status->mgm_name);
			$importStatusTable .= sprintf('%-' . $this->col_width[$i++] . "s", $mgm_import_status->device_type);
			$importStatusTable .= sprintf('%-' . $this->col_width[$i++] . "s", ($mgm_import_status->import_active=='t'?'Ja':'Nein'));
			$importStatusTable .= sprintf('%-' . $this->col_width[$i++] . "s", substr($mgm_import_status->import_time_last_successful,0,$this->col_width[$i]-1));
			$importStatusTable .= sprintf('%-' . $this->col_width[$i++] . "s", substr($mgm_import_status->import_time_last_error,0,$this->col_width[$i]-1));
			$importStatusTable .= sprintf('%-' . $this->col_width[$i++] . "s", $mgm_import_status->import_count_24h_successful);
			$importStatusTable .= sprintf('%-' . $this->col_width[$i++] . "s", $mgm_import_status->import_count_24h_error);
			$importStatusTable .= sprintf('%-' . $this->col_width[$i++] . "s", str_replace(array("\r\n", "\n", "\r"), "; ", $mgm_import_status->last_import_error));
			$importStatusTable .= "$linebreak";
		} // naechste Zeile
		$importStatusTable .= "$end_tag";
		if ($mgm_number==0) $importStatusTable .= "Keine Import-Status-Informationen gefunden.$linebreak";
		return $importStatusTable;
	}
}
?>