<?php
// $Id: multi-language.php,v 1.1.2.2 2012-04-17 04:59:28 tim Exp $
// $Source: /home/cvs/iso/package/web/include/Attic/multi-language.php,v $
require_once ("db-base.php");
require_once ("PEAR.php");

class Multilanguage extends DbConfig {
//	var $db_connection;
	var $language;
	var $config;
	var $error;

// syntax in iso.conf for default language:
// set language english
	
// syntax in gui.conf:
// user xxx language german
// user yyy language english
	
	function __construct($user) {	// $user is not used for db access
		$this->error = new PEAR();
		$this->dbuser = 'textreader';
		$this->dbpw = "";
		if (!$this->ReadDbConfigFromSession()) { 
//			$this->InitDbConfig();
			$this->ParseAndInitDbConfig(); 
		}
		
		$config = new DbConfig('textreader','');
		$language_config = $this->language='ger';  // default language if no setting found in iso.conf: ger(man) 
		// if (isset($lang)) $this->language = substr($lang, 0, 3);  //  only use first 3 chars as language id
		if (isset($user) and $user!=='') $this->language = $this->read_language_from_config_file($user);
		$this->db_connection = new DbConnection($config);
		if ($this->error->isError($this->db_connection))
			$this->error->raiseError("F-RCF: Connection not initialized. " . $this->db_connection->getMessage());
		if (!$this->ReadDbConfigFromSession()) { $this->InitDbConfig(); }
		
	}
	function read_language_from_config_file ($user) {
		$language='ger';  // default language if no setting found in iso.conf: ger(man)
		$lines = $this->GetConfigLines();
		foreach ($lines as $line) {
			if (preg_match("/^\s*user\s+($user)\s+language\s+ger/i", $line)) $language = 'ger';
			if (preg_match("/^\s*user\s+($user)\s+language\s+eng/i", $line)) $language = 'eng';
		}
		return $language;
	}
	function get_text_msg_no_break ($id, $format) {
		return '<span class="nobr">' . $this->get_text_msg($id, $format). '</span>';
	}
	function get_text_msg ($id, $format) {
		$text_field = "text_msg_" . $this->language;
		$sql_code ="SELECT $text_field FROM text_msg WHERE text_msg_id='$id'";
		$db_connection = $this->db_connection;
		$text = $db_connection->iso_db_query($sql_code);
		if ($this->error->isError($text)) $this->error->raiseError($text->getMessage());
		$result = $text->data[0]["$text_field"];
		if ($format=='html' or $format=='simple.html')
			$result = htmlentities($result, ENT_QUOTES | ENT_IGNORE, "UTF-8");
		return $result;
	}
}
?>